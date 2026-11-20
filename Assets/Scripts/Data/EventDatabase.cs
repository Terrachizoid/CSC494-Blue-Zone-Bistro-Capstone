// Event content is immutable at runtime. Each event owns any number of typed effect rows.
using System;
using System.Collections.Generic;
using UnityEngine;
// 2026-11-19: Craving (request weight), SortingRefund (refund multiplier), ShelfLife (days added to food bought that day);
// targets Category (a food type, e.g. Fruit), Shelf ("storeId/shelfId") and Craving (a craving tag).
public enum EventEffectType { Price, Availability, AttendancePopularity, Craving, SortingRefund, ShelfLife }
public enum EventTarget { Store, Food, Bistro, Category, Shelf, Craving }
public enum EventOperation { Add, Multiply, Close }
[Serializable] public class EventEffect
{
    public EventEffectType type;
    public EventTarget target;
    public string targetId;
    public EventOperation operation;
    public float value;
    public EventEffect Copy()=>(EventEffect)MemberwiseClone();
}
[Serializable] public class EventDefinition
{
    public string id, headline, summary, details;
    public int minDay=2, maxDay=-1, durationDays=1;
    public float weight=1;
    // False = never chosen in easy mode (BalanceConfig.easyMode), e.g. closures and shortages.
    public bool easyMode=true;
    public Sprite image;
    // Optional compact art; defaults to the same simple emblem. CSV-owned presentation only.
    public Sprite thumbnail;
    public string paperColor = "#E9E2D0", accentColor = "#A6B7A0";
    public AudioClip notificationAudio, detailAudio;
    public ClaimData[] codex=Array.Empty<ClaimData>();
    public List<EventEffect> effects=new List<EventEffect>();
}
[CreateAssetMenu(menuName="Blue Zone Bistro/Event Database")]
public class EventDatabase : ScriptableObject
{
    public List<EventDefinition> events=new List<EventDefinition>();
}
