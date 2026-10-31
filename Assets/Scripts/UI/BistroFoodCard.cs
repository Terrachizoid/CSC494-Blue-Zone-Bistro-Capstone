// Reusable interaction view. Gameplay decisions belong to the owning screen and services.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BistroFoodCard : MonoBehaviour, IBistroDragSource, IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string Key { get; private set; }
    public string Kind { get; private set; }
    public bool CanDrag => canDrag;
    public bool IsDragging => dragging;
    public bool IsFlipped => flipped;
    private Color dragColor, inkColor;
    private string front, back;
    private Text heading, detail;
    private bool illustrated;
    private GameObject titlePlate;
    private GameObject shelfFooter;
    private GameObject shelfForeground;
    private GameObject shelfBack;
    private Color shelfTitleColor;
    private Sprite shelfArtwork;
    private BistroUITheme presentationTheme;
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
        card.presentationTheme=theme;
        card.faceGroup=rect.gameObject.AddComponent<CanvasGroup>();
        card.reducedMotion=theme.reducedMotion;card.flipSeconds=Mathf.Max(.01f,theme.cardFlipSeconds);
        card.dragColor=theme.card;card.dragColor.a=.90f;card.inkColor=theme.ink;
        card.canvas=parent.GetComponentInParent<Canvas>();
        card.browsingScroll=parent.GetComponentInParent<ScrollRect>();
        card.heading=BistroWidgets.Label(rect,food==null?key:food.displayName,theme,new Vector2(.06f,.78f),new Vector2(.94f,.97f),22,theme.ink);
        var image=BistroWidgets.Box(rect,"Image placeholder",Color.Lerp(theme.card,theme.accent,.5f),new Vector2(.12f,.34f),new Vector2(.88f,.76f));
        if(food?.icon!=null) {image.GetComponent<Image>().sprite=food.icon; image.GetComponent<Image>().preserveAspect=true; image.GetComponent<Image>().color=Color.white;}
        image.GetComponent<Image>().raycastTarget=false; card.icon=image.gameObject;
        // Appeal badge on the front (child of the image, so it hides when the card flips).
        // Stands in for a delight icon until art exists; it is not a Traffic Light answer.
        if(theme.showCardAppeal && food!=null)
        {
            // Soft strip along the bottom of the image, in the card's own palette.
            var badge=BistroWidgets.Box(image,"Appeal badge",Color.Lerp(theme.card,theme.accent,.25f),new Vector2(0f,0f),new Vector2(1f,.24f));
            badge.GetComponent<Image>().raycastTarget=false;
            var appeal=BistroWidgets.Label(badge,$"Appeal {food.ingredientAppeal:0.#}/10",theme,Vector2.zero,Vector2.one,14,theme.ink);
            // Never wrap or shrink: wrapping pushed the number out of the box.
            appeal.resizeTextForBestFit=false;appeal.horizontalOverflow=HorizontalWrapMode.Overflow;appeal.verticalOverflow=VerticalWrapMode.Overflow;
        }
        card.front=(food?.marketingLabel??"")+"\n"+stats+"\nClick to flip";
        card.back=food==null?"Food unavailable": "Category: "+IntroMealPlan.CategoryName(food.recipeCategory)+"\n\nIngredients\n"+food.ingredientList+"\n\nDaily Dozen\n"+IntroMealPlan.ReadableList(food.dailyDozenCategories)+"\n\nStorage: "+IntroMealPlan.Readable(food.storageType);
        if(theme.debugCardDetails && Debug.isDebugBuild && food!=null) card.back+="\nDEBUG: "+food.trafficLight;
        card.detail=BistroWidgets.Label(rect,card.front,theme,new Vector2(.06f,.03f),new Vector2(.94f,.33f),17,theme.ink);
        return card;
    }
    // Optional presentation skin; retains the same identity, flip state and drag intent.
    // All art comes from the food's data asset (Foods.csv) and the UI theme; no art lookups by ID here.
    // logo: the selling store's logo (null on pantry/service cards).
    public void ApplyShelfArtwork(FoodData food,string price=null,int servings=0,Sprite logo=null)
    {
        var artwork=food==null?null:food.icon;
        if(artwork==null)return;
        illustrated=true;
        shelfArtwork=artwork;
        var theme=presentationTheme;
        var cardShadow=GetComponent<Shadow>()??gameObject.AddComponent<Shadow>();
        cardShadow.effectColor=new Color(.09f,.14f,.10f,.28f);cardShadow.effectDistance=new Vector2(3,-3);cardShadow.useGraphicAlpha=true;
        var rootImage=GetComponent<Image>();rootImage.sprite=BistroShelfStyle.Rounded;rootImage.type=Image.Type.Sliced;
        if(GetComponent<Mask>()==null)gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var rt=(RectTransform)icon.transform;
        rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
        var img=icon.GetComponent<Image>();img.sprite=artwork;img.color=Color.white;img.preserveAspect=false;
        icon.transform.SetAsFirstSibling();
        var reverse=BistroWidgets.Box(transform,"Illustrated card back",Color.white,Vector2.zero,Vector2.one);
        reverse.GetComponent<Image>().sprite=food.cardBack!=null?food.cardBack:artwork;reverse.GetComponent<Image>().raycastTarget=false;
        var veil=BistroWidgets.Box(reverse,"Reading contrast",new Color(.06f,.10f,.09f,.58f),Vector2.zero,Vector2.one);
        veil.GetComponent<Image>().raycastTarget=false;shelfBack=reverse.gameObject;
        // Move existing appeal badge out of the illustration's focal area.
        if(icon.transform.childCount>0){var badge=icon.transform.GetChild(0);back+="\n"+badge.GetComponentInChildren<Text>().text;badge.gameObject.SetActive(false);}
        // Small appeal tag in the top-right corner of the front (the store logo sits top-left), clear of the art's centre.
        if(theme==null||theme.showCardAppeal)
        {
            var chip=BistroShelfStyle.Panel(icon.transform,"Appeal tag",new Color(.10f,.16f,.13f,.62f),new Vector2(.60f,.86f),new Vector2(.95f,.955f));
            chip.GetComponent<Image>().raycastTarget=false;
            BistroShelfStyle.Text(chip,$"Appeal {food.ingredientAppeal:0.#}",theme,Vector2.zero,Vector2.one,14,Color.white,false,TextAnchor.MiddleCenter).raycastTarget=false;
        }
        if(logo!=null)BistroShelfIcon.Add(icon.transform,logo,"leaf",new Vector2(.07f,.84f),new Vector2(.19f,.95f),food.lightLogo?new Color(.93f,.95f,.78f):BistroShelfStyle.Green);
        back+="\n\n"+front.Replace("Click to flip","");
        var plate=BistroShelfStyle.Panel(transform,"Name plaque",new Color(.96f,.93f,.84f),new Vector2(.04f,.16f),new Vector2(.96f,.33f));
        plate.GetComponent<Image>().raycastTarget=false;titlePlate=plate.gameObject;
        var plaqueSprite=theme==null?null:food.darkPlaque?theme.plaqueDark:theme.plaqueLight;
        if(plaqueSprite!=null){plate.GetComponent<Image>().sprite=plaqueSprite;plate.GetComponent<Image>().type=Image.Type.Simple;plate.GetComponent<Image>().color=Color.white;}
        // Neighbouring cards tilt opposite ways so a shelf row looks hand-placed.
        plate.localRotation=Quaternion.Euler(0,0,transform.GetSiblingIndex()%2==0?1.2f:-1.1f);
        shelfTitleColor=food.darkPlaque?BistroShelfStyle.Cream:BistroShelfStyle.Ink;
        if(food.cardSprig&&theme!=null&&theme.cardSprig!=null)
        {
            var leaves=BistroShelfStyle.Artwork(transform,"Card sprig",theme.cardSprig,new Vector2(-.035f,.265f),new Vector2(.24f,.435f),Color.white,true);
            shelfForeground=leaves.gameObject;
        }
        heading.transform.SetAsLastSibling();detail.transform.SetAsLastSibling();
        if(BistroShelfStyle.TitleFont!=null)heading.font=BistroShelfStyle.TitleFont;heading.fontStyle=FontStyle.Normal;heading.resizeTextMaxSize=27;
        var footer=BistroWidgets.Box(transform,"Price and servings",Color.clear,Vector2.zero,new Vector2(1,.16f));footer.GetComponent<Image>().raycastTarget=false;shelfFooter=footer.gameObject;
        BistroShelfStyle.Text(footer,servings+" servings",theme,new Vector2(.07f,.08f),new Vector2(.56f,.95f),17,Color.white);
        BistroShelfStyle.Text(footer,price??"",theme,new Vector2(.55f,.08f),new Vector2(.94f,.95f),24,Color.white,false,TextAnchor.MiddleRight).fontStyle=FontStyle.Bold;
        SetFace();
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
        if(illustrated)
        {
            titlePlate.SetActive(!flipped);
            if(shelfBack!=null)shelfBack.SetActive(flipped);
            if(shelfForeground!=null)shelfForeground.SetActive(!flipped);
            heading.color=flipped?BistroShelfStyle.Cream:shelfTitleColor;
            if(shelfFooter!=null)shelfFooter.SetActive(!flipped);
            detail.gameObject.SetActive(flipped);
            heading.rectTransform.anchorMin=new Vector2(.06f,flipped?.78f:.16f);
            heading.rectTransform.anchorMax=new Vector2(.94f,flipped?.97f:.33f);
            detail.rectTransform.anchorMax=new Vector2(.94f,flipped?.77f:.22f);
            detail.color=new Color(.98f,.97f,.92f);
        }
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
        if(shelfArtwork!=null){ghost.GetComponent<Image>().sprite=shelfArtwork;ghost.GetComponent<Image>().color=Color.white;}
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


