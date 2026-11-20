// File responsibility: Import settings for game art under Assets/Art (foods, recipes, categories, stores, UI).
// New images become single UI sprites automatically. Settings you change by hand afterwards are kept.
using System;
using UnityEditor;
using UnityEngine;

public sealed class BistroArtImport : AssetPostprocessor
{
    // Art referenced from the CSVs and the UI theme. Concepts/ is left alone.
    private static readonly string[] Folders =
    {
        "Assets/Art/Endings",
        "Assets/Art/Events",
        "Assets/Art/Foods",
        "Assets/Art/Recipes",
        "Assets/Art/Categories",
        "Assets/Art/Stores",
        "Assets/Art/UI"
    };

    private static bool IsGameArt(string path)
    {
        foreach (string folder in Folders)
            if (path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void OnPreprocessTexture()
    {
        // Only first import: a later Inspector change (compression, size) must survive reimports.
        if (IsGameArt(assetPath) && assetImporter.importSettingsMissing)
            Configure((TextureImporter)assetImporter);
    }

    private static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        ConfigurePanel(importer);
    }

    // Screen surfaces only; per-item food/recipe/store art still comes from CSVs.
    private static bool ConfigurePanel(TextureImporter importer)
    {
        Vector4 border;
        switch(importer.assetPath)
        {
            case "Assets/Art/UI/panelPaper.png": border=new Vector4(80,80,80,80);break;
            case "Assets/Art/UI/guideNotePanel.png": border=new Vector4(90,80,220,190);break;
            case "Assets/Art/UI/planPanel.png": border=new Vector4(80,80,80,145);break;
            case "Assets/Art/UI/suggestionFrame.png": border=new Vector4(110,110,110,110);break;
            case "Assets/Art/UI/newTag.png": border=new Vector4(220,220,220,220);break;
            case "Assets/Art/UI/soldOutStamp.png": border=Vector4.zero;break;
            default:return false;
        }
        importer.spriteBorder=border;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=4096;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        return true;
    }

    [MenuItem("Tools/Blue Zone Bistro/Configure illustrated panel borders")]
    public static void ConfigurePanelBorders()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Art/UI"}))
        {
            var importer=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if(importer==null||!ConfigurePanel(importer))continue;
            importer.SaveAndReimport();
        }
    }

    // Fixes images that were imported before this script compiled (they would not load as sprites).
    // Runs at the start of Import All CSV Content, so CSV paths always resolve to sprites.
    [MenuItem("Tools/Blue Zone Bistro/Fix game art import settings")]
    public static void EnsureSprites()
    {
        int fixedCount = 0;
        string[] existing = Array.FindAll(Folders, AssetDatabase.IsValidFolder);
        if (existing.Length == 0) return;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", existing))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) ||
                importer.textureType == TextureImporterType.Sprite) continue;
            Configure(importer);
            importer.SaveAndReimport();
            fixedCount++;
        }
        if (fixedCount > 0) Debug.Log($"Set {fixedCount} game art image(s) to Sprite import.");
    }
}
