// Convex image-space hit region; receives clicks while dragging bubbles to the parent ScrollRect.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BistroWorldRegion : MaskableGraphic, ICanvasRaycastFilter
{
    public Vector2[] Points;
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();if(Points==null||Points.Length<3)return;
        var r=rectTransform.rect;
        foreach(var p in Points)helper.AddVert(new Vector3(r.xMin+p.x*r.width,r.yMax-p.y*r.height),color,Vector2.zero);
        for(int i=1;i<Points.Length-1;i++)helper.AddTriangle(0,i,i+1);
    }
    public bool IsRaycastLocationValid(Vector2 point,Camera camera)
    {
        if(Points==null||Points.Length<3||!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,point,camera,out var local))return false;
        var r=rectTransform.rect;var p=new Vector2((local.x-r.xMin)/r.width,(r.yMax-local.y)/r.height);bool inside=false;
        for(int i=0,j=Points.Length-1;i<Points.Length;j=i++)
            if((Points[i].y>p.y)!=(Points[j].y>p.y)&&p.x<(Points[j].x-Points[i].x)*(p.y-Points[i].y)/(Points[j].y-Points[i].y)+Points[i].x)inside=!inside;
        return inside;
    }
}

public sealed class BistroWorldButton : Button
{
    public override void OnPointerClick(PointerEventData data){if(!data.dragging)base.OnPointerClick(data);}
}

