// Reusable interaction view. Gameplay decisions belong to the owning screen and services.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BistroFoodCard : MonoBehaviour, IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string Key { get; private set; }
    public string Kind { get; private set; }
    public bool CanDrag => canDrag;
    public bool IsDragging => dragging;
    public bool IsFlipped => flipped;
    private Color dragColor, inkColor;
    private string front, back;
    private Text heading, detail;
    private GameObject icon;
    private bool flipped, dragging, canDrag;
    private RectTransform ghost;
    private Canvas canvas;
    private Coroutine flipAnimation;
    private float suppressClickUntil;
    private CanvasGroup faceGroup;
    private ScrollRect browsingScroll;
    private bool scrolling;
    private bool reducedMotion;
    private float flipSeconds;
    private bool dropClaimed;
    private int? activePointerId;
    private PointerEventData activePointerEvent;
    // One physical gesture may produce only one drop intent, even if an input module
    // repeats its callback. The owning service still decides whether that intent is valid.
    public bool TryClaimDrop(int pointerId)
    {
        if(!canDrag || !dragging || dropClaimed || activePointerId!=pointerId)return false;
        dropClaimed=true;return true;
    }
    public static BistroFoodCard Create(Transform parent,FoodData food,string key,string kind,string stats,BistroUITheme theme,bool draggable=true)
    {
        var rect=BistroWidgets.Box(parent,"Food card",theme.card,Vector2.zero,Vector2.one);
        if(theme.cardFrame!=null) { var img=rect.GetComponent<Image>(); img.sprite=theme.cardFrame; img.type=Image.Type.Sliced; }
        var card=rect.gameObject.AddComponent<BistroFoodCard>(); card.Key=key; card.Kind=kind; card.canDrag=draggable;
        card.faceGroup=rect.gameObject.AddComponent<CanvasGroup>();
        card.reducedMotion=theme.reducedMotion;card.flipSeconds=Mathf.Max(.01f,theme.cardFlipSeconds);
        card.dragColor=theme.card;card.dragColor.a=.90f;card.inkColor=theme.ink;
        card.canvas=parent.GetComponentInParent<Canvas>();
        card.browsingScroll=parent.GetComponentInParent<ScrollRect>();
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
        if(e.button!=PointerEventData.InputButton.Left || dragging || scrolling || e.dragging || Time.unscaledTime<suppressClickUntil || flipAnimation!=null)return;
        if(reducedMotion){RestoreFace(!flipped);return;}
        flipAnimation=StartCoroutine(Flip());
    }
    // Restore presentation after a data refresh without replaying the flip animation.
    public void RestoreFace(bool showBack)
    {
        if(flipAnimation!=null)StopCoroutine(flipAnimation);
        flipAnimation=null;flipped=showBack;transform.localScale=Vector3.one;SetFace();
    }
    private void SetFace()
    {
        icon.SetActive(!flipped); detail.text=flipped?back:front;
        detail.rectTransform.anchorMax=new Vector2(.94f,flipped?.77f:.33f);
    }
    private IEnumerator Flip()
    {
        float elapsed=0;bool swapped=false;
        while(elapsed<flipSeconds)
        {
            elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/flipSeconds);
            if(t>=.5f&&!swapped){flipped=!flipped;SetFace();swapped=true;}
            transform.localScale=new Vector3(Mathf.Max(.025f,Mathf.Abs(Mathf.Cos(t*Mathf.PI))),1,1);
            yield return null;
        }
        transform.localScale=Vector3.one;flipAnimation=null;
    }
    public void OnBeginDrag(PointerEventData e)
    {
        if(e.button!=PointerEventData.InputButton.Left || activePointerId.HasValue)return;
        if(canDrag && canvas==null)return;
        activePointerId=e.pointerId;activePointerEvent=e;
        if(!canDrag){scrolling=browsingScroll!=null;if(scrolling)browsingScroll.OnBeginDrag(e);return;}
        dragging=true;dropClaimed=false;
        faceGroup.alpha=.6f;
        if(flipAnimation!=null){StopCoroutine(flipAnimation);flipAnimation=null;transform.localScale=Vector3.one;}
        ghost=BistroWidgets.Box(canvas.transform,"Dragging card",dragColor,Vector2.zero,Vector2.zero);
        ghost.sizeDelta=new Vector2(145,195); ghost.GetComponent<Image>().raycastTarget=false;
        var t=new GameObject("Name",typeof(RectTransform),typeof(Text)); t.transform.SetParent(ghost,false);
        var rt=(RectTransform)t.transform; rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=Vector2.zero;rt.offsetMax=Vector2.zero;
        var label=t.GetComponent<Text>();label.font=heading.font;label.text=heading.text;label.color=inkColor;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
        OnDrag(e);
    }
    public void OnInitializePotentialDrag(PointerEventData e){if(!activePointerId.HasValue && browsingScroll!=null)browsingScroll.OnInitializePotentialDrag(e);}
    public void OnDrag(PointerEventData e)
    {
        if(activePointerId!=e.pointerId)return;
        if(scrolling){browsingScroll.OnDrag(e);return;}
        if(ghost!=null && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,e.position,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var local))
            ghost.localPosition=new Vector3(local.x,local.y,0);
    }
    public void OnEndDrag(PointerEventData e)
    {
        if(activePointerId!=e.pointerId)return;
        if(scrolling&&browsingScroll!=null)browsingScroll.OnEndDrag(e);
        scrolling=false;CancelDrag();suppressClickUntil=Time.unscaledTime+.1f;
    }
    private void CancelDrag()
    {
        if(scrolling && browsingScroll!=null && activePointerEvent!=null)
        {browsingScroll.OnEndDrag(activePointerEvent);browsingScroll.StopMovement();}
        scrolling=false;activePointerId=null;activePointerEvent=null;
        if(ghost!=null)Destroy(ghost.gameObject);ghost=null;dragging=false;if(faceGroup!=null)faceGroup.alpha=1;
    }
    private void OnApplicationFocus(bool focused){if(!focused)CancelDrag();}
    private void OnDisable(){CancelDrag();scrolling=false;StopAllCoroutines();flipAnimation=null;transform.localScale=Vector3.one;}
}


