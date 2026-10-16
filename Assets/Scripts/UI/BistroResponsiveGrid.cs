// Keeps the normal authored card size, shrinking only when one card cannot fit.
// The grid still wraps into as many columns as available; it never clips a lone card.
using UnityEngine;
using UnityEngine.UI;
[RequireComponent(typeof(GridLayoutGroup))]
public class BistroResponsiveGrid : MonoBehaviour
{
    private Vector2 preferred=new Vector2(166,238);
    private GridLayoutGroup grid;
    public void SetPreferred(Vector2 size){preferred=size;Apply();}
    private void Awake(){grid=GetComponent<GridLayoutGroup>();}
    private void LateUpdate(){Apply();}
    public void Apply()
    {
        if(grid==null)grid=GetComponent<GridLayoutGroup>();
        float width=Mathf.Max(1,((RectTransform)transform).rect.width-grid.padding.horizontal);
        float factor=Mathf.Min(1,width/Mathf.Max(1,preferred.x));
        Vector2 next=preferred*factor;
        if((grid.cellSize-next).sqrMagnitude>.01f)grid.cellSize=next;
    }
}
