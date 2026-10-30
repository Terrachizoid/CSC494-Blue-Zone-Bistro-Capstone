using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private string dialoguePageKey;
    private int dialoguePage;
    private static Sprite dialogueFade;
    private class DialogueRequest{public RectTransform slot;public string speaker,text,actionLabel;public Action action,cancel;public bool block;public Action<RectTransform> decorate;}
    private readonly List<DialogueRequest> dialogueRequests=new List<DialogueRequest>();
    // Only one dialogue box is ever on screen. Calls made while a screen is built are queued and
    // FlushDialogue (end of Refresh) shows the one requested last, which is also the top layer:
    // a panel the player opened > the Blue Zone Guide > guest lines, dish results and day summaries.
    // The others are not lost: they are requested again on the next refresh, after the shown one is closed.
    // Callers keep ownership of all actions; decorate adds extra buttons to the frame if it is shown.
    private void DialoguePanel(string speaker,string text,string actionLabel,Action action,Action cancel=null,bool block=true,Action<RectTransform> decorate=null)
    {
        // An inactive placeholder keeps the request's layer order among other screen elements.
        var slot=BistroWidgets.Box(screen,"Dialogue presentation",Color.clear,Vector2.zero,new Vector2(1,.91f));
        slot.gameObject.SetActive(false);
        dialogueRequests.Add(new DialogueRequest{slot=slot,speaker=speaker,text=text,actionLabel=actionLabel,action=action,cancel=cancel,block=block,decorate=decorate});
    }
    private void FlushDialogue()
    {
        if(dialogueRequests.Count==0)return;
        var shown=dialogueRequests[dialogueRequests.Count-1];
        foreach(var request in dialogueRequests)if(request!=shown&&request.slot!=null)Destroy(request.slot.gameObject);
        dialogueRequests.Clear();
        if(shown.slot==null)return;
        shown.slot.gameObject.SetActive(true);
        var frame=BuildDialogue(shown.slot,shown.speaker,shown.text,shown.actionLabel,shown.action,shown.cancel,shown.block);
        shown.decorate?.Invoke(frame);
    }
    private RectTransform BuildDialogue(RectTransform root,string speaker,string text,string actionLabel,Action action,Action cancel,bool block)
    {
        string key=speaker+"/"+text;
        if(dialoguePageKey!=key){dialoguePageKey=key;dialoguePage=0;}
        var pages=new List<string>();string page="";
        foreach(var line in (text??"").Split('\n')){
            foreach(var word in line.Split(' ')){
                if(page.Length+word.Length>330){pages.Add(page.Trim());page="";}
                page+=word+" ";
            }
            page+="\n";
            if(page.Split('\n').Length>5){pages.Add(page.Trim());page="";}
        }
        if(!string.IsNullOrWhiteSpace(page)||pages.Count==0)pages.Add(page.Trim());
        dialoguePage=Mathf.Clamp(dialoguePage,0,pages.Count-1);
        root.GetComponent<Image>().raycastTarget=block;
        if(dialogueFade==null){
            var t=new Texture2D(1,64,TextureFormat.RGBA32,false);t.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++)t.SetPixel(0,y,new Color(.025f,.04f,.035f,Mathf.Lerp(.98f,0,Mathf.SmoothStep(0,1,Mathf.Clamp01((y/63f-.6f)/.4f)))));
            t.Apply();dialogueFade=Sprite.Create(t,new Rect(0,0,1,64),new Vector2(.5f,.5f));
        }
        var frame=BistroWidgets.Box(root,"Bottom dialogue",Color.white,Vector2.zero,new Vector2(1,.35f));frame.GetComponent<Image>().sprite=dialogueFade;
        // Speaker and text stay below .64 of the frame, where the fade is fully dark.
        BistroShelfStyle.Text(frame,speaker,theme,new Vector2(.045f,.48f),new Vector2(.23f,.64f),23,new Color(.80f,.85f,.68f),true);
        var body=BistroShelfStyle.Text(frame,pages[dialoguePage],theme,new Vector2(.25f,.21f),new Vector2(.94f,.64f),22,Color.white);
        body.alignment=TextAnchor.UpperLeft;
        bool last=dialoguePage==pages.Count-1;
        BistroActionButton.Create(frame,last?actionLabel:"Next  ›",theme,new Vector2(.78f,.045f),new Vector2(.96f,.19f),BistroActionRole.Primary,()=>Change(()=>{if(!last)dialoguePage++;else{dialoguePageKey=null;action?.Invoke();}}));
        if(dialoguePage>0)BistroActionButton.Create(frame,"‹  Previous",theme,new Vector2(.25f,.045f),new Vector2(.40f,.19f),BistroActionRole.Secondary,()=>Change(()=>dialoguePage--));
        if(cancel!=null)BistroActionButton.Create(frame,"Back",theme,new Vector2(.59f,.045f),new Vector2(.74f,.19f),BistroActionRole.Secondary,()=>Change(()=>{dialoguePageKey=null;cancel();}));
        if(pages.Count>1)BistroShelfStyle.Text(frame,$"{dialoguePage+1} / {pages.Count}",theme,new Vector2(.045f,.08f),new Vector2(.20f,.20f),14,Color.white);
        return frame;
    }
    private void ServiceButton(Transform parent,string text,Vector2 min,Vector2 max,BistroActionRole role,Action action)=>BistroActionButton.Create(parent,text,theme,min,max,role,action);
}
