// File responsibility: Plays sound cues and music for the UI. Presentation only: it never reads or changes game rules.
// Anything can call BistroAudio.Play("card.flip"); the cue -> clip table is SoundDatabase (Sounds CSV). Music crossfades
// between two sources; effects round-robin a few sources so each can have its own pitch. Volumes are saved in
// PlayerPrefs. In web builds the browser only allows audio after the first click; Unity resumes it automatically.
using System.Collections.Generic;
using UnityEngine;

public sealed class BistroAudio : MonoBehaviour
{
    private static BistroAudio instance;
    private SoundDatabase sounds;
    private readonly List<AudioSource> effects = new List<AudioSource>();
    private AudioSource musicA, musicB;
    private int nextEffect;
    private readonly float[] effectGains = new float[EffectVoices];
    private static bool volumeDirty;
    private static float saveVolumeAt;
    private static void VolumeChanged() { volumeDirty=true; saveVolumeAt=Time.unscaledTime+.35f; }
    private static void SaveVolumes() { if (!volumeDirty) return; PlayerPrefs.Save(); volumeDirty=false; }
    private string musicCue;
    private AudioClip musicClip;
    private float fade = 1;
    private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
    private const int EffectVoices = 6;
    private const float FadeSeconds = 1.2f, RepeatGuard = .05f;
    public const string MusicVolumeKey = "bistro.musicVolume", EffectsVolumeKey = "bistro.effectsVolume";

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicVolumeKey, .55f);
        set { PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value)); VolumeChanged(); if (instance != null) instance.ApplyMusicVolume(); }
    }
    public static float EffectsVolume
    {
        get => PlayerPrefs.GetFloat(EffectsVolumeKey, .8f);
        set { PlayerPrefs.SetFloat(EffectsVolumeKey, Mathf.Clamp01(value)); VolumeChanged(); if(instance!=null) for(int i=0;i<instance.effects.Count;i++) instance.effects[i].volume=instance.effectGains[i]*EffectsVolume; }
    }

    public static BistroAudio Attach(GameObject host, SoundDatabase database)
    {
        var audio = host.GetComponent<BistroAudio>();
        if (audio == null) audio = host.AddComponent<BistroAudio>();
        audio.sounds = database;
        instance = audio;
        return audio;
    }

    // Plays a sound effect cue (or its nearest parent cue). Silent when nothing is assigned.
    public static void Play(string cueId)
    {
        if (instance == null || instance.sounds == null || string.IsNullOrEmpty(cueId)) return;
        instance.PlayEffect(cueId);
    }

    // Switches the music to a cue (crossfade). The same cue keeps playing; a cue with no clip fades to silence.
    public static void Music(string cueId)
    {
        if (instance == null) return;
        instance.SetMusic(cueId);
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        for (int i = 0; i < EffectVoices; i++) effects.Add(NewSource(false));
        musicA = NewSource(true); musicB = NewSource(true);
    }

    private AudioSource NewSource(bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
        return source;
    }

    private void PlayEffect(string cueId)
    {
        var cue = sounds.Find(cueId);
        if (cue == null || cue.kind != SoundKind.Sfx) return;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(cue.id, out float last) && now - last < RepeatGuard) return; // one click, one sound
        lastPlayed[cue.id] = now;
        var clip = Pick(cue);
        if (clip == null) return;
        var source = effects[nextEffect];
        effectGains[nextEffect] = cue.volume;
        nextEffect = (nextEffect + 1) % effects.Count;
        source.pitch = 1 + Random.Range(-cue.pitchVariation, cue.pitchVariation);
        source.volume = cue.volume * EffectsVolume;
        source.clip = clip;
        source.Play();
    }

    private void SetMusic(string cueId)
    {
        if (string.Equals(cueId, musicCue)) return;
        musicCue = cueId;
        var cue = sounds != null ? sounds.Find(cueId) : null;
        var clip = cue != null && cue.kind == SoundKind.Music ? Pick(cue) : null;
        if (clip != null && clip == musicClip) return; // same track for a neighbouring place: keep it playing
        var previous = musicClip;
        musicClip = clip;
        // Swap: the old source fades out, the new one fades in.
        var swap = musicA; musicA = musicB; musicB = swap;
        outgoingVolume = musicB.isPlaying ? targetVolume * fade : 0;
        musicA.Stop();
        musicA.clip = clip;
        musicA.volume = 0;
        if (clip != null)
        {
            musicA.Play();
            // Versions of one track (low / normal / high intensity) share their bar count, though low may be a little
            // faster or slower: continue at the same point in the loop (as a fraction) so the change lands on the same
            // bar instead of restarting the song. Another song starts from the top.
            if (previous != null && musicB.isPlaying && previous.samples > 0 && SameSong(previous, clip))
            {
                double at = (double)musicB.timeSamples / previous.samples;
                musicA.timeSamples = Mathf.Clamp((int)(at * clip.samples), 0, clip.samples - 1);
            }
        }
        targetVolume = cue != null ? cue.volume : 0;
        fade = 0;
    }

    // Intensity versions of one song are files named <song>, <song>_low and <song>_high (Assets/Audio/Music).
    private static bool SameSong(AudioClip a, AudioClip b) => string.Equals(SongName(a), SongName(b));

    private static string SongName(AudioClip clip)
    {
        string name = clip.name;
        if (name.EndsWith("_low", System.StringComparison.Ordinal)) return name.Substring(0, name.Length - 4);
        if (name.EndsWith("_high", System.StringComparison.Ordinal)) return name.Substring(0, name.Length - 5);
        return name;
    }

    private float targetVolume, outgoingVolume;

    private void Update()
    {
        if (volumeDirty && Time.unscaledTime >= saveVolumeAt) SaveVolumes();
        if (fade >= 1) return;
        fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / FadeSeconds);
        ApplyMusicVolume();
        if (fade >= 1 && musicB.isPlaying) musicB.Stop();
    }

    private void ApplyMusicVolume()
    {
        float master = MusicVolume;
        if (musicA != null) musicA.volume = targetVolume * master * fade;
        if (musicB != null && musicB.isPlaying) musicB.volume = outgoingVolume * master * (1 - fade);
    }

    private static AudioClip Pick(SoundCue cue)
    {
        if (cue.clips == null || cue.clips.Length == 0) return null;
        var clip = cue.clips[Random.Range(0, cue.clips.Length)];
        return clip != null ? clip : System.Array.Find(cue.clips, c => c != null);
    }

    private void OnApplicationPause(bool paused) { if(paused) SaveVolumes(); }
    private void OnApplicationQuit() { SaveVolumes(); }
    private void OnDestroy() { SaveVolumes(); if (instance == this) instance = null; }
}
