// Illustrated shelf composition: all prices, stock and transaction validation remain in existing services.
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    // Top bar, left to right: logo (opens the menu, like a site logo), back button in stores, where you are,
    // then Day, Popularity, Wallet, Cart (market only), Recipes, Guests, Pantry, Help. Every item uses HudItem,
    // so icons, font, colour and spacing match. Day, Popularity and Wallet open a one-time guide lesson
    // (Tutorials.csv rows triggered by tutorial.toolbar_*); after that, clicking them does nothing.
    private void GlobalToolbar()
    {
        var header=BistroShelfStyle.Panel(screen,"Global toolbar",new Color(.94f,.92f,.86f,.90f),new Vector2(0,.91f),Vector2.one,false);
        BistroSurface.Dress(header,BistroShelfStyle.Green,theme.cardSprig);
        var ink=BistroShelfStyle.Ink;
        bool market=game.Loop.Phase==GamePhase.Market;
        var logo=BistroShelfStyle.Button(header,"",theme,new Vector2(.005f,.12f),new Vector2(.092f,.88f),Color.clear,ink,()=>Change(()=>modal="main-menu"));
        BistroShelfIcon.Add(logo.transform,"Menu",new Vector2(.03f,.28f),new Vector2(.19f,.72f),BistroShelfStyle.Green);
        BistroShelfStyle.Text(logo.transform,"BLUE ZONE",theme,new Vector2(.21f,.1f),new Vector2(1,.9f),15,BistroShelfStyle.Green,true);
        bool back=market&&store!=null;
        if(back)BistroShelfStyle.Button(header,shelf!=null?"← Store":"← City",theme,new Vector2(.096f,.18f),new Vector2(.178f,.82f),new Color(.84f,.83f,.77f),ink,()=>Change(()=>{if(shelf!=null)shelf=null;else store=null;}));
        string place=market?(store?.displayName??"City"):game.Loop.Phase==GamePhase.Service?(mealRecipe==null?"Bistro":"Meal building"):game.Loop.Phase==GamePhase.FoodSorting?"Sorting":"Day complete";
        if(market&&store!=null&&shelf!=null)place+=" · "+(store.shelves.FirstOrDefault(s=>s!=null&&s.id==shelf)?.displayName??shelf);
        string symbol=market?"City":game.Loop.Phase==GamePhase.Service?"Bistro":game.Loop.Phase==GamePhase.FoodSorting?"Sort":"Day";
        HudItem(header,symbol,place,back?.185f:.10f,.345f,null,market&&store!=null?store.logo:null);
        HudItem(header,"Day",$"Day {game.State.currentDay} / {game.Balance.totalDays}",.35f,.43f,()=>game.Loop.InspectToolbar("day"));
        float publicity=game.Queries.Market.Publicity;
        HudItem(header,"Popularity",$"Popularity {game.State.popularity:0}"+(publicity==0?"":$" ({publicity:+0;-0})"),.43f,.575f,()=>game.Loop.InspectToolbar("popularity"));
        HudItem(header,"wallet","$"+game.State.money.ToString("0.00"),.575f,.645f,()=>game.Loop.InspectToolbar("wallet"));
        if(market)HudItem(header,"Cart","Cart · "+cart.Sum(c=>c.packageQuantity),.645f,.725f,()=>modal="cart");
        HudItem(header,"Recipes","Recipes",.725f,.80f,()=>modal="recipes");
        HudItem(header,"Guests","Guests",.80f,.868f,()=>modal="guests");
        HudItem(header,"Pantry","Pantry",.868f,.935f,()=>modal="pantry");
        HudItem(header,"Help","Help",.935f,.997f,()=>modal="tutorial-help");
    }
    // One top-bar item: a fixed-size icon, then the label in the body font and ink colour.
    // action==null makes a plain label (the current place); sprite overrides the vector symbol (store logo).
    private void HudItem(RectTransform header,string symbol,string label,float left,float right,System.Action action,Sprite sprite=null)
    {
        var ink=BistroShelfStyle.Ink;
        RectTransform host;
        if(action!=null)host=(RectTransform)BistroShelfStyle.Button(header,"",theme,new Vector2(left,.15f),new Vector2(right,.85f),Color.clear,ink,()=>Change(action)).transform;
        else{host=BistroWidgets.Box(header,"Place",Color.clear,new Vector2(left,.15f),new Vector2(right,.85f));host.GetComponent<Image>().raycastTarget=false;}
        var slot=(RectTransform)new GameObject("Icon",typeof(RectTransform)).transform;slot.SetParent(host,false);
        slot.anchorMin=slot.anchorMax=new Vector2(0,.5f);slot.pivot=new Vector2(0,.5f);slot.sizeDelta=new Vector2(24,24);slot.anchoredPosition=new Vector2(4,0);
        BistroShelfIcon.Add(slot,sprite,symbol,Vector2.zero,Vector2.one,ink);
        var text=BistroShelfStyle.Text(host,label,theme,Vector2.zero,Vector2.one,18,ink);
        text.rectTransform.offsetMin=new Vector2(34,0);text.alignment=TextAnchor.MiddleLeft;
        // Same size everywhere; only the place name (store + shelf can be long) may shrink.
        text.resizeTextMinSize=action==null?13:17;
    }
    private void IllustratedShelf()
    {
        var cream=BistroShelfStyle.Cream;var ink=BistroShelfStyle.Ink;
        // Shelf colour and store logo come from Stores.csv (shelf_colours, logo_path).
        var shelfInfo=store.shelves.FirstOrDefault(s=>s!=null&&string.Equals(s.id,shelf,System.StringComparison.OrdinalIgnoreCase));
        Color rack; if(!ColorUtility.TryParseHtmlString(shelfInfo?.colour,out rack))rack=new Color(.43f,.51f,.44f);
        screen.GetComponent<Image>().color=rack*.94f;
        // Wide full-height viewport; four cards per row at normal desktop sizes.
        var grid=BistroWidgets.Grid(screen,theme,new Vector2(.012f,.015f),new Vector2(.705f,.90f));
        var viewport=(RectTransform)grid.parent;viewport.GetComponent<Image>().color=rack;
        var responsive=grid.GetComponent<BistroResponsiveGrid>();responsive.enabled=false;
        var layout=grid.GetComponent<GridLayoutGroup>();layout.spacing=new Vector2(20,32);layout.padding=new RectOffset(30,26,22,24);
        var rails=BistroWidgets.Box(grid,"Continuous shelf rails",Color.clear,Vector2.zero,Vector2.one);rails.GetComponent<Image>().raycastTarget=false;
        rails.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
        foreach(var listing in game.Queries.GetListings(store).Where(l=>l.shelfId==shelf))
        {
            var price=game.Queries.GetPrice(listing);
            var card=BistroFoodCard.Create(grid,listing.food,listing.id,"offer",price.Display+" · "+listing.servingsPerPackage+" servings",theme);
            card.ApplyShelfArtwork(listing.food,"$"+price.effectivePrice.ToString("0.00"),listing.servingsPerPackage,store.logo);
        }
        grid.gameObject.AddComponent<BistroShelfLayout>().Initialize(rails,theme.shelfRail);
        AddShelfScrollbar(viewport);
        // Repeating rack hardware is decorative; cards remain the only product targets.
        var upright=BistroShelfStyle.Panel(viewport,"Sage steel upright",new Color(.56f,.61f,.51f),new Vector2(.006f,0),new Vector2(.023f,1),false);
        upright.GetComponent<Image>().raycastTarget=false;
        for(int i=0;i<20;i++)
        {
            float y=.025f+i*.05f;
            var slot=BistroShelfStyle.Panel(upright,"Rack slot",new Color(.29f,.36f,.28f),new Vector2(.36f,y),new Vector2(.64f,y+.024f));
            slot.GetComponent<Image>().raycastTarget=false;
        }
        var panel=BistroShelfStyle.Panel(screen,"Your cart",cream,new Vector2(.724f,.025f),new Vector2(.985f,.89f));
        panel.gameObject.AddComponent<BistroShelfCartTarget>().Drop=AddToCart;
        BistroShelfIcon.Add(panel,theme.cartIcon,"cart",new Vector2(.06f,.85f),new Vector2(.24f,.97f),ink);
        var quote=game.Loop.QuoteCart(cart);
        if(quote.success&&quote.total>game.State.money){var rim=panel.gameObject.AddComponent<Outline>();rim.effectColor=theme.warning;rim.effectDistance=new Vector2(3,3);}
        BistroShelfStyle.Text(panel,"Your Cart",theme,new Vector2(.27f,.84f),new Vector2(.94f,.97f),36,ink,true);
        var items=BistroWidgets.Grid(panel,theme,new Vector2(.055f,.47f),new Vector2(.945f,.83f));
        ((RectTransform)items.parent).GetComponent<Image>().color=cream;
        items.GetComponent<BistroResponsiveGrid>().enabled=false;var itemsLayout=items.GetComponent<GridLayoutGroup>();
        itemsLayout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;itemsLayout.constraintCount=1;itemsLayout.cellSize=new Vector2(310,100);itemsLayout.spacing=new Vector2(0,10);itemsLayout.padding=new RectOffset(2,2,2,2);
        foreach(var entry in cart)
        {
            var listing=game.Queries.GetListing(entry.listingId);if(listing==null)continue;
            var row=BistroShelfStyle.Panel(items,"Cart item",new Color(.97f,.95f,.90f),Vector2.zero,Vector2.one);
            var thumb=BistroShelfStyle.Panel(row,"Food thumbnail",Color.white,new Vector2(.02f,.10f),new Vector2(.25f,.90f));
            var art=listing.food?.icon;thumb.GetComponent<Image>().sprite=art;thumb.GetComponent<Image>().raycastTarget=false;
            BistroShelfStyle.Text(row,listing.food.displayName,theme,new Vector2(.29f,.45f),new Vector2(.96f,.94f),22,ink,true);
            BistroShelfStyle.Text(row,$"{entry.packageQuantity} × {listing.servingsPerPackage} servings",theme,new Vector2(.29f,.08f),new Vector2(.70f,.46f),17,ink);
            BistroShelfStyle.Text(row,"$"+(game.Queries.GetPrice(listing).effectivePrice*entry.packageQuantity).ToString("0.00"),theme,new Vector2(.68f,.08f),new Vector2(.96f,.46f),21,ink,false,TextAnchor.MiddleRight);
        }
        if(cart.Count==0)BistroShelfStyle.Text(panel,"Drag food here\nto start your cart",theme,new Vector2(.12f,.54f),new Vector2(.88f,.77f),23,new Color(.40f,.45f,.40f),false,TextAnchor.MiddleCenter);
        BistroWidgets.Box(panel,"Divider",new Color(.73f,.73f,.67f),new Vector2(.07f,.45f),new Vector2(.93f,.452f)).GetComponent<Image>().raycastTarget=false;
        BistroShelfStyle.Text(panel,cart.Sum(c=>c.packageQuantity)+" packages",theme,new Vector2(.07f,.37f),new Vector2(.67f,.44f),24,ink,false);
        BistroShelfStyle.Text(panel,quote.success?"$"+quote.total.ToString("0.00"):"—",theme,new Vector2(.65f,.37f),new Vector2(.93f,.44f),26,ink,false,TextAnchor.MiddleRight);
        BistroShelfStyle.Text(panel,"After checkout",theme,new Vector2(.07f,.30f),new Vector2(.67f,.37f),22,new Color(.35f,.39f,.36f));
        BistroShelfStyle.Text(panel,quote.success?"$"+(game.State.money-quote.total).ToString("0.00"):quote.error,theme,new Vector2(.64f,.30f),new Vector2(.93f,.37f),26,BistroShelfStyle.Green,false,TextAnchor.MiddleRight);
        BistroShelfStyle.Button(panel,"Review cart",theme,new Vector2(.07f,.19f),new Vector2(.93f,.28f),BistroShelfStyle.Green,cream,()=>Change(()=>modal="cart"));
        // Both lead to the existing confirmation flow; no purchase is committed by this view.
        BistroShelfStyle.Button(panel,"Checkout",theme,new Vector2(.07f,.085f),new Vector2(.93f,.175f),new Color(.60f,.73f,.45f),ink,()=>Change(()=>modal="cart"));
        BistroShelfStyle.Text(panel,"Drag to add · Click cards to inspect",theme,new Vector2(.06f,.015f),new Vector2(.94f,.065f),15,ink,false,TextAnchor.MiddleCenter);
    }
    private void AddShelfScrollbar(RectTransform viewport)
    {
        var track=BistroShelfStyle.Panel(viewport,"Scrollbar",new Color(.34f,.41f,.35f),new Vector2(.988f,.025f),new Vector2(.998f,.975f));
        var handle=BistroShelfStyle.Panel(track,"Handle",BistroShelfStyle.Cream,Vector2.zero,Vector2.one);
        var bar=track.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
        viewport.GetComponent<ScrollRect>().verticalScrollbar=bar;
    }
    // Same visual vocabulary as the shelf, with the existing drag-out removal and guarded checkout.
    private void IllustratedCartReview()
    {
        var shade=BistroWidgets.Box(screen,"Modal blocker",new Color(0,0,0,.55f),Vector2.zero,new Vector2(1,.91f));
        var panel=BistroShelfStyle.Panel(shade,"Cart review",BistroShelfStyle.Cream,new Vector2(.07f,.06f),new Vector2(.93f,.94f));
        BistroShelfStyle.Text(panel,"Review your cart",theme,new Vector2(.04f,.88f),new Vector2(.70f,.98f),34,BistroShelfStyle.Ink,true);
        BistroShelfStyle.Button(panel,"Close",theme,new Vector2(.83f,.90f),new Vector2(.96f,.97f),theme.accent,BistroShelfStyle.Ink,()=>Change(()=>modal=null));
        BistroShelfStyle.Text(panel,notice,theme,new Vector2(.04f,.83f),new Vector2(.96f,.89f),17,BistroShelfStyle.Ink);
        var grid=BistroWidgets.Grid(panel,theme,new Vector2(.04f,.24f),new Vector2(.96f,.82f));
        ((RectTransform)grid.parent).GetComponent<Image>().color=new Color(.43f,.51f,.44f);
        grid.GetComponent<BistroResponsiveGrid>().SetPreferred(new Vector2(210,310));
        foreach(var entry in cart)
        {
            var listing=game.Queries.GetListing(entry.listingId);if(listing==null)continue;
            var card=BistroFoodCard.Create(grid,listing.food,listing.id,"remove-cart",entry.packageQuantity+" packages · drag out to remove one",theme);
            card.ApplyShelfArtwork(listing.food,"$"+(game.Queries.GetPrice(listing).effectivePrice*entry.packageQuantity).ToString("0.00"),listing.servingsPerPackage*entry.packageQuantity,listing.store?.logo);
        }
        if(cart.Count==0)BistroShelfStyle.Text(panel,"Your cart is empty",theme,new Vector2(.2f,.45f),new Vector2(.8f,.65f),28,BistroShelfStyle.Ink,true,TextAnchor.MiddleCenter);
        var remove=BistroShelfStyle.Panel(panel,"Remove one package",new Color(.83f,.82f,.75f),new Vector2(.04f,.05f),new Vector2(.49f,.21f));
        BistroShelfStyle.Text(remove,"Drag here to remove one package",theme,new Vector2(.05f,.1f),new Vector2(.95f,.9f),23,BistroShelfStyle.Ink,false,TextAnchor.MiddleCenter);
        remove.gameObject.AddComponent<BistroShelfCartTarget>().Drop=(id,kind)=>{if(kind=="remove-cart")Change(()=>{var entry=cart.Find(c=>c.listingId==id);if(entry!=null&&--entry.packageQuantity==0)cart.Remove(entry);});};
        var quote=game.Loop.QuoteCart(cart);
        BistroShelfStyle.Text(panel,quote.success?$"Total ${quote.total:0.00}  ·  Wallet ${game.State.money:0.00}":quote.error,theme,new Vector2(.54f,.16f),new Vector2(.96f,.23f),23,BistroShelfStyle.Ink);
        BistroShelfStyle.Button(panel,"Confirm checkout",theme,new Vector2(.54f,.05f),new Vector2(.96f,.15f),BistroShelfStyle.Green,BistroShelfStyle.Cream,()=>Change(()=>{
            var result=game.Loop.Checkout(cart);if(result.success){cart.Clear();modal=null;}notice=result.success?"Checkout complete":result.error;
        }));
    }
}






