// Copy to Assets/Editor in an isolated Unity project with the Bistro UI primitives.
// Run -batchmode -nographics -executeMethod UnityUIInteractionSmoke.Run (without -quit).
// Uses actual Play Mode frames and exits with a nonzero code on assertion failure.
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public static class UnityUIInteractionSmoke
{
    const string Pending="Bistro.UI.smoke.pending";
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
    [InitializeOnLoadMethod] static void Register(){EditorApplication.playModeStateChanged+=Changed;}
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
        {SessionState.SetBool(Pending,false);CheckUI();}
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static async Task Frames(int count=3)
    {
        int start=Time.frameCount;var deadline=DateTime.UtcNow.AddSeconds(15);
        while(Time.frameCount-start<count){if(DateTime.UtcNow>deadline)throw new Exception("Play Mode frames stalled");await Task.Delay(20);}
    }
    static async void CheckUI()
    {
        try
        {
            var root=new GameObject("Test canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var events=new GameObject("Events",typeof(EventSystem));
            var theme=ScriptableObject.CreateInstance<BistroUITheme>();
            var food=ScriptableObject.CreateInstance<FoodData>();food.displayName="Smoke food";
            var viewport=BistroWidgets.Box(root.transform,"Narrow shelf",Color.gray,Vector2.zero,Vector2.zero);
            viewport.sizeDelta=new Vector2(180,280);
            var grid=BistroWidgets.Grid(viewport,theme,Vector2.zero,Vector2.one);
            var card=BistroFoodCard.Create(grid,food,"F","offer","3 servings",theme);
            var fixedCard=BistroFoodCard.Create(grid,food,"B","batch","1 serving",theme,false);
            for(int i=0;i<5;i++)BistroFoodCard.Create(grid,food,"B"+i,"batch","1 serving",theme,false);
            await Frames(5);Canvas.ForceUpdateCanvases();
            Check(grid.GetComponent<GridLayoutGroup>().cellSize.x<=144.1f,"Narrow shelf clips a card");
            viewport.sizeDelta=new Vector2(600,280);await Frames(5);Canvas.ForceUpdateCanvases();
            Check(Mathf.Abs(grid.GetComponent<GridLayoutGroup>().cellSize.x-166)<.1f,"Card size did not recover after resize");
            card.RestoreFace(true);Check(card.IsFlipped,"Back restoration failed");card.RestoreFace(false);
            var pointer=new PointerEventData(events.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left,position=new Vector2(100,100)};
            card.OnPointerClick(pointer);await Task.Delay(350);
            Check(card.IsFlipped && (card.transform.localScale-Vector3.one).sqrMagnitude<.001f,"Flip failed to finish");
            pointer.button=PointerEventData.InputButton.Right;card.OnPointerClick(pointer);await Task.Delay(300);
            Check(card.IsFlipped,"Right click unexpectedly flipped a card");pointer.button=PointerEventData.InputButton.Left;
            int drops=0;var slot=BistroDropSlot.Create(root.transform,"Test slot","",theme,new Vector2(.5f,0),Vector2.one,false,(id,kind)=>drops++,null);
            pointer.pointerDrag=card.gameObject;card.OnBeginDrag(pointer);card.OnDrag(pointer);slot.OnDrop(pointer);slot.OnDrop(pointer);card.OnEndDrag(pointer);
            Check(drops==1&&!card.IsDragging,"Valid drag did not complete once");
            card.OnBeginDrag(pointer);
            var otherPointer=new PointerEventData(events.GetComponent<EventSystem>()){pointerId=123,button=PointerEventData.InputButton.Left,pointerDrag=card.gameObject};
            slot.OnDrop(otherPointer);card.OnEndDrag(otherPointer);
            Check(drops==1&&card.IsDragging,"Another pointer hijacked an active card drag");card.OnEndDrag(pointer);
            card.OnBeginDrag(pointer);card.SendMessage("OnApplicationFocus",false);slot.OnDrop(pointer);card.OnEndDrag(pointer);
            Check(drops==1,"Cancelled drag committed a drop");
            await Frames();Check(root.transform.Find("Dragging card")==null,"Drag ghost leaked");
            var scroll=grid.GetComponentInParent<ScrollRect>();scroll.verticalNormalizedPosition=.5f;scroll.StopMovement();
            pointer.pointerDrag=fixedCard.gameObject;pointer.position=new Vector2(100,100);
            fixedCard.OnInitializePotentialDrag(pointer);fixedCard.OnBeginDrag(pointer);float before=grid.anchoredPosition.y;
            pointer.position=new Vector2(100,160);fixedCard.OnDrag(pointer);fixedCard.OnEndDrag(pointer);
            Check(Mathf.Abs(grid.anchoredPosition.y-before)>1,"Pantry card blocked shelf scrolling");
            Time.timeScale=0;BistroMotion.Pop(viewport);await Task.Delay(300);
            Check(Mathf.Abs(viewport.GetComponent<CanvasGroup>().alpha-1)<.001f && (viewport.localScale-Vector3.one).sqrMagnitude<.001f,"Motion stuck while game time paused");
            BistroMotion.Pop(viewport);await Frames(1);BistroMotion.Pop(viewport);await Task.Delay(300);
            Check(viewport.GetComponents<BistroMotion>().Length==1,"Repeated entrance added competing components");
            BistroMotion.Pop(viewport);await Frames(1);viewport.gameObject.SetActive(false);viewport.gameObject.SetActive(true);await Frames();
            Check(Mathf.Abs(viewport.GetComponent<CanvasGroup>().alpha-1)<.001f && (viewport.localScale-Vector3.one).sqrMagnitude<.001f,"Interrupted entrance left a faded or shrunken panel");
            var authored=BistroWidgets.Box(root.transform,"Authored transform",Color.gray,Vector2.zero,Vector2.one);
            authored.localScale=new Vector3(.8f,.9f,1);authored.gameObject.AddComponent<CanvasGroup>().alpha=.7f;
            BistroMotion.Pop(authored);await Task.Delay(300);
            Check((authored.localScale-new Vector3(.8f,.9f,1)).sqrMagnitude<.001f && Mathf.Abs(authored.GetComponent<CanvasGroup>().alpha-.7f)<.001f,"Entrance lost authored scale/opacity");
            Time.timeScale=1;
            Debug.Log("PASS: Unity UI resize, flip, face restore, right-click guard, single drop, focus cancellation, ghost cleanup, pantry scroll, unscaled/restarted/interrupted animation, authored scale/opacity.");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
