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
        if(e.type==EventEffectType.AttendancePopularity)return $"Attendance publicity: {e.value:+0;-0;0} (earned popularity unchanged)";
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
            var image=BistroWidgets.Box(eventToast,"News image",theme.accent,new Vector2(.02f,.08f),new Vector2(.26f,.92f));
            if(definition?.image!=null){image.GetComponent<Image>().sprite=definition.image;image.GetComponent<Image>().color=Color.white;image.GetComponent<Image>().preserveAspect=true;}
            BistroWidgets.Label(eventToast,(definition?.headline??toastRecord.eventId)+"\n"+(definition?.summary??"")+"\nClick to read",theme,new Vector2(.28f,.04f),new Vector2(.97f,.96f),19,BistroShelfStyle.Ink);
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
    }    private void EventPanel()
    {
        if(modal=="event-detail"&&!choosingEventCodex){
            if(eventCodex!=null){
                var entry=eventCodex;
                bool linked=Uri.TryCreate(entry.sourceUrl,UriKind.Absolute,out var source)&&(source.Scheme=="https"||source.Scheme=="http");
                DialoguePanel(entry.shortText??"Codex",(entry.codexText??"Evidence content has not been supplied yet.")+"\n\n"+entry.sourceTitle+"\n"+entry.sourceCitation+(!entry.sourceChecked?"\nPrototype / source not verified.":""),"Back to event",()=>eventCodex=null,
                    decorate:frame=>{if(linked)BistroActionButton.Create(frame,"Open source",theme,new Vector2(.57f,.045f),new Vector2(.75f,.19f),BistroActionRole.Secondary,()=>Application.OpenURL(source.AbsoluteUri));});
            }else{
                var news=game.Queries.GetEvent(detailRecord?.eventId);
                string description=$"Days {detailRecord?.startDay}–{detailRecord?.expiresOnDay-1} · "+(detailRecord?.Active(game.State.currentDay)==true?"Active":"Expired")+"\n\n"+(news?.details??"Details are unavailable.")+"\n\n"+(detailRecord==null?"":string.Join("\n",detailRecord.effects.Select(EffectText)));
                var entries=(news?.codex??Array.Empty<ClaimData>()).Where(c=>c!=null).ToArray();
                DialoguePanel(news?.headline??"Neighbourhood news",description,"All news",()=>{modal="news";if(eventAudio!=null)eventAudio.Stop();},()=>{modal=null;if(eventAudio!=null)eventAudio.Stop();},
                    decorate:frame=>{if(entries.Length>0)BistroActionButton.Create(frame,"Codex",theme,new Vector2(.42f,.045f),new Vector2(.56f,.19f),BistroActionRole.Secondary,()=>Change(()=>{if(entries.Length==1)eventCodex=entries[0];else choosingEventCodex=true;}));});
            }
            return;
        }
        var shade=BistroWidgets.Box(screen,"Modal blocker",new Color(0,0,0,.8f),Vector2.zero,new Vector2(1,.91f));
        var panel=BistroWidgets.Box(shade,"News",theme.background,new Vector2(.06f,.06f),new Vector2(.94f,.91f));
        BistroWidgets.Button(panel,"Close",theme,new Vector2(.82f,.90f),new Vector2(.97f,.98f),()=>Change(()=>{modal=null;eventCodex=null;if(eventAudio!=null)eventAudio.Stop();}));
        if(modal=="news")
        {
            BistroWidgets.Label(panel,"NEIGHBOURHOOD NEWS",theme,new Vector2(.04f,.90f),new Vector2(.78f,.98f),28);
            var list=BistroWidgets.Grid(panel,theme,new Vector2(.04f,.04f),new Vector2(.96f,.87f));
            foreach(var r in game.Queries.GetEventHistory().AsEnumerable().Reverse())
            {
                var row=BistroWidgets.Box(list,"News entry",theme.panel,Vector2.zero,Vector2.one);var def=game.Queries.GetEvent(r.eventId);
                BistroWidgets.Button(row,$"{def?.headline??r.eventId}\nDays {r.startDay}–{r.expiresOnDay-1} · {(r.Active(game.State.currentDay)?"Active":"Expired")}",theme,Vector2.zero,Vector2.one,()=>OpenEvent(r));
            }
            if(game.Queries.GetEventHistory().Count==0)BistroWidgets.Label(panel,"No news yet. Random events begin on day 2.",theme,new Vector2(.1f,.3f),new Vector2(.9f,.7f));
            return;
        }
        var definition=game.Queries.GetEvent(detailRecord?.eventId);
        BistroWidgets.Button(panel,"← All news",theme,new Vector2(.03f,.90f),new Vector2(.25f,.98f),()=>Change(()=>{modal="news";eventCodex=null;if(eventAudio!=null)eventAudio.Stop();}));
        if(eventCodex!=null)
        {
            BistroWidgets.ReadingPanel(panel,(eventCodex.shortText??"Codex")+"\n\n"+(string.IsNullOrWhiteSpace(eventCodex.codexText)?"Evidence content has not been supplied yet.":eventCodex.codexText)+"\n\n"+eventCodex.sourceTitle+"\n"+eventCodex.sourceCitation+(!eventCodex.sourceChecked?"\nPrototype / source not verified.":""),theme,new Vector2(.04f,.16f),new Vector2(.96f,.87f));
            if(Uri.TryCreate(eventCodex.sourceUrl,UriKind.Absolute,out var url)&&(url.Scheme=="https"||url.Scheme=="http"))BistroWidgets.Button(panel,"Open source",theme,new Vector2(.55f,.03f),new Vector2(.94f,.12f),()=>Application.OpenURL(url.AbsoluteUri));
            BistroWidgets.Button(panel,"Back to event",theme,new Vector2(.04f,.03f),new Vector2(.43f,.12f),()=>Change(()=>eventCodex=null));return;
        }
        var art=BistroWidgets.Box(panel,"Event artwork",theme.panel,new Vector2(.04f,.49f),new Vector2(.40f,.86f));
        if(definition?.image!=null){art.GetComponent<Image>().sprite=definition.image;art.GetComponent<Image>().preserveAspect=true;art.GetComponent<Image>().color=Color.white;}
        else BistroWidgets.Label(art,"NEWS",theme,Vector2.zero,Vector2.one,32);
        BistroWidgets.Label(panel,definition?.headline??detailRecord?.eventId??"Event unavailable",theme,new Vector2(.44f,.69f),new Vector2(.96f,.87f),28);
        BistroWidgets.Label(panel,$"Days {detailRecord?.startDay}–{detailRecord?.expiresOnDay-1}\n"+(detailRecord?.Active(game.State.currentDay)==true?"Active":"Expired"),theme,new Vector2(.44f,.49f),new Vector2(.96f,.67f));
        string effects=detailRecord==null?"":string.Join("\n",detailRecord.effects.Select(EffectText));
        BistroWidgets.ReadingPanel(panel,(definition?.details??"Authored details are unavailable.")+"\n\nEffects:\n"+effects,theme,new Vector2(.04f,.15f),new Vector2(.96f,.46f));
        var claims=definition?.codex??Array.Empty<ClaimData>();
        if(claims.Any(c=>c!=null))BistroWidgets.Button(panel,"Codex: read more",theme,new Vector2(.04f,.025f),new Vector2(.45f,.12f),()=>Change(()=>{if(claims.Count(c=>c!=null)==1)eventCodex=claims.First(c=>c!=null);else choosingEventCodex=true;}));
        if(choosingEventCodex)
        {
            var picker=BistroWidgets.Box(panel,"Codex entries",theme.background,new Vector2(.02f,.015f),new Vector2(.98f,.88f));
            var grid=BistroWidgets.Grid(picker,theme,new Vector2(.02f,.13f),new Vector2(.98f,.98f));
            foreach(var claim in claims.Where(c=>c!=null)){var row=BistroWidgets.Box(grid,"Codex entry",theme.panel,Vector2.zero,Vector2.one);BistroWidgets.Button(row,claim.shortText??claim.id,theme,Vector2.zero,Vector2.one,()=>Change(()=>{eventCodex=claim;choosingEventCodex=false;}));}
            BistroWidgets.Button(picker,"Back to event",theme,new Vector2(.04f,.02f),new Vector2(.45f,.11f),()=>Change(()=>choosingEventCodex=false));
        }
    }
}
