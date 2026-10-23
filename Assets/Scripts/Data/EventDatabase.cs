// Event content is immutable at runtime. Each event owns any number of typed effect rows.
using System;
using System.Collections.Generic;
using UnityEngine;
public enum EventEffectType { Price, Availability, AttendancePopularity }
public enum EventTarget { Store, Food, Bistro }
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
    public Sprite image;
    public AudioClip notificationAudio, detailAudio;
    public ClaimData[] codex=Array.Empty<ClaimData>();
    public List<EventEffect> effects=new List<EventEffect>();
}
[CreateAssetMenu(menuName="Blue Zone Bistro/Event Database")]
public class EventDatabase : ScriptableObject
{
    public List<EventDefinition> events=new List<EventDefinition>();
}
