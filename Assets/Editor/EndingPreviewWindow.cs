// Editor-only score sandbox. Uses the real selector with a detached state; never saves or changes a live run.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class EndingPreviewWindow : EditorWindow
{
    [SerializeField] private EndingDatabase database;
    [SerializeField] private float popularity=800, wellbeing=80;
    private EndingDefinition selected;
    private DialogueLineData[] lines=Array.Empty<DialogueLineData>();
    private int lineIndex;
    private Vector2 scroll;

    [MenuItem("Tools/Blue Zone Bistro/Preview Endings")]
    public static void Open()
    {
        var window=GetWindow<EndingPreviewWindow>("Ending Preview");
        window.minSize=new Vector2(460,540);
        window.Show();
    }
    private void OnEnable()
    {
        if(database==null)database=AssetDatabase.LoadAssetAtPath<EndingDatabase>(EndingCsvImporter.AssetPath);
        Evaluate();
    }
    private void Evaluate()
    {
        var sandbox=new GameSessionState{popularity=popularity,communityWellbeing=wellbeing};
        string id=new EndingService(sandbox,database).Select();
        selected=database?.endings?.FirstOrDefault(e=>e!=null&&e.id==id);
        lines=(selected?.dialogue?.lines??Array.Empty<DialogueLineData>()).Where(l=>l!=null&&!string.IsNullOrWhiteSpace(l.textTemplate)).OrderBy(l=>l.lineOrder).ToArray();
        lineIndex=0;
        Repaint();
    }
    private void Preset(string label,float p,float h)
    {if(GUILayout.Button(label)){popularity=p;wellbeing=h;Evaluate();}}
    private void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("No Play Mode required. This previews the real ending selection and slide content in an Editor panel. Your current game and checkpoint are untouched.",MessageType.Info);
        EditorGUI.BeginChangeCheck();
        database=(EndingDatabase)EditorGUILayout.ObjectField("Ending database",database,typeof(EndingDatabase),false);
        popularity=EditorGUILayout.FloatField("Popularity",popularity);
        wellbeing=EditorGUILayout.FloatField("Community wellbeing",wellbeing);
        if(EditorGUI.EndChangeCheck())Evaluate();
        EditorGUILayout.LabelField("Quick scores (based on initial thresholds)",EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();Preset("Healthy / popular",800,80);Preset("Healthy / niche",300,80);EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();Preset("Unhealthy / popular",800,40);Preset("Struggling",300,40);EditorGUILayout.EndHorizontal();
        if(GUILayout.Button("Re-evaluate imported content / restart story"))Evaluate();
        if(database==null)EditorGUILayout.HelpBox("Import All CSV Content, then reopen this window or assign an EndingDatabase.",MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(selected?.title??"Run completed — no matching content",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Selected ID",selected?.id??"None");
        if(selected!=null)EditorGUILayout.LabelField("Priority / fallback",selected.priority+" / "+selected.fallback);
        Sprite art=selected?.image;
        foreach(var line in lines.Take(lineIndex+1))if(line.backgroundImage!=null)art=line.backgroundImage;
        var area=GUILayoutUtility.GetRect(200,Mathf.Max(120,(position.width-35)*.45f),GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area,new Color(.20f,.27f,.26f));
        if(art!=null)
        {
            Rect source=art.rect;
            float scale=Mathf.Min(area.width/source.width,area.height/source.height);
            var target=new Rect(area.center.x-source.width*scale/2,area.center.y-source.height*scale/2,source.width*scale,source.height*scale);
            GUI.DrawTextureWithTexCoords(target,art.texture,new Rect(source.x/art.texture.width,source.y/art.texture.height,source.width/art.texture.width,source.height/art.texture.height));
        }
        else GUI.Label(area,"Artwork placeholder",EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField(lines.Length==0?"No dialogue lines available.":lines[lineIndex].textTemplate,EditorStyles.wordWrappedLabel,GUILayout.MinHeight(65));
        EditorGUILayout.LabelField(lines.Length==0?"":$"Line {lineIndex+1} of {lines.Length}");
        EditorGUILayout.BeginHorizontal();
        using(new EditorGUI.DisabledScope(lineIndex==0))if(GUILayout.Button("Previous"))lineIndex--;
        using(new EditorGUI.DisabledScope(lineIndex+1>=lines.Length))if(GUILayout.Button("Continue"))lineIndex++;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }
}
