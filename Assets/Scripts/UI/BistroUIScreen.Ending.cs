// Ending presentation only. Reads the persisted ID; lines advance without rerunning selection or gameplay effects.
using System;
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
        BistroWidgets.ReadingPanel(screen,text,theme,new Vector2(.06f,.15f),new Vector2(.94f,.38f));
        if(endingFinished)BistroWidgets.Label(screen,"Story complete",theme,new Vector2(.62f,.04f),new Vector2(.94f,.12f),22);
        if(lines.Length>0&&!endingFinished)
            BistroWidgets.Button(screen,endingLine+1<lines.Length?"Continue":"Finish story",theme,new Vector2(.62f,.04f),new Vector2(.94f,.12f),()=>Change(()=>{if(endingLine+1<lines.Length)endingLine++;else endingFinished=true;}));
        BistroWidgets.Button(screen,"Main menu",theme,new Vector2(.06f,.04f),new Vector2(.30f,.12f),()=>Change(()=>game.ReturnToMenu()));
        if(game.SaveFailed)BistroWidgets.Button(screen,"Retry save",theme,new Vector2(.34f,.04f),new Vector2(.58f,.12f),()=>Change(()=>game.RetrySave()));
    }
}
