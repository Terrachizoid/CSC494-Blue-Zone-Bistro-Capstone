// Navigation changes animate content once. Overlay changes never restart the page beneath.
// No artwork names or gameplay decisions enter the motion layer.
using UnityEngine;
public partial class BistroUIScreen
{
    private string overlayMotionContext;
    private void AnimateNavigation()
    {
        string page=game.IsMainMenu?"menu":$"{game.Loop.Phase}/{store?.id}/{shelf}/{mealRecipe?.id}/{game.Loop.Service.CurrentCustomerId}/{game.Loop.Service.awaitingDishAcknowledgement}/{game.Loop.Dialogue?.Sequence?.id}";
        string overlay=game.IsMainMenu?null:modal;
        if(!game.IsMainMenu && (modal=="tutorial-help" || (modal==null && game.Loop.Tutorials?.IsText==true)))
            overlay=$"tutorial/{game.Loop.Tutorials?.Active?.id}/{game.Loop.Tutorials?.Record?.stepId}";
        bool pageChanged=page!=motionContext,overlayChanged=overlay!=overlayMotionContext;
        motionContext=page;overlayMotionContext=overlay;
        if(theme.reducedMotion)return;
        RectTransform target=null;
        if(overlay!=null)
        {
            if(!overlayChanged)return;
            var blocker=screen.Find("Modal blocker");
            if(blocker!=null&&blocker.childCount>0)target=blocker.GetChild(0) as RectTransform;
            if(target==null)target=screen.Find(overlay.StartsWith("tutorial/")?"Tutorial":"Return confirmation") as RectTransform;
        }
        else if(pageChanged)
        {
            foreach(string name in new[]{"City viewport","Store interior","Bistro interior","Card shelf","Reading panel"})
            {target=screen.Find(name) as RectTransform;if(target!=null)break;}
        }
        // Closing a modal simply reveals the existing browsing pose. The HUD never scales.
        if(target!=null)BistroMotion.Pop(target,theme.panelRevealSeconds);
    }
}
