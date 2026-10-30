using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// A reference card, not a meal builder. Flipping changes presentation only.
public sealed class BistroRecipeCard : MonoBehaviour, IPointerClickHandler
{
    private GameObject front,back;
    private Coroutine flipAnimation;
    private bool reducedMotion;
    private float flipSeconds;
    public System.Action Selected;
    public bool IsFlipped=>back!=null&&back.activeSelf;
    public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&!e.dragging){if(Selected!=null)Selected();else Flip();}}
    public void Flip()
    {
        if(flipAnimation!=null)return;
        if(reducedMotion||!Application.isPlaying){SwapFace();return;}
        flipAnimation=StartCoroutine(AnimateFlip());
    }
    private void SwapFace(){bool next=!IsFlipped;front.SetActive(!next);back.SetActive(next);}
    private IEnumerator AnimateFlip()
    {
        // Begin on a fresh frame so a long scene-loading frame cannot skip the whole flip.
        yield return null;
        float elapsed=0;bool swapped=false;
        while(elapsed<flipSeconds)
        {
            elapsed+=Mathf.Min(Time.unscaledDeltaTime,.05f);float t=Mathf.Clamp01(elapsed/flipSeconds);
            if(t>=.5f&&!swapped){SwapFace();swapped=true;}
            transform.localScale=new Vector3(Mathf.Max(.025f,Mathf.Abs(Mathf.Cos(t*Mathf.PI))),1,1);
            yield return null;
        }
        transform.localScale=Vector3.one;flipAnimation=null;
    }
    private void OnDisable(){StopAllCoroutines();flipAnimation=null;transform.localScale=Vector3.one;}
    // Washed out when the pantry cannot fill the required slots. Still clickable: a mismatched dish is allowed.
    // A cream wash on top (not group opacity), so the stacked layers of the card never show through each other.
    public void MarkUnavailable()
    {
        if(transform.Find("Unavailable wash")!=null)return;
        var wash=BistroWidgets.Box(transform,"Unavailable wash",new Color(.95f,.93f,.87f,.55f),Vector2.zero,Vector2.one);
        wash.SetAsLastSibling();wash.GetComponent<Image>().raycastTarget=false;
    }
    // Art: recipe.icon (dish) and recipe.cardBack (background only) from Recipes.csv; category icons from Categories.csv via foods.
    // The dish is drawn once, in the top picture; the rest of the front shows the background-only card back.
    public static BistroRecipeCard Create(Transform parent,RecipeData recipe,BistroUITheme theme,FoodDatabase foods)
    {
        var root=BistroShelfStyle.Panel(parent,"Recipe "+recipe.id,BistroShelfStyle.Cream,Vector2.zero,Vector2.one);
        var shadow=root.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.18f);shadow.effectDistance=new Vector2(2,-3);
        root.gameObject.AddComponent<Mask>().showMaskGraphic=true;
        var card=root.gameObject.AddComponent<BistroRecipeCard>();
        card.reducedMotion=theme.reducedMotion;card.flipSeconds=Mathf.Max(.1f,theme.cardFlipSeconds);
        var front=BistroWidgets.Box(root,"Recipe front",BistroShelfStyle.Cream,Vector2.zero,Vector2.one);card.front=front.gameObject;
        var art=recipe.icon;
        var picture=BistroWidgets.Box(front,"Dish artwork",Color.white,new Vector2(0,.30f),Vector2.one);
        picture.GetComponent<Image>().sprite=art;
        if(art==null){picture.GetComponent<Image>().color=new Color(.76f,.81f,.71f);BistroShelfStyle.Text(picture,"Dish illustration",theme,Vector2.zero,Vector2.one,22,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);}
        front.GetComponent<Image>().sprite=recipe.cardBack;front.GetComponent<Image>().color=recipe.cardBack!=null?Color.white:BistroShelfStyle.Cream;
        BistroShelfStyle.SoftVeil(front,"Footer veil",new Vector2(-.06f,-.08f),new Vector2(1.06f,.38f),theme.recipeFrontVeilOpacity);
        var name=BistroShelfStyle.Panel(front,"Recipe name",BistroShelfStyle.Cream,new Vector2(.04f,.29f),new Vector2(.96f,.43f));
        BistroShelfStyle.Text(name,recipe.displayName,theme,new Vector2(.04f,0),new Vector2(.96f,1),25,BistroShelfStyle.Ink,true,TextAnchor.MiddleCenter);
        BistroShelfStyle.Text(front,"$"+recipe.baseSalePrice.ToString("0.00")+"\nBase price",theme,new Vector2(.07f,.16f),new Vector2(.93f,.29f),22,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
        var tags=(recipe.cravingTags??new CravingTag[0]).Where(t=>t!=CravingTag.None).Distinct().ToArray();
        for(int i=0;i<tags.Length;i++)
        {
            float w=.9f/tags.Length;var badge=BistroShelfStyle.Panel(front,"Craving",new Color(.77f,.82f,.69f),new Vector2(.05f+i*w,.08f),new Vector2(.04f+(i+1)*w,.15f));
            BistroShelfStyle.Text(badge,tags[i].ToString(),theme,Vector2.zero,Vector2.one,16,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
        }
        BistroShelfStyle.Text(front,"Click to flip",theme,new Vector2(.08f,.01f),new Vector2(.92f,.07f),14,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
        var back=BistroWidgets.Box(root,"Recipe back",Color.white,Vector2.zero,Vector2.one);card.back=back.gameObject;
        back.GetComponent<Image>().sprite=recipe.cardBack;
        if(back.GetComponent<Image>().sprite==null)back.GetComponent<Image>().color=BistroShelfStyle.Cream;
        // No reading veil: the back art is already a soft background (Owen, 2026-10-28). Category tiles carry their own panels.
        var veil=BistroWidgets.Box(back,"Requirements",Color.clear,new Vector2(.035f,.035f),new Vector2(.965f,.965f));
        BistroShelfStyle.Text(veil,recipe.displayName,theme,new Vector2(.05f,.86f),new Vector2(.95f,.98f),23,BistroShelfStyle.Ink,true,TextAnchor.MiddleCenter);
        Group(veil,recipe,theme,foods,false,.48f,.85f);
        Group(veil,recipe,theme,foods,true,.13f,.48f);
        BistroShelfStyle.Text(veil,"Choose foods from these categories\nClick to flip back",theme,new Vector2(.04f,.01f),new Vector2(.96f,.12f),14,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
        back.gameObject.SetActive(false);
        foreach(var graphic in root.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=graphic.name=="Card shelf";
        root.GetComponent<Image>().raycastTarget=true;
        return card;
    }
    private static void Group(RectTransform parent,RecipeData recipe,BistroUITheme theme,FoodDatabase foods,bool optional,float bottom,float top)
    {
        var area=BistroWidgets.Box(parent,optional?"Optional":"Required",Color.clear,new Vector2(.04f,bottom),new Vector2(.96f,top));
        BistroShelfStyle.Text(area,optional?"Optional":"Required",theme,new Vector2(0,.79f),Vector2.one,19,BistroShelfStyle.Ink,true);
        var slots=(recipe.slots??new RecipeSlot[0]).Where(s=>s!=null&&s.optional==optional).GroupBy(s=>s.category).Select(g=>new {category=g.Key,quantity=g.Sum(s=>(long)s.servings)}).ToArray();
        if(slots.Length==0){BistroShelfStyle.Text(area,"None",theme,new Vector2(0,.1f),new Vector2(1,.7f),17,BistroShelfStyle.Ink);return;}
        var grid=BistroWidgets.Grid(area,theme,Vector2.zero,new Vector2(1,.78f));
        ((RectTransform)grid.parent).GetComponent<Image>().color=Color.clear;
        grid.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(84,114));
        var layout=grid.GetComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.Flexible;layout.cellSize=new Vector2(84,114);layout.spacing=new Vector2(6,6);layout.padding=new RectOffset(0,0,0,0);
        foreach(var slot in slots)
        {
            var tile=BistroShelfStyle.Panel(grid,"Ingredient "+slot.category,BistroShelfStyle.Cream,Vector2.zero,Vector2.one);
            var icon=BistroWidgets.Box(tile,"Category icon",Color.white,new Vector2(.08f,.46f),new Vector2(.92f,.98f));
            icon.GetComponent<Image>().sprite=foods==null?null:foods.CategoryIcon(slot.category);icon.GetComponent<Image>().preserveAspect=true;
            if(icon.GetComponent<Image>().sprite==null)icon.gameObject.SetActive(false);
            BistroShelfStyle.Text(tile,IntroMealPlan.CategoryName(slot.category)+"\n×"+slot.quantity,theme,new Vector2(.02f,.02f),new Vector2(.98f,.46f),14,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
        }
    }
}


