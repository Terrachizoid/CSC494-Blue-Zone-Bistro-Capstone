// Invisible drop behaviour on the cream cart panel; reuses the existing single-drop contract.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
// Anything the player can drag onto a drop area: shelf food cards and cart rows.
public interface IBistroDragSource { string Key { get; } string Kind { get; } bool TryClaimDrop(int pointerId); }
public sealed class BistroShelfCartTarget : MonoBehaviour, IDropHandler
{
    public Action<string,string> Drop;
    public void OnDrop(PointerEventData e)
    {
        var source=e.pointerDrag==null?null:e.pointerDrag.GetComponent<IBistroDragSource>();
        if(source!=null&&source.TryClaimDrop(e.pointerId))Drop?.Invoke(source.Key,source.Kind);
    }
}
