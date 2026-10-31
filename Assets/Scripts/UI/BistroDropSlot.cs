// Reusable interaction view. Gameplay decisions belong to the owning screen and services.
using System;
using System.Collections.Generic;
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
            BistroWidgets.Label(rect,contents,theme,new Vector2(.08f,.12f),new Vector2(.92f,.73f),18).name="Slot contents";
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
        BistroWidgets.Label(rect,open!=null?"Drop here · click to inspect":"Drop here · drag out to remove",theme,new Vector2(.03f,0),new Vector2(.97f,.12f),13).name="Slot hint";
        return slot;
    }
    // foodArt: card-front sprites of the seated foods, in seat order (null entries keep the text card).
    public void ApplyIllustratedContents(Sprite[] foodArt,Sprite categoryIcon=null)
    {
        var image=GetComponent<Image>();image.sprite=BistroShelfStyle.Rounded;image.type=Image.Type.Sliced;image.color=new Color(.84f,.87f,.79f);
        var shadow=gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.13f);shadow.effectDistance=new Vector2(2,-3);
        foreach(var text in GetComponentsInChildren<Text>()){text.color=BistroShelfStyle.Ink;if(BistroShelfStyle.BodyFont!=null)text.font=BistroShelfStyle.BodyFont;}
        if(categoryIcon!=null){
            var icon=BistroWidgets.Box(transform,"Requirement icon",Color.white,new Vector2(.04f,.76f),new Vector2(.19f,.96f));icon.GetComponent<Image>().sprite=categoryIcon;icon.GetComponent<Image>().preserveAspect=true;icon.GetComponent<Image>().raycastTarget=false;
            var title=GetComponentInChildren<Text>();title.rectTransform.anchorMin=new Vector2(.21f,.74f);title.alignment=TextAnchor.MiddleLeft;
        }
        int i=0;
        foreach(Transform child in transform){
            if(child.name!="Seated card")continue;
            var art=foodArt!=null&&i<foodArt.Length?foodArt[i]:null;i++;if(art==null)continue;
            var face=child.GetComponent<Image>();face.sprite=art;face.color=Color.white;
            var caption=child.GetComponentInChildren<Text>();if(caption!=null){
                caption.rectTransform.anchorMin=new Vector2(.02f,.02f);caption.rectTransform.anchorMax=new Vector2(.98f,.30f);
                caption.color=BistroShelfStyle.Ink;caption.fontSize=12;caption.resizeTextMinSize=8;caption.resizeTextMaxSize=12;
                var plaque=BistroWidgets.Box(child,"Mini title",BistroShelfStyle.Cream,new Vector2(0,0),new Vector2(1,.32f));plaque.GetComponent<Image>().raycastTarget=false;plaque.SetAsFirstSibling();
            }
        }
    }
    public struct Row { public string key, label; public Sprite art; }
    // Contents as a scrollable list of small rows. Drag a row onto another slot to move it (that slot's drop
    // rule decides), or anywhere that is not a drop area to take it out (takeOut). Releasing it back on this
    // slot does nothing. Replaces the old "click the slot, then drag to a remove area" flow.
    public void ShowRows(IList<Row> rows,string kind,Action<string> takeOut,BistroUITheme theme)
    {
        if(rows==null||rows.Count==0)return;
        var placeholder=transform.Find("Slot contents");if(placeholder!=null)placeholder.gameObject.SetActive(false);
        var viewport=BistroWidgets.Box(transform,"Slot rows",Color.clear,new Vector2(.05f,.13f),new Vector2(.95f,.73f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content=new GameObject("Rows",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
        content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.offsetMin=content.offsetMax=Vector2.zero;
        var stack=content.gameObject.AddComponent<VerticalLayoutGroup>();stack.spacing=4;stack.childControlWidth=stack.childForceExpandWidth=true;stack.childControlHeight=true;stack.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<BistroWheelScroll>(); // wheel scrolls; click-drag picks up a row
        scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;
        foreach(var item in rows)
        {
            var row=BistroShelfStyle.Panel(content,"Slot item",new Color(.98f,.96f,.91f,.95f),Vector2.zero,Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=34;
            var thumb=BistroWidgets.Box(row,"Food thumbnail",Color.clear,new Vector2(.02f,.08f),new Vector2(.2f,.92f));
            var image=thumb.GetComponent<Image>();image.raycastTarget=false;if(item.art!=null){image.sprite=item.art;image.color=Color.white;image.preserveAspect=true;}
            BistroShelfStyle.Text(row,item.label,theme,new Vector2(.23f,0),new Vector2(.98f,1),15,BistroShelfStyle.Ink).raycastTarget=false;
            string key=item.key;
            BistroDragChip.Attach(row,key,kind,item.art).DroppedOutside=()=>takeOut?.Invoke(key);
        }
    }
    public void OnDrop(PointerEventData e){hoverScale=1;hoveringCard=null;var source=e.pointerDrag?.GetComponent<IBistroDragSource>();if(drop!=null && source!=null && source.TryClaimDrop(e.pointerId))drop(source.Key,source.Kind);}
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&!e.dragging)open?.Invoke();}
    public void OnPointerEnter(PointerEventData e){hoveringCard=e.pointerDrag?.GetComponent<BistroFoodCard>();if(!reducedMotion && hoveringCard!=null && hoveringCard.IsDragging)hoverScale=1.025f;}
    public void OnPointerExit(PointerEventData e){hoveringCard=null;hoverScale=1;rim.enabled=warning;}
}


