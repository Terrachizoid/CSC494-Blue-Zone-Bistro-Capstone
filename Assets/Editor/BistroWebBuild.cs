// Repeatable Web publication entry point. Run in an isolated project copy when the
// working project is open in Unity. Imports current CSV content before building.
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

public static class BistroWebBuild
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("BISTRO_WEB_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new BuildFailedException("Set BISTRO_WEB_OUTPUT to the Web build output directory.");

        AllContentCsvImporter.ImportAll();
        if (!DialogueCsvImporter.EnsureCurrent() || !BistroContentValidator.Validate())
            throw new BuildFailedException("Content import/validation failed before Web build.");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        GameBootstrapSetup.SetupLoadedScenes();
        EditorSceneManager.SaveOpenScenes();

        // Fallback supports hosts that do not supply compression response headers.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Bistro Web build failed: " + report.summary.result);
        UnityEngine.Debug.Log("BISTRO_WEB_BUILD_SUCCEEDED: " + output);
    }
}
