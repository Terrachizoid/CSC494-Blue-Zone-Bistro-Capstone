// Reusable interaction view. Gameplay decisions belong to the owning screen and services.
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BistroDropSlot : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Action<string,string> drop;
    private Action open;
    private Outline rim;
    private bool warning;
    private float hoverScale=1;
    private bool reducedMotion;
    private BistroFoodCard hoveringCard;
    private void Update()
    {
        if(hoveringCard==null || !hoveringCard.IsDragging)hoverScale=1;
        Vector3 target=Vector3.one*hoverScale;
        if((transform.localScale-target).sqrMagnitude<.000001f)return;
        var next=Vector3.Lerp(transform.localScale,target,1-Mathf.Exp(-20*Time.unscaledDeltaTime));
        transform.localScale=(next-target).sqrMagnitude<.000001f?target:next;
    }
    private void OnDisable(){hoveringCard=null;hoverScale=1;transform.localScale=Vector3.one;}
    public static BistroDropSlot Create(Transform parent,string title,string contents,BistroUITheme theme,Vector2 min,Vector2 max,bool warning,Action<string,string> drop,Action open,string[] cardNames=null)
    {
        var rect=BistroWidgets.Box(parent,title,theme.panel,min,max);
        if(theme.slotFrame!=null)rect.GetComponent<Image>().sprite=theme.slotFrame;
        var slot=rect.gameObject.AddComponent<BistroDropSlot>();slot.drop=drop;slot.open=open;slot.warning=warning;
        slot.reducedMotion=theme.reducedMotion;
        slot.rim=rect.gameObject.AddComponent<Outline>();slot.rim.effectColor=theme.warning;slot.rim.effectDistance=new Vector2(3,3);slot.rim.enabled=warning;
        BistroWidgets.Label(rect,title,theme,new Vector2(.05f,.74f),new Vector2(.95f,.98f),22);
        if(cardNames==null || cardNames.Length==0)
            BistroWidgets.Label(rect,contents,theme,new Vector2(.08f,.12f),new Vector2(.92f,.73f),18);
        else
        {
            for(int i=0;i<Math.Min(3,cardNames.Length);i++)
            {
                float x=.08f+i*.23f;
                var small=BistroWidgets.Box(rect,"Seated card",theme.card,new Vector2(x,.22f),new Vector2(x+.32f,.70f));
                small.localRotation=Quaternion.Euler(0,0,4-i*4);small.GetComponent<Image>().raycastTarget=false;
                BistroWidgets.Label(small,cardNames[i],theme,new Vector2(.06f,.06f),new Vector2(.94f,.94f),16,theme.ink);
            }
            if(cardNames.Length>3)BistroWidgets.Label(rect,$"+{cardNames.Length-3} more",theme,new Vector2(.65f,.12f),new Vector2(.96f,.25f),14);
        }
        BistroWidgets.Label(rect,"Drop here · click to inspect",theme,new Vector2(.03f,0),new Vector2(.97f,.12f),13);
        return slot;
    }
    public void OnDrop(PointerEventData e){hoverScale=1;hoveringCard=null;var card=e.pointerDrag?.GetComponent<BistroFoodCard>();if(drop!=null && card!=null && card.TryClaimDrop(e.pointerId))drop(card.Key,card.Kind);}
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&!e.dragging)open?.Invoke();}
    public void OnPointerEnter(PointerEventData e){hoveringCard=e.pointerDrag?.GetComponent<BistroFoodCard>();if(!reducedMotion && hoveringCard!=null && hoveringCard.IsDragging)hoverScale=1.025f;}
    public void OnPointerExit(PointerEventData e){hoveringCard=null;hoverScale=1;rim.enabled=warning;}
}


