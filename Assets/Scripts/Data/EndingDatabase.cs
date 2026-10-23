// Authored ending rules and presentation references. Blank CSV bounds become -1 (unrestricted).
using System;
using System.Collections.Generic;
using UnityEngine;
[Serializable]
public class EndingDefinition
{
    public string id, title;
    public float minPopularity=-1, maxPopularity=-1, minWellbeing=-1, maxWellbeing=-1;
    // Money may be negative, so its bounds use explicit flags instead of -1. Min inclusive, max exclusive.
    public bool hasMinMoney, hasMaxMoney;
    public float minMoney, maxMoney;
    public int priority;
    public bool fallback;
    public DialogueSequenceData dialogue;
    public Sprite image;
}
[CreateAssetMenu(menuName="Blue Zone Bistro/Ending Database")]
public class EndingDatabase : ScriptableObject
{
    public List<EndingDefinition> endings=new List<EndingDefinition>();
}
