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
        if(e.type==EventEffectType.Price)return (e.target==EventTarget.Food||e.target==EventTarget.Store)&&!string.IsNullOrWhiteSpace(e.targetId)&&
            (e.operation==EventOperation.Add||(e.operation==EventOperation.Multiply&&e.value>=0));
        if(e.type==EventEffectType.Availability)return e.target==EventTarget.Store&&!string.IsNullOrWhiteSpace(e.targetId)&&e.operation==EventOperation.Close&&e.value==0;
        return e.type==EventEffectType.AttendancePopularity&&e.target==EventTarget.Bistro&&string.IsNullOrEmpty(e.targetId)&&e.operation==EventOperation.Add;
    }
    public string ClosureReason(string storeId)
    {
        var closing=Active.Where(r=>r.effects.Any(e=>e.type==EventEffectType.Availability&&Same(e.targetId,storeId))).ToList();
        return closing.Count==0?null:$"Closed until day {closing.Max(r=>r.expiresOnDay)}";
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
            if(e.type==EventEffectType.Price&&((e.target==EventTarget.Store&&Same(e.targetId,listing.store?.id))||(e.target==EventTarget.Food&&Same(e.targetId,listing.food?.id))))
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
