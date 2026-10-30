// Fits the image without stretching; normalized regions remain aligned on resize.
using UnityEngine;
using UnityEngine.UI;
public sealed class BistroWorldLayout : MonoBehaviour
{
    public RectTransform Viewport; public float Aspect=16f/9f; public float Zoom=1; public bool Cover;
    private Vector2 last;
    private float lastZoom,lastAspect;private bool lastCover;
    public void Apply()
    {
        if(Viewport==null)return;var size=Viewport.rect.size;if(size.x<=0||size.y<=0)return;
        if(size==last&&Zoom==lastZoom&&Aspect==lastAspect&&Cover==lastCover)return;
        last=size;lastZoom=Zoom;lastAspect=Aspect;lastCover=Cover;
        float w=size.x,h=w/Aspect;
        if(Cover?h<size.y:h>size.y){h=size.y;w=h*Aspect;}
        ((RectTransform)transform).sizeDelta=new Vector2(w,h)*Zoom;
    }
    private void LateUpdate(){Apply();}
}
