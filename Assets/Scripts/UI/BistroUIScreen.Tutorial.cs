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
        // Day-1 lesson steps that happen inside the recipe book show their text over the book (the book stays open).
        if(!tutorial.IsText && !tutorial.InstructionSeen && modal=="recipes" && ShopLesson()){game.Loop.MarkTutorialSeen();ShowLessonNote(tutorial.Instruction);}
        // Text lessons show over any open panel (Guide dialogues are drawn last) and leave that panel open.
        if(modal=="main-menu")return;
        if(!tutorial.IsText&&modal!="tutorial-help")return;
        bool text=tutorial.IsText;
        DialoguePanel("Blue Zone Guide",tutorial.Instruction,text?"Continue":"Back to play",()=>{
            if(text)game.Loop.ContinueTutorial();if(!text||modal=="tutorial-help")modal=null;});
    }
}
