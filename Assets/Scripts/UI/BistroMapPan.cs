// File responsibility: Click-drag panning for the painted city map, with edges that never show past the artwork.
// The map rests inside a slightly smaller area than the image; dragging past that stretches into the leftover strip
// with growing resistance and stops at the image's real edge, and on release the map springs back to rest.
// Works with BistroMapZoom (wheel zoom): after any zoom the position is pulled back inside the same limits.
// Position is remembered as 0..1 across screen rebuilds (0.5, 0.5 = centred).
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BistroMapPan : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Range(0f, 0.2f)] public float StripFraction = 0.06f;   // stretch strip per side, as a share of the image size
    public Action<Vector2> Changed;
    private RectTransform viewport, content;
    private Canvas canvas;
    private bool dragging, smooth = true;
    private Vector2 velocity, remembered;
    private bool restorePending;

    public static BistroMapPan Attach(RectTransform viewport, RectTransform content, Vector2 normalized, bool smooth)
    {
        var pan = viewport.gameObject.AddComponent<BistroMapPan>();
        pan.viewport = viewport; pan.content = content; pan.smooth = smooth;
        pan.canvas = viewport.GetComponentInParent<Canvas>();
        pan.remembered = normalized; pan.restorePending = true;
        pan.Restore();       // place it now, in the same frame the screen is rebuilt, so the map never shows centred first
        return pan;
    }

    // Half the extra size per axis (how far the centre may move before an image edge enters the viewport),
    // and the resting limit inside it.
    private void Limits(out Vector2 hard, out Vector2 rest)
    {
        var size = Vector2.Scale(content.rect.size, (Vector2)content.localScale);
        var view = viewport.rect.size;
        hard = new Vector2(Mathf.Max(0, (size.x - view.x) / 2), Mathf.Max(0, (size.y - view.y) / 2));
        var strip = new Vector2(Mathf.Min(size.x * StripFraction, hard.x * .5f), Mathf.Min(size.y * StripFraction, hard.y * .5f));
        rest = hard - strip;
    }

    private void Restore()
    {
        if (!restorePending || viewport.rect.width <= 0) return;   // layout not ready yet: LateUpdate tries again
        Limits(out var hard, out var rest);
        var p = new Vector2((.5f - remembered.x) * 2 * rest.x, (.5f - remembered.y) * 2 * rest.y);
        content.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -hard.x, hard.x), Mathf.Clamp(p.y, -hard.y, hard.y));
        restorePending = false;
    }

    private Vector2 Normalized(Vector2 position, Vector2 rest) =>
        new Vector2(rest.x > 0 ? .5f - position.x / (2 * rest.x) : .5f, rest.y > 0 ? .5f - position.y / (2 * rest.y) : .5f);

    public void OnBeginDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) { dragging = true; velocity = Vector2.zero; } }
    public void OnEndDrag(PointerEventData e) { dragging = false; }
    public void OnDrag(PointerEventData e)
    {
        if (!dragging || content == null) return;
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        var delta = e.delta / Mathf.Max(.01f, scale);
        Limits(out var hard, out var rest);
        var p = content.anchoredPosition;
        p.x = Step(p.x, delta.x, hard.x, rest.x); p.y = Step(p.y, delta.y, hard.y, rest.y);
        content.anchoredPosition = p;
        velocity = Vector2.Lerp(velocity, delta / Mathf.Max(.001f, Time.unscaledDeltaTime), .5f);
    }

    // Free movement inside rest; outward movement past rest slows to zero at the hard edge.
    private static float Step(float position, float delta, float hard, float rest)
    {
        float next = position + delta;
        if (Mathf.Abs(next) <= rest || Mathf.Sign(delta) != Mathf.Sign(position)) return Mathf.Clamp(next, -hard, hard);
        float strip = Mathf.Max(.001f, hard - rest);
        float over = Mathf.Clamp01((Mathf.Abs(position) - rest) / strip);
        float resisted = position + delta * (1 - over) * (1 - over);
        return Mathf.Clamp(resisted, -hard, hard);
    }

    private void LateUpdate()
    {
        if (content == null || viewport == null) return;
        Restore();
        Limits(out var hard, out var rest);
        var p = content.anchoredPosition;
        float dt = Time.unscaledDeltaTime;
        if (!dragging)
        {
            // Glide a little after release, but only inside the resting area.
            if (velocity.sqrMagnitude > 1f && Mathf.Abs(p.x) <= rest.x && Mathf.Abs(p.y) <= rest.y)
            {
                p += velocity * dt;
                velocity *= Mathf.Exp(-6f * dt);
            }
            else velocity = Vector2.zero;
            var target = new Vector2(Mathf.Clamp(p.x, -rest.x, rest.x), Mathf.Clamp(p.y, -rest.y, rest.y));
            p = smooth ? Vector2.Lerp(p, target, 1 - Mathf.Exp(-10f * dt)) : target;
        }
        p = new Vector2(Mathf.Clamp(p.x, -hard.x, hard.x), Mathf.Clamp(p.y, -hard.y, hard.y));   // never past the image (also after zoom-out)
        if (p != content.anchoredPosition)
        {
            content.anchoredPosition = p;
            var n = Normalized(p, rest);
            Changed?.Invoke(new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y)));
        }
    }
}
