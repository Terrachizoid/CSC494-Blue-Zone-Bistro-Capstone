// Real Unity import, selection, save and presentation-data checks. Does not touch player save keys.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class EndingChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static void Run()
    {
        foreach(string path in Directory.GetFiles("Assets/Art/Endings","*.png"))
        {var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();}
        DialogueCsvImporter.ImportDialogues();EndingCsvImporter.Import();
        var db=AssetDatabase.LoadAssetAtPath<EndingDatabase>(EndingCsvImporter.AssetPath);
        Check(db!=null&&db.endings.Count==4,"Four ending definitions missing");
        var s=new GameSessionState();var service=new EndingService(s,db);
        foreach(var item in new[]{new{p=750f,h=70f,id="E_BLUE"},new{p=749f,h=70f,id="E_NICHE"},new{p=750f,h=69f,id="E_POPULAR"},new{p=749f,h=69f,id="E_STRUGGLING"}})
        {s.popularity=item.p;s.communityWellbeing=item.h;Check(service.Select()==item.id,"Ending boundary: "+item.id);}
        foreach(var e in db.endings)Check(e.image!=null&&e.dialogue.lines.Length==3&&e.dialogue.lines.All(l=>l.backgroundImage!=null),"Slides missing: "+e.id);
        s.popularity=900;s.communityWellbeing=90;service.Complete();s.popularity=0;service.Complete();Check(s.endingId=="E_BLUE","Selected ending was rerolled");
        s.gameCompleted=true;var store=new CheckpointStore("unused.ending.test");Check(CheckpointStore.Decode(store.Capture(s),out var restored,out _)&&restored.endingId=="E_BLUE","Ending save round trip failed");
        var fixture=ScriptableObject.CreateInstance<EndingDatabase>();fixture.endings.Add(new EndingDefinition{id="fallback",fallback=true,minPopularity=9999});
        Check(new EndingService(s,fixture).Select()=="fallback","Fallback did not cover gap");
        fixture.endings.Add(new EndingDefinition{id="B",priority=10});fixture.endings.Add(new EndingDefinition{id="A",priority=10});
        Check(new EndingService(s,fixture).Select()=="A","Priority tie unstable");fixture.endings[1].priority=20;Check(new EndingService(s,fixture).Select()=="B","Priority ignored");
        Check(new EndingService(s,null).Select()==null,"Missing database must fail safely");
        string pathCsv="Assets/GameData/CSV/Blue_Zone_Bistro_Endings.csv",original=File.ReadAllText(pathCsv);
        try{File.WriteAllText(pathCsv,original.Replace("\"true\"","\"false\""));EndingCsvImporter.Import();Check(db.endings.Count==4&&db.endings.Count(e=>e.fallback)==1,"Invalid import destroyed valid endings");}
        finally{File.WriteAllText(pathCsv,original);}
        UnityEngine.Object.DestroyImmediate(fixture);
        Debug.Log("PASS: four ending boundaries, deterministic priority, explicit fallback, missing database, twelve slides, save persistence, atomic invalid-import rejection.");
    }
}
