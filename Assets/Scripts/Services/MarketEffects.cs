// Effective values are derived from authored baselines. Shared by UI, checkout and scheduling; no asset writes.
using System;
using System.Collections.Generic;
using System.Linq;
public class PriceQuote
{
    public float baselinePrice, effectivePrice;
    public bool valid;
    public string error;
    public List<string> explanations=new List<string>();
    public string Display=>valid?(baselinePrice==effectivePrice?$"${effectivePrice:0.00}":$"${baselinePrice:0.00} → ${effectivePrice:0.00}"):error;
}
public class MarketEffects
{
    private readonly GameSessionState state;
    public MarketEffects(GameSessionState state){this.state=state;}
    public IEnumerable<EventRecord> Active=>(state.events??new List<EventRecord>()).Where(e=>e!=null&&e.Active(state.currentDay));
    public static bool ValidEffect(EventEffect e)
    {
        if(e==null||!CommunityProgress.Finite(e.value))return false;
        bool id=!string.IsNullOrWhiteSpace(e.targetId);
        bool multiply=e.operation==EventOperation.Multiply&&e.value>=0;
        switch(e.type)
        {
            case EventEffectType.Price:
                return id&&(e.target==EventTarget.Food||e.target==EventTarget.Store||(e.target==EventTarget.Category&&IsCategory(e.targetId)))&&(e.operation==EventOperation.Add||multiply);
            case EventEffectType.Availability:
                return id&&(e.target==EventTarget.Store||e.target==EventTarget.Food||(e.target==EventTarget.Shelf&&e.targetId.Contains("/")))&&e.operation==EventOperation.Close&&e.value==0;
            case EventEffectType.AttendancePopularity:
                return e.target==EventTarget.Bistro&&string.IsNullOrEmpty(e.targetId)&&e.operation==EventOperation.Add;
            case EventEffectType.Craving:
                return e.target==EventTarget.Craving&&Enum.TryParse(e.targetId,true,out CravingTag tag)&&tag!=CravingTag.None&&multiply;
            case EventEffectType.SortingRefund:
                return e.target==EventTarget.Bistro&&string.IsNullOrEmpty(e.targetId)&&multiply;
            case EventEffectType.ShelfLife:
                return id&&(e.target==EventTarget.Food||(e.target==EventTarget.Category&&IsCategory(e.targetId)))&&e.operation==EventOperation.Add&&e.value==Math.Round(e.value);
            default: return false;
        }
    }
    private static bool IsCategory(string id)=>Enum.TryParse(id,true,out FoodCategory c)&&c!=FoodCategory.Unassigned;
    private static bool Matches(EventEffect e,FoodData food)=>food!=null&&((e.target==EventTarget.Food&&Same(e.targetId,food.id))||
        (e.target==EventTarget.Category&&food.Categories.Any(c=>Same(e.targetId,c.ToString()))));
    // Whole-store closures only (food and shelf closures are "sold out", see UnavailableReason).
    public string ClosureReason(string storeId)
    {
        var closing=Active.Where(r=>r.effects.Any(e=>e.type==EventEffectType.Availability&&e.target==EventTarget.Store&&Same(e.targetId,storeId))).ToList();
        return closing.Count==0?null:$"Closed until day {closing.Max(r=>r.expiresOnDay)}";
    }
    public bool AnyStoreClosed=>Active.Any(r=>r.effects.Any(e=>e.type==EventEffectType.Availability&&e.target==EventTarget.Store));
    // Why this listing can't be bought today: its store is closed, or the food or its shelf is sold out. Null = available.
    public string UnavailableReason(StoreListingData listing)
    {
        if(listing?.store==null)return "Not available.";
        string closed=ClosureReason(listing.store.id);if(closed!=null)return closed;
        var sold=Active.Where(r=>r.effects.Any(e=>e.type==EventEffectType.Availability&&((e.target==EventTarget.Food&&Same(e.targetId,listing.food?.id))||
            (e.target==EventTarget.Shelf&&Same(e.targetId,listing.store.id+"/"+listing.shelfId))))).ToList();
        return sold.Count==0?null:$"Sold out until day {sold.Max(r=>r.expiresOnDay)}";
    }
    // How much more likely guests are to ask for this craving today (product of active multipliers; 1 = normal).
    public float CravingWeight(CravingTag tag)
    {
        double w=1;foreach(var r in Active)foreach(var e in r.effects)
            if(e.type==EventEffectType.Craving&&ValidEffect(e)&&Same(e.targetId,tag.ToString()))w*=e.value;
        return (float)w;
    }
    // Multiplier on the sorting refund rate for purchases made today (snapshotted into the receipt at checkout).
    public float RefundMultiplier
    {
        get{double m=1;foreach(var r in Active)foreach(var e in r.effects)if(e.type==EventEffectType.SortingRefund&&ValidEffect(e))m*=e.value;return (float)m;}
    }
    // Days added to (or taken from) the shelf life of this food when bought today.
    public int ShelfLifeChange(FoodData food)
    {
        int days=0;foreach(var r in Active)foreach(var e in r.effects)if(e.type==EventEffectType.ShelfLife&&ValidEffect(e)&&Matches(e,food))days+=(int)e.value;
        return days;
    }
    public float Publicity
    {
        get {double total=0;foreach(var record in Active)foreach(var effect in record.effects)if(effect.type==EventEffectType.AttendancePopularity)total+=effect.value;
            return (float)Math.Max(-float.MaxValue,Math.Min(float.MaxValue,total));}
    }
    public PriceQuote Quote(StoreListingData listing)
    {
        var q=new PriceQuote{baselinePrice=listing?.gamePrice??0};
        if(listing==null||!CommunityProgress.Finite(q.baselinePrice)||q.baselinePrice<0){q.error="Invalid listing price.";return q;}
        double add=0,multiply=1;
        foreach(var record in Active.OrderBy(r=>r.eventId,StringComparer.Ordinal))foreach(var e in record.effects)
            if(e.type==EventEffectType.Price&&((e.target==EventTarget.Store&&Same(e.targetId,listing.store?.id))||Matches(e,listing.food)))
            {
                if(!ValidEffect(e)){q.error="Invalid event price modifier.";return q;}
                if(e.operation==EventOperation.Add)add+=e.value;else multiply*=e.value;
                q.explanations.Add(record.eventId+": "+(e.operation==EventOperation.Add?$"{e.value:+0.00;-0.00;0}":$"×{e.value:0.##}"));
            }
        double amount=Math.Round(Math.Max(0,q.baselinePrice+add)*multiply,2,MidpointRounding.AwayFromZero);
        if(double.IsNaN(amount)||double.IsInfinity(amount)||amount>float.MaxValue){q.error="Event price is outside the supported range.";return q;}
        q.effectivePrice=(float)amount;q.valid=true;return q;
    }
    private static bool Same(string a,string b)=>string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
}
