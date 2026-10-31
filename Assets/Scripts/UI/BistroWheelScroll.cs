// A scroll list that moves only with the mouse wheel (or trackpad scroll). Click-drags are ignored, so items
// inside it can be dragged out reliably. Used for slot and cart rows; the city map keeps its normal drag-to-pan.
using UnityEngine.EventSystems;
using UnityEngine.UI;
public sealed class BistroWheelScroll : ScrollRect
{
    public override void OnInitializePotentialDrag(PointerEventData eventData){}
    public override void OnBeginDrag(PointerEventData eventData){}
    public override void OnDrag(PointerEventData eventData){}
    public override void OnEndDrag(PointerEventData eventData){}
}
