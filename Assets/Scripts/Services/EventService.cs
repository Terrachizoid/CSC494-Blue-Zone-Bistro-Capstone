// Idempotent morning scheduler. Persists chosen effects before reservations/checkpoint; expiry is a date comparison.
using System;
using System.Linq;
using System.Collections.Generic;
public class EventService
{
    private readonly GameSessionState state;
    private readonly EventDatabase content;
    private readonly BalanceConfig balance;
    private readonly StoreListingDatabase listings;
    private readonly Func<double> random;
    public EventService(GameSessionState state,EventDatabase content,BalanceConfig balance,StoreListingDatabase listings,Func<double> random=null)
    {this.state=state;this.content=content;this.balance=balance;this.listings=listings;var rng=new Random();this.random=random??rng.NextDouble;}
    private double Roll(){double n=random();if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidOperationException("Invalid event random sample.");return Math.Max(0,Math.Min(.999999999,n));}
    public void PrepareMorning()
    {
        if(state.eventPlanDay==state.currentDay||state.gameCompleted)return;
        if(state.events==null)state.events=new List<EventRecord>();
        if(!CommunityProgress.Finite(balance.noEventChance)||!CommunityProgress.Finite(balance.twoEventChance)||balance.noEventChance<0||balance.twoEventChance<0||balance.noEventChance+balance.twoEventChance>1)throw new InvalidOperationException("Invalid event probabilities.");
        var additions=new List<EventRecord>();
        if(state.currentDay>=Math.Max(2,balance.eventsStartDay)&&content!=null)
        {
            double roll=Roll();int count=roll<balance.noEventChance?0:roll<balance.noEventChance+balance.twoEventChance?2:1;
            var candidates=content.events.Where(e=>e!=null&&e.minDay<=state.currentDay&&(e.maxDay<0||state.currentDay<=e.maxDay)&&e.durationDays>0&&e.weight>0&&CommunityProgress.Finite(e.weight)&&e.effects.Count>0&&e.effects.All(MarketEffects.ValidEffect)&&!state.events.Any(r=>r.eventId==e.id&&r.Active(state.currentDay))).ToList();
            while(additions.Count<count)
            {
                var choices=candidates.Where(e=>Allowed(e,additions)).ToList();if(choices.Count==0)break;
                double ticket=Roll()*choices.Sum(e=>(double)e.weight);var chosen=choices.Last();
                foreach(var e in choices){ticket-=e.weight;if(ticket<0){chosen=e;break;}}
                additions.Add(new EventRecord{eventId=chosen.id,startDay=state.currentDay,expiresOnDay=checked(state.currentDay+chosen.durationDays),effects=chosen.effects.Select(e=>e.Copy()).ToList()});candidates.Remove(chosen);
            }
        }
        state.events.AddRange(additions);state.eventPlanDay=state.currentDay;
    }
    private bool Allowed(EventDefinition candidate,List<EventRecord> additions)
    {
        var existing=state.events.Where(r=>r.Active(state.currentDay)).Concat(additions).SelectMany(r=>r.effects).ToList();
        var all=existing.Concat(candidate.effects).ToList();
        var available=listings.Listings.Where(l=>l!=null&&l.store!=null&&l.food!=null&&l.unlockDay<=state.currentDay&&l.store.unlockDay<=state.currentDay&&
            (string.IsNullOrEmpty(l.requiredFlag)||state.flags.HasFlag(l.requiredFlag))).ToList();
        foreach(var e in candidate.effects)
        {
            if(e.target==EventTarget.Store&&!available.Any(l=>l.store.id==e.targetId))return false;
            if(e.target==EventTarget.Food&&!available.Any(l=>l.food.id==e.targetId))return false;
        }
        var closed=new HashSet<string>(all.Where(e=>e.type==EventEffectType.Availability).Select(e=>e.targetId),StringComparer.OrdinalIgnoreCase);
        if(closed.Count>0&&!available.Any(l=>!closed.Contains(l.store.id)))return false;
        // Avoid announcing a sale in a store simultaneously closed by any active event.
        if(all.Any(e=>e.type==EventEffectType.Price&&e.target==EventTarget.Store&&closed.Contains(e.targetId)))return false;
        return true;
    }
}
