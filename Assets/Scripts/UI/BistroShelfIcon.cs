// Existing vector HUD shapes rasterized once with supersampled alpha; not food/world art.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroShelfIcon : MaskableGraphic
{
    public string Symbol;
    // Uses the sprite when one is assigned (store logo, theme icon); otherwise draws the vector symbol.
    public static void Add(Transform parent,Sprite sprite,string fallbackSymbol,Vector2 min,Vector2 max,Color tint)
    {
        if(sprite!=null){BistroShelfStyle.Artwork(parent,fallbackSymbol+" icon",sprite,min,max,tint,true);return;}
        Add(parent,fallbackSymbol,min,max,tint);
    }
    public static void Add(Transform parent,string symbol,Vector2 min,Vector2 max,Color tint)
    {
        if(string.IsNullOrEmpty(symbol))return;
        var go=new GameObject(symbol+" icon",typeof(RectTransform));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
        var icon=go.AddComponent<BistroShelfIcon>();icon.Symbol=symbol;icon.color=tint;icon.raycastTarget=false;
    }
    private static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    private bool baking;
    public override Texture mainTexture=>GetTexture();
    private Texture2D GetTexture()
    {
        string key=Symbol??"";
        if(textures.TryGetValue(key,out var cached)&&cached!=null)return cached;
        const int size=128,scale=4,high=size*scale;
        var coverage=new bool[high*high];
        var mesh=new Mesh();
        using(var geometry=new VertexHelper())
        {
            baking=true;BuildSymbol(geometry);baking=false;geometry.FillMesh(mesh);
        }
        var vertices=mesh.vertices;var indices=mesh.triangles;
        for(int t=0;t<indices.Length;t+=3)
        {
            Vector2 a=(Vector2)vertices[indices[t]]*scale,b=(Vector2)vertices[indices[t+1]]*scale,c=(Vector2)vertices[indices[t+2]]*scale;
            float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.001f)continue;
            int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))),0,high-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))),0,high-1);
            int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))),0,high-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))),0,high-1);
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
            {
                if(coverage[y*high+x])continue;
                var point=new Vector2(x+.5f,y+.5f);
                float u=Cross(b-a,point-a),v=Cross(c-b,point-b),w=Cross(a-c,point-c);
                if(area>0?u>=0&&v>=0&&w>=0:u<=0&&v<=0&&w<=0)coverage[y*high+x]=true;
            }
        }
        if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            int samples=0;for(int sy=0;sy<scale;sy++)for(int sx=0;sx<scale;sx++)if(coverage[(y*scale+sy)*high+x*scale+sx])samples++;
            pixels[y*size+x]=new Color32(255,255,255,(byte)(samples*255/(scale*scale)));
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Smooth HUD "+key,filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        texture.SetPixels32(pixels);texture.Apply(true,true);textures[key]=texture;return texture;
    }
    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    protected override void OnPopulateMesh(VertexHelper v)
    {
        v.Clear();var r=rectTransform.rect;float size=Mathf.Min(r.width,r.height);var origin=r.center-Vector2.one*size*.5f;
        v.AddVert(origin,color,new Vector2(0,0));v.AddVert(origin+Vector2.up*size,color,new Vector2(0,1));
        v.AddVert(origin+Vector2.one*size,color,new Vector2(1,1));v.AddVert(origin+Vector2.right*size,color,new Vector2(1,0));
        v.AddTriangle(0,1,2);v.AddTriangle(0,2,3);
    }
    private void BuildSymbol(VertexHelper v)
    {
        v.Clear();
        switch(Symbol)
        {
            case "leaf": Poly(v,new Vector2(.48f,.1f),new Vector2(.12f,.42f),new Vector2(.18f,.83f),new Vector2(.42f,.65f));Poly(v,new Vector2(.5f,.15f),new Vector2(.48f,.68f),new Vector2(.94f,.94f),new Vector2(.88f,.40f));break;
            case "Menu":for(int i=0;i<3;i++)Rect(v,.14f,.25f+i*.23f,.86f,.33f+i*.23f);break;
            case "Customer":Circle(v,.5f,.75f,.17f);Poly(v,new Vector2(.16f,0),new Vector2(.22f,.45f),new Vector2(.38f,.56f),new Vector2(.62f,.56f),new Vector2(.78f,.45f),new Vector2(.84f,0));break;
            case "Guests":Circle(v,.35f,.72f,.16f);Circle(v,.73f,.67f,.12f);Rect(v,.12f,.20f,.55f,.51f);Rect(v,.61f,.20f,.88f,.47f);break;
            case "Recipes":Circle(v,.28f,.66f,.18f);Circle(v,.51f,.76f,.22f);Circle(v,.75f,.66f,.18f);Rect(v,.29f,.27f,.75f,.63f);Rect(v,.26f,.12f,.78f,.20f);break;
            case "Pantry":Rect(v,.19f,.14f,.84f,.70f);Rect(v,.27f,.77f,.75f,.85f);break;
            case "wallet":Rect(v,.12f,.20f,.90f,.73f);Rect(v,.22f,.79f,.75f,.86f);break;
            case "cart":Line(v,new Vector2(.06f,.85f),new Vector2(.22f,.85f),.07f);Line(v,new Vector2(.22f,.85f),new Vector2(.35f,.30f),.07f);Line(v,new Vector2(.32f,.40f),new Vector2(.80f,.40f),.07f);Line(v,new Vector2(.28f,.72f),new Vector2(.91f,.72f),.07f);Line(v,new Vector2(.91f,.72f),new Vector2(.80f,.40f),.07f);Circle(v,.39f,.16f,.07f);Circle(v,.76f,.16f,.07f);break;
            case "LightGo":Circle(v,.5f,.5f,.44f);break;
            case "LightSlow":Poly(v,new Vector2(.5f,.93f),new Vector2(.04f,.10f),new Vector2(.96f,.10f));break;
            case "LightStop":{var p=new Vector2[8];for(int i=0;i<8;i++){float a=Mathf.PI/8+i*Mathf.PI/4;p[i]=new Vector2(.5f+Mathf.Cos(a)*.46f,.5f+Mathf.Sin(a)*.46f);}Poly(v,p);break;}
            case "Cart":Poly(v,new Vector2(.24f,.36f),new Vector2(.84f,.36f),new Vector2(.92f,.72f),new Vector2(.18f,.72f));Line(v,new Vector2(.04f,.86f),new Vector2(.18f,.86f),.07f);Line(v,new Vector2(.16f,.86f),new Vector2(.24f,.36f),.07f);Circle(v,.34f,.18f,.08f);Circle(v,.76f,.18f,.08f);break;
            case "City":Rect(v,.08f,.08f,.32f,.60f);Rect(v,.38f,.08f,.62f,.90f);Rect(v,.68f,.08f,.92f,.48f);break;
            case "Bistro":Rect(v,.18f,.26f,.66f,.70f);Rect(v,.69f,.38f,.83f,.58f);Rect(v,.08f,.12f,.90f,.20f);Rect(v,.32f,.78f,.38f,.94f);Rect(v,.46f,.78f,.52f,.94f);break;
            case "Sort":Circle(v,.5f,.80f,.13f);Circle(v,.5f,.50f,.13f);Circle(v,.5f,.20f,.13f);break;
            case "Day":Rect(v,.14f,.10f,.86f,.70f);Rect(v,.14f,.75f,.86f,.86f);Rect(v,.30f,.80f,.38f,.96f);Rect(v,.62f,.80f,.70f,.96f);break;
            case "Popularity":{var p=new Vector2[12];p[0]=new Vector2(.5f,.47f);for(int i=0;i<=10;i++){float a=Mathf.PI/2+i*Mathf.PI/5,r=i%2==0?.46f:.19f;p[i+1]=new Vector2(.5f+Mathf.Cos(a)*r,.47f+Mathf.Sin(a)*r);}Poly(v,p);break;}
            case "Help":Circle(v,.5f,.57f,.37f);Poly(v,new Vector2(.26f,.34f),new Vector2(.16f,.04f),new Vector2(.48f,.22f));break;
            default:Rect(v,.18f,.20f,.83f,.76f);break;
        }
    }
    private void Rect(VertexHelper v,float x,float y,float right,float top){Poly(v,new Vector2(x,y),new Vector2(x,top),new Vector2(right,top),new Vector2(right,y));}
    private void Circle(VertexHelper v,float x,float y,float radius){for(int i=0;i<64;i++){float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Poly(v,new Vector2(x,y),new Vector2(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),new Vector2(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius));}}
    private void Line(VertexHelper v,Vector2 a,Vector2 b,float width){var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Poly(v,a+n,b+n,b-n,a-n);}
    private void Poly(VertexHelper v,params Vector2[] points)
    {
        int start=v.currentVertCount;var r=baking?new Rect(0,0,128,128):rectTransform.rect;float size=Mathf.Min(r.width,r.height);var origin=r.center-Vector2.one*size*.5f;
        foreach(var p in points)v.AddVert(origin+p*size,color,Vector2.zero);
        for(int i=1;i<points.Length-1;i++)v.AddTriangle(start,start+i,start+i+1);
    }
}

