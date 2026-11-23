// File responsibility: Draws one character on the service stage and moves them, all from stored timestamps so a screen
// rebuild never restarts a motion. Art comes in three optional grades, best available wins:
//   video (a loop under StreamingAssets, once ready)  >  separate parts stacked back to front  >  one whole image.
// Motion in code: breathing and a gentle bob while idle (parts sway a little behind each other), a small bounce and a
// cross-fade when the expression changes, a slide in on arrival and a slide out on leaving. Reduced motion keeps only
// short fades. No gameplay decisions here: BistroUIScreen.Characters.cs decides who stands here and with which face.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class BistroCharacterPortrait : MonoBehaviour
{
    // What to show for one expression. Any field may be empty; a Look with nothing in it draws nothing.
    public sealed class Look
    {
        public Sprite image;
        public Sprite[] parts;
        public string video;
        // Placeholder until art exists: the old silhouette, tinted per expression so face changes are visible.
        public bool silhouette;
        public Color silhouetteTint = new Color(.30f, .39f, .36f);
        public bool IsEmpty => !silhouette && image == null && (parts == null || parts.Length == 0) && string.IsNullOrWhiteSpace(video);
    }

    public const float SlideInSeconds = .45f, SlideOutSeconds = .38f, ChangeSeconds = .28f, BounceSeconds = .42f;
    private const float SlideDistance = .32f; // Of the stage width.

    private RectTransform motion;
    private CanvasGroup motionGroup, previousGroup, currentGroup;
    private readonly List<RectTransform> currentParts = new List<RectTransform>();
    private readonly List<Image> currentStills = new List<Image>();
    private Look current;
    private float changedAt, arrivedAt, leftAt;
    private bool reducedMotion, hasPrevious, videoShown;
    private float phase;
    private BistroCharacterVideoCache videos;
    private Material grade; // The guest's colour grade (BistroUIScreen.Grade.cs); null = drawn as is.

    private void ApplyGrade()
    {
        if (grade == null || motion == null) return;
        foreach (var graphic in motion.GetComponentsInChildren<Graphic>(true)) graphic.material = grade;
    }

    // changedAt: when the expression last changed (bounce + cross-fade from `previous`); arrivedAt: when this character
    // arrived (slide in); leftAt: when they left (slide out), or NaN while they stay.
    public static BistroCharacterPortrait Add(RectTransform stage, string name, Look look, Look previous,
        float changedAt, float arrivedAt, float leftAt, bool reducedMotion, BistroCharacterVideoCache videos, Material grade = null)
    {
        var root = Stretch(new GameObject(name, typeof(RectTransform)), stage);
        var portrait = root.gameObject.AddComponent<BistroCharacterPortrait>();
        portrait.current = look; portrait.changedAt = changedAt; portrait.arrivedAt = arrivedAt; portrait.leftAt = leftAt;
        portrait.reducedMotion = reducedMotion; portrait.videos = videos;
        portrait.phase = (((name + (look?.image != null ? look.image.name : "")).GetHashCode() & 0x7fffffff) % 628) / 100f;
        portrait.motion = Stretch(new GameObject("Motion", typeof(RectTransform), typeof(CanvasGroup)), root);
        portrait.motion.pivot = new Vector2(.5f, 0); // Breathe and bounce from the feet.
        portrait.motionGroup = portrait.motion.GetComponent<CanvasGroup>();
        portrait.motionGroup.blocksRaycasts = false; portrait.motionGroup.interactable = false;
        portrait.hasPrevious = previous != null && !previous.IsEmpty && Time.unscaledTime - changedAt < ChangeSeconds;
        if (portrait.hasPrevious) portrait.previousGroup = portrait.Layer("Previous expression", previous, null, null);
        portrait.currentGroup = portrait.Layer("Expression", look, portrait.currentParts, portrait.currentStills);
        portrait.grade = grade;
        portrait.TryShowVideo(); // The outgoing face of a cross-fade stays a still: the fade is too short to matter.
        portrait.ApplyGrade();
        portrait.Apply();
        return portrait;
    }

    private CanvasGroup Layer(string name, Look look, List<RectTransform> parts, List<Image> stills)
    {
        var layer = Stretch(new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)), motion);
        layer.pivot = new Vector2(.5f, 0);
        if (look == null) return layer.GetComponent<CanvasGroup>();
        if (look.silhouette && look.image == null && (look.parts == null || look.parts.Length == 0))
        {
            BistroShelfIcon.Add(layer, "Customer", Vector2.zero, Vector2.one, look.silhouetteTint);
            foreach (var graphic in layer.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            return layer.GetComponent<CanvasGroup>();
        }
        var sprites = look.parts != null && look.parts.Length > 0 ? look.parts : new[] { look.image };
        foreach (var sprite in sprites)
        {
            if (sprite == null) continue;
            var rect = Stretch(new GameObject(sprite.name, typeof(RectTransform), typeof(Image)), layer);
            var image = rect.GetComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            parts?.Add(rect); stills?.Add(image);
        }
        return layer.GetComponent<CanvasGroup>();
    }

    private void TryShowVideo()
    {
        if (videoShown || videos == null || currentGroup == null || string.IsNullOrWhiteSpace(current?.video)) return;
        var texture = videos.Get(current.video);
        if (texture == null) return;
        var parent = (RectTransform)currentGroup.transform;
        var rect = Stretch(new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)), parent);
        var raw = rect.GetComponent<RawImage>(); raw.texture = texture; raw.raycastTarget = false;
        var fit = rect.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = texture.width / (float)Mathf.Max(1, texture.height);
        foreach (var still in currentStills) if (still != null) still.enabled = false;
        currentParts.Clear(); // The video carries its own motion; parts no longer sway.
        videoShown = true;
        if (grade != null) raw.material = grade;
    }

    private void Update() { if (!videoShown) TryShowVideo(); Apply(); }
    private void LateUpdate() { Apply(); }

    private void Apply()
    {
        if (motion == null) return;
        float now = Time.unscaledTime;
        var size = ((RectTransform)transform).rect.size;
        float x = 0, alpha = 1;
        if (!float.IsNaN(leftAt))
        {
            float k = Mathf.Clamp01((now - leftAt) / SlideOutSeconds);
            alpha = 1 - k;
            if (!reducedMotion) x = -k * k * SlideDistance * size.x;
        }
        else
        {
            float k = Mathf.Clamp01((now - arrivedAt) / SlideInSeconds);
            alpha = k;
            if (!reducedMotion) { float e = 1 - (1 - k) * (1 - k) * (1 - k); x = (1 - e) * SlideDistance * size.x; }
        }
        float breath = 0, bob = 0, bounce = 0, lift = 0;
        if (!reducedMotion)
        {
            breath = Mathf.Sin(now * Mathf.PI * 2 / 3.6f + phase) * .010f;
            bob = Mathf.Sin(now * Mathf.PI * 2 / 2.7f + phase) * .005f * size.y;
            float b = (now - changedAt) / BounceSeconds;
            if (b >= 0 && b < 1) { float s = Mathf.Sin(b * Mathf.PI) * (1 - b); bounce = s * .045f; lift = s * .02f * size.y; }
        }
        motion.anchoredPosition = new Vector2(x, bob + lift);
        motion.localScale = new Vector3(1 - breath * .35f + bounce * .4f, 1 + breath + bounce, 1);
        motionGroup.alpha = alpha;
        // Parts sway slightly behind the layer under them (hair after head after body).
        for (int i = 1; i < currentParts.Count; i++)
            if (currentParts[i] != null)
                currentParts[i].anchoredPosition = reducedMotion ? Vector2.zero
                    : new Vector2(0, Mathf.Sin(now * Mathf.PI * 2 / 2.7f + phase - i * .45f) * .0025f * size.y);
        float c = hasPrevious ? Mathf.Clamp01((now - changedAt) / ChangeSeconds) : 1;
        if (previousGroup != null) previousGroup.alpha = 1 - c;
        if (currentGroup != null) currentGroup.alpha = c;
    }

    private static RectTransform Stretch(GameObject go, Transform parent)
    {
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}

