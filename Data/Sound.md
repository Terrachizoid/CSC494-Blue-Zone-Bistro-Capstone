# Sound and music

Added 2026-11-22. Presentation only: no game rule reads or changes sound.

## How it works

- The code plays **cues** named after what happened (`button.primary`, `card.flip`, `dish.good`, `music.city`), never
  files. `Assets/GameData/CSV/Blue_Zone_Bistro_Sounds.csv` is the one place that maps cues to clips
  (Import All CSV Content -> `Assets/GameData/Audio/SoundDatabase.asset`, wired into GameBootstrap by Setup).
- Lookup falls back to parent cues: `event.news.E003` -> `event.news` -> `event` -> silence. So a specific sound can
  be added later as one more row (e.g. a cue for one event) without code, and a missing file is silence, not an error.
- Where cues come from (`Scripts/UI/BistroUIScreen.Sound.cs`, `BistroAudio.cs`):
  - every Button on screen after each rebuild: `button.primary/secondary/caution` (BistroActionButton roles), else `button`;
  - cards and drag rows: `card.flip`, `card.pick`, `card.drop`, `card.return`;
  - moments noticed between rebuilds: `day.start`, `money.spend`/`money.earn`, `event.news`, `dish.good/okay/unhealthy/failed`,
    `sort.done`, `guest.arrive`/`guest.leave`;
  - music by place: `music.menu/city/store[.<store id>]/sorting/bistro/results/ending` (1.2 s crossfade; the same file for two places
    keeps playing).
- Columns: `cue_id`, `kind` (Sfx/Music), `clip_paths` (`|` = random variants), `volume` (0-1), `pitch_variation`
  (random +/-, makes repeats less robotic), `source`, `license`, `notes`.
- Volumes: `BistroAudio.MusicVolume` / `EffectsVolume` (saved in PlayerPrefs; defaults 0.55 / 0.8). No sliders in the
  menu yet.
- Import settings (`Editor/BistroAudioImport.cs`, first import only): effects Vorbis, mono, decompressed on load;
  music Vorbis 0.55, streamed. Web: browsers allow audio only after the first click; Unity resumes it automatically.

## Music now (placeholders)

