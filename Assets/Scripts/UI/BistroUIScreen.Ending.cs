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
        var viewport=BistroWidgets.Box(screen,"Ending scene",theme.panel,Vector2.zero,Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        var background=BistroWidgets.Box(viewport,"Ending artwork",theme.panel,new Vector2(.5f,.5f),new Vector2(.5f,.5f));
        if(art!=null)
        {
            var image=background.GetComponent<Image>();image.sprite=art;image.color=Color.white;image.raycastTarget=false;
            var layout=background.gameObject.AddComponent<BistroWorldLayout>();layout.Viewport=viewport;layout.Aspect=art.rect.width/art.rect.height;layout.Cover=true;
            Canvas.ForceUpdateCanvases();layout.Apply();
        }
        var heading=BistroShelfStyle.Panel(screen,"Ending title",new Color(.94f,.92f,.86f,.90f),new Vector2(.035f,.81f),new Vector2(.64f,.965f));
        heading.GetComponent<Image>().raycastTarget=false;BistroSurface.Dress(heading,BistroShelfStyle.Green,theme.cardSprig);
        BistroShelfStyle.Text(heading,"BLUE ZONE BISTRO  /  EPILOGUE",theme,new Vector2(.035f,.67f),new Vector2(.96f,.92f),15,BistroShelfStyle.Green);
        BistroShelfStyle.Text(heading,ending?.title??"The end",theme,new Vector2(.035f,.13f),new Vector2(.96f,.68f),30,BistroShelfStyle.Ink,true);
        string text=lines.Length==0?"Your run is complete. Ending content is unavailable, but you can return to the main menu.":lines[endingLine].textTemplate;
        if(endingFinished||lines.Length==0)text=RunRecap();
        DialoguePanel(ending?.title??"Your bistro",text,endingFinished||lines.Length==0?"Main menu":endingLine+1<lines.Length?"Continue":"Finish story",()=>{
            if(endingFinished||lines.Length==0)game.ReturnToMenu();else if(endingLine+1<lines.Length)endingLine++;else endingFinished=true;
        },()=>game.ReturnToMenu());
        if(game.SaveFailed)BistroActionButton.Create(screen,"Retry save",theme,new Vector2(.77f,.86f),new Vector2(.96f,.94f),BistroActionRole.Secondary,()=>Change(()=>game.RetrySave()));
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
