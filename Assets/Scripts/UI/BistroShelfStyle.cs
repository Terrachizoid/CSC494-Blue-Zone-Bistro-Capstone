// Shared visual primitives for the illustrated market. No stock or purchase rules live here.
using System;
using UnityEngine;
using UnityEngine.UI;

public static class BistroShelfStyle
{
    public static readonly Color Cream=new Color(.94f,.92f,.86f), Ink=new Color(.09f,.16f,.15f), Green=new Color(.10f,.28f,.22f);
    private static Sprite rounded;
    private static Sprite softVeil;
    // UI opacity mask: preserves a readable centre and feathers into the artwork.
    public static RectTransform SoftVeil(Transform parent,string name,Vector2 min,Vector2 max,float opacity)
    {
        if(softVeil==null)
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size;
                float edge=Mathf.Min(Mathf.Min(u,1-u),Mathf.Min(v,1-v));
                float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/.22f));
                pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();
            softVeil=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f));
        }
        var tint=Cream;tint.a=Mathf.Clamp01(opacity);
        var panel=Panel(parent,name,tint,min,max,false);
        panel.GetComponent<Image>().sprite=softVeil;
        return panel;
    }
    private static Font titleFont,bodyFont;
    // Draws a sprite (from a data asset or the UI theme). Hidden when no art is assigned.
    public static RectTransform Artwork(Transform parent,string name,Sprite sprite,Vector2 min,Vector2 max,Color tint,bool preserveAspect=false)
    {
        var r=Panel(parent,name,tint,min,max,false);var image=r.GetComponent<UnityEngine.UI.Image>();
        image.sprite=sprite;image.preserveAspect=preserveAspect;image.raycastTarget=false;
        if(sprite==null)r.gameObject.SetActive(false);
        return r;
    }
    public static Font TitleFont=>titleFont!=null?titleFont:(titleFont=Resources.Load<Font>("BistroArt/Fonts/Lora-Bold"));
    public static Font BodyFont=>bodyFont!=null?bodyFont:(bodyFont=Resources.Load<Font>("BistroArt/Fonts/Body"));
    public static Sprite Rounded
    {
        get
        {
            if(rounded!=null)return rounded;
            const int size=64;const float radius=12;
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);tex.name="Shelf rounded shape";tex.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(radius-(x+.5f),x+.5f-(size-radius));
                float dy=Mathf.Max(radius-(y+.5f),y+.5f-(size-radius));
                float distance=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-distance+.5f));
            }
            tex.SetPixels(pixels);tex.Apply();
            rounded=Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));
            return rounded;
        }
    }
    public static RectTransform Panel(Transform parent,string name,Color color,Vector2 min,Vector2 max,bool round=true)
    {
        var r=BistroWidgets.Box(parent,name,color,min,max);
        if(round){var image=r.GetComponent<Image>();image.sprite=Rounded;image.type=Image.Type.Sliced;}
        return r;
    }
    public static Text Text(Transform parent,string value,BistroUITheme theme,Vector2 min,Vector2 max,int size,Color color,bool title=false,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var t=BistroWidgets.Label(parent,value,theme,min,max,size,color);t.font=title?TitleFont:BodyFont;
        if(t.font==null)t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment=align;return t;
    }
    public static Button Button(Transform parent,string label,BistroUITheme theme,Vector2 min,Vector2 max,Color fill,Color ink,Action action,bool title=false)
    {
        var r=Panel(parent,label,fill,min,max);var button=r.gameObject.AddComponent<Button>();button.targetGraphic=r.GetComponent<Image>();
        Text(r,label,theme,new Vector2(.07f,.06f),new Vector2(.93f,.94f),22,ink,title,TextAnchor.MiddleCenter);
        button.onClick.AddListener(()=>action());return button;
    }
}

