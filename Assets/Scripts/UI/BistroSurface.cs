using UnityEngine;
using UnityEngine.UI;

// Subtle shading on existing panel geometry. No gameplay or per-item art selection.
public sealed class BistroSurface : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh)
    {
        if(!IsActive())return;
        var rect=((RectTransform)transform).rect;var vertex=new UIVertex();
        for(int i=0;i<mesh.currentVertCount;i++)
        {
            mesh.PopulateUIVertex(ref vertex,i);
            float y=Mathf.InverseLerp(rect.yMin,rect.yMax,vertex.position.y);
            Color tint=vertex.color;float shade=Mathf.Lerp(.93f,1.025f,y);
            tint.r=Mathf.Clamp01(tint.r*shade);tint.g=Mathf.Clamp01(tint.g*shade);tint.b=Mathf.Clamp01(tint.b*shade);
            vertex.color=tint;mesh.SetUIVertex(vertex,i);
        }
    }
    public static void Dress(RectTransform panel,Color accent,Sprite sprig=null)
    {
        if(panel.GetComponent<BistroSurface>()!=null)return;
        panel.gameObject.AddComponent<BistroSurface>();
        var line=BistroWidgets.Box(panel,"Fine accent",new Color(accent.r,accent.g,accent.b,.28f),new Vector2(.035f,.025f),new Vector2(.965f,.03f));
        line.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
        if(sprig!=null)BistroShelfStyle.Artwork(panel,"Quiet botanical detail",sprig,new Vector2(.78f,.10f),new Vector2(.98f,.90f),new Color(accent.r,accent.g,accent.b,.10f),true);
    }
}
