// The city is a clipped, inertial ScrollRect world. Stores are children of that world;
// header, pantry and checkout remain outside it. Position is UI-only, retained across rebuilds.
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private Vector2 cityPosition=new Vector2(.5f,.5f);
    private float cityZoomLevel=1f; // wheel zoom on the city map, kept across rebuilds
    private void FallbackCityMap()
    {
        var viewport=BistroWidgets.Box(screen,"City viewport",theme.panel,new Vector2(.03f,.19f),new Vector2(.97f,.89f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var world=BistroWidgets.Box(viewport,"City world",theme.panel,Vector2.zero,Vector2.one);
        world.sizeDelta=new Vector2(700,500);
        if(theme.cityBackground!=null){world.GetComponent<Image>().sprite=theme.cityBackground;world.GetComponent<Image>().color=Color.white;GradeScenery(world.GetComponent<Image>());}
        else
        {
            // Low-contrast streets and districts remain replaceable with a single city sprite.
            for(int x=0;x<6;x++)for(int y=0;y<5;y++)
            {
                float left=.035f+x*.16f,bottom=.04f+y*.19f;
                var block=BistroWidgets.Box(world,"Neighbourhood block",Color.Lerp(theme.panel,theme.accent,.12f+(x+y)%3*.04f),new Vector2(left,bottom),new Vector2(left+.13f,bottom+.15f));
                block.GetComponent<Image>().raycastTarget=false;
                for(int h=0;h<3;h++)
                {
                    var house=BistroWidgets.Box(block,"Building",Color.Lerp(theme.panel,theme.card,.17f),new Vector2(.10f+h*.29f,.22f),new Vector2(.32f+h*.29f,.76f));
                    house.GetComponent<Image>().raycastTarget=false;
                }
            }
        }
        var stores=game.Queries.GetStores();
        for(int i=0;i<stores.Count;i++)
        {
            var item=stores[i];float x=.18f+(i%3)*.23f,y=.55f-(i/3)*.18f;
            string closed=game.Queries.Market.ClosureReason(item.id);
            var button=BistroWidgets.Button(world,item.displayName+(closed==null?"":"\nCLOSED"),theme,new Vector2(x,y),new Vector2(x+.18f,y+.09f),()=>Change(()=>store=item));
            button.interactable=closed==null;
            if(closed!=null)BistroWidgets.Label(world,closed,theme,new Vector2(x,y-.05f),new Vector2(x+.18f,y),14);
        }
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport=viewport;scroll.content=world;scroll.horizontal=true;scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Elastic;scroll.elasticity=.12f;scroll.inertia=true;scroll.decelerationRate=.08f;scroll.scrollSensitivity=35;
        Canvas.ForceUpdateCanvases();scroll.normalizedPosition=cityPosition;
        scroll.onValueChanged.AddListener(value=>cityPosition=value);
        BistroWidgets.Label(screen,"THE NEIGHBOURHOOD  ·  Drag to explore",theme,new Vector2(.05f,.82f),new Vector2(.77f,.88f),24);
    }
}

