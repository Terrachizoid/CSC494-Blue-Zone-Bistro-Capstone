// Transactional tutorial CSV import: validate all rows before replacing the shared content asset.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
public static class TutorialCsvImporter
{
    public const string AssetPath="Assets/GameData/TutorialDatabase.asset";
    const string CsvPath="Assets/GameData/CSV/Blue_Zone_Bistro_Tutorials.csv";
    [MenuItem("Tools/Blue Zone Bistro/Import Tutorial CSV")]
    public static void Import()=>EnsureCurrent(true);
    public static bool EnsureCurrent(bool force=false)
    {
        if(!File.Exists(CsvPath)){Debug.LogWarning("Tutorial CSV missing; tutorials will not be enabled.");return false;}
        string fingerprint;
        using(var sha=SHA256.Create())fingerprint=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(CsvPath)));
        var database=AssetDatabase.LoadAssetAtPath<TutorialDatabase>(AssetPath);
        if(!force&&database!=null&&database.fingerprint==fingerprint)return true;
        var headers=new[]{"tutorial_id","trigger_flag","behaviour_id","activation_phase","priority","step_id","text","completion_flags"};
        if(!CsvImportUtility.TryRead(CsvPath,"Tutorials",headers,out var rows,out var columns))return false;
        var definitions=new List<TutorialDefinition>();
        foreach(var row in rows.Skip(1))
        {
            string Get(string key)=>CsvImportUtility.Get(row,columns,key).Trim();
            string id=Get("tutorial_id"),behaviour=Get("behaviour_id"),phase=Get("activation_phase"),step=Get("step_id");
            if(string.IsNullOrEmpty(id)&&row.All(string.IsNullOrWhiteSpace))continue;
            if(string.IsNullOrEmpty(id)||string.IsNullOrEmpty(Get("trigger_flag"))||string.IsNullOrEmpty(step)||string.IsNullOrWhiteSpace(Get("text"))||
                !new[]{"Shopping","FirstMeal","TextOnly"}.Contains(behaviour)||!Enum.TryParse<GamePhase>(phase,out var parsed)||!Enum.IsDefined(typeof(GamePhase),parsed)||parsed.ToString()!=phase||
                !int.TryParse(Get("priority"),out var priority))
            {Debug.LogError("Invalid tutorial row: "+id);return false;}
            var definition=definitions.Find(d=>d.id==id);
            string[] flags=Get("completion_flags").Split(new[]{'|'},StringSplitOptions.RemoveEmptyEntries);
            if(definition==null)
            {
                definition=new TutorialDefinition{id=id,triggerFlag=Get("trigger_flag"),behaviourId=behaviour,activationPhase=phase,priority=priority,completionFlags=flags};
                definitions.Add(definition);
            }
            if(definition.behaviourId!=behaviour||definition.triggerFlag!=Get("trigger_flag")||definition.activationPhase!=phase||definition.priority!=priority||
                !definition.completionFlags.SequenceEqual(flags)||definition.steps.Any(s=>s.stepId==step))
            {Debug.LogError("Conflicting or duplicate tutorial step: "+id+"/"+step);return false;}
            definition.steps.Add(new TutorialStepText{stepId=step,text=Get("text")});
        }
        if(definitions.Count==0){Debug.LogError("Tutorial CSV has no definitions.");return false;}
        foreach(var definition in definitions)
        {
            var expected=definition.behaviourId=="Shopping"?new[]{"shop"}:definition.behaviourId=="FirstMeal"?new[]{"choose","build","feedback"}:new[]{"explain"};
            if(expected.Any(step=>!definition.steps.Any(s=>s.stepId==step)))
            {Debug.LogError("Missing tutorial step: "+definition.id);return false;}
        }
        if(database==null){database=ScriptableObject.CreateInstance<TutorialDatabase>();AssetDatabase.CreateAsset(database,AssetPath);}
        database.tutorials=definitions;database.fingerprint=fingerprint;EditorUtility.SetDirty(database);AssetDatabase.SaveAssets();return true;
    }
}
