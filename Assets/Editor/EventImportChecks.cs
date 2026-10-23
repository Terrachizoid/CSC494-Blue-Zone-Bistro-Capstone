// Run only in an isolated test project: verifies real importer/JsonUtility and deliberately invalid CSV cases.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class EventImportChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        ClaimCsvImporter.ImportClaims();EventCsvImporter.Import();
        var db=AssetDatabase.LoadAssetAtPath<EventDatabase>(EventCsvImporter.AssetPath);
        Check(db!=null&&db.events.Count==4&&db.events.Sum(e=>e.effects.Count)==5,"Missing event content");
        Check(db.events.All(e=>e.image!=null&&e.codex.Length==1&&e.codex[0]!=null),"Event artwork/codex failed to resolve");
        var state=new GameSessionState{currentDay=2,eventPlanDay=2};
        var definition=db.events.First(e=>e.id=="EV_PUBLICITY");
        state.events.Add(new EventRecord{eventId=definition.id,startDay=2,expiresOnDay=4,effects=definition.effects.Select(e=>e.Copy()).ToList()});
        var store=new CheckpointStore("unused.event.test");
        Check(CheckpointStore.Decode(store.Capture(state),out var restored,out var error),error);
        Check(restored.eventPlanDay==2&&restored.events[0].expiresOnDay==4&&new MarketEffects(restored).Publicity==500,"Event checkpoint lost modifiers");
        new EventService(restored,db,ScriptableObject.CreateInstance<BalanceConfig>(),null,()=>throw new Exception("Resume rerolled")).PrepareMorning();
        restored.events[0].effects[0].value=float.NaN;Check(!CheckpointStore.Decode(store.Capture(restored),out _,out _),"Invalid saved modifier accepted");
        const string path="Assets/GameData/CSV/Blue_Zone_Bistro_EventEffects.csv";string original=File.ReadAllText(path);
        try
        {
            foreach(string invalid in new[]{original.Replace("\"Multiply\"","\"Unknown\""),original.Replace("\"P001\"","\"missing_food\""),original.Replace("\"0.8\"","\"NaN\"")})
            {File.WriteAllText(path,invalid);EventCsvImporter.Import();Check(db.events.Count==4&&db.events.Sum(e=>e.effects.Count)==5&&db.events[0].effects[0].value==.8f,"Bad event import replaced valid content");}
        }
        finally{File.WriteAllText(path,original);}
        Debug.Log("PASS: event import, optional silent audio, image/codex references, multi-effect snapshot serialization, no resume reroll, corrupt-save rejection and atomic invalid-content rejection.");
    }
}