Procedural backing loops (no lead melody) from `Tools/Art/ProceduralMusic/tension.py --backing`, each in three
intensities of 16 bars: `city` (D dorian; map, menu, ending), `choosing` (E minor; stores and sorting, one continuous
track) and `cooking` (A minor; bistro and results), plus `_low` and `_high` files. Normal and high all run at 128 bpm
(city's pace; choosing and cooking were slowed from 144 and 136 on 2026-11-22 because they felt rushed). The desolate
low versions keep their old tempo (city 128, choosing 144, cooking 136).

Popularity picks the version: below `musicLowBelow` (200) the `.low` cue (desolate), from `musicHighFrom` (500) the
`.high` cue (fuller drums), otherwise normal; a 15-point buffer prevents flicking (UI Theme > Music intensity).
Versions of one song (files `<song>`, `<song>_low`, `<song>_high`) continue at the same point of the loop (as a
fraction, since low may differ in tempo), so a switch lands on the same bar. A store can have its own track:
rows `music.store.<store id>` and `.low`/`.high` win when present. Missing `.low`/`.high` rows use the normal cue.

## Prompt pack for real music (ElevenLabs Music v2 or similar)

AI tools cannot take commercial songs as audio references (uploads are copyright-screened), and licensed tools refuse
artist names, so the direction is written in words. Our own placeholder files can be uploaded as an audio reference
(project-made) to hold tempo and length. Paste the shared block first, then one place, then one intensity.

**Shared style (every track):** Instrumental only, no vocals. Modern mellow neo-soul / lo-fi with a dreamy, slightly
psychedelic production. Warm electric piano (Rhodes-like) playing jazzy extended chords (minor 9ths, major 7ths, sus
chords); round analog synth bass with a soft phaser/chorus wobble; dusty, laid-back live-sounding drums with swing
(soft kick, brushed or rimshot snare, quiet crisp hi-hats); airy pads and gentle tape saturation; sparse melodic hooks
on a soft synth or clean guitar with lots of space. Comforting but not cheerful: bittersweet, grown-up, a little
introspective. Mixed to sit under a game's interface: no sudden loud hits, no long intro or outro, steady level.
Seamless loop that starts and ends on the same groove, 45-60 seconds.

**Places:**
- City (map, menu, ending): D minor with a dorian colour, about 88 BPM. The most open and hopeful: walking pace, wide
  pads, a gentle guitar or bell motif answering the electric piano. Stepping out into a city you are slowly changing.
- Choosing (stores and sorting): E minor, about 94 BPM. Darker and more focused: tighter 16th-note hi-hats, a
  syncopated bassline, tension chords that resolve late (a dominant chord with a raised leading tone before the home
  chord). Weighing choices against a budget; quietly urgent, never stressful.
- Cooking (bistro and results): A minor, about 90 BPM. Warm and busy: a funky but relaxed bassline with ghost notes,
  clinky percussion (shaker, light kitchen-like taps), electric piano comping in short stabs. A small kitchen during
  service, guests waiting, everything under control.

**Intensities (same key, tempo and length as the normal version):**
- Normal: as described.
- Low (desolate): stripped back; no drums except a soft distant kick each bar, held bass notes, washed-out pads and a
  few lonely electric-piano notes with long reverb. An empty street, a quiet bistro.
- High: fuller; a second percussion layer (shaker, claps, open hi-hat), a brighter counter-melody, a slightly busier
  bassline. Confident and energetic, still chill.

Make the normal version first, then ask the tool for the low and high versions of that same track (variation / extend
/ section editing), so they match. Export, convert to .ogg, trim all three to the same number of bars, save over the
placeholder paths in the CSV (or add new paths) and run Import All CSV Content. Record tool, plan, date, prompt and
receipt in ASSET_LICENSES.md.

## Brief for whoever fills it in (Codex or a person)

1. Licences: CC0 only for effects (asset rule from the capstone plan), or music made on a paid AI plan whose terms
   allow use in a game. Record every file in `ASSET_LICENSES.md` and in the CSV's `source`/`license` columns.
2. Effects: download Kenney's UI Audio, Interface Sounds and Casino Audio packs (all CC0, kenney.nl). The CSV `source`
   column suggests a pack per cue. Missing ones (door bell, newspaper rustle, stamp, morning birds, plate on counter):
   Freesound with the CC0 filter. Convert to .ogg, trim silence, keep UI sounds under ~0.3 s.
3. Save files at the exact paths in `clip_paths` (`Assets/Audio/Sfx/<cue>.ogg`, `<cue>_2.ogg` for variants). A cue
   with fewer files: delete the extra paths from the row.
4. Music: follow the prompt pack above (paid plan whose terms allow use in a game; record prompt, date, plan, receipt).
5. Run Import All CSV Content; the console lists any file still missing.

## Free effects integrated — 2026-11-22

All 21 original SFX cues now have clips; added `panel.open`, `panel.close`, `event.news.open`, `event.news.page`, `event.news.close`, and `bistro.enter`. Total: 27 SFX cue rows plus 17 existing music rows. 37 selected Ogg files total 425,426 bytes (415.5 KiB). Music files and music mappings are unchanged.

Source libraries: Kenney UI Audio, Interface Sounds, Casino Audio and RPG Audio (CC0). Bistro entry uses a 6-second faded, filtered excerpt from a public CC0 cafe recording preview. Only selected files are inside Assets; full archives stay outside the project. Exact source files/licences/processing: SoundEffects_20261122.md and Tools/Art/SoundEffectSources_20261122.csv. Some foley is deliberately approximate: book paper for newspapers, quiet pot contact for plate placement. Review these by ear in context.

Panel sounds are based on changes of `modal`, with newspaper article IDs tracked separately: archive→article or a different article plays page movement; closing plays paper close; an unchanged modal does not replay on redraw. Entering Service plays `bistro.enter` once, including returning from the city; ordinary Service redraws do not replay it. No gameplay service or rule changes. SoundPass still attaches one generic click listener per newly built button; a quiet click can accompany contextual paper/door sounds. Existing six-voice SFX pool and 50 ms repeat guard remain; very rapid actions may interrupt older effects, including entrance ambience.

Connected SampleScene's previously missing `GameBootstrap.sounds` reference to the existing SoundDatabase GUID. Reimported Sounds CSV only and preserved its database GUID. Assets/Audio/Sfx metas come from the verified disposable import. Import settings verified: Vorbis compressed, force mono, DecompressOnLoad for immediate playback. The on-disk Ogg sources use stereo/32 kHz; Unity forces mono on import. Short effects are decompressed in RAM by design; compressed does not mean zero runtime memory.

Validation: disposable Unity 6000.4.1f1, WebGL target, current live scripts copied in. Compile and `Tools/UI/SoundValidation.cs` passed: 44 nonempty cue rows, 37 compressed/mono-configured effects with valid durations, modal/newspaper transitions, cue parent fallback, Play Mode effect playback, duplicate-repeat suppression and music playback. Log: ignored Tools/UI/ReviewProject/sound-validation.log. Live CSV hashes checked before database/meta handoff.

Limitations: no listening-based final mix approval, complete live UI click-through, mobile/browser audio-unlock test or new release size measurement. All three standalone PowerShell checks were attempted but blocked before tests by existing fixture stubs missing UnityEngine.Color for CharacterExpressionDatabase.tint (CS0246); Unity itself compiled successfully. Music remains the procedural placeholder set pending ElevenLabs trial; no paid requests or subscription made.


### Physical sound revision (2026-11-22)
User rejected sci-fi opening/click sounds and scraping dish sounds. Buttons now use mouse press/release, panel open uses card-placement taps, panel close stays unchanged. Newspaper uses HenKonen recorded paper; guest arrival/departure use quiet steps instead of doors. Paper/click levels raised moderately. result.dish and result.day share CapsLok's register composite, played once on entry; generic money change is suppressed on that same refresh. Result pagination is silent. Legacy dish outcome rows remain available but are no longer triggered by result presentation. Gameplay rules unchanged.
Sources are CC0 public previews; files remain 32 kHz Vorbis with mono Unity import. Provenance/hashes: Tools/Art/SoundEffectSources_20261122.csv. Audio was checked technically, not perceptually heard by the agent; user listening approval remains necessary.
Validation: isolated Unity compilation/import passed (46 cues, 38 compressed mono effects), panel and result edge checks passed, Play Mode effect playback/repeat guard/music playback passed. Full browser playthrough remains unverified.

### Music references confirmed by user (2026-11-22)
Forget by Pogo; Waiting to Wake Up by White Ash; Ring Ring Ring by Tyler (titles/artist spelling as supplied, exact releases not yet verified). Reference the supporting instrumental feel: chill but lively, enjoyable rhythm and melodic interest, no singing or sampled vocals. Do not copy melodies or recordings. Existing procedural tracks remain placeholders; references do not approve them. Local original composition is possible without subscription, but is procedural synthesis rather than a connected learned music generator.

### Main-menu sound controls (2026-11-22)
User kept the existing Claude-generated music; both audition tracks remain external and unapproved. Main menu now has a separate matching sage sound panel with Background music and Sound effects sliders, percentages, and 0% mute. Uses existing BistroAudio PlayerPrefs keys; changes apply immediately without rebuilding UI or altering the CSV. Preference writes are debounced 0.35 seconds and flushed on pause/quit/destruction. Active effects and outgoing crossfade music now respect master volume changes immediately. No gameplay/save-progress ownership changes.

Validation: isolated Unity import/Play Mode checks passed, including active-effect and crossfade mute. Separate panel check passed independent slider bindings and 0/100% limits; rendered layout inspected. Full browser pointer/keyboard interaction and persistence across a browser restart remain unverified.
