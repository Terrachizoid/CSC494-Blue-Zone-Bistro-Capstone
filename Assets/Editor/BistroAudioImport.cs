// File responsibility: Import settings for game audio under Assets/Audio (first import only; later Inspector changes
// are kept). Sound effects: decompressed on load (instant playback). Music: compressed Vorbis, streamed, mono off.
// Keeps the web download small: music is the bulk of audio size.
using System;
using UnityEditor;
using UnityEngine;

public sealed class BistroAudioImport : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/", StringComparison.OrdinalIgnoreCase) || !assetImporter.importSettingsMissing) return;
        var importer = (AudioImporter)assetImporter;
        bool music = assetPath.StartsWith("Assets/Audio/Music/", StringComparison.OrdinalIgnoreCase);
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = music ? .55f : .7f;
        settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = !music;
        importer.loadInBackground = music;
    }
}
