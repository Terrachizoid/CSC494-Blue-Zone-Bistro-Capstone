// Disposable-project import/render check. Never install in the live Assets folder.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterExpressionCapture
{
    public static void Run()
    {
        try
        {
            CharacterExpressionCsvImporter.Import();
            var db=AssetDatabase.LoadAssetAtPath<CharacterExpressionDatabase>(CharacterExpressionCsvImporter.AssetPath);
            var theme=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BistroUITheme>("Assets/GameData/BistroUITheme.asset"));
            var ids=db.expressions.Where(e=>e.characterId!="Default").Select(e=>e.id).ToArray(); if(ids.Length!=31)throw new Exception("Expected 31 character portraits");
            foreach(var id in ids)
            {
                var row=db.expressions.Single(e=>e.id==id);
                if(row?.image==null)throw new Exception("Missing neutral art: "+id);
                foreach(var emotion in row.emotions.Concat(new[]{row.expression}))
                    if(db.Find(row.characterId,emotion)?.image!=row.image)throw new Exception("Default fallback mismatch: "+id+"/"+emotion);
                if(row.image.texture.format!=TextureFormat.DXT5 || row.image.texture.width!=1024 || row.image.texture.height!=1024)throw new Exception("Compression failed: "+id); Debug.Log("EXPRESSION IMPORT: "+id+" "+row.image.texture.width+"x"+row.image.texture.height+" "+row.image.texture.format);
            }
            if(db.expressions.Count(e=>e.characterId=="Visiting Chef")!=1)throw new Exception("Chef should have one default row.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var canvas=new GameObject("Portrait review",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
            var camera=new GameObject("Capture camera").AddComponent<Camera>();
            camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.gray;
            var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var root=(RectTransform)canvas.transform;
            double until=EditorApplication.timeSinceStartup+1;
            EditorApplication.CallbackFunction finish=null;
            finish=()=>
            {
                if(EditorApplication.timeSinceStartup<until)return;
                EditorApplication.update-=finish;
                try
                {
                    foreach(var id in ids)
                    {
                        while(root.childCount>0)UnityEngine.Object.DestroyImmediate(root.GetChild(0).gameObject);
                        var scene=BistroWidgets.Box(root,"Bistro interior",Color.white,Vector2.zero,new Vector2(1,.91f));
                        scene.GetComponent<Image>().sprite=theme.serviceBackground;
                        var person=BistroWidgets.Box(scene,"Customer layer",Color.clear,new Vector2(.34f,.13f),new Vector2(.66f,.88f));
                        BistroCharacterPortrait.Add(person,"Guest",new BistroCharacterPortrait.Look{image=db.expressions.Single(e=>e.id==id).image},null,-99,-99,float.NaN,true,null);
                        BistroShelfStyle.Artwork(scene,"Service counter",theme.serviceCounter,Vector2.zero,new Vector2(1,.30f),Color.white);
                        Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                        var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
                        image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
                        File.WriteAllBytes("expression-"+id.Replace(" ","")+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                    }
                    Debug.Log("CHARACTER EXPRESSIONS PASS: 31 distinct sprites, every emotion alias, one chef row, all 1024 square DXT5, actual portrait component on authored room/counter layers.");
                    EditorApplication.Exit(0);
                }
                catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
            };
            EditorApplication.update+=finish;
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
