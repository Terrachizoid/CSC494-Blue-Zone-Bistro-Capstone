// File responsibility: Persistent runtime host for the low-poly 3D city (optional; BistroUITheme.use3DCity).
// Instantiates the generated city prefab once, far from the UI, with its own camera rendering into a texture
// that the city screen shows. Camera state (pan, zoom) lives here, so it survives the UI's screen rebuilds.
// Pan has elastic edges (resistance past the city, spring back on release); zoom eases toward the cursor.
// Clicks are resolved by raycasting CityLandmark colliders. No gameplay logic lives here.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class BistroCity3D : MonoBehaviour
{
    private static BistroCity3D instance;
    private static readonly Vector3 Origin = new Vector3(20000f, 0f, 20000f); // far from anything the UI camera sees

    public float Yaw = -28f, Pitch = 42f, FieldOfView = 30f;
    [Tooltip("Orthographic (no perspective, diorama look). Costs the same to render; it only changes the look.")]
    public bool Orthographic;
    public Camera ViewCamera { get; private set; }
    public bool Dragging { get; set; }
    public readonly List<CityLandmark> Landmarks = new List<CityLandmark>();

    private RenderTexture texture;
    private Bounds land;                 // city area (water excluded), world space
    private Vector3 target;              // ground point the camera looks at
    private float distance, distanceTarget, minDistance, maxDistance;
    private Vector3 zoomFocus;
    private bool zoomFocusValid;
    private int users;

    public static BistroCity3D Current => instance;

    public static BistroCity3D Ensure(GameObject prefab, bool orthographic = false)
    {
        if (instance != null) { instance.Orthographic = orthographic; return instance; }
        if (prefab == null && FindAnyObjectByType<CityLighting>() == null) return null;
        var host = new GameObject("City 3D (runtime)");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<BistroCity3D>();
        instance.Orthographic = orthographic;
        instance.Build(prefab);
        return instance;
    }

    private void Build(GameObject prefab)
    {
        // Prefer the city placed in the scene (Generate Low-Poly City puts one there): baked lightmaps and probes
        // belong to the scene, so only that copy is lit with the bake. Otherwise make a copy from the prefab.
        var placed = FindAnyObjectByType<CityLighting>();
        var city = placed != null ? placed.gameObject : Instantiate(prefab, Origin, Quaternion.identity, transform);
        if (placed == null) city.name = "Low-Poly City";
        bool any = false;
        foreach (var r in city.GetComponentsInChildren<Renderer>())
        {
            if (r.name.StartsWith("water") || r is not MeshRenderer) continue;
            if (!any) { land = r.bounds; any = true; } else land.Encapsulate(r.bounds);
        }
        if (!any) land = new Bounds(Origin, new Vector3(400, 10, 300));
        Landmarks.AddRange(city.GetComponentsInChildren<CityLandmark>());
        target = new Vector3(land.center.x, land.min.y + 0.8f, land.center.z);
        maxDistance = Mathf.Max(land.size.x, land.size.z) * 1.05f;
        minDistance = Mathf.Max(60f, maxDistance * 0.18f);
        distance = distanceTarget = maxDistance * 0.85f;

        var cameraObject = new GameObject("City camera");
        cameraObject.transform.SetParent(transform, false);
        ViewCamera = cameraObject.AddComponent<Camera>();
        ViewCamera.clearFlags = CameraClearFlags.SolidColor;
        ViewCamera.backgroundColor = new Color(0.84f, 0.88f, 0.89f);
        ViewCamera.fieldOfView = FieldOfView;
        ViewCamera.nearClipPlane = 1f;
        ViewCamera.farClipPlane = 4000f;
        ViewCamera.enabled = false;

        // Lighting and fog live on the prefab's CityLighting (Sun child + Blinn-Phong globals). The camera uses the
        // 3D (Universal) renderer; the project's default 2D renderer has no shadow maps.
        var lighting = city.GetComponent<CityLighting>();
        var cameraData = ViewCamera.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = true;
        // Water's screen-space reflections read the finished opaque image and the depth buffer.
        cameraData.requiresColorOption = CameraOverrideOption.On;
        cameraData.requiresDepthOption = CameraOverrideOption.On;
        cameraData.antialiasing = AntialiasingMode.None;   // MSAA comes from the URP asset (graphics settings)
        if (lighting != null)
        {
            cameraData.SetRenderer(lighting.rendererIndex);
            ViewCamera.backgroundColor = lighting.fogColor;
            lighting.fogStart = maxDistance * 0.9f; lighting.fogEnd = maxDistance * 3f;
        }
        PlaceCamera();
    }

    // A view registers while it is on screen; the camera only renders while someone is looking.
    public void AddUser() { users++; if (ViewCamera != null) ViewCamera.enabled = true; }
    public void RemoveUser() { users = Mathf.Max(0, users - 1); if (users == 0 && ViewCamera != null) ViewCamera.enabled = false; Dragging = false; }

    /// <summary>The texture the camera currently renders into (null before the first view asked for one).</summary>
    public RenderTexture CurrentTexture => texture;

    public RenderTexture TextureFor(int width, int height)
    {
        // Graphics settings: render scale (fewer pixels, upscaled by the UI) and MSAA samples.
        width = Mathf.Clamp(Mathf.RoundToInt(width * BistroGraphics.RenderScale), 16, 4096);
        height = Mathf.Clamp(Mathf.RoundToInt(height * BistroGraphics.RenderScale), 16, 4096);
        int msaa = BistroGraphics.Msaa;
        if (texture == null || texture.width != width || texture.height != height || texture.antiAliasing != msaa)
        {
            if (texture != null) { ViewCamera.targetTexture = null; texture.Release(); Destroy(texture); }
            texture = new RenderTexture(width, height, 24) { name = "City view", antiAliasing = msaa };
            ViewCamera.targetTexture = texture;
        }
        return texture;
    }

    private Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);
    private float GroundY => land.min.y + 0.8f;

    // Pixel drag to ground movement. Past the city edge the movement is damped (rubber band).
    public void Pan(Vector2 pixelDelta, float viewHeightPixels)
    {
        if (ViewCamera == null || viewHeightPixels <= 0) return;
        float worldPerPixel = 2f * distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) / viewHeightPixels;
        var right = ViewCamera.transform.right; right.y = 0; right.Normalize();
        var forward = ViewCamera.transform.forward; forward.y = 0; forward.Normalize();
        var move = -right * pixelDelta.x * worldPerPixel - forward * pixelDelta.y * worldPerPixel / Mathf.Max(0.2f, Mathf.Sin(Pitch * Mathf.Deg2Rad));
        target.x += Rubber(target.x, move.x, land.min.x, land.max.x);
        target.z += Rubber(target.z, move.z, land.min.z, land.max.z);
    }
    private static float Rubber(float position, float delta, float min, float max)
    {
        float over = position < min ? min - position : position > max ? position - max : 0f;
        bool outward = (position <= min && delta < 0) || (position >= max && delta > 0);
        return outward ? delta / (1f + over / 40f) : delta;
    }

    // Mouse wheel: notches > 0 zooms in, toward the ground point under the cursor (viewport coordinates 0..1).
    public void Zoom(float notches, Vector2 viewportPoint)
    {
        if (ViewCamera == null) return;
        distanceTarget = Mathf.Clamp(distanceTarget * Mathf.Pow(0.87f, notches), minDistance, maxDistance);
        zoomFocusValid = GroundPoint(viewportPoint, out zoomFocus);
    }

    public bool Pick(Vector2 viewportPoint, out string landmarkId)
    {
        landmarkId = null;
        if (ViewCamera == null) return false;
        if (!Physics.Raycast(ViewCamera.ViewportPointToRay(viewportPoint), out var hit, 6000f)) return false;
        var landmark = hit.collider.GetComponentInParent<CityLandmark>();
        if (landmark == null) return false;
        landmarkId = landmark.landmarkId;
        return true;
    }

    // Viewport position (0..1) of a landmark's roof, for floating name tags. False if behind the camera.
    public bool TagPosition(CityLandmark landmark, out Vector2 viewportPoint)
    {
        viewportPoint = default;
        if (ViewCamera == null || landmark == null) return false;
        var box = landmark.GetComponent<Collider>();
        var top = box != null ? new Vector3(box.bounds.center.x, box.bounds.max.y + 4f, box.bounds.center.z) : landmark.transform.position;
        var p = ViewCamera.WorldToViewportPoint(top);
        viewportPoint = new Vector2(p.x, p.y);
        return p.z > 0;
    }

    private bool GroundPoint(Vector2 viewportPoint, out Vector3 point)
    {
        var ray = ViewCamera.ViewportPointToRay(viewportPoint);
        var plane = new Plane(Vector3.up, new Vector3(0, GroundY, 0));
        if (plane.Raycast(ray, out float t)) { point = ray.GetPoint(t); return true; }
        point = default; return false;
    }

    private void LateUpdate()
    {
        if (ViewCamera == null || !ViewCamera.enabled) return;
        float dt = Time.unscaledDeltaTime;
        // Ease zoom; keep the point under the cursor in place while zooming.
        if (Mathf.Abs(distance - distanceTarget) > 0.01f)
        {
            float next = Mathf.Lerp(distance, distanceTarget, 1f - Mathf.Exp(-12f * dt));
            if (zoomFocusValid) { var shift = (zoomFocus - target) * (1f - next / distance); target.x += shift.x; target.z += shift.z; }
            distance = next;
        }
        // Spring back inside the city when not dragging.
        if (!Dragging)
        {
            float k = 1f - Mathf.Exp(-9f * dt);
            target.x = Mathf.Lerp(target.x, Mathf.Clamp(target.x, land.min.x, land.max.x), k);
            target.z = Mathf.Lerp(target.z, Mathf.Clamp(target.z, land.min.z, land.max.z), k);
        }
        target.y = GroundY;
        PlaceCamera();
    }

    private void PlaceCamera()
    {
        if (ViewCamera == null) return;
        // Orthographic shows the same ground span as the perspective view would at this distance.
        ViewCamera.orthographic = Orthographic;
        ViewCamera.fieldOfView = FieldOfView;
        ViewCamera.orthographicSize = distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
        var rotation = Rotation;
        ViewCamera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
    }

    private void OnDestroy()
    {
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (instance == this) instance = null;
    }
}
