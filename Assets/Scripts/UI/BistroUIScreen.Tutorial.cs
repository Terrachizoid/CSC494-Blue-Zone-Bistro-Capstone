// Text-only presentation of tutorial state. No UI coordinates or objects enter tutorial behaviours.
using UnityEngine;
public partial class BistroUIScreen
{
    private string displayedTutorialStep;
    private void PresentTutorial()
    {
        var tutorial=game.Loop.Tutorials;
        if(tutorial?.Active==null){displayedTutorialStep=null;if(modal=="tutorial-help")modal=null;return;}
        string key=tutorial.Active.id+"/"+tutorial.Record.stepId;
        if(displayedTutorialStep!=key)
        {
            displayedTutorialStep=key;
            if(!tutorial.IsText && modal==null)modal="tutorial-help";
        }
        if(!tutorial.IsText)
            BistroWidgets.Button(screen,"Tutorial instructions",theme,new Vector2(.02f,.005f),new Vector2(.25f,.055f),()=>Change(()=>modal="tutorial-help"));
        if(!tutorial.IsText&&modal!="tutorial-help")return;
        var shade=BistroWidgets.Box(screen,"Tutorial",new Color(0,0,0,.85f),Vector2.zero,new Vector2(1,.91f));
        BistroWidgets.Label(shade,"BISTRO GUIDE",theme,new Vector2(.14f,.73f),new Vector2(.86f,.86f),30);
        BistroWidgets.ReadingPanel(shade,tutorial.Instruction,theme,new Vector2(.12f,.28f),new Vector2(.88f,.71f));
        BistroWidgets.Button(shade,tutorial.IsText?"Continue":"Back to play",theme,new Vector2(.30f,.11f),new Vector2(.70f,.23f),()=>Change(()=>{
            if(tutorial.IsText)game.Loop.ContinueTutorial();modal=null;}));
    }
}
