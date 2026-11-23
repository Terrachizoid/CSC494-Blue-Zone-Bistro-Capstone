// A small row (e.g. a cart line or a slot item) that is dragged out like a food card. Any click-drag picks it up;
// lists of these rows scroll with the mouse wheel only (Owen, 2026-10-30), so a quick drag is never mistaken for a scroll.
// Uses the same drop contract as cards (IBistroDragSource), so existing drop targets accept it unchanged.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public sealed class BistroDragChip : MonoBehaviour, IBistroDragSource, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string Key { get; private set; }
    public string Kind { get; private set; }
    // Called when the row is released where no drop area accepted it ("dragged out").
    public System.Action DroppedOutside;
    private Sprite art;
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform ghost;
    private int? pointer;
    private bool dragging, claimed;
    public static BistroDragChip Attach(RectTransform row,string key,string kind,Sprite art)
    {
        var chip=row.gameObject.AddComponent<BistroDragChip>();
        chip.Key=key;chip.Kind=kind;chip.art=art;
        var parentCanvas=row.GetComponentInParent<Canvas>();chip.canvas=parentCanvas!=null?parentCanvas.rootCanvas:null;
        chip.group=row.GetComponent<CanvasGroup>();if(chip.group==null)chip.group=row.gameObject.AddComponent<CanvasGroup>();
        return chip;
    }
    public bool TryClaimDrop(int pointerId)
    {
        if(!dragging||claimed||pointer!=pointerId)return false;
        claimed=true;return true;
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if(e.button!=PointerEventData.InputButton.Left||pointer.HasValue)return;
        if(canvas==null)return;
        pointer=e.pointerId;
        dragging=true;claimed=false;group.alpha=.5f;
        BistroAudio.Play("card.pick");
        ghost=BistroWidgets.Box(canvas.transform,"Dragging cart item",Color.white,Vector2.zero,Vector2.zero);
        ghost.sizeDelta=new Vector2(110,110);
        var image=ghost.GetComponent<Image>();image.raycastTarget=false;image.preserveAspect=true;
        if(art!=null)image.sprite=art;else image.color=new Color(.94f,.92f,.86f,.9f);
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if(pointer!=e.pointerId)return;
        if(ghost!=null&&RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,
            canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var local))
            ghost.localPosition=new Vector3(local.x,local.y,0);
    }
    public void OnEndDrag(PointerEventData e)
    {
        if(pointer!=e.pointerId)return;
        bool outside=dragging&&!claimed;
        Cancel();
        if(outside){BistroAudio.Play("card.return");DroppedOutside?.Invoke();}
    }
    private void Cancel()
    {
        if(ghost!=null)Destroy(ghost.gameObject);
        ghost=null;dragging=false;pointer=null;
        if(group!=null)group.alpha=1;
    }
    private void OnDisable(){Cancel();}
}