// Keeps character videos playing across screen rebuilds (the UI is rebuilt on every change, a VideoPlayer must not be).
// Lives on the BistroUIScreen object. Holds a few loops at once and lets the oldest go; a clip that fails to load
// (missing file, a format the browser can't play) is remembered, and the still image keeps showing.
public sealed class BistroCharacterVideoCache : MonoBehaviour
{
    private sealed class Entry { public VideoPlayer player; public RenderTexture texture; public bool ready, failed; public float usedAt; }
    private const int MaxLoaded = 3;
    private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

    // The playing texture, or null while it loads or if it failed.
    public Texture Get(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim().Replace('\\', '/');
        if (!entries.TryGetValue(path, out var entry)) { Trim(); entry = Load(path); entries[path] = entry; }
        entry.usedAt = Time.unscaledTime;
        return entry.ready && !entry.failed ? entry.texture : null;
    }

    private Entry Load(string path)
    {
        var entry = new Entry { usedAt = Time.unscaledTime };
        var go = new GameObject("Character video " + path);
        go.transform.SetParent(transform, false);
        var player = go.AddComponent<VideoPlayer>();
        entry.player = player;
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.url = Application.streamingAssetsPath.TrimEnd('/') + "/" + path;
        player.isLooping = true;
        player.skipOnDrop = true;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.errorReceived += (p, message) =>
        {
            entry.failed = true;
            Debug.LogWarning($"Character video '{path}' could not play ({message}). Showing the still image instead.");
        };
        player.prepareCompleted += p =>
        {
            entry.texture = new RenderTexture((int)Math.Max(1, p.width), (int)Math.Max(1, p.height), 0);
            entry.texture.Create();
            p.targetTexture = entry.texture;
            p.sendFrameReadyEvents = true;
            p.Play();
        };
        // Shown only once a real frame exists, so the stage never flashes an empty texture.
        player.frameReady += (p, frame) => { entry.ready = true; p.sendFrameReadyEvents = false; };
        player.Prepare();
        return entry;
    }

    private void Trim()
    {
        while (entries.Count >= MaxLoaded)
        {
            string oldest = null; float when = float.MaxValue;
            foreach (var pair in entries) if (pair.Value.usedAt < when) { when = pair.Value.usedAt; oldest = pair.Key; }
            if (oldest == null) return;
            Release(entries[oldest]);
            entries.Remove(oldest);
        }
    }

    private static void Release(Entry entry)
    {
        if (entry.player != null) { entry.player.Stop(); Destroy(entry.player.gameObject); }
        if (entry.texture != null) { entry.texture.Release(); Destroy(entry.texture); }
    }

    private void OnDestroy() { foreach (var entry in entries.Values) Release(entry); entries.Clear(); }
}
