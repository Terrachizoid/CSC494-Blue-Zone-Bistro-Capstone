# Run in a fresh PowerShell process. Unity stubs support pure operation tests only.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sources = @'
using System;
namespace UnityEngine {
 public class Object { }
 public class ScriptableObject : Object { }
 public class Sprite : Object { }
 public class TextAsset : Object { }
 public class AudioClip : Object { }
 public static class Mathf { public static float Max(float a, float b) => Math.Max(a,b); }
 public static class Random { public static float Range(float a, float b) => a; }
 public class MonoBehaviour : Object { public bool enabled; }
 public interface ISerializationCallbackReceiver { void OnBeforeSerialize(); void OnAfterDeserialize(); }
 public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
 public class SerializeField : Attribute { }
 public class HeaderAttribute : Attribute { public HeaderAttribute(string s) {} }
 public class TextAreaAttribute : Attribute { public TextAreaAttribute() {} public TextAreaAttribute(int a, int b) {} }
 public class MinAttribute : Attribute { public MinAttribute(float n) {} }
 public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) {} }
 public static class Debug { public static void LogError(object s, Object o = null) {} public static void LogWarning(object s) {} }
}
'@
$paths = @(
    'Assets/Scripts/Data/*.cs',
    'Assets/Scripts/Runtime/GameSessionState.cs',
    'Assets/Scripts/Runtime/InventoryEntry.cs',
    'Assets/Scripts/Runtime/CustomerState.cs',
    'Assets/Scripts/Runtime/ServiceSessionState.cs',
    'Assets/Scripts/Runtime/Dialogue/GameFlagState.cs',
    'Assets/Scripts/Runtime/Dialogue/DialogueHistoryState.cs',
    'Assets/Scripts/Runtime/Dialogue/DialogueRuntimeContext.cs',
    'Assets/Scripts/Runtime/Dialogue/DialogueService.cs',
    'Assets/Scripts/Runtime/Dialogue/DialoguePlaybackController.cs',
    'Assets/Scripts/Services/*.cs',
    'Assets/Scripts/Core/GameLoop.cs',
    'Assets/Scripts/Core/GamePhase.cs'
)
# Keep each compilation unit separate so its using directives remain valid.
$testTemp = Join-Path ([IO.Path]::GetTempPath()) ('BistroTests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testTemp | Out-Null
$stubPath = Join-Path $testTemp 'UnityStubs.cs'
Set-Content -LiteralPath $stubPath -Value $sources
$files = @($stubPath)
foreach ($path in $paths) { $files += (Get-ChildItem (Join-Path $projectRoot $path)).FullName }
$files += Join-Path $PSScriptRoot 'GameOperationsChecks.cs'
$files += Join-Path $PSScriptRoot 'DialogueChecks.cs'
Add-Type -Path $files -CompilerOptions '/nowarn:0649'
[GameOperationsChecks]::Run()
[DialogueChecks]::Run()
