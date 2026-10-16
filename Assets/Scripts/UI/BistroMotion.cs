// Owns one presentation animation per object. Preserves authored scale/opacity and
// restores them when interrupted; never delays or commits a gameplay operation.
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BistroMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 restScale;
    private float targetScale=1;
    private float restAlpha=1;
    private Selectable selectable;
    private CanvasGroup group;
    private Coroutine reveal;
    private bool hovered;
    private bool CanRespond=>selectable!=null && selectable.IsInteractable();
    private void Awake(){restScale=transform.localScale;selectable=GetComponent<Selectable>();}
    public static void Pop(RectTransform panel,float duration=.18f)
    {
        if(panel==null || !panel.gameObject.activeInHierarchy)return;
        var motion=panel.GetComponent<BistroMotion>();
        if(motion==null)motion=panel.gameObject.AddComponent<BistroMotion>();
        motion.BeginReveal(duration);
    }
    private void BeginReveal(float duration)
    {
        bool restarting=reveal!=null;
        if(restarting)StopCoroutine(reveal);
        if(group==null){group=GetComponent<CanvasGroup>();if(group==null)group=gameObject.AddComponent<CanvasGroup>();}
        if(!restarting)restAlpha=group.alpha;
        duration=float.IsNaN(duration)||float.IsInfinity(duration)?.18f:Mathf.Clamp(duration,.01f,1);
        // Restart from the currently displayed pose rather than snapping backwards.
        reveal=StartCoroutine(Reveal(duration,restarting?transform.localScale:restScale*.98f,restarting?group.alpha:restAlpha*.35f));
    }
    private IEnumerator Reveal(float duration,Vector3 fromScale,float fromAlpha)
    {
        float elapsed=0;
        while(elapsed<duration)
        {
            float t=Mathf.Clamp01(elapsed/duration);float eased=1-Mathf.Pow(1-t,3);
            group.alpha=Mathf.Lerp(fromAlpha,restAlpha,eased);
            transform.localScale=Vector3.Lerp(fromScale,restScale,eased);
            yield return null;elapsed+=Time.unscaledDeltaTime;
        }
        transform.localScale=restScale;group.alpha=restAlpha;reveal=null;
    }
    private void Update()
    {
        if(reveal!=null)return;
        if(!CanRespond)targetScale=1;
        Vector3 target=restScale*targetScale;
        if((transform.localScale-target).sqrMagnitude<.000001f)return;
        var next=Vector3.Lerp(transform.localScale,target,1-Mathf.Exp(-22*Time.unscaledDeltaTime));
        transform.localScale=(next-target).sqrMagnitude<.000001f?target:next;
    }
    private void ResetPose()
    {
        if(reveal!=null)StopCoroutine(reveal);
        reveal=null;hovered=false;targetScale=1;transform.localScale=restScale;
        if(group!=null)group.alpha=restAlpha;
    }
    private void OnDisable(){ResetPose();}
    private void OnApplicationFocus(bool focused){if(!focused)ResetPose();}
    public void OnPointerEnter(PointerEventData e){hovered=true;if(CanRespond)targetScale=1.015f;}
    public void OnPointerExit(PointerEventData e){hovered=false;targetScale=1;}
    public void OnPointerDown(PointerEventData e){if(CanRespond && e.button==PointerEventData.InputButton.Left)targetScale=.97f;}
    public void OnPointerUp(PointerEventData e){targetScale=hovered&&CanRespond?1.015f:1;}
}
