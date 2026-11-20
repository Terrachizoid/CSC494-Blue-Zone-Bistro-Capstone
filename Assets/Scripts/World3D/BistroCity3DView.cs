// File responsibility: The city screen's window onto BistroCity3D. Shows the camera's texture in a RawImage and
// turns pointer input into pan (click-drag), zoom (mouse wheel) and building clicks. Name tags (UI panels made by
// the screen) follow their buildings. Rebuilt with the screen; the city and camera state persist in BistroCity3D.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public sealed class BistroCity3DView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
    public Action<string> Clicked;
    private BistroCity3D city;
    private RawImage image;
    private Canvas canvas;
    private readonly List<(CityLandmark landmark, RectTransform tag)> tags = new List<(CityLandmark, RectTransform)>();
    private bool registered;

    public static BistroCity3DView Attach(RawImage image, BistroCity3D city)
    {
        var view = image.gameObject.AddComponent<BistroCity3DView>();
        view.image = image; view.city = city;
        view.canvas = image.GetComponentInParent<Canvas>();
        image.color = Color.white;
        // Show the last city frame at once: a RawImage without a texture draws solid white, which flashed for a frame
        // every time the screen was rebuilt (any click that changes the UI).
        if (city != null && city.CurrentTexture != null) image.texture = city.CurrentTexture;
        view.Register();
        return view;
    }

    // Pins a UI tag (child of this view) above the landmark with that ID. Returns false if the city has no such landmark.
    public bool AddTag(string landmarkId, RectTransform tag)
    {
        if (city == null) return false;
        foreach (var landmark in city.Landmarks)
            if (landmark != null && landmark.landmarkId == landmarkId)
            {
                tag.anchorMin = tag.anchorMax = new Vector2(0.5f, 0.5f);
                tag.pivot = new Vector2(0.5f, 0f);
                tags.Add((landmark, tag));
                return true;
            }
        return false;
    }

    private void Register() { if (!registered && city != null) { city.AddUser(); registered = true; } }
    private void OnEnable() => Register();
    private void OnDisable() { if (registered && city != null) { city.RemoveUser(); registered = false; } }

    private Vector2 PixelSize()
    {
        var size = ((RectTransform)transform).rect.size;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        return size * scale;
    }

    private bool ViewportPoint(PointerEventData e, out Vector2 point)
    {
        point = default;
        var rect = (RectTransform)transform;
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, cam, out var local)) return false;
        point = Rect.PointToNormalized(rect.rect, local);
        return true;
    }

    private void LateUpdate()
    {
        if (city == null || image == null) return;
        var pixels = PixelSize();
        // Right after a rebuild the layout may not have sized this view yet; asking for a texture then would replace
        // the good one with a tiny empty one (another one-frame flash). Keep the current texture until sized.
        if (pixels.x >= 32f && pixels.y >= 32f)
            image.texture = city.TextureFor(Mathf.RoundToInt(pixels.x), Mathf.RoundToInt(pixels.y));
        else if (image.texture == null && city.CurrentTexture != null)
            image.texture = city.CurrentTexture;
        var size = ((RectTransform)transform).rect.size;
        foreach (var (landmark, tag) in tags)
        {
            if (tag == null) continue;
            bool visible = city.TagPosition(landmark, out var vp) && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
            if (tag.gameObject.activeSelf != visible) tag.gameObject.SetActive(visible);
            if (visible) tag.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
        }
    }

    public void OnBeginDrag(PointerEventData e) { if (city != null && e.button == PointerEventData.InputButton.Left) city.Dragging = true; }
    public void OnDrag(PointerEventData e) { if (city != null && city.Dragging) city.Pan(e.delta, PixelSize().y); }
    public void OnEndDrag(PointerEventData e) { if (city != null) city.Dragging = false; }
    public void OnScroll(PointerEventData e)
    {
        if (city == null || Mathf.Approximately(e.scrollDelta.y, 0f) || !ViewportPoint(e, out var vp)) return;
        city.Zoom(Mathf.Sign(e.scrollDelta.y), vp);
    }
    public void OnPointerClick(PointerEventData e)
    {
        if (city == null || e.dragging || e.button != PointerEventData.InputButton.Left || !ViewportPoint(e, out var vp)) return;
        if (city.Pick(vp, out var id)) Clicked?.Invoke(id);
    }
}
