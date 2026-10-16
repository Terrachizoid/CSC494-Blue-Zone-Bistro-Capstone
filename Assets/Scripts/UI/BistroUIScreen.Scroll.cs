// Preserve browsing position across redraws caused by card placement/removal and modal panels.
// Each logical screen and hierarchy path has its own position; never share shelf/recipe scroll state.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public partial class BistroUIScreen
{
    private readonly Dictionary<string,float> scrollPositions=new Dictionary<string,float>();
    private string renderedContext;
    private string ScrollContext()=>$"{game.State.currentDay}/{game.Loop.Phase}/{store?.id}/{shelf}/{mealRecipe?.id}/{game.Loop.Service.CurrentCustomerId}";
    private string ScrollKey(ScrollRect scroll)
    {
        var parts=new List<string>();
        for(var node=scroll.transform;node!=null && node!=screen;node=node.parent)
            parts.Add(node.name+":"+node.GetSiblingIndex());
        parts.Reverse();return string.Join("/",parts);
    }
    private void RememberScroll()
    {
        if(screen==null || renderedContext==null)return;
        foreach(var scroll in screen.GetComponentsInChildren<ScrollRect>().Where(s=>!s.horizontal))
            scrollPositions[renderedContext+"/"+ScrollKey(scroll)]=scroll.verticalNormalizedPosition;
    }
    private void RestoreScroll()
    {
        renderedContext=ScrollContext();
        // Layout must settle before ScrollRect can calculate a meaningful normalized position.
        Canvas.ForceUpdateCanvases();
        foreach(var grid in screen.GetComponentsInChildren<BistroResponsiveGrid>())grid.Apply();
        Canvas.ForceUpdateCanvases();
        foreach(var scroll in screen.GetComponentsInChildren<ScrollRect>().Where(s=>!s.horizontal))
        {
            scroll.StopMovement();
            scroll.verticalNormalizedPosition=scrollPositions.TryGetValue(renderedContext+"/"+ScrollKey(scroll),out var position)
                ? Mathf.Clamp01(position):1;
        }
        // Bound transient navigation memory over very long runs.
        if(scrollPositions.Count>512)scrollPositions.Clear();
    }
}
