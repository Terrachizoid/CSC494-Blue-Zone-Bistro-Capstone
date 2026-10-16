// Main menu and checkpoint navigation. Returning never saves unfinished work.
using UnityEngine;
public partial class BistroUIScreen
{
    private GameLoop subscribedLoop;
    private bool confirmNewGame;
    private void BindNewSession()
    {
        if(subscribedLoop!=null)subscribedLoop.PhaseChanged-=PhaseChanged;
        subscribedLoop=game.Loop;subscribedLoop.PhaseChanged+=PhaseChanged;
        ResetMeal();cart.Clear();answers.Clear();scrollPositions.Clear();renderedContext=null;
        store=null;shelf=null;modal=null;notice=game.SaveMessage??"";confirmNewGame=false;
        displayedTutorialStep=null;
        cardFaces.Clear();cityPosition=new Vector2(.5f,.5f);motionContext=null;overlayMotionContext=null;
    }
    private void MainMenu()
    {
        BistroWidgets.Label(screen,"BLUE ZONE BISTRO",theme,new Vector2(.10f,.72f),new Vector2(.90f,.92f),42);
        BistroWidgets.Label(screen,"Progress saves at the start of each day.\nClosing mid-day resumes from that day's beginning.\nSaves belong to this browser/device.",theme,new Vector2(.15f,.47f),new Vector2(.85f,.69f),23);
        BistroWidgets.Button(screen,confirmNewGame?"Confirm new game — replace saved progress":"Start new game",theme,new Vector2(.22f,.32f),new Vector2(.78f,.43f),()=>Change(()=>{
            if(game.HasCheckpoint&&!confirmNewGame){confirmNewGame=true;return;}
            if(game.NewGame())BindNewSession();}));
        if(confirmNewGame)BistroWidgets.Button(screen,"Cancel",theme,new Vector2(.22f,.20f),new Vector2(.78f,.29f),()=>Change(()=>confirmNewGame=false));
        else if(game.HasCheckpoint)BistroWidgets.Button(screen,game.ResumeLabel,theme,new Vector2(.22f,.20f),new Vector2(.78f,.29f),()=>Change(()=>{if(game.Resume())BindNewSession();}));
        BistroWidgets.Label(screen,game.SaveMessage??"",theme,new Vector2(.08f,.03f),new Vector2(.92f,.16f),19);
    }
    private void ReturnMenuPanel()
    {
        var shade=BistroWidgets.Box(screen,"Return confirmation",new Color(0,0,0,.85f),Vector2.zero,Vector2.one);
        BistroWidgets.Label(shade,"Return to main menu?\nUnfinished work since the day-start checkpoint will be discarded when you resume.",theme,new Vector2(.14f,.47f),new Vector2(.86f,.77f),26);
        BistroWidgets.Button(shade,"Keep playing",theme,new Vector2(.12f,.28f),new Vector2(.46f,.41f),()=>Change(()=>modal=null));
        BistroWidgets.Button(shade,"Return to menu",theme,new Vector2(.54f,.28f),new Vector2(.88f,.41f),()=>Change(()=>{modal=null;game.ReturnToMenu();if(dialogueAudio!=null)dialogueAudio.Stop();lastVoicedLine=null;}));
    }
}
