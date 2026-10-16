// Reusable Canvas primitives, flip/drag cards, and rule-agnostic drop slots.
// The owning screen supplies drag meaning, warning state and callbacks; views never spend money.
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class BistroWidgets
{
    public static RectTransform Box(Transform parent, string name, Color color, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent,false);
        var r = (RectTransform)go.transform; r.anchorMin=min; r.anchorMax=max; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color=color;
        return r;
    }
    public static Text Label(Transform parent, string value, BistroUITheme theme, Vector2 min, Vector2 max, int size=20, Color? color=null)
    {
        // Text and Image both derive from Graphic; Unity permits only one per object.
        // Destroy(Image) is deferred until frame end, so create a text-only object directly.
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        var t=go.GetComponent<Text>(); t.text=value; t.font=theme.font != null ? theme.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize=size; t.color=color ?? theme.card; t.alignment=TextAnchor.MiddleCenter;
        t.resizeTextForBestFit=true; t.resizeTextMinSize=12; t.resizeTextMaxSize=size; t.raycastTarget=false;
        return t;
    }
    public static Button Button(Transform parent,string title,BistroUITheme theme,Vector2 min,Vector2 max,Action action)
    {
        var r=Box(parent,title,theme.accent,min,max); var b=r.gameObject.AddComponent<Button>();
        b.targetGraphic=r.GetComponent<Image>(); Label(r,title,theme,new Vector2(.04f,.05f),new Vector2(.96f,.95f),20,theme.ink);
        if(!theme.reducedMotion)r.gameObject.AddComponent<BistroMotion>();
        b.onClick.AddListener(()=>action()); return b;
    }
    public static RectTransform Grid(Transform parent,BistroUITheme theme,Vector2 min,Vector2 max)
    {
        var viewport=Box(parent,"Card shelf",theme.panel,min,max); viewport.gameObject.AddComponent<RectMask2D>();
        var content=Box(viewport,"Cards",Color.clear,new Vector2(0,1),Vector2.one);
        content.pivot=new Vector2(.5f,1);
        var grid=content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize=new Vector2(166,238); grid.spacing=new Vector2(16,16); grid.padding=new RectOffset(18,18,18,18);
        content.gameObject.AddComponent<BistroResponsiveGrid>();
        var fit=content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport; scroll.content=content;
        scroll.horizontal=false; scroll.scrollSensitivity=35; scroll.movementType=ScrollRect.MovementType.Clamped;
        return content;
    }

    // A non-interactive hint above an empty shelf; it never becomes a grid item or drop target.
    public static void EmptyShelf(RectTransform content,string message,BistroUITheme theme)
    {
        if(content.childCount==0)Label(content.parent,message,theme,new Vector2(.08f,.20f),new Vector2(.92f,.80f),22);
    }

    // Long feedback must scroll rather than shrink until unreadable or clip off-screen.
    public static void ReadingPanel(Transform parent,string value,BistroUITheme theme,Vector2 min,Vector2 max)
    {
        var viewport=Box(parent,"Reading panel",theme.panel,min,max);
        viewport.gameObject.AddComponent<RectMask2D>();
        var text=Label(viewport,value,theme,new Vector2(0,1),Vector2.one,22);
        text.rectTransform.pivot=new Vector2(.5f,1);
        text.rectTransform.offsetMin=new Vector2(18,0);text.rectTransform.offsetMax=new Vector2(-18,0);
        text.alignment=TextAnchor.UpperLeft;text.resizeTextForBestFit=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
        var fit=text.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=text.rectTransform;
        scroll.horizontal=false;scroll.scrollSensitivity=35;scroll.movementType=ScrollRect.MovementType.Clamped;
    }
}


