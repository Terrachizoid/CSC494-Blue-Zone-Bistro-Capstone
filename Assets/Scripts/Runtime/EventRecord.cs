// Save-safe event instance. Snapshot effects so a content reimport cannot change an already saved day's prices.
using System;
using System.Collections.Generic;
[Serializable] public class EventRecord
{
    public string eventId;
    public int startDay, expiresOnDay;
    public List<EventEffect> effects=new List<EventEffect>();
    public bool Active(int day)=>startDay<=day&&day<expiresOnDay;
}
