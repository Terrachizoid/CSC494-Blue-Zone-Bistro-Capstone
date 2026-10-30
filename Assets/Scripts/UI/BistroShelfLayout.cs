// Responsive four-column shelf layout. Rails follow grid rows, never participate in card input/layout.
using UnityEngine;
using UnityEngine.UI;
public sealed class BistroShelfLayout : MonoBehaviour
{
    private GridLayoutGroup grid;
    private RectTransform rails; private Sprite railArt;
    // Rail art comes from the UI theme (shelfRail); without it a flat two-tone rail is drawn.
    public void Initialize(RectTransform decorations,Sprite artwork){railArt=artwork;rails=decorations;grid=GetComponent<GridLayoutGroup>();Apply();}
    private void LateUpdate(){Apply();}
    public void Apply()
    {
        if(grid==null||rails==null)return;
        float width=((RectTransform)transform).rect.width;
        int columns=width<650?3:4;
        float cardWidth=Mathf.Max(60,(width-grid.padding.horizontal-grid.spacing.x*(columns-1))/columns);
        grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;grid.cellSize=new Vector2(cardWidth,cardWidth*1.45f);
        int rows=Mathf.CeilToInt((transform.childCount-1)/(float)columns);
        while(rails.childCount<rows)
        {
            var rail=BistroShelfStyle.Panel(rails,"Oak rail",new Color(.72f,.59f,.40f),Vector2.zero,Vector2.one,false);
            rail.GetComponent<Image>().raycastTarget=false;
            var wood=railArt;
            if(wood!=null){rail.GetComponent<Image>().sprite=wood;rail.GetComponent<Image>().color=Color.white;}
            else {var edge=BistroShelfStyle.Panel(rail,"Light edge",new Color(.88f,.77f,.58f),new Vector2(0,.60f),Vector2.one,false);edge.GetComponent<Image>().raycastTarget=false;}
            var shadow=rail.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.1f,.15f,.1f,.25f);shadow.effectDistance=new Vector2(0,-3);
        }
        for(int i=0;i<rails.childCount;i++)
        {
            var r=(RectTransform)rails.GetChild(i);r.gameObject.SetActive(i<rows);
            r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);
            r.offsetMin=new Vector2(0,0);r.offsetMax=new Vector2(0,0);
            r.sizeDelta=new Vector2(0,18);r.anchoredPosition=new Vector2(0,-grid.padding.top-(i+1)*grid.cellSize.y-i*grid.spacing.y);
        }
    }
}

