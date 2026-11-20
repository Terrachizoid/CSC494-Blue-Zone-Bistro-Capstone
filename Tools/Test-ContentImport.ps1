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
 public class TooltipAttribute : Attribute { public TooltipAttribute(string s) {} }
 public static class Debug {
  public static List<string> Errors = new List<string>();
  public static void Log(object s) { }
  public static void LogError(object s, Object o = null) { Errors.Add(s.ToString()); }
  public static List<string> Warnings = new List<string>();
  public static void LogWarning(object s) { Warnings.Add(s.ToString()); }
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
$sources += @('Assets/Editor/Importers/TutorialCsvImporter.cs','Assets/Scripts/Core/GamePhase.cs') | ForEach-Object { Join-Path $projectRoot $_ }
$sources += Join-Path $projectRoot 'Assets/Editor/Importers/FoodCsvImporter.cs'
Add-Type -Path $sources -CompilerOptions '/nowarn:0649'

$originalDirectory=[Environment]::CurrentDirectory
try {
    $csvDirectory=Join-Path $tempRoot 'Assets/GameData/CSV'
    New-Item -ItemType Directory -Path $csvDirectory -Force | Out-Null
    foreach ($name in @('DialogueSequences','DialogueLines')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot "Assets/GameData/CSV/Blue_Zone_Bistro_$name.csv") -Destination $csvDirectory
    }
    [Environment]::CurrentDirectory=$tempRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/GameData/CSV/Blue_Zone_Bistro_Foods.csv') -Destination $csvDirectory
    [FoodCsvImporter]::ImportFoods()
    $food=[UnityEditor.AssetDatabase]::Assets['Assets/GameData/Foods/P001.asset']
    if (!$food -or $food.ingredientAppeal -ne 3 -or [UnityEngine.Debug]::Errors.Count) { throw 'Food appeal import failed' }
    $foodPath=Join-Path $csvDirectory 'Blue_Zone_Bistro_Foods.csv'
    $foodCsv=[IO.File]::ReadAllText($foodPath)
    foreach ($bad in @('NaN','11')) {
        $badCsv=[regex]::Replace($foodCsv,',3(\r?\n)',(","+$bad+'$1'))
        [IO.File]::WriteAllText($foodPath,$badCsv)
        [FoodCsvImporter]::ImportFoods()
        if (![UnityEngine.Debug]::Errors.Count -or $food.ingredientAppeal -ne 3) { throw 'Invalid appeal import was not atomic' }
        [UnityEngine.Debug]::Errors.Clear()
    }
    [IO.File]::WriteAllText($foodPath,$foodCsv)
    Write-Output 'PASS: ingredient appeal import and atomic rejection of non-finite/out-of-range values.'
    $tomato=[UnityEditor.AssetDatabase]::Assets['Assets/GameData/Foods/P019.asset']
    $oats=[UnityEditor.AssetDatabase]::Assets['Assets/GameData/Foods/P001.asset']
    if (!$tomato.lightLogo -or !$tomato.cardSprig -or $tomato.darkPlaque -or $oats.lightLogo -or $oats.cardSprig) { throw 'card_style import failed' }
    Write-Output 'PASS: card_style flags imported from Foods.csv (art paths resolve only inside Unity).'
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
    if (@([UnityEngine.Debug]::Warnings | Where-Object { $_ -match "'-1' is invalid" }).Count) {
        throw 'Valid unrestricted dialogue bounds produced warnings'
    }
    $optionalParser=[DialogueCsvImporter].GetMethod('ParseOptionalInt',[Reflection.BindingFlags]'NonPublic,Static')
    foreach ($minimum in @(0,1)) {
        [UnityEngine.Debug]::Warnings.Clear()
        $value=$optionalParser.Invoke($null,[object[]]@(' -1 ',-1,$minimum,100,'test','optional_bound'))
        if ($value -ne -1 -or [UnityEngine.Debug]::Warnings.Count) { throw 'Explicit -1 sentinel rejected' }
    }
    foreach ($invalid in @('0','-2','invalid')) {
        [UnityEngine.Debug]::Warnings.Clear()
        $value=$optionalParser.Invoke($null,[object[]]@($invalid,-1,1,100,'test','max_day'))
        if ($value -ne -1 -or [UnityEngine.Debug]::Warnings.Count -ne 1) { throw 'Invalid bound no longer warns' }
    }
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
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/GameData/CSV/Blue_Zone_Bistro_Tutorials.csv') -Destination $csvDirectory
    if (![TutorialCsvImporter]::EnsureCurrent()) { throw 'Tutorial import failed' }
    $tutorialDb=[UnityEditor.AssetDatabase]::Assets['Assets/GameData/TutorialDatabase.asset']
    if ($tutorialDb.tutorials.Count -ne 26 -or ($tutorialDb.tutorials.steps | Measure-Object).Count -ne 33) { throw 'Tutorial definition/step count incorrect' }
    $tutorialPath=Join-Path $csvDirectory 'Blue_Zone_Bistro_Tutorials.csv'
    $tutorialCsv=[IO.File]::ReadAllText($tutorialPath)
    $tutorialFingerprint=$tutorialDb.fingerprint
    foreach ($invalidCsv in @($tutorialCsv.Replace('FirstMeal','UnknownBehaviour'),$tutorialCsv.Replace(',build,',',choose,'),$tutorialCsv.Replace(',FoodSorting,',',4,'))) {
        [IO.File]::WriteAllText($tutorialPath,$invalidCsv)
        if ([TutorialCsvImporter]::EnsureCurrent() -or $tutorialDb.fingerprint -ne $tutorialFingerprint) { throw 'Invalid tutorial import modified good content' }
    }
    [IO.File]::WriteAllText($tutorialPath,$tutorialCsv)
    if (![TutorialCsvImporter]::EnsureCurrent()) { throw 'Tutorial freshness check failed' }
    Write-Output 'PASS: eleven tutorial definitions (incl. three Any-phase top-bar lessons), thirteen steps, and atomic rejection of unknown behaviours, duplicate steps and invalid phases.'
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
