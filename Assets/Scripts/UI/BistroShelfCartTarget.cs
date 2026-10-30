// Invisible drop behaviour on the cream cart panel; reuses the existing single-drop contract.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
public sealed class BistroShelfCartTarget : MonoBehaviour, IDropHandler
{
    public Action<string,string> Drop;
    public void OnDrop(PointerEventData e)
    {
        var card=e.pointerDrag==null?null:e.pointerDrag.GetComponent<BistroFoodCard>();
        if(card!=null&&card.TryClaimDrop(e.pointerId))Drop?.Invoke(card.Key,card.Kind);
    }
}
