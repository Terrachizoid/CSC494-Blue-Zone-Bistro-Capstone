// Validates every row before replacing the database. Art is optional; blank paths preserve manual art.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class EndingCsvImporter
{
    public const string AssetPath="Assets/GameData/EndingDatabase.asset";
    [MenuItem("Tools/Blue Zone Bistro/Import Endings CSV")]
    public static void Import()
    {
        if(!CsvImportUtility.TryRead("Assets/GameData/CSV/Blue_Zone_Bistro_Endings.csv","Endings",
            new[]{"ending_id","title","min_popularity","max_popularity","min_wellbeing","max_wellbeing","priority","fallback","dialogue_id","image_path"},out var rows,out var columns))return;
        var old=AssetDatabase.LoadAssetAtPath<EndingDatabase>(AssetPath);
        var result=new List<EndingDefinition>();var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach(var row in rows.Skip(1).Where(r=>r.Any(v=>!string.IsNullOrWhiteSpace(v))))
            {
                Func<string,string> get=k=>CsvImportUtility.Get(row,columns,k).Trim();
                var e=new EndingDefinition{id=get("ending_id"),title=get("title"),priority=int.Parse(get("priority"),CultureInfo.InvariantCulture),fallback=bool.Parse(get("fallback")),
                    minPopularity=Bound(get("min_popularity")),maxPopularity=Bound(get("max_popularity")),minWellbeing=Bound(get("min_wellbeing")),maxWellbeing=Bound(get("max_wellbeing"))};
                // Optional money bounds (may be negative), e.g. a bankrupt ending with max_money -10.
                if(columns.ContainsKey("min_money")&&get("min_money").Length>0){e.hasMinMoney=true;e.minMoney=Money(get("min_money"));}
                if(columns.ContainsKey("max_money")&&get("max_money").Length>0){e.hasMaxMoney=true;e.maxMoney=Money(get("max_money"));}
                if(e.hasMinMoney&&e.hasMaxMoney&&e.maxMoney<=e.minMoney)throw new Exception("Invalid money bounds: "+e.id);
                if(string.IsNullOrWhiteSpace(e.id)||!ids.Add(e.id)||string.IsNullOrWhiteSpace(e.title)||
                    (e.maxPopularity>=0&&e.maxPopularity<=e.minPopularity)||(e.maxWellbeing>=0&&e.maxWellbeing<=e.minWellbeing))throw new Exception("Invalid ID, title or bounds: "+e.id);
                e.dialogue=AssetDatabase.LoadAssetAtPath<DialogueSequenceData>("Assets/GameData/Dialogues/"+get("dialogue_id")+".asset");
                if(e.dialogue==null||e.dialogue.trigger!=DialogueTrigger.Ending||e.dialogue.lines==null||e.dialogue.lines.Length==0)throw new Exception("Missing ending dialogue: "+e.id);
                e.image=CsvImportUtility.LoadOptionalAsset(get("image_path"),old?.endings.Find(x=>x.id==e.id)?.image,e.id,"image_path");
                result.Add(e);
            }
            if(result.Count(e=>e.fallback)!=1)throw new Exception("Exactly one fallback ending is required.");
        }
        catch(Exception error){Debug.LogError("Ending import cancelled; existing content retained. "+error.Message);return;}
        if(old==null){old=ScriptableObject.CreateInstance<EndingDatabase>();AssetDatabase.CreateAsset(old,AssetPath);}
        old.endings=result;EditorUtility.SetDirty(old);AssetDatabase.SaveAssets();
    }
    static float Money(string text)
    {
        float v=float.Parse(text,CultureInfo.InvariantCulture);
        if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Money bounds must be finite numbers.");return v;
    }
    static float Bound(string text)
    {
        if(text.Length==0)return -1;
        float v=float.Parse(text,CultureInfo.InvariantCulture);
        if(float.IsNaN(v)||float.IsInfinity(v)||v<0)throw new Exception("Bounds must be blank or finite nonnegative numbers.");return v;
    }
}
