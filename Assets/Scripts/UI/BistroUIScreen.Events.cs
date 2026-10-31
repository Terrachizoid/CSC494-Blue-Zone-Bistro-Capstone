// Nonblocking timed news notices plus a persistent news archive. Presentation never activates or expires events.
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public partial class BistroUIScreen
{
    private readonly HashSet<string> announcedEvents=new HashSet<string>();
    private RectTransform eventToast;
    private EventRecord toastRecord, detailRecord;
    private float toastElapsed;
    private AudioSource eventAudio;
    private ClaimData eventCodex;
    private bool choosingEventCodex;
    private void PlayEventAudio(AudioClip clip)
    {if(eventAudio==null)eventAudio=gameObject.AddComponent<AudioSource>();eventAudio.playOnAwake=false;eventAudio.Stop();if(clip!=null){eventAudio.clip=clip;eventAudio.Play();}}
    private void OpenEvent(EventRecord record)
    {detailRecord=record;eventCodex=null;choosingEventCodex=false;modal="event-detail";PlayEventAudio(game.Queries.GetEvent(record.eventId)?.detailAudio);dirty=true;}
    private string EffectText(EventEffect e)
    {
        string target=e.target==EventTarget.Food?game.Queries.Foods.GetById(e.targetId)?.displayName:
            e.target==EventTarget.Store?game.Queries.GetStores().Find(s=>s.id==e.targetId)?.displayName:"Bistro";
        target=target??e.targetId;
        if(e.type==EventEffectType.Availability)return target+": temporarily closed";
        if(e.type==EventEffectType.AttendancePopularity)return "More people are curious about the bistro: extra guests may visit";
        return target+(e.operation==EventOperation.Add?$": package price {e.value:+$0.00;-$0.00;$0.00}":$": package prices {(e.value-1)*100:+0.#;-0.#;0}%");
    }
    private void TickEventToast()
    {
        bool visible=!game.IsMainMenu&&game.Loop.Phase==GamePhase.Market&&store==null&&modal==null&&game.Loop.Tutorials?.IsText!=true;
        if(eventToast!=null)eventToast.gameObject.SetActive(visible);
        if(!visible)return;
        if(toastRecord==null)
        {
            toastRecord=game.Queries.GetEventHistory().FirstOrDefault(r=>r.startDay==game.State.currentDay&&!announcedEvents.Contains(r.eventId+":"+r.startDay));
            if(toastRecord==null)return;
            announcedEvents.Add(toastRecord.eventId+":"+toastRecord.startDay);toastElapsed=0;
            var definition=game.Queries.GetEvent(toastRecord.eventId);
            eventToast=BistroShelfStyle.Panel(canvas.transform,"Event notification",BistroShelfStyle.Cream,new Vector2(.5f,.5f),new Vector2(.5f,.5f)); eventToast.pivot=new Vector2(.5f,0);eventToast.sizeDelta=new Vector2(350,130);
            var tail=BistroWidgets.Box(eventToast,"Speech bubble tail",BistroShelfStyle.Cream,new Vector2(.48f,0),new Vector2(.52f,0));tail.sizeDelta=new Vector2(14,14);tail.localRotation=Quaternion.Euler(0,0,45);tail.GetComponent<Image>().raycastTarget=false;
            eventToast.GetComponent<Image>().color=NewsColor(definition?.paperColor,BistroShelfStyle.Cream);
            tail.GetComponent<Image>().color=eventToast.GetComponent<Image>().color;
            var image=NewsArt(eventToast,definition?.thumbnail??definition?.image,new Vector2(.02f,.08f),new Vector2(.29f,.92f));
            BistroShelfStyle.Text(eventToast,definition?.headline??toastRecord.eventId,theme,new Vector2(.32f,.17f),new Vector2(.95f,.85f),21,BistroShelfStyle.Ink,true);
            NewsRule(eventToast,new Vector2(.32f,.10f),new Vector2(.95f,.12f),NewsColor(definition?.accentColor,BistroShelfStyle.Green));
            NewsPaperFinish(eventToast);
            var button=eventToast.gameObject.AddComponent<Button>();var record=toastRecord;button.onClick.AddListener(()=>OpenEvent(record));
            image.GetComponent<Image>().raycastTarget=false;PlayEventAudio(definition?.notificationAudio);
        }
        toastElapsed+=Time.unscaledDeltaTime;
        if(eventToast!=null){eventToast.SetAsLastSibling();PositionTowerToast();}
        if(toastElapsed>=6){Destroy(eventToast.gameObject);eventToast=null;toastRecord=null;}
    }
    private void PositionTowerToast()
    {
        var world=screen.GetComponentsInChildren<BistroWorldLayout>().FirstOrDefault();
        var area=(RectTransform)canvas.transform;
        Vector2 point=new Vector2(0,area.rect.height*.20f);
        if(world!=null){var rect=(RectTransform)world.transform;var region=BistroWorldArt.Get("city")?.regions?.FirstOrDefault(r=>r.id=="NEWS");
            if(region!=null){var local=new Vector3((region.marker.x-.5f)*rect.rect.width,(.5f-region.marker.y)*rect.rect.height+28,0);point=area.InverseTransformPoint(rect.TransformPoint(local));}}
        point.x=Mathf.Clamp(point.x,area.rect.xMin+180,area.rect.xMax-180);
        point.y=Mathf.Clamp(point.y,area.rect.yMin+20,area.rect.yMax-area.rect.height*.09f-145);
        eventToast.anchoredPosition=point;
        eventToast.localScale=Vector3.one*(theme.reducedMotion?1:Mathf.Lerp(.9f,1,Mathf.Clamp01(toastElapsed/.25f)));
    }
    private static Texture2D newsPaperGrain;
    private void NewsPaperFinish(RectTransform parent)
    {
        if(newsPaperGrain==null){
            newsPaperGrain=new Texture2D(128,128,TextureFormat.RGBA32,false){name="News paper grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            var random=new System.Random(716);var pixels=new Color[128*128];
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(.20f,.16f,.10f,(float)random.NextDouble()*.055f);
            newsPaperGrain.SetPixels(pixels);newsPaperGrain.Apply(false,true);
        }
        var layer=new GameObject("Paper print finish",typeof(RectTransform),typeof(RawImage));layer.transform.SetParent(parent,false);
        var rect=(RectTransform)layer.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        var image=layer.GetComponent<RawImage>();image.texture=newsPaperGrain;image.uvRect=new Rect(0,0,6,4);image.raycastTarget=false;
    }
    private static Color NewsColor(string hex,Color fallback) => ColorUtility.TryParseHtmlString(hex??"",out var color)?color:fallback;
    private RectTransform NewsArt(Transform parent,Sprite sprite,Vector2 min,Vector2 max)
    {
        var art=BistroWidgets.Box(parent,"Event illustration",Color.clear,min,max);
        var image=art.GetComponent<Image>();image.raycastTarget=false;
        if(sprite!=null){image.sprite=sprite;image.color=Color.white;image.preserveAspect=true;}
        return art;
    }
    private void NewsRule(Transform parent,Vector2 min,Vector2 max,Color color)
    {BistroWidgets.Box(parent,"Printed rule",color,min,max).GetComponent<Image>().raycastTarget=false;}
    private void NewsReading(RectTransform parent,string text,Vector2 min,Vector2 max)
    {
        BistroWidgets.ReadingPanel(parent,text,theme,min,max);
        var reading=parent.GetChild(parent.childCount-1);
        reading.GetComponent<Image>().color=Color.clear;
        var body=reading.GetComponentInChildren<Text>();body.color=BistroShelfStyle.Ink;body.fontSize=20;
        Canvas.ForceUpdateCanvases();
        var scroll=reading.GetComponent<ScrollRect>();scroll.verticalNormalizedPosition=1;
        scroll.content.anchoredPosition=Vector2.zero;
    }
    private void CloseNews(){modal=null;eventCodex=null;choosingEventCodex=false;if(eventAudio!=null)eventAudio.Stop();}
    private void EventPanel()
    {
        var definition=game.Queries.GetEvent(detailRecord?.eventId);
        var shade=BistroWidgets.Box(screen,"News dimmer",new Color(0,0,0,.65f),Vector2.zero,Vector2.one);
        var paper=NewsColor(definition?.paperColor,BistroShelfStyle.Cream);
        var accent=NewsColor(definition?.accentColor,BistroShelfStyle.Green);
        var panel=BistroShelfStyle.Panel(shade,"Newspaper",paper,new Vector2(.13f,.055f),new Vector2(.87f,.965f));
        BistroShelfStyle.Text(panel,"THE BLUE ZONE POST",theme,new Vector2(.04f,.91f),new Vector2(.70f,.975f),26,BistroShelfStyle.Ink,true);
        NewsRule(panel,new Vector2(.04f,.90f),new Vector2(.96f,.903f),BistroShelfStyle.Ink);
        BistroActionButton.Create(panel,"Close",theme,new Vector2(.82f,.925f),new Vector2(.96f,.98f),BistroActionRole.Secondary,()=>Change(CloseNews));
        if(modal=="news")
        {
            var list=BistroWidgets.Grid(panel,theme,new Vector2(.04f,.06f),new Vector2(.96f,.87f));
            foreach(var record in game.Queries.GetEventHistory().AsEnumerable().Reverse())
            {
                var news=game.Queries.GetEvent(record.eventId);
                var row=BistroShelfStyle.Panel(list,"News entry",NewsColor(news?.paperColor,BistroShelfStyle.Cream),Vector2.zero,Vector2.one);
                NewsArt(row,news?.thumbnail??news?.image,new Vector2(.02f,.15f),new Vector2(.25f,.85f));
                BistroShelfStyle.Text(row,news?.headline??record.eventId,theme,new Vector2(.29f,.40f),new Vector2(.95f,.90f),22,BistroShelfStyle.Ink,true);
                BistroWidgets.Label(row,$"Days {record.startDay}–{record.expiresOnDay-1} · {(record.Active(game.State.currentDay)?"Active":"Expired")}",theme,new Vector2(.29f,.08f),new Vector2(.95f,.35f),16,BistroShelfStyle.Ink);
                row.gameObject.AddComponent<Button>().onClick.AddListener(()=>OpenEvent(record));
            }
            if(game.Queries.GetEventHistory().Count==0)BistroWidgets.Label(panel,"No news yet.",theme,new Vector2(.1f,.3f),new Vector2(.9f,.7f));
            return;
        }
        BistroActionButton.Create(panel,"All news",theme,new Vector2(.04f,.025f),new Vector2(.23f,.085f),BistroActionRole.Secondary,()=>Change(()=>{modal="news";eventCodex=null;choosingEventCodex=false;}));
        if(eventCodex!=null)
        {
            var entry=eventCodex;
            NewsReading(panel,(entry.shortText??"Codex")+"\n\n"+(entry.codexText??"Evidence content has not been supplied yet.")+"\n\n"+entry.sourceTitle+"\n"+entry.sourceCitation+(!entry.sourceChecked?"\nPrototype / source not verified.":""),new Vector2(.06f,.14f),new Vector2(.94f,.86f));
            BistroActionButton.Create(panel,"Back to article",theme,new Vector2(.28f,.025f),new Vector2(.55f,.085f),BistroActionRole.Secondary,()=>Change(()=>eventCodex=null));
            if(Uri.TryCreate(entry.sourceUrl,UriKind.Absolute,out var url)&&(url.Scheme=="https"||url.Scheme=="http"))BistroActionButton.Create(panel,"Open source",theme,new Vector2(.65f,.025f),new Vector2(.96f,.085f),BistroActionRole.Primary,()=>Application.OpenURL(url.AbsoluteUri));
            return;
        }
        if(choosingEventCodex)
        {
            var list=BistroWidgets.Grid(panel,theme,new Vector2(.05f,.17f),new Vector2(.95f,.85f));
            foreach(var claim in (definition?.codex??Array.Empty<ClaimData>()).Where(c=>c!=null))BistroActionButton.Create(list,claim.shortText,theme,Vector2.zero,Vector2.one,BistroActionRole.Secondary,()=>Change(()=>{eventCodex=claim;choosingEventCodex=false;}));
            BistroActionButton.Create(panel,"Back to article",theme,new Vector2(.28f,.025f),new Vector2(.55f,.085f),BistroActionRole.Secondary,()=>Change(()=>choosingEventCodex=false));return;
        }
        BistroShelfStyle.Text(panel,definition?.headline??"Neighbourhood news",theme,new Vector2(.045f,.73f),new Vector2(.955f,.88f),40,BistroShelfStyle.Ink,true);
        var field=BistroWidgets.Box(panel,"Printed colour field",accent,new Vector2(.04f,.14f),new Vector2(.48f,.70f));field.GetComponent<Image>().raycastTarget=false;
        NewsArt(panel,definition?.image,new Vector2(.055f,.17f),new Vector2(.465f,.68f));
        BistroShelfStyle.Text(panel,definition?.summary??"",theme,new Vector2(.53f,.57f),new Vector2(.95f,.70f),24,BistroShelfStyle.Ink,true);
        string duration=$"Days {detailRecord?.startDay}–{detailRecord?.expiresOnDay-1} · "+(detailRecord?.Active(game.State.currentDay)==true?"Active":"Expired");
        string effects=detailRecord==null?"":string.Join("\n",detailRecord.effects.Select(EffectText));
        NewsReading(panel,(definition?.details??"Details are unavailable.")+"\n\n"+duration+"\n\n"+effects,new Vector2(.53f,.14f),new Vector2(.95f,.55f));
        foreach(var reading in panel.GetComponentsInChildren<Image>())if(reading.name=="Reading panel")reading.color=Color.clear;
        NewsPaperFinish(panel);
        NewsRule(panel,new Vector2(.04f,.11f),new Vector2(.96f,.112f),BistroShelfStyle.Ink);
        var entries=(definition?.codex??Array.Empty<ClaimData>()).Where(c=>c!=null).ToArray();
        if(entries.Length>0)BistroActionButton.Create(panel,"Codex · Read more",theme,new Vector2(.65f,.025f),new Vector2(.96f,.085f),BistroActionRole.Secondary,()=>Change(()=>{if(entries.Length==1)eventCodex=entries[0];else choosingEventCodex=true;}));
    }
}
