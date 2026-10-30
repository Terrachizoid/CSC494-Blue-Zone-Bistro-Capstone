// Ending presentation only. Reads the persisted ID; lines advance without rerunning selection or gameplay effects.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public partial class BistroUIScreen
{
    private int endingLine;
    private bool endingFinished;
    private void EndingScreen()
    {
        var ending=game.Queries.GetEnding(game.State.endingId);
        var lines=(ending?.dialogue?.lines??Array.Empty<DialogueLineData>()).Where(l=>l!=null&&!string.IsNullOrWhiteSpace(l.textTemplate)).OrderBy(l=>l.lineOrder).ToArray();
        endingLine=Mathf.Clamp(endingLine,0,Math.Max(0,lines.Length-1));
        Sprite art=ending?.image;
        foreach(var line in lines.Take(endingLine+1))if(line.backgroundImage!=null)art=line.backgroundImage;
        var background=BistroWidgets.Box(screen,"Ending artwork",theme.panel,new Vector2(.04f,.40f),new Vector2(.96f,.91f));
        if(art!=null){var image=background.GetComponent<Image>();image.sprite=art;image.color=Color.white;image.preserveAspect=true;}
        else BistroWidgets.Label(background,"YOUR BISTRO'S STORY",theme,new Vector2(.1f,.3f),new Vector2(.9f,.7f),32);
        BistroWidgets.Label(screen,ending?.title??"Run completed",theme,new Vector2(.05f,.91f),new Vector2(.95f,.99f),32);
        string text=lines.Length==0?"Your run is complete. Ending content is unavailable, but you can return to the main menu.":lines[endingLine].textTemplate;
        if(endingFinished||lines.Length==0)text=RunRecap();
        DialoguePanel(ending?.title??"Your bistro",text,endingFinished||lines.Length==0?"Main menu":endingLine+1<lines.Length?"Continue":"Finish story",()=>{
            if(endingFinished||lines.Length==0)game.ReturnToMenu();else if(endingLine+1<lines.Length)endingLine++;else endingFinished=true;
        },()=>game.ReturnToMenu());
        if(game.SaveFailed)BistroWidgets.Button(screen,"Retry save",theme,new Vector2(.34f,.04f),new Vector2(.58f,.12f),()=>Change(()=>game.RetrySave()));
    }
    // End-of-game recap from the persisted serve history and customer records (no new calculations of outcomes).
    private string RunRecap()
    {
        var serves=game.State.recipeServes??new List<RecipeServeRecord>();
        int green=serves.Sum(x=>x.green),yellow=serves.Sum(x=>x.yellow),red=serves.Sum(x=>x.red);
        var tried=serves.Select(x=>x.recipeId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        int unlocked=game.Queries.GetUnlockedRecipes().Count;
        var covered=new HashSet<DailyDozenCategory>(game.State.customers.Where(c=>c.dailyDozenEaten!=null).SelectMany(c=>c.dailyDozenEaten));
        var missing=Enum.GetValues(typeof(DailyDozenCategory)).Cast<DailyDozenCategory>().Where(g=>g!=DailyDozenCategory.None&&!covered.Contains(g)).ToList();
        string guests=string.Join("\n",game.State.customers.Select(c=>{var d=game.Queries.GetCustomer(c.customerId);
            return d==null?null:$"  {d.displayName}: health {d.startingHealth} → {c.health}";}).Where(x=>x!=null));
        return $"Dishes served {serves.Count} ({serves.Count(x=>x.built)} matched their recipe)\n"+
            $"Servings cooked: {green} green · {yellow} yellow · {red} red\n"+
            $"Recipes tried: {tried.Count} of {unlocked}\n"+
            $"Daily Dozen groups served: {covered.Count} of {covered.Count+missing.Count}"+(missing.Count==0?" (all of them!)":" · never served: "+IntroMealPlan.ReadableList(missing))+"\n"+
            $"Money ${game.State.money:0.00} · Popularity {game.State.popularity:0} · Community wellbeing {game.State.communityWellbeing:0}\n"+
            (guests.Length==0?"":"Guests:\n"+guests);
    }
}
