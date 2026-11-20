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
            // Click-drag pans; the map never shows past the artwork: it rests slightly inside the image, stretches into
            // the leftover strip with growing resistance and springs back on release. The mouse wheel zooms toward the cursor.
            BistroMapZoom.Attach(viewport,world,cityZoomLevel,!theme.reducedMotion).Changed=level=>cityZoomLevel=level;
            BistroMapPan.Attach(viewport,world,cityPosition,!theme.reducedMotion).Changed=v=>cityPosition=v;
        }
        return world;
    }
    // clickWhenDisabled: drawn greyed but still answers a click (e.g. a store not open yet, so the Guide can comment).
    private void WorldTarget(RectTransform world,BistroWorldArt.Region region,string label,bool enabled,Action action,bool clickWhenDisabled=false)
    {
        if(region.polygon==null||region.polygon.Length<3)return;
        var go=new GameObject("Region "+region.id,typeof(RectTransform));go.transform.SetParent(world,false);
        var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var hit=go.AddComponent<BistroWorldRegion>();hit.Points=region.polygon;hit.color=Color.white;
        var button=go.AddComponent<BistroWorldButton>();button.targetGraphic=hit;button.interactable=enabled||clickWhenDisabled;
        var colours=button.colors;colours.normalColor=new Color(1,1,1,.015f);colours.highlightedColor=new Color(.91f,1,.82f,.22f);colours.pressedColor=new Color(.91f,1,.82f,.32f);colours.selectedColor=colours.highlightedColor;colours.disabledColor=new Color(.18f,.19f,.2f,.22f);colours.fadeDuration=0;
        if(!enabled)colours.normalColor=colours.highlightedColor=colours.pressedColor=colours.selectedColor=colours.disabledColor;
        button.colors=colours;
        button.onClick.AddListener(()=>Change(action));
        float x=region.marker.x,y=1-region.marker.y;
        var badge=BistroShelfStyle.Panel(world,"Marker "+region.id,enabled?new Color(.94f,.92f,.86f,.89f):new Color(.78f,.78f,.75f,.85f),new Vector2(x-.065f,y-.022f),new Vector2(x+.065f,y+.022f));
        badge.GetComponent<Image>().raycastTarget=false;
        BistroSurface.Dress(badge,BistroShelfStyle.Green,theme.cardSprig);
        var shadow=badge.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.17f,.14f,.16f);shadow.effectDistance=new Vector2(1,-2);
        var logo=game.Queries.GetStores().FirstOrDefault(s=>s.id==region.id)?.logo;
        BistroShelfIcon.Add(badge,logo,region.id=="NEWS"?"Help":"City",new Vector2(.04f,.23f),new Vector2(.15f,.79f),BistroShelfStyle.Green);
        BistroShelfStyle.Text(badge,label,theme,new Vector2(.19f,.05f),new Vector2(.96f,.95f),16,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter).raycastTarget=false;
        // The name tag looks like a button, so it acts as one too: same action as the region (supervisor note 2026-11-13).
        if(enabled||clickWhenDisabled)
        {
            badge.GetComponent<Image>().raycastTarget=true;
            var tagButton=badge.gameObject.AddComponent<BistroWorldButton>();tagButton.targetGraphic=badge.GetComponent<Image>();
            var tint=tagButton.colors;tint.highlightedColor=tint.selectedColor=new Color(.96f,1f,.92f);tint.pressedColor=new Color(.88f,.95f,.84f);tint.fadeDuration=0;tagButton.colors=tint;
            tagButton.onClick.AddListener(()=>Change(action));
        }
    }
    private void CityMap()
    {
        if(theme.use3DCity&&theme.city3DPrefab!=null&&City3DMap())return;
        var view=BistroWorldArt.Get("city");if(view==null||theme.cityBackground==null){FallbackCityMap();return;}
        var world=WorldImage(theme.cityBackground,true);var stores=game.Queries.GetStores();
        foreach(var region in view.regions??Array.Empty<BistroWorldArt.Region>())
        {
            if(region.id=="NEWS"){WorldTarget(world,region,"News",true,Gate(()=>modal="news"));continue;}
            if(region.id=="BISTRO"){WorldTarget(world,region,game.Loop.BistroOpened?"Back to your Bistro":region.label,game.Loop.Phase==GamePhase.Market,()=>{
                if(LessonHolds()){LessonRefuse();return;}
                // Service already started today (easy mode): go straight back to the guests.
                if(!game.Loop.BistroOpened){modal="bistro-confirm";return;}
                if(!game.Loop.EnterBistro())Refused(game.Loop.Tutorials?.LastError??"The bistro can't open right now.");});continue;}
            var item=stores.FirstOrDefault(s=>s.id==region.id);string closed=item==null?"Not open yet":game.Queries.Market.ClosureReason(item.id);
            WorldTarget(world,region,closed==null?item.displayName:region.label+" · "+closed,closed==null,()=>{
                if(LessonHolds()){LessonRefuse();return;}
                var current=game.Queries.GetStores().FirstOrDefault(s=>s.id==region.id);
                string reason=current==null?"Not open yet":game.Queries.Market.ClosureReason(current.id);
                if(reason==null){store=current;shelf=null;return;}
                notice=region.label+": "+reason;
                game.Loop.NoteSeen(current==null?TutorialFlags.ClickedLockedStore:TutorialFlags.ClickedClosedStore);
            },true);
        }
        // Future stores without authored image regions remain reachable.
        int index=0;foreach(var item in stores.Where(s=>!(view.regions??Array.Empty<BistroWorldArt.Region>()).Any(r=>r.id==s.id)))
        {var target=item;BistroShelfStyle.Button(screen,target.displayName,theme,new Vector2(.02f+index*.19f,.2f),new Vector2(.20f+index++*.19f,.26f),BistroShelfStyle.Cream,BistroShelfStyle.Ink,()=>Change(()=>{if(game.Queries.Market.ClosureReason(target.id)==null)store=target;}));}
    }
    // Shopping ends only here: confirming opens the bistro and starts service. A non-empty cart offers to check out first.
    private void BistroConfirmation()
    {
        int packages=cart.Sum(c=>c.packageQuantity);
        string after=game.Balance.cityDuringService?"You can come back to the city during service.":"Once the bistro opens, the market is closed until tomorrow.";
        string text=packages>0
            ?$"You still have {packages} package{(packages==1?"":"s")} in your cart that you haven't bought. Check out first, or leave them and open the bistro? {after}"
            :$"Finished shopping? {after} Your guests are waiting.";
        DialoguePanel("Ready to open the bistro?",text,"Open the bistro",()=>{
            modal=null;
            if(game.Loop.EnterBistro()){if(!game.Balance.cityDuringService)cart.Clear();}
            else Refused(game.Loop.Tutorials?.LastError??"The bistro can't open right now.");
        },()=>modal=null,true,packages==0?null:(Action<RectTransform>)(frame=>
            BistroActionButton.Create(frame,"Check out first",theme,new Vector2(.38f,.045f),new Vector2(.57f,.19f),BistroActionRole.Secondary,()=>Change(()=>{modal=null;AskCheckout();}))));
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
    // Optional low-poly city (theme.use3DCity). Same actions as the painted map; returns false to fall back to 2D.
    private bool City3DMap()
    {
        var city=BistroCity3D.Ensure(theme.city3DPrefab,theme.city3DOrthographic);if(city==null)return false;
        var viewport=BistroWidgets.Box(screen,"City viewport",new Color(.84f,.88f,.89f),new Vector2(0,0),new Vector2(1,.91f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var image=new GameObject("City 3D view",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(viewport,false);var rect=(RectTransform)image.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var view=BistroCity3DView.Attach(image,city);
        var stores=game.Queries.GetStores();
        Action<string> open=id=>{
            if(id=="NEWS"){modal="news";return;}
            var current=game.Queries.GetStores().FirstOrDefault(s=>s.id==id);
            if(current!=null&&game.Queries.Market.ClosureReason(current.id)==null){store=current;shelf=null;}
        };
        view.Clicked=id=>{if(id=="NEWS"||stores.Any(s=>s.id==id))Change(()=>open(id));};
        foreach(var landmark in city.Landmarks)
        {
            if(landmark==null)continue;string id=landmark.landmarkId;
            var item=stores.FirstOrDefault(s=>s.id==id);
            if(id!="NEWS"&&item==null)continue; // the bistro and other props have no city action
            string closed=id=="NEWS"?null:game.Queries.Market.ClosureReason(id);
            string label=id=="NEWS"?"News":closed==null?item.displayName:item.displayName+" · "+closed;
            var tag=BistroShelfStyle.Panel(rect,"Marker "+id,closed==null?new Color(.94f,.92f,.86f,.92f):new Color(.78f,.78f,.75f,.88f),new Vector2(.5f,.5f),new Vector2(.5f,.5f));
            tag.sizeDelta=new Vector2(Mathf.Clamp(label.Length*9+56,120,320),36);
            BistroSurface.Dress(tag,BistroShelfStyle.Green,theme.cardSprig);
            var shadow=tag.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.10f,.17f,.14f,.16f);shadow.effectDistance=new Vector2(1,-2);
            BistroShelfIcon.Add(tag,item?.logo,id=="NEWS"?"Help":"City",new Vector2(.03f,.16f),new Vector2(.12f,.84f),BistroShelfStyle.Green);
            BistroShelfStyle.Text(tag,label,theme,new Vector2(.14f,.05f),new Vector2(.96f,.95f),16,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
            var button=tag.gameObject.AddComponent<Button>();button.targetGraphic=tag.GetComponent<Image>();button.interactable=closed==null;
            button.onClick.AddListener(()=>Change(()=>open(id)));
            view.AddTag(id,tag);
        }
        return true;
    }
}
