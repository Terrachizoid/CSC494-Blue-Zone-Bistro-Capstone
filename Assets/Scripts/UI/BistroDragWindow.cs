// A floating panel the player can drag anywhere on screen, like a window (used by "Today's plan").
// Moves its RectTransform by the pointer delta, stays inside its parent, and reports the final offset
// so the owner can restore it when the screen is rebuilt. A click without dragging still reaches buttons inside.
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BistroDragWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action<Vector2> Moved;
    // Highest point the panel may reach, as a fraction of the parent's height (the top bar starts at 0.91).
    public float topLimit = 1f;
    private RectTransform rect;
    private Canvas canvas;
    private RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;

    public void Place(Vector2 offset) { Rect.anchoredPosition = offset; Clamp(); }
    public void OnBeginDrag(PointerEventData e) { }
    public void OnDrag(PointerEventData e)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1f;
        Rect.anchoredPosition += e.delta / scale;
        Clamp();
    }
    public void OnEndDrag(PointerEventData e) { Moved?.Invoke(Rect.anchoredPosition); }

    // Keep the whole panel inside the parent (the screen).
    private void Clamp()
    {
        var parent = Rect.parent as RectTransform;
        if (parent == null) return;
        Vector2 position = Rect.localPosition;
        Vector2 min = position + Rect.rect.min, max = position + Rect.rect.max;
        var bounds = parent.rect;
        bounds.yMax = bounds.yMin + bounds.height * Mathf.Clamp01(topLimit);
        float dx = Mathf.Max(0, bounds.xMin - min.x) - Mathf.Max(0, max.x - bounds.xMax);
        float dy = Mathf.Max(0, bounds.yMin - min.y) - Mathf.Max(0, max.y - bounds.yMax);
        Rect.anchoredPosition += new Vector2(dx, dy);
    }
}
