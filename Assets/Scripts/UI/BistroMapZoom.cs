// Mouse-wheel zoom for a panned map (the city). Zooms toward the cursor and eases to the target, so the point
// under the mouse stays put. Sits on the ScrollRect's viewport; the ScrollRect keeps click-drag panning and its
// elastic edges pull the map back if a zoom-out leaves it past an edge.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
public sealed class BistroMapZoom : MonoBehaviour, IScrollHandler
{
    public float MinZoom=1f, MaxZoom=2.6f, StepPerNotch=1.15f;
    public Action<float> Changed;   // the screen remembers the level across rebuilds
    private RectTransform content;
    private Canvas canvas;
    private float current=1f, target=1f;
    private Vector2 focusLocal;     // point in content space that should stay under the cursor
    public static BistroMapZoom Attach(RectTransform viewport,RectTransform content,float level,bool smooth)
    {
        var zoom=viewport.gameObject.AddComponent<BistroMapZoom>();
        zoom.content=content;zoom.canvas=viewport.GetComponentInParent<Canvas>();zoom.smooth=smooth;
        zoom.current=zoom.target=Mathf.Clamp(level,zoom.MinZoom,zoom.MaxZoom);
        content.localScale=Vector3.one*zoom.current;
        return zoom;
    }
    private bool smooth=true;
    public void OnScroll(PointerEventData e)
    {
        if(content==null||Mathf.Approximately(e.scrollDelta.y,0))return;
        var camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(content,e.position,camera,out focusLocal))return;
        target=Mathf.Clamp(target*(e.scrollDelta.y>0?StepPerNotch:1f/StepPerNotch),MinZoom,MaxZoom);
        if(!smooth)Apply(target);
    }
    private void Update()
    {
        if(content==null||Mathf.Abs(current-target)<.0005f)return;
        float next=Mathf.Lerp(current,target,1-Mathf.Exp(-14f*Time.unscaledDeltaTime));
        Apply(Mathf.Abs(next-target)<.0005f?target:next);
    }
    // Scale around the focus point: its position in the parent stays the same.
    private void Apply(float level)
    {
        content.anchoredPosition+=focusLocal*(current-level);
        content.localScale=Vector3.one*level;
        current=level;
        Changed?.Invoke(level);
    }
}
