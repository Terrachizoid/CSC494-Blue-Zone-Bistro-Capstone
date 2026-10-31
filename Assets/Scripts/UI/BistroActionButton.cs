using System;
using UnityEngine;
using UnityEngine.EventSystems;

public enum BistroActionRole { Primary, Secondary, Caution }

// Reusable contextual controls. Callers supply role, bounds and action; no label/ID inference.
public sealed class BistroActionButton : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private UnityEngine.UI.Outline rim;
    private UnityEngine.UI.Shadow shadow;
    private UnityEngine.UI.Button button;
    private Color edge;
    public BistroActionRole Role { get; private set; }
    public static UnityEngine.UI.Button Create(Transform parent,string label,BistroUITheme theme,Vector2 min,Vector2 max,BistroActionRole role,Action action,bool title=false)
    {
        Color fill=role==BistroActionRole.Primary?theme.actionPrimary:role==BistroActionRole.Caution?theme.actionCaution:theme.actionSecondary;
        var rect=BistroShelfStyle.Panel(parent,label,fill,min,max);
        rect.gameObject.AddComponent<BistroSurface>();
        var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
        if(!theme.reducedMotion)rect.gameObject.AddComponent<BistroMotion>(); // shared hover/press motion family
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.08f,1.08f,1.08f);colors.selectedColor=new Color(1.06f,1.06f,1.06f);colors.pressedColor=new Color(.84f,.87f,.82f);colors.disabledColor=new Color(.65f,.65f,.65f,.65f);colors.fadeDuration=theme.reducedMotion?0:.10f;button.colors=colors;
        var controller=rect.gameObject.AddComponent<BistroActionButton>();controller.Role=role;controller.button=button;
        controller.shadow=rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();controller.shadow.effectColor=new Color(.08f,.14f,.11f,.18f);controller.shadow.effectDistance=new Vector2(0,-2);
        controller.edge=new Color(theme.actionInk.r,theme.actionInk.g,theme.actionInk.b,.22f);
        controller.rim=rect.gameObject.AddComponent<UnityEngine.UI.Outline>();controller.rim.effectColor=controller.edge;controller.rim.effectDistance=new Vector2(1,-1);
        var highlight=BistroWidgets.Box(rect,"Upper edge",new Color(1,1,1,.24f),new Vector2(.06f,.92f),new Vector2(.94f,.94f));highlight.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
        var text=BistroShelfStyle.Text(rect,label,theme,new Vector2(.07f,.08f),new Vector2(.93f,.92f),22,theme.actionInk,title,TextAnchor.MiddleCenter);text.raycastTarget=false;
        button.onClick.AddListener(()=>action?.Invoke());return button;
    }
    public void OnSelect(BaseEventData e){rim.effectColor=new Color(.98f,.96f,.84f,.95f);rim.effectDistance=new Vector2(2,-2);}
    public void OnDeselect(BaseEventData e){rim.effectColor=edge;rim.effectDistance=new Vector2(1,-1);}
    public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&button.IsInteractable())shadow.effectDistance=Vector2.zero;}
    public void OnPointerUp(PointerEventData e){shadow.effectDistance=new Vector2(0,-2);}
    public void OnPointerExit(PointerEventData e){shadow.effectDistance=new Vector2(0,-2);}
    private void OnDisable(){if(shadow!=null)shadow.effectDistance=new Vector2(0,-2);}
}
