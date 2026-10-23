// Selects exactly one eligible ending, then an explicit fallback. Never controls presentation.
using System;
using System.Linq;
public class EndingService
{
    private readonly GameSessionState state;
    private readonly EndingDatabase database;
    public EndingService(GameSessionState state,EndingDatabase database){this.state=state;this.database=database;}
    private static bool InRange(float value,float min,float max)=>CommunityProgress.Finite(value)&&(min<0||value>=min)&&(max<0||value<max);
    private bool MoneyInRange(EndingDefinition e)=>CommunityProgress.Finite(state.money)&&(!e.hasMinMoney||state.money>=e.minMoney)&&(!e.hasMaxMoney||state.money<e.maxMoney);
    public string Select()
    {
        var rows=(database?.endings??new System.Collections.Generic.List<EndingDefinition>())
            .Where(e=>e!=null&&!string.IsNullOrWhiteSpace(e.id)).OrderByDescending(e=>e.priority).ThenBy(e=>e.id,StringComparer.Ordinal);
        return (rows.FirstOrDefault(e=>InRange(state.popularity,e.minPopularity,e.maxPopularity)&&InRange(state.communityWellbeing,e.minWellbeing,e.maxWellbeing)&&MoneyInRange(e))
            ??rows.FirstOrDefault(e=>e.fallback))?.id;
    }
    // Completed saves keep their original selection even when tuning changes later.
    public void Complete(){if(string.IsNullOrEmpty(state.endingId))state.endingId=Select();}
}
