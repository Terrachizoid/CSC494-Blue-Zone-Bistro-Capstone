// UI motion. Only what actually changed animates; unchanged parts (top bar, room background) never pulse.
// - Moving to another place (phase, store, shelf, main menu): the whole screen cross-fades over the old one.
// - Same place: the screen is still rebuilt, but only top-level pieces that appeared (a panel, a dialogue box)
//   fade and pop in, and pieces that disappeared fade out as a short-lived copy. Everything else swaps
//   in the same frame, which looks identical, so it does not flicker.
// No artwork names or gameplay decisions enter the motion layer.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public partial class BistroUIScreen
{
    private RectTransform outgoingScreen;
    private Coroutine screenFade;
    // Called by Refresh instead of destroying the current screen.
    private void RetireScreen()
    {
        FinishScreenFade();
        if(screen==null)return;
        outgoingScreen=screen;outgoingScreen.name="Outgoing screen";
        var group=Group(outgoingScreen);group.alpha=1;group.blocksRaycasts=false;group.interactable=false;
    }
    // Ends a running place cross-fade at once (rapid clicks): the newest screen is fully shown.
    private void FinishScreenFade()
    {
        if(screenFade!=null){StopCoroutine(screenFade);screenFade=null;}
        var group=screen!=null?screen.GetComponent<CanvasGroup>():null;if(group!=null)group.alpha=1;
        DropOutgoing();
    }
    private void DropOutgoing(){if(outgoingScreen!=null){outgoingScreen.gameObject.SetActive(false);Destroy(outgoingScreen.gameObject);outgoingScreen=null;}}
    private static CanvasGroup Group(Component c){var g=c.GetComponent<CanvasGroup>();return g!=null?g:c.gameObject.AddComponent<CanvasGroup>();}
    private IEnumerator Fade(RectTransform target,float from,float to,float duration,bool destroyAtEnd,bool screenSwap)
    {
        var group=Group(target);group.alpha=from;
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
        {
            if(target==null)yield break;
            float k=t/duration;group.alpha=Mathf.Lerp(from,to,k*k*(3-2*k));
            yield return null;
        }
        if(target==null)yield break;
        group.alpha=to;
        if(destroyAtEnd)Destroy(target.gameObject);
        if(screenSwap){DropOutgoing();screenFade=null;}
    }
    private void AnimateNavigation()
    {
        string place=game.IsMainMenu?"menu":$"{game.Loop.Phase}/{store?.id}/{shelf}";
        bool placeChanged=place!=motionContext;motionContext=place;
        if(theme.reducedMotion||outgoingScreen==null||!isActiveAndEnabled){DropOutgoing();return;}
        float duration=theme.screenFadeSeconds;
        if(placeChanged){screenFade=StartCoroutine(Fade(screen,0,1,duration,false,true));return;}
        var before=new HashSet<string>();foreach(Transform child in outgoingScreen)before.Add(child.name);
        var now=new HashSet<string>();foreach(Transform child in screen)now.Add(child.name);
        foreach(Transform child in screen)
            if(!before.Contains(child.name))
            {
                StartCoroutine(Fade((RectTransform)child,0,1,duration,false,false));
                if(child.childCount>0)BistroMotion.Pop(child.GetChild(0) as RectTransform,theme.panelRevealSeconds);
            }
        var leaving=new List<RectTransform>();
        foreach(Transform child in outgoingScreen)if(!now.Contains(child.name))leaving.Add((RectTransform)child);
        foreach(var ghost in leaving)
        {
            ghost.SetParent(screen,false);ghost.SetAsLastSibling();
            var group=Group(ghost);group.blocksRaycasts=false;group.interactable=false;
            StartCoroutine(Fade(ghost,1,0,duration,true,false));
        }
        DropOutgoing();
    }
}
