// Pure service regression checks: baseline preservation, shared checkout prices, date expiry and deterministic scheduling.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class EventChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static EventEffect Price(EventTarget target,string id,EventOperation op,float value)=>new EventEffect{type=EventEffectType.Price,target=target,targetId=id,operation=op,value=value};
    static EventEffect Closed(string id)=>new EventEffect{type=EventEffectType.Availability,target=EventTarget.Store,targetId=id,operation=EventOperation.Close};
    static EventRecord Record(string id,params EventEffect[] effects)=>new EventRecord{eventId=id,startDay=2,expiresOnDay=4,effects=effects.ToList()};
    public static void Run()
    {
        var balance=ScriptableObject.CreateInstance<BalanceConfig>();
        var store=ScriptableObject.CreateInstance<StoreData>();store.id="S";store.unlockDay=1;
        var other=ScriptableObject.CreateInstance<StoreData>();other.id="T";other.unlockDay=1;
        var food=ScriptableObject.CreateInstance<FoodData>();food.id="F";food.storageType=StorageType.Dry;food.trafficLight=TrafficLight.Green;
        var listing=ScriptableObject.CreateInstance<StoreListingData>();listing.id="L";listing.store=store;listing.food=food;listing.gamePrice=11;listing.servingsPerPackage=2;listing.unlockDay=1;
        var second=ScriptableObject.CreateInstance<StoreListingData>();second.id="M";second.store=other;second.food=food;second.gamePrice=10;second.servingsPerPackage=1;second.unlockDay=1;
        var db=ScriptableObject.CreateInstance<StoreListingDatabase>();
        // Database public setter mirrors importer usage.
        db.SetItems(new List<StoreListingData>{listing,second});
        var state=new GameSessionState{currentDay=2,money=100};var market=new MarketEffects(state);
        state.events.Add(Record("A",Price(EventTarget.Store,"S",EventOperation.Add,2),Price(EventTarget.Store,"S",EventOperation.Multiply,.8f)));
        state.events.Add(Record("B",Price(EventTarget.Food,"F",EventOperation.Multiply,1.5f)));
        Check(Math.Abs(market.Quote(listing).effectivePrice-15.6f)<.001f&&market.Quote(second).effectivePrice==15,"Composed price/food targets failed");
        Check(listing.gamePrice==11&&market.Quote(listing).explanations.Count==3,"Baseline or explanations lost");
        var purchase=new PurchaseService(state,db,balance);var cart=new List<CartEntry>{new CartEntry{listingId="L",packageQuantity=2}};
        Check(Math.Abs(purchase.Quote(cart).total-31.2f)<.001f,"Cart bypassed effective price");
        Check(purchase.Checkout(cart).success&&Math.Abs(state.money-68.8f)<.001f&&Math.Abs(state.inventory[0].purchaseCostPerServing-7.8f)<.001f,"Checkout/batch cost mismatch");
        state.shoppingReceipt=null;state.events.Add(Record("C",Closed("S")));
        var money=state.money;var count=state.inventory.Count;
        Check(!purchase.Buy("L").success&&!purchase.Checkout(cart).success&&state.money==money&&state.inventory.Count==count,"Closed store transaction was not atomic");
        state.currentDay=4;Check(market.ClosureReason("S")==null&&market.Quote(listing).effectivePrice==11,"Expiry did not restore baseline");
        state.events.Clear();state.currentDay=2;
        state.events.Add(Record("P",new EventEffect{type=EventEffectType.AttendancePopularity,target=EventTarget.Bistro,operation=EventOperation.Add,value=500}));
        Check(market.Publicity==500&&state.popularity==300,"Publicity changed earned reputation");state.currentDay=4;Check(market.Publicity==0,"Publicity did not expire");
        var content=ScriptableObject.CreateInstance<EventDatabase>();
        content.events.Add(new EventDefinition{id="close",minDay=2,durationDays=2,effects=new List<EventEffect>{Closed("S")}});
        content.events.Add(new EventDefinition{id="sale",minDay=2,durationDays=2,effects=new List<EventEffect>{Price(EventTarget.Store,"S",EventOperation.Multiply,.8f)}});
        balance.noEventChance=0;balance.twoEventChance=1;
        var scheduled=new GameSessionState{currentDay=1};int rolls=0;
        var scheduler=new EventService(scheduled,content,balance,db,()=>{rolls++;return 0;});
        scheduler.PrepareMorning();Check(rolls==0&&scheduled.events.Count==0,"Day one protection failed");
        scheduled.currentDay=2;scheduler.PrepareMorning();int used=rolls;
        Check(scheduled.events.Count==1&&scheduled.events[0].eventId=="close","Conflicting closure/sale selected");
        scheduler.PrepareMorning();Check(rolls==used&&scheduled.events.Count==1,"Morning rerolled");
        new EventService(scheduled,content,balance,db,()=>throw new Exception("Resume rerolled")).PrepareMorning();
        content.events[0].effects[0].targetId="T";Check(scheduled.events[0].effects[0].targetId=="S","Saved effects were not detached");
        var one=ScriptableObject.CreateInstance<StoreListingDatabase>();one.SetItems(new List<StoreListingData>{listing});
        content.events[0].effects[0].targetId="S";content.events.RemoveAt(1);
        var lastStore=new GameSessionState{currentDay=2};new EventService(lastStore,content,balance,one,()=>0).PrepareMorning();Check(lastStore.events.Count==0,"All accessible stores closed");
        Check(!MarketEffects.ValidEffect(new EventEffect{type=EventEffectType.Availability,target=EventTarget.Food,targetId="F",operation=EventOperation.Close}),"Unsupported effect accepted");
        Console.WriteLine("PASS: event composition, cross-store food prices, immutable baselines, checkout and batch costs, closure atomicity, expiry, publicity, day-one protection, cached selection, conflicts and last-store protection.");
    }
}
