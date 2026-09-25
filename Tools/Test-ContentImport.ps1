# Exercises real editor importer code with an in-memory AssetDatabase, without modifying Unity assets.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$stubSource = @'
using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { }
 public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => (T)Activator.CreateInstance(typeof(T)); }
 public class Sprite : Object { }
 public class TextAsset : Object { }
 public class AudioClip : Object { }
 public class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
 public class SerializeField : Attribute { }
 public class HeaderAttribute : Attribute { public HeaderAttribute(string s) {} }
 public class TextAreaAttribute : Attribute { public TextAreaAttribute() {} public TextAreaAttribute(int a,int b) {} }
 public class MinAttribute : Attribute { public MinAttribute(float n) {} }
 public class RangeAttribute : Attribute { public RangeAttribute(float a,float b) {} }
 public static class Debug {
  public static List<string> Errors = new List<string>();
  public static void Log(object s) { }
  public static void LogError(object s, Object o = null) { Errors.Add(s.ToString()); }
  public static void LogWarning(object s) { }
 }
}
namespace UnityEditor {
 public class MenuItem : Attribute { public MenuItem(string name) {} }
 public static class EditorUtility { public static void SetDirty(UnityEngine.Object obj) {} }
 public static class AssetDatabase {
  public static Dictionary<string,UnityEngine.Object> Assets = new Dictionary<string,UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);
  public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => Assets.TryGetValue(path,out var value) ? value as T : null;
  public static void CreateAsset(UnityEngine.Object obj,string path) { Assets[path] = obj; }
  public static bool IsValidFolder(string path) => true;
  public static void CreateFolder(string parent,string name) {}
  public static void SaveAssets() {}
  public static void Refresh() {}
 }
}
'@
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('BistroImportChecks-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$stubPath=Join-Path $tempRoot 'EditorStubs.cs'
Set-Content -LiteralPath $stubPath -Value $stubSource
$sources=@($stubPath) + @(Get-ChildItem (Join-Path $projectRoot 'Assets/Scripts/Data') -Filter '*.cs' | ForEach-Object FullName)
$sources += @('Assets/Editor/Importers/CsvImportUtility.cs','Assets/Editor/Importers/DialogueCsvImporter.cs') | ForEach-Object { Join-Path $projectRoot $_ }
Add-Type -Path $sources -CompilerOptions '/nowarn:0649'

$originalDirectory=[Environment]::CurrentDirectory
try {
    $csvDirectory=Join-Path $tempRoot 'Assets/GameData/CSV'
    New-Item -ItemType Directory -Path $csvDirectory -Force | Out-Null
    foreach ($name in @('DialogueSequences','DialogueLines')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot "Assets/GameData/CSV/Blue_Zone_Bistro_$name.csv") -Destination $csvDirectory
    }
    [Environment]::CurrentDirectory=$tempRoot
    foreach ($row in Import-Csv (Join-Path $projectRoot 'Assets/GameData/CSV/Blue_Zone_Bistro_Customers.csv')) {
        $customer=New-Object CustomerData; $customer.id=$row.customer_id
        [UnityEditor.AssetDatabase]::CreateAsset($customer,"Assets/GameData/Customers/$($customer.id).asset")
    }
    foreach ($row in Import-Csv (Join-Path $projectRoot 'Assets/GameData/CSV/Blue_Zone_Bistro_Recipes.csv')) {
        $recipe=New-Object RecipeData; $recipe.id=$row.recipe_id
        [UnityEditor.AssetDatabase]::CreateAsset($recipe,"Assets/GameData/Recipes/$($recipe.id).asset")
    }
    [UnityEditor.AssetDatabase]::CreateAsset((New-Object CustomerDatabase),'Assets/GameData/Customers/CustomerDatabase.asset')
    [UnityEditor.AssetDatabase]::CreateAsset((New-Object RecipeDatabase),'Assets/GameData/Recipes/RecipeDatabase.asset')
    [DialogueCsvImporter]::ImportDialogues()
    $database=[UnityEditor.AssetDatabase]::Assets['Assets/GameData/Dialogues/DialogueDatabase.asset']
    if (!$database -or !$database.importedCsvFingerprint -or [UnityEngine.Debug]::Errors.Count) { throw 'Valid dialogue import failed' }
    $originalCount=$database.Dialogues.Count
    $originalFingerprint=$database.importedCsvFingerprint
    $sequencePath=Join-Path $csvDirectory 'Blue_Zone_Bistro_DialogueSequences.csv'
    $originalCsv=[IO.File]::ReadAllText($sequencePath)
    [IO.File]::WriteAllText($sequencePath,$originalCsv.Replace('U001','missing_customer'))
    [DialogueCsvImporter]::ImportDialogues()
    if ([UnityEngine.Debug]::Errors.Count -eq 0 -or $database.Dialogues.Count -ne $originalCount -or $database.importedCsvFingerprint -ne $originalFingerprint) {
        throw 'Missing customer was not rejected without modifying imported data'
    }
    [UnityEngine.Debug]::Errors.Clear()
    [IO.File]::WriteAllText($sequencePath,$originalCsv.Replace('BeforeService','999'))
    [DialogueCsvImporter]::ImportDialogues()
    if ([UnityEngine.Debug]::Errors.Count -eq 0 -or $database.importedCsvFingerprint -ne $originalFingerprint) { throw 'Undefined enum was imported' }
    if ([CsvImportUtility]::ParseFloat('Infinity',7,0,'test','price') -ne 7 -or [CsvImportUtility]::ParseFloat('NaN',7,0,'test','price') -ne 7) {
        throw 'Non-finite numeric content was accepted'
    }
    $headerFixture=Join-Path $tempRoot 'duplicate-header.csv'
    [IO.File]::WriteAllText($headerFixture,"id,ID`nfirst,second`n")
    $parsedRows=$null; $parsedColumns=$null
    if ([CsvImportUtility]::TryRead($headerFixture,'Fixture',@('id'),[ref]$parsedRows,[ref]$parsedColumns)) {
        throw 'Duplicate header silently replaced an earlier column'
    }
    [IO.File]::WriteAllText($sequencePath,$originalCsv)
    if (![DialogueCsvImporter]::EnsureCurrent()) { throw 'Unchanged valid dialogue not recognized as current' }
    Write-Output "PASS: $originalCount dialogues imported by actual importer code; missing references and undefined enums preserve existing data; non-finite prices and duplicate headers rejected."
} finally {
    [Environment]::CurrentDirectory=$originalDirectory
    # Only delete this test's verified, uniquely named temporary directory.
    $resolvedTemp=[IO.Path]::GetFullPath($tempRoot)
    $allowedParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if ($resolvedTemp.StartsWith($allowedParent,[StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedTemp).StartsWith('BistroImportChecks-')) {
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
