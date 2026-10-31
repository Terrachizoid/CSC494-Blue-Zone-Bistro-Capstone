using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    // art: theme.cityBackground for the city, StoreData.shopBackground (Stores.csv) for a store.
    private RectTransform WorldImage(Sprite art,bool city)
    {
        var viewport=BistroWidgets.Box(screen,city?"City viewport":"Store viewport",new Color(.65f,.66f,.64f),new Vector2(0,0),new Vector2(1,.91f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var world=BistroWidgets.Box(viewport,city?"City artwork":"Store artwork",Color.white,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
        var image=world.GetComponent<Image>();image.sprite=art;
        var layout=world.gameObject.AddComponent<BistroWorldLayout>();layout.Viewport=viewport;layout.Aspect=image.sprite.rect.width/image.sprite.rect.height;layout.Cover=city;layout.Zoom=city?Mathf.Max(1.1f,theme.cityMapZoom):1;
        Canvas.ForceUpdateCanvases();layout.Apply();
        if(city)
        {
            // Click-drag pans. Elastic edges: dragging past an edge gets harder the further you pull, and the map
            // springs back when released. The mouse wheel zooms toward the cursor (it no longer scrolls).
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=world;
            scroll.horizontal=scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Elastic;scroll.elasticity=.12f;
            scroll.inertia=true;scroll.decelerationRate=.08f;scroll.scrollSensitivity=0;
            BistroMapZoom.Attach(viewport,world,cityZoomLevel,!theme.reducedMotion).Changed=level=>cityZoomLevel=level;
            Canvas.ForceUpdateCanvases();scroll.normalizedPosition=cityPosition;scroll.onValueChanged.AddListener(v=>cityPosition=v);
        }
        return world;
    }
    private void WorldTarget(RectTransform world,BistroWorldArt.Region region,string label,bool enabled,Action action)
    {
        if(region.polygon==null||region.polygon.Length<3)return;
        var go=new GameObject("Region "+region.id,typeof(RectTransform));go.transform.SetParent(world,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var hit=go.AddComponent<BistroWorldRegion>();hit.Points=region.polygon;hit.color=Color.white;
        var button=go.AddComponent<BistroWorldButton>();button.targetGraphic=hit;button.interactable=enabled;
        var colours=button.colors;colours.normalColor=new Color(1,1,1,.015f);colours.highlightedColor=new Color(.91f,1,.82f,.22f);colours.pressedColor=new Color(.91f,1,.82f,.32f);colours.selectedColor=colours.highlightedColor;colours.disabledColor=new Color(.18f,.19f,.2f,.22f);colours.fadeDuration=0;button.colors=colours;
        button.onClick.AddListener(()=>Change(action));
        float x=region.marker.x,y=1-region.marker.y;
        var badge=BistroShelfStyle.Panel(world,"Marker "+region.id,enabled?new Color(.94f,.92f,.86f,.89f):new Color(.78f,.78f,.75f,.85f),new Vector2(x-.065f,y-.022f),new Vector2(x+.065f,y+.022f));
        badge.GetComponent<Image>().raycastTarget=false;
        BistroSurface.Dress(badge,BistroShelfStyle.Green,theme.cardSprig);
        var shadow=badge.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.17f,.14f,.16f);shadow.effectDistance=new Vector2(1,-2);
        var logo=game.Queries.GetStores().FirstOrDefault(s=>s.id==region.id)?.logo;
        BistroShelfIcon.Add(badge,logo,region.id=="NEWS"?"Help":"City",new Vector2(.04f,.23f),new Vector2(.15f,.79f),BistroShelfStyle.Green);
        BistroShelfStyle.Text(badge,label,theme,new Vector2(.19f,.05f),new Vector2(.96f,.95f),16,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
    }
    private void CityMap()
    {
        var view=BistroWorldArt.Get("city");if(view==null||theme.cityBackground==null){FallbackCityMap();return;}
        var world=WorldImage(theme.cityBackground,true);var stores=game.Queries.GetStores();
        foreach(var region in view.regions??Array.Empty<BistroWorldArt.Region>())
        {
            if(region.id=="NEWS"){WorldTarget(world,region,"News",true,()=>modal="news");continue;}
            var item=stores.FirstOrDefault(s=>s.id==region.id);string closed=item==null?"Not open yet":game.Queries.Market.ClosureReason(item.id);
            WorldTarget(world,region,closed==null?item.displayName:region.label+" · "+closed,closed==null,()=>{
                var current=game.Queries.GetStores().FirstOrDefault(s=>s.id==region.id);
                if(current!=null&&game.Queries.Market.ClosureReason(current.id)==null){store=current;shelf=null;}
            });
        }
        // Future stores without authored image regions remain reachable.
        int index=0;foreach(var item in stores.Where(s=>!(view.regions??Array.Empty<BistroWorldArt.Region>()).Any(r=>r.id==s.id)))
        {var target=item;BistroShelfStyle.Button(screen,target.displayName,theme,new Vector2(.02f+index*.19f,.2f),new Vector2(.20f+index++*.19f,.26f),BistroShelfStyle.Cream,BistroShelfStyle.Ink,()=>Change(()=>{if(game.Queries.Market.ClosureReason(target.id)==null)store=target;}));}
    }
    private bool StoreOverview()
    {
        var view=BistroWorldArt.Get(store.id);if(view==null||store.shopBackground==null)return false;
        var world=WorldImage(store.shopBackground,false);var listings=game.Queries.GetListings(store);var closed=game.Queries.Market.ClosureReason(store.id);
        foreach(var region in view.regions??Array.Empty<BistroWorldArt.Region>())
        {
            bool available=closed==null&&listings.Any(l=>l.shelfId==region.id);
            string label=store.shelves.FirstOrDefault(s=>s!=null&&s.id==region.id)?.displayName??region.label;
            WorldTarget(world,region,label+(available?"":" · unavailable"),available,()=>{
                if(game.Queries.Market.ClosureReason(store.id)==null&&game.Queries.GetListings(store).Any(l=>l.shelfId==region.id))shelf=region.id;
            });
        }
        int index=0;foreach(var id in listings.Select(l=>l.shelfId).Distinct().Where(id=>!(view.regions??Array.Empty<BistroWorldArt.Region>()).Any(r=>r.id==id)))
        {string target=id;BistroShelfStyle.Button(screen,target,theme,new Vector2(.02f+index*.19f,.2f),new Vector2(.20f+index++*.19f,.26f),BistroShelfStyle.Cream,BistroShelfStyle.Ink,()=>Change(()=>{if(game.Queries.Market.ClosureReason(store.id)==null)shelf=target;}));}
        return true;
    }
}


