// Reusable interaction view. Gameplay decisions belong to the owning screen and services.
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BistroFoodCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string Key { get; private set; }
    public string Kind { get; private set; }
    public bool CanDrag => canDrag;
    private Color dragColor, inkColor;
    private string front, back;
    private Text heading, detail;
    private GameObject icon;
    private bool flipped, dragging, canDrag;
    private RectTransform ghost;
    private Canvas canvas;
    public static BistroFoodCard Create(Transform parent,FoodData food,string key,string kind,string stats,BistroUITheme theme,bool draggable=true)
    {
        var rect=BistroWidgets.Box(parent,"Food card",theme.card,Vector2.zero,Vector2.one);
        if(theme.cardFrame!=null) { var img=rect.GetComponent<Image>(); img.sprite=theme.cardFrame; img.type=Image.Type.Sliced; }
        var card=rect.gameObject.AddComponent<BistroFoodCard>(); card.Key=key; card.Kind=kind; card.canDrag=draggable;
        card.dragColor=theme.card;card.dragColor.a=.90f;card.inkColor=theme.ink;
        card.canvas=parent.GetComponentInParent<Canvas>();
        card.heading=BistroWidgets.Label(rect,food==null?key:food.displayName,theme,new Vector2(.06f,.78f),new Vector2(.94f,.97f),22,theme.ink);
        var image=BistroWidgets.Box(rect,"Image placeholder",Color.Lerp(theme.card,theme.accent,.5f),new Vector2(.12f,.34f),new Vector2(.88f,.76f));
        if(food?.icon!=null) {image.GetComponent<Image>().sprite=food.icon; image.GetComponent<Image>().preserveAspect=true; image.GetComponent<Image>().color=Color.white;}
        image.GetComponent<Image>().raycastTarget=false; card.icon=image.gameObject;
        card.front=(food?.marketingLabel??"")+"\n"+stats+"\nClick to flip";
        card.back=food==null?"Food unavailable": "Ingredients\n"+food.ingredientList+"\n\nDaily Dozen\n"+string.Join(", ",food.dailyDozenCategories)+"\n\nStorage: "+food.storageType+"\nCodex research pending";
        if(theme.debugCardDetails && Debug.isDebugBuild && food!=null) card.back+="\nDEBUG: "+food.trafficLight;
        card.detail=BistroWidgets.Label(rect,card.front,theme,new Vector2(.06f,.03f),new Vector2(.94f,.33f),17,theme.ink);
        return card;
    }
    public void OnPointerClick(PointerEventData e)
    {
        if(dragging || e.dragging)return; flipped=!flipped; icon.SetActive(!flipped); detail.text=flipped?back:front;
        detail.rectTransform.anchorMax=new Vector2(.94f,flipped?.77f:.33f);
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if(!canDrag)return; dragging=true;
        ghost=BistroWidgets.Box(canvas.transform,"Dragging card",dragColor,Vector2.zero,Vector2.zero);
        ghost.sizeDelta=new Vector2(145,195); ghost.GetComponent<Image>().raycastTarget=false;
        var t=new GameObject("Name",typeof(RectTransform),typeof(Text)); t.transform.SetParent(ghost,false);
        var rt=(RectTransform)t.transform; rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
        var label=t.GetComponent<Text>();label.font=heading.font;label.text=heading.text;label.color=inkColor;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
        OnDrag(e);
    }
    public void OnDrag(PointerEventData e) {if(ghost!=null) ghost.position=e.position;}
    public void OnEndDrag(PointerEventData e) {if(ghost!=null)Destroy(ghost.gameObject);ghost=null;dragging=false;}
    private void OnDisable(){if(ghost!=null)Destroy(ghost.gameObject);}
}


