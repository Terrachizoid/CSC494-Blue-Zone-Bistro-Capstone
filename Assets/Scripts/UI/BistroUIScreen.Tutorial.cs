// Text-only presentation of tutorial state. No UI coordinates or objects enter tutorial behaviours.
using UnityEngine;
public partial class BistroUIScreen
{
    private void PresentTutorial()
    {
        var tutorial=game.Loop.Tutorials;
        if(tutorial?.Active==null){
            // Help repeats only the lesson in progress; finished lessons are not replayed here.
            if(modal=="tutorial-help")DialoguePanel("Blue Zone Guide","All guides are complete for now. A new one will appear here when there is something new to learn.","Back to play",()=>modal=null);
            return;
        }
        // An action step's instruction opens by itself once, as soon as no other panel is open. "Seen" is stored
        // on the lesson record, so another lesson stepping in between does not make this one start over.
        if(!tutorial.IsText && !tutorial.InstructionSeen && modal==null){game.Loop.MarkTutorialSeen();modal="tutorial-help";}
        if(modal!=null&&modal!="tutorial-help")return;
        if(!tutorial.IsText&&modal!="tutorial-help")return;
        DialoguePanel("Blue Zone Guide",tutorial.Instruction,tutorial.IsText?"Continue":"Back to play",()=>{
            if(tutorial.IsText)game.Loop.ContinueTutorial();modal=null;});
    }
}
