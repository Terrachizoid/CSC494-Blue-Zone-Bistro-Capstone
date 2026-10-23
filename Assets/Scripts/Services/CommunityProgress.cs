// Shared tuning validation, old-save initialization and pure global outcome calculation.
// Customer clamps never erase a dish's contribution to the bistro/community.
using System;
using System.Collections.Generic;
public static class CommunityProgress
{
    public static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
    public static bool Valid(BalanceConfig b)
    {
        if(b==null||b.maxDelight<1)return false;
        foreach(float v in new[]{b.maximumPopularity,b.startingPopularity,b.popularityPerDelight,
            b.failedRecipePopularityPenalty,b.maximumPopularityGainPerDish,b.maximumCommunityWellbeing,
            b.startingCommunityWellbeing,b.wellbeingPerHealthPoint,b.ingredientAppealMultiplier,
            b.recipeDelightMultiplier,b.delightPerResolvedCraving,b.popularityPerMissedCraving,b.rentPerDay,b.walkInIncomePerPopularity,b.walkInPopularityThreshold})if(!Finite(v)||v<0)return false;
        if(b.maximumPopularity<=0||b.maximumCommunityWellbeing<=0||b.startingPopularity>b.maximumPopularity||
            b.startingCommunityWellbeing>b.maximumCommunityWellbeing||b.cravingsStartDay<1||
            b.minimumDailyCravings<1||b.maximumDailyCravings<b.minimumDailyCravings||b.maximumDailyCravings>6||
            b.attendanceThresholds==null||b.attendanceThresholds.Length==0||b.attendanceThresholds[0]!=0)return false;
        float previous=-1;
        foreach(float t in b.attendanceThresholds){if(!Finite(t)||t<=previous||t>b.maximumPopularity)return false;previous=t;}
        return true;
    }
    public static void Initialize(GameSessionState s,BalanceConfig b)
    {
        if(!Valid(b))throw new ArgumentException("Invalid popularity, wellbeing or craving balance configuration.");
        if(s.communityStateVersion==0){s.popularity=b.startingPopularity;s.communityWellbeing=b.startingCommunityWellbeing;s.attendancePlanDay=0;s.communityStateVersion=1;}
        if(s.communityStateVersion!=1||!Finite(s.popularity)||!Finite(s.communityWellbeing))throw new ArgumentException("Invalid community state.");
        s.popularity=Clamp(s.popularity,b.maximumPopularity);s.communityWellbeing=Clamp(s.communityWellbeing,b.maximumCommunityWellbeing);
        if(s.dailyCustomerIds==null)s.dailyCustomerIds=new List<string>();
        if(s.unlockedCustomerIds==null)s.unlockedCustomerIds=new List<string>();
    }
    public static float Clamp(double value,float max)=>(float)Math.Max(0,Math.Min(max,value));
    public static int Capacity(float popularity,BalanceConfig b)
    {int n=0;foreach(float t in b.attendanceThresholds)if(popularity>=t)n++;return Math.Max(1,n);}
    public static void Evaluate(GameSessionState s,BalanceConfig b,ServeResult r)
    {
        double gain=-b.failedRecipePopularityPenalty;
        if(r.recipeBuilt)
        {
            double fraction=s.currentDay<b.cravingsStartDay?1:
                r.activeCravings.Length==0?0:(double)r.cravingsResolved.Length/r.activeCravings.Length;
            gain=Math.Min(b.maximumPopularityGainPerDish,Math.Max(0,r.dishDelight)*b.popularityPerDelight*fraction);
            // Ignoring a customer's request costs reputation. This depends on cravings only, never on health.
            if(s.currentDay>=b.cravingsStartDay)gain-=b.popularityPerMissedCraving*(r.activeCravings.Length-r.cravingsResolved.Length);
        }
        r.popularityDelta=Clamp(s.popularity+gain,b.maximumPopularity)-s.popularity;
        r.communityWellbeingDelta=Clamp(s.communityWellbeing+(double)r.dishHealthEffect*b.wellbeingPerHealthPoint,b.maximumCommunityWellbeing)-s.communityWellbeing;
    }
}
