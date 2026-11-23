# Offline CC0 audio preparation. Libraries remain outside Assets; only selected clips ship.
param([string]$Sources='C:/Users/TERRA/.codex/visualizations/2026/09/21/01a0c490-ad67-7e80-9c87-d625cf4b4e02/AudioSources',
      [string]$Ffmpeg='C:/Program Files/Krita (x64)/bin/ffmpeg.exe')
$ErrorActionPreference='Stop'
$map=@{
 'button'='ui-audio/Audio/mouseclick1.ogg'
 'button.primary_1'='ui-audio/Audio/mouseclick1.ogg';'button.primary_2'='ui-audio/Audio/mouserelease1.ogg'
 'button.secondary_1'='ui-audio/Audio/mouseclick1.ogg';'button.secondary_2'='ui-audio/Audio/mouserelease1.ogg'
 'button.caution'='ui-audio/Audio/mouseclick1.ogg'
 'card'='casino-audio/Audio/card-slide-1.ogg'
 'card.flip_1'='casino-audio/Audio/card-slide-2.ogg';'card.flip_2'='casino-audio/Audio/card-slide-3.ogg';'card.flip_3'='casino-audio/Audio/card-slide-4.ogg'
 'card.pick_1'='casino-audio/Audio/card-shove-1.ogg';'card.pick_2'='casino-audio/Audio/card-shove-2.ogg'
 'card.drop_1'='casino-audio/Audio/card-place-1.ogg';'card.drop_2'='casino-audio/Audio/card-place-2.ogg';'card.drop_3'='casino-audio/Audio/card-place-3.ogg'
 'card.return'='casino-audio/Audio/card-slide-5.ogg'
 'guest.arrive'='rpg-audio/Audio/footstep00.ogg';'guest.leave'='rpg-audio/Audio/footstep01.ogg'
 'dish'='rpg-audio/Audio/metalPot1.ogg';'dish.okay'='rpg-audio/Audio/metalPot2.ogg'
 'dish.good'='interface-sounds/Audio/confirmation_001.ogg';'dish.unhealthy'='interface-sounds/Audio/back_002.ogg';'dish.failed'='interface-sounds/Audio/error_001.ogg'
 'money.spend_1'='casino-audio/Audio/chips-handle-1.ogg';'money.spend_2'='casino-audio/Audio/chips-handle-2.ogg'
 'money.earn_1'='casino-audio/Audio/chips-collide-1.ogg';'money.earn_2'='casino-audio/Audio/chips-collide-2.ogg'
 'sort.done'='rpg-audio/Audio/bookPlace1.ogg';'event.news'='paper.mp3'
 'day.start'='interface-sounds/Audio/glass_001.ogg'
 'panel.open_1'='casino-audio/Audio/card-place-1.ogg';'panel.open_2'='casino-audio/Audio/card-place-2.ogg'
 'panel.close'='interface-sounds/Audio/close_001.ogg'
 'event.news.page_1'='paper.mp3';'event.news.page_2'='paper.mp3'
 'event.news.close'='paper.mp3'
 'result.register'='register.mp3'
 'bistro.enter'='cafe-855905-preview.mp3'
}
New-Item -ItemType Directory -Path Assets/Audio/Sfx -Force | Out-Null
$records=@()
foreach($key in ($map.Keys | Sort-Object)){
 $source=Join-Path $Sources $map[$key];$output='Assets/Audio/Sfx/'+$key+'.ogg'
 $duration=if($key -eq 'bistro.enter'){6}else{2}
 $scan=& $Ffmpeg -hide_banner -i $source -t $duration -af volumedetect -f null NUL 2>&1
 $peak=[regex]::Match(($scan -join "`n"),'max_volume: (-?[0-9.]+) dB')
 if(!$peak.Success){throw ('Cannot measure '+$source)}
 $gain=(-8-[double]::Parse($peak.Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)).ToString([Globalization.CultureInfo]::InvariantCulture)
 $filter='volume='+$gain+'dB,afade=t=in:d=0.005,areverse,afade=t=in:d=0.025,areverse'
 if($key -eq 'bistro.enter'){$filter='highpass=f=180,lowpass=f=2200,volume='+$gain+'dB,afade=t=in:d=0.7,afade=t=out:st=3.8:d=2.2'}
 # Bundled ffmpeg has the native stereo Vorbis encoder; Unity imports SFX as mono.
 & $Ffmpeg -y -loglevel error -i $source -t $duration -af $filter -ac 2 -ar 32000 -c:a vorbis -strict -2 -q:a 3 $output
 if($LASTEXITCODE -ne 0){throw ('Encode failed '+$key)}
 $records += [pscustomobject]@{file=$output;source=$map[$key];license='CC0-1.0';sha256=(Get-FileHash $output).Hash}
}
$records | Export-Csv Tools/Art/SoundEffectSources_20261122.csv -NoTypeInformation -Encoding utf8
