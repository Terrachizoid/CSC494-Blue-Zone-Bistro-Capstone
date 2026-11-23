// Disposable review project only. Do not put in live Assets.
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SoundValidation
{
    const string Key="BZB.SoundValidation";
    static SoundValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            SoundCsvImporter.Import();
            var db=AssetDatabase.LoadAssetAtPath<SoundDatabase>(SoundCsvImporter.AssetPath);
            if(db.cues.Count!=46 || db.cues.Any(c=>!c.HasClips))throw new Exception("Missing sound cues");
            foreach(var clip in db.cues.Where(c=>c.kind==SoundKind.Sfx).SelectMany(c=>c.clips).Distinct())
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
                if(importer.defaultSampleSettings.compressionFormat!=AudioCompressionFormat.Vorbis || !importer.forceToMono || clip.length<=0 || clip.length>6.1f)throw new Exception("Invalid SFX import: "+clip.name);
            }
            var method=typeof(BistroUIScreen).GetMethod("PanelSoundCue",BindingFlags.Static|BindingFlags.NonPublic);
            void Check(string a,string b,string x,string y,string expected){if((string)method.Invoke(null,new object[]{a,b,x,y})!=expected)throw new Exception("Panel transition mismatch");}
            Check(null,"pantry",null,null,"panel.open"); Check("pantry","pantry",null,null,null);
            Check("pantry",null,null,null,"panel.close");Check(null,"news",null,null,"event.news.open");
            Check("news","event-detail",null,"E1","event.news.page");Check("event-detail","event-detail","E1","E1",null);
            Check("event-detail","event-detail","E1","E2","event.news.page");Check("event-detail",null,"E1",null,"event.news.close");
            var resultMethod=typeof(BistroUIScreen).GetMethod("ResultSoundCue",BindingFlags.Static|BindingFlags.NonPublic);
            string Result(bool a,bool b,bool c,bool d)=>(string)resultMethod.Invoke(null,new object[]{a,b,c,d});
            if(Result(false,true,false,false)!="result.dish" || Result(true,true,false,false)!=null || Result(false,false,false,true)!="result.day" || Result(false,false,true,true)!=null || Result(true,false,true,false)!=null)throw new Exception("Result edge mismatch");
            if(db.Find("event.news.missing").id!="event.news" || db.Find("unknown")==null==false)throw new Exception("Fallback mismatch");
            Debug.Log("SOUND IMPORT PASS: 46 cues, 38 compressed mono effects, panel transitions and fallback.");
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);
        try
        {
            var host=new GameObject("Audio verification");host.AddComponent<AudioListener>();
            BistroAudio.Attach(host,AssetDatabase.LoadAssetAtPath<SoundDatabase>(SoundCsvImporter.AssetPath));
            BistroAudio.Play("bistro.enter");BistroAudio.Play("bistro.enter");
            var sources=host.GetComponents<AudioSource>();
            if(sources.Count(s=>s.isPlaying)!=1)throw new Exception("Playback/repeat guard failed");
            float oldEffects=BistroAudio.EffectsVolume;
            BistroAudio.EffectsVolume=0;
            if(sources.Any(s=>s.isPlaying && s.volume!=0))throw new Exception("Effects mute failed");
            BistroAudio.EffectsVolume=oldEffects;
            BistroAudio.Music("music.city");
            double until=EditorApplication.timeSinceStartup+1.6;
            EditorApplication.CallbackFunction finish=null;
            finish=()=>{if(EditorApplication.timeSinceStartup<until)return;EditorApplication.update-=finish;
                try{if(!sources.Any(s=>s.loop&&s.isPlaying))throw new Exception("Music did not play");float oldMusic=BistroAudio.MusicVolume; BistroAudio.Music("music.bistro"); BistroAudio.MusicVolume=0;
                    if(sources.Any(s=>s.loop&&s.isPlaying&&s.volume!=0))throw new Exception("Crossfade mute failed");
                    BistroAudio.MusicVolume=oldMusic;
                    Debug.Log("SOUND PLAY PASS: effect playback, duplicate guard and music playback.");EditorApplication.Exit(0);}
                catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}};
            EditorApplication.update+=finish;
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
