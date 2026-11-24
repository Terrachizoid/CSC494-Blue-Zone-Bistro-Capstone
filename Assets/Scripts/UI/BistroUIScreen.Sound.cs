// Sound wiring (2026-11-22). Presentation only: cues are named after what happened, never after files; the Sounds CSV
// (SoundDatabase) maps cues to clips, and a missing cue is silence. Three sources of cues:
// - every Button on screen gets a click cue after each rebuild ("button.primary/secondary/caution", else "button"),
//   so no button needs its own sound code;
// - cards and drag rows play their own (card.flip / card.pick / card.drop / card.return);
// - moments are noticed here by comparing the session with the last rebuild: a new day, money spent or earned,
//   the newspaper opening, the dish result, sorting finished, a guest arriving or leaving (Characters.cs).
// Music follows the place (music.menu/city/store[.<id>]/sorting/bistro/results/ending) and popularity (.low/.high
// versions that share tempo and length, switched without losing the beat); it crossfades.
using System;
using UnityEngine;
using UnityEngine.UI;
public partial class BistroUIScreen
{
    private BistroAudio audioPlayer;
    private int soundDay = -1;
    private float soundMoney = float.NaN;
    private string soundModal;
    private string soundNewsId;
    private bool soundInBistro;
    private bool soundDishShown, soundSortShown, soundDayResultsShown;

    // Called at the end of every Refresh.
    private void SoundPass()
    {
        if (audioPlayer == null) audioPlayer = BistroAudio.Attach(gameObject, game.Sounds);
        // The screen is built fresh each time, so each button here is new and gets exactly one click listener.
        foreach (var button in screen.GetComponentsInChildren<Button>(true))
        {
            var role = button.GetComponent<BistroActionButton>();
            string cue = role != null ? "button." + role.Role.ToString().ToLowerInvariant() : "button";
            button.onClick.AddListener(() => BistroAudio.Play(cue));
        }
        BistroAudio.Music(ChooseMusic());
        if (game.IsMainMenu) { soundDay = -1; soundMoney = float.NaN; soundModal = soundNewsId = null; soundInBistro = false; soundDishShown = soundSortShown = soundDayResultsShown = false; return; }
        SoundMoments();
    }

    private string MusicCue()
    {
        if (game.IsMainMenu) return "music.menu";
        switch (game.Loop.Phase)
        {
            case GamePhase.Completed: return "music.ending";
            case GamePhase.Market: return store == null ? "music.city" : "music.store";
            case GamePhase.FoodSorting: return "music.sorting";
            case GamePhase.Service: return "music.bistro";
            default: return "music.results";
        }
    }

    // Place + popularity tier. A store can have its own track: rows "music.store.<store id>[.low/.high]" win when present.
    private string ChooseMusic()
    {
        string tier = MusicTier(), cue = MusicCue() + tier;
        var sounds = game.Sounds;
        if (sounds != null && !game.IsMainMenu && game.Loop.Phase == GamePhase.Market && store != null)
        {
            string own = "music.store." + store.id;
            if (sounds.Has(own + tier)) return own + tier;
            if (sounds.Has(own)) return own;
        }
        return cue;
    }

    // Popularity sets the music's intensity: ".low" (desolate) below theme.musicLowBelow, ".high" (full, extra drums)
    // from theme.musicHighFrom, otherwise the normal version. A 15-point buffer stops flicking near a threshold.
    // Missing ".low"/".high" rows fall back to the normal cue.
    private string musicTier = "";
    private string MusicTier()
    {
        if (game.IsMainMenu || game.Loop.Phase == GamePhase.Completed) return musicTier = "";
        float p = game.State.popularity, buffer = 15;
        if (musicTier == ".low" ? p < theme.musicLowBelow + buffer : p < theme.musicLowBelow) musicTier = ".low";
        else if (musicTier == ".high" ? p >= theme.musicHighFrom - buffer : p >= theme.musicHighFrom) musicTier = ".high";
        else musicTier = "";
        return musicTier;
    }

    private void SoundMoments()
    {
        var state = game.State;
        if (soundDay >= 0 && state.currentDay != soundDay) BistroAudio.Play("day.start");
        soundDay = state.currentDay;
        bool dish = ResultCardVisible(); // The card stays up through the reaction (2026-11-23).
        bool dayResults = game.Loop.Phase == GamePhase.Results;
        string resultCue = ResultSoundCue(soundDishShown, dish, soundDayResultsShown, dayResults);
        if (resultCue != null) BistroAudio.Play(resultCue);
        if (resultCue == null && !float.IsNaN(soundMoney) && Mathf.Abs(state.money - soundMoney) >= .005f)
            BistroAudio.Play(state.money < soundMoney ? "money.spend" : "money.earn");
        soundMoney = state.money;
        string newsId = modal == "event-detail" ? detailRecord?.eventId : null;
        string panelCue = PanelSoundCue(soundModal, modal, soundNewsId, newsId);
        if (panelCue != null) BistroAudio.Play(panelCue);
        soundModal = modal;
        soundNewsId = newsId;
        bool inBistro = game.Loop.Phase == GamePhase.Service;
        if (inBistro && !soundInBistro) BistroAudio.Play("bistro.enter");
        soundInBistro = inBistro;
        soundDishShown = dish;
        soundDayResultsShown = dayResults;
        bool sorted = game.Loop.Phase == GamePhase.FoodSorting && game.Loop.SortingResult != null;
        if (sorted && !soundSortShown) BistroAudio.Play("sort.done");
        soundSortShown = sorted;
    }

    // Transition-only: rebuilding a panel or updating its contents must not replay its sound.
    private static string PanelSoundCue(string previous, string current, string previousNews, string currentNews)
    {
        if (previous == current && previousNews == currentNews) return null;
        bool wasNews = previous == "news" || previous == "event-detail";
        bool isNews = current == "news" || current == "event-detail";
        if (isNews) return wasNews ? "event.news.page" : "event.news.open";
        if (wasNews) return "event.news.close";
        if (current != null) return "panel.open";
        return previous != null ? "panel.close" : null;
    }

    // One receipt cue per newly displayed result; pagination/refresh is silent.
    private static string ResultSoundCue(bool wasDish, bool dish, bool wasDay, bool day)
    {
        if (dish && !wasDish) return "result.dish";
        if (day && !wasDay) return "result.day";
        return null;
    }
}
