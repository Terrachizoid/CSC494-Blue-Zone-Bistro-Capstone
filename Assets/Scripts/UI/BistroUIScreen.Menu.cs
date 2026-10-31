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
        endingLine=0;endingFinished=false;
        announcedEvents.Clear();toastRecord=null;detailRecord=null;eventCodex=null;
        if(eventToast!=null)Destroy(eventToast.gameObject);eventToast=null;if(eventAudio!=null)eventAudio.Stop();
        cardFaces.Clear();cityPosition=new Vector2(.5f,.5f);cityZoomLevel=1f;motionContext=null;
    }
    private void MainMenu()
    {
        var art=BistroWidgets.Box(screen,"Welcome city",Color.white,Vector2.zero,Vector2.one);
        art.GetComponent<UnityEngine.UI.Image>().sprite=theme.cityBackground;
        art.GetComponent<UnityEngine.UI.Image>().preserveAspect=true;
        art.gameObject.AddComponent<BistroMenuBlur>();
        BistroWidgets.Box(screen,"Welcome veil",new Color(.12f,.20f,.17f,.22f),Vector2.zero,Vector2.one);
        var panel=BistroShelfStyle.Panel(screen,"Welcome panel",new Color(.22f,.32f,.29f,.96f),new Vector2(.07f,.10f),new Vector2(.49f,.90f));
        var shadow=panel.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=new Color(0,0,0,.2f);shadow.effectDistance=new Vector2(4,-5);
        if(theme.cardSprig!=null){var sprig=BistroWidgets.Box(panel,"Botanical accent",new Color(.78f,.84f,.68f,.35f),new Vector2(.68f,.66f),new Vector2(.97f,.96f));sprig.GetComponent<UnityEngine.UI.Image>().sprite=theme.cardSprig;sprig.GetComponent<UnityEngine.UI.Image>().preserveAspect=true;sprig.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;}
        BistroShelfStyle.Text(panel,"WELCOME TO",theme,new Vector2(.09f,.82f),new Vector2(.91f,.90f),18,new Color(.73f,.80f,.66f));
        BistroShelfStyle.Text(panel,"Blue Zone\nBistro",theme,new Vector2(.08f,.54f),new Vector2(.95f,.82f),54,BistroShelfStyle.Cream,true);
        BistroWidgets.Box(panel,"Title rule",new Color(.72f,.78f,.63f,.7f),new Vector2(.09f,.51f),new Vector2(.42f,.514f)).GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
        BistroShelfStyle.Text(panel,"Good food.\nA growing community.",theme,new Vector2(.09f,.37f),new Vector2(.91f,.49f),23,BistroShelfStyle.Cream);
        BistroShelfStyle.Button(panel,confirmNewGame?"Replace saved progress":"Start new game",theme,new Vector2(.09f,.23f),new Vector2(.91f,.33f),new Color(.74f,.81f,.65f),BistroShelfStyle.Ink,()=>Change(()=>{
            if(game.HasCheckpoint&&!confirmNewGame){confirmNewGame=true;return;}
            if(game.NewGame())BindNewSession();}));
        if(confirmNewGame)BistroShelfStyle.Button(panel,"Cancel",theme,new Vector2(.09f,.11f),new Vector2(.91f,.21f),new Color(.36f,.45f,.39f),BistroShelfStyle.Cream,()=>Change(()=>confirmNewGame=false));
        else if(game.HasCheckpoint)BistroShelfStyle.Button(panel,game.ResumeLabel,theme,new Vector2(.09f,.11f),new Vector2(.91f,.21f),new Color(.36f,.45f,.39f),BistroShelfStyle.Cream,()=>Change(()=>{if(game.Resume())BindNewSession();}));
        BistroShelfStyle.Text(panel,confirmNewGame?"Your existing save will be replaced.":game.SaveMessage??"",theme,new Vector2(.09f,.025f),new Vector2(.91f,.10f),16,BistroShelfStyle.Cream);
        var caption=BistroShelfStyle.Panel(screen,"City caption",new Color(.22f,.32f,.29f,.86f),new Vector2(.64f,.10f),new Vector2(.94f,.20f));
        BistroShelfStyle.Text(caption,"A little bistro. A whole city.",theme,new Vector2(.06f,.12f),new Vector2(.94f,.88f),22,BistroShelfStyle.Cream,true,TextAnchor.MiddleCenter);
    }

    private void ReturnMenuPanel()
    {
        DialoguePanel("Blue Zone Guide","Return to main menu? You will restart today when you return.","Return to menu",()=>{modal=null;game.ReturnToMenu();if(dialogueAudio!=null)dialogueAudio.Stop();lastVoicedLine=null;},()=>modal=null);
    }
    // Presentation pass for legacy overlay builders; never changes actions, content or scene objects.
    private void StyleMenu(Transform root)
    {
        foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if(image.GetComponentInParent<BistroFoodCard>()!=null)continue;
            if(image.color==theme.background)image.color=BistroShelfStyle.Cream;
            else if(image.color==theme.panel)image.color=new Color(.86f,.88f,.81f);
            else if(image.color==theme.accent)image.color=new Color(.72f,.79f,.64f);
            if(image.name=="Tutorial")image.color=BistroShelfStyle.Cream;
            if(image.sprite==null&&image.color.a>=.99f){image.sprite=BistroShelfStyle.Rounded;image.type=UnityEngine.UI.Image.Type.Sliced;}
        }
        foreach(var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
            if(text.GetComponentInParent<BistroFoodCard>()!=null)continue;
            if(text.color==theme.card||text.color==theme.ink)text.color=BistroShelfStyle.Ink;
            if(BistroShelfStyle.BodyFont!=null)text.font=text.fontSize>=26?BistroShelfStyle.TitleFont:BistroShelfStyle.BodyFont;
        }
    }}
