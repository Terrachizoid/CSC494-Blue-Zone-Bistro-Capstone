// Imports events and linked effects atomically. Unsupported operations/targets never silently succeed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class EventCsvImporter
{
    public const string AssetPath="Assets/GameData/EventDatabase.asset";
    private const string Root="Assets/GameData/CSV/Blue_Zone_Bistro_";
    [MenuItem("Tools/Blue Zone Bistro/Import Events CSV")]
    public static void Import()
    {
        if(!CsvImportUtility.TryRead(Root+"Events.csv","Events",new[]{"event_id","headline","summary","details","min_day","max_day","duration_days","weight","image_path","notification_audio_path","detail_audio_path","codex_ids"},out var rows,out var cols)||
            !CsvImportUtility.TryRead(Root+"EventEffects.csv","Event effects",new[]{"event_id","effect_type","target_type","target_id","operation","value"},out var effectRows,out var effectCols))return;
        var old=AssetDatabase.LoadAssetAtPath<EventDatabase>(AssetPath);var data=new List<EventDefinition>();
        try
        {
            foreach(var row in rows.Skip(1).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v))))
            {
                Func<string,string> g=k=>CsvImportUtility.Get(row,cols,k).Trim();
                var e=new EventDefinition{id=g("event_id"),headline=g("headline"),summary=g("summary"),details=g("details"),minDay=Int(g("min_day")),maxDay=Int(g("max_day")),durationDays=Int(g("duration_days")),weight=Float(g("weight"))};
                if(string.IsNullOrWhiteSpace(e.id)||data.Any(d=>string.Equals(d.id,e.id,StringComparison.OrdinalIgnoreCase))||string.IsNullOrWhiteSpace(e.headline)||e.minDay<2||(e.maxDay!=-1&&e.maxDay<e.minDay)||e.durationDays<1||e.durationDays>365||e.weight<=0)throw new Exception("Invalid event fields: "+e.id);
                var previous=old?.events.Find(d=>d.id==e.id);
                e.image=CsvImportUtility.LoadOptionalAsset(g("image_path"),previous?.image,e.id,"image_path");
                e.thumbnail=CsvImportUtility.LoadOptionalAsset(g("thumbnail_path"),previous?.thumbnail,e.id,"thumbnail_path");
                e.paperColor=string.IsNullOrWhiteSpace(g("paper_color"))?previous?.paperColor??"#E9E2D0":g("paper_color");
                e.accentColor=string.IsNullOrWhiteSpace(g("accent_color"))?previous?.accentColor??"#A6B7A0":g("accent_color");
                e.notificationAudio=CsvImportUtility.LoadOptionalAsset(g("notification_audio_path"),previous?.notificationAudio,e.id,"notification_audio_path");
                e.detailAudio=CsvImportUtility.LoadOptionalAsset(g("detail_audio_path"),previous?.detailAudio,e.id,"detail_audio_path");
                e.codex=g("codex_ids").Split('|').Where(id=>!string.IsNullOrWhiteSpace(id)).Select(id=>AssetDatabase.LoadAssetAtPath<ClaimData>("Assets/GameData/Claims/"+id.Trim()+".asset")??throw new Exception("Missing event codex: "+id)).ToArray();
                data.Add(e);
            }
            foreach(var row in effectRows.Skip(1).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v))))
            {
                Func<string,string> g=k=>CsvImportUtility.Get(row,effectCols,k).Trim();
                var parent=data.Find(e=>e.id==g("event_id"))??throw new Exception("Unknown effect event ID.");
                var effect=new EventEffect{type=EnumValue<EventEffectType>(g("effect_type")),target=EnumValue<EventTarget>(g("target_type")),targetId=g("target_id"),operation=EnumValue<EventOperation>(g("operation")),value=Float(g("value"))};
                if(!MarketEffects.ValidEffect(effect))throw new Exception("Unsupported effect on "+parent.id);
                if(effect.target==EventTarget.Store&&AssetDatabase.LoadAssetAtPath<StoreData>("Assets/GameData/Stores/"+effect.targetId+".asset")==null)throw new Exception("Unknown store: "+effect.targetId);
                if(effect.target==EventTarget.Food&&AssetDatabase.LoadAssetAtPath<FoodData>("Assets/GameData/Foods/"+effect.targetId+".asset")==null)throw new Exception("Unknown food: "+effect.targetId);
                parent.effects.Add(effect);
            }
            if(data.Count==0||data.Any(e=>e.effects.Count==0))throw new Exception("Each event needs at least one effect.");
        }
        catch(Exception ex){Debug.LogError("Event import cancelled; previous content retained. "+ex.Message);return;}
        if(old==null){old=ScriptableObject.CreateInstance<EventDatabase>();AssetDatabase.CreateAsset(old,AssetPath);}
        old.events=data;EditorUtility.SetDirty(old);AssetDatabase.SaveAssets();
    }
    static int Int(string v)=>int.Parse(v,CultureInfo.InvariantCulture);
    static float Float(string v){float n=float.Parse(v,CultureInfo.InvariantCulture);if(!CommunityProgress.Finite(n))throw new Exception("Non-finite event value.");return n;}
    static T EnumValue<T>(string v) where T:struct {if(!Enum.TryParse<T>(v,false,out var value)||!Enum.IsDefined(typeof(T),value)||char.IsDigit(v.FirstOrDefault()))throw new Exception("Unknown event vocabulary: "+v);return value;}
}
