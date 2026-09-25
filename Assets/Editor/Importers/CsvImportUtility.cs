// File responsibility: Shared editor CSV parsing, typed conversion, ID checks, asset loading, and import reporting.
// Optional art paths preserve existing references; content errors are reported in the Unity Console.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CsvImportUtility
{
    public static bool TryRead(
        string csvPath,
        string contentName,
        string[] requiredColumns,
        out List<List<string>> rows,
        out Dictionary<string, int> columns)
    {
        rows = null;
        columns = null;

        if (!File.Exists(csvPath))
        {
            Debug.LogError($"{contentName} CSV was not found at: {csvPath}");
            return false;
        }

        try
        {
            rows = ParseCsv(File.ReadAllText(csvPath));
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Could not parse {contentName} CSV '{csvPath}': " +
                exception.Message);
            return false;
        }

        if (rows.Count < 2)
        {
            Debug.LogError($"The {contentName} CSV has no data rows.");
            return false;
        }

        Dictionary<string, int> parsedColumns;
        try { parsedColumns = BuildColumnMap(rows[0]); }
        catch (InvalidDataException exception)
        {
            Debug.LogError($"Invalid {contentName} CSV header: {exception.Message}");
            return false;
        }
        columns = parsedColumns;

        string[] missingColumns = requiredColumns
            .Where(column => !parsedColumns.ContainsKey(column))
            .ToArray();

        if (missingColumns.Length > 0)
        {
            Debug.LogError(
                $"{contentName} CSV is missing required columns: " +
                string.Join(", ", missingColumns));
            return false;
        }

        return true;
    }

    public static string Get(
        List<string> row,
        Dictionary<string, int> columns,
        string columnName)
    {
        if (!columns.TryGetValue(columnName, out int index))
        {
            throw new InvalidDataException(
                $"Required CSV column '{columnName}' is missing.");
        }

        return index < row.Count ? row[index] : string.Empty;
    }

    public static bool IsBlankRow(List<string> row)
    {
        return row.All(string.IsNullOrWhiteSpace);
    }

    public static bool IsSafeId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && id.All(character =>
            char.IsLetterOrDigit(character) ||
            character == '_' ||
            character == '-');
    }

    public static T ParseEnum<T>(
        string text,
        T fallback,
        string ownerId,
        string columnName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (Enum.TryParse(text.Trim(), true, out T result) && Enum.IsDefined(typeof(T), result))
        {
            return result;
        }

        Debug.LogWarning(
            $"{ownerId}: '{text}' is not a valid {columnName}. " +
            $"Using {fallback}.");
        return fallback;
    }

    public static T[] ParseEnumList<T>(
        string text,
        T fallback,
        string ownerId,
        string columnName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new[] { fallback };
        }

        List<T> values = new List<T>();

        foreach (string part in text.Split('|'))
        {
            if (Enum.TryParse(part.Trim(), true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                if (!values.Contains(parsed))
                {
                    values.Add(parsed);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"{ownerId}: '{part}' is not a valid " +
                    $"{columnName} value.");
            }
        }

        return values.Count > 0 ? values.ToArray() : new[] { fallback };
    }

    public static int ParseInt(
        string text,
        int fallback,
        int minimum,
        int maximum,
        string ownerId,
        string columnName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (int.TryParse(
                text.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result) &&
            result >= minimum && result <= maximum)
        {
            return result;
        }

        Debug.LogWarning(
            $"{ownerId}: {columnName} '{text}' is invalid. " +
            $"Using {fallback}.");
        return fallback;
    }

    public static float ParseFloat(
        string text,
        float fallback,
        float minimum,
        string ownerId,
        string columnName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        if (float.TryParse(
                text.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result) &&
            !float.IsNaN(result) && !float.IsInfinity(result) && result >= minimum)
        {
            return result;
        }

        Debug.LogWarning(
            $"{ownerId}: {columnName} '{text}' is invalid. " +
            $"Using {fallback}.");
        return fallback;
    }

    public static bool ParseBool(
        string text,
        bool fallback,
        string ownerId,
        string columnName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string normalized = text.Trim().ToLowerInvariant();

        if (normalized == "true" || normalized == "yes" || normalized == "1")
        {
            return true;
        }

        if (normalized == "false" || normalized == "no" || normalized == "0")
        {
            return false;
        }

        Debug.LogWarning(
            $"{ownerId}: {columnName} '{text}' is invalid. " +
            $"Using {fallback}.");
        return fallback;
    }

    public static string[] SplitIds(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text.Split('|')
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static T[] LoadAssetsByIds<T>(
        string text,
        string assetFolder,
        string ownerId,
        string relationName) where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();

        foreach (string id in SplitIds(text))
        {
            string path = $"{assetFolder}/{id}.asset";
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                Debug.LogWarning(
                    $"{ownerId}: {relationName} ID '{id}' was not found " +
                    $"at '{path}'.");
                continue;
            }

            assets.Add(asset);
        }

        return assets.ToArray();
    }

    public static T LoadOptionalAsset<T>(
        string path,
        T existing,
        string ownerId,
        string fieldName) where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return existing;
        }

        T imported = AssetDatabase.LoadAssetAtPath<T>(path.Trim());

        if (imported == null)
        {
            Debug.LogWarning(
                $"{ownerId}: no {typeof(T).Name} was found for " +
                $"{fieldName} at '{path}'. The existing reference was preserved.");
            return existing;
        }

        return imported;
    }

    public static T GetOrCreateAsset<T>(
        string assetPath,
        out bool isNew) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        isNew = asset == null;

        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        return asset;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (string.IsNullOrEmpty(parent))
        {
            throw new InvalidDataException(
                $"Cannot determine parent folder for '{path}'.");
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    public static void SaveAndReport(
        string contentName,
        int created,
        int updated,
        int rejected)
    {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"{contentName} import finished. Created: {created}, " +
            $"updated: {updated}, rejected: {rejected}.");
    }

    private static Dictionary<string, int> BuildColumnMap(
        List<string> headerRow)
    {
        Dictionary<string, int> columns = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < headerRow.Count; i++)
        {
            string header = headerRow[i]
                .Trim()
                .TrimStart('\uFEFF');

            if (!string.IsNullOrEmpty(header))
            {
                if (columns.ContainsKey(header)) throw new InvalidDataException($"Duplicate column '{header}'.");
                columns.Add(header, i);
            }
        }

        return columns;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];

            if (insideQuotes)
            {
                if (character == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    insideQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    field.Append(character);
                    break;
            }
        }

        if (insideQuotes)
        {
            throw new InvalidDataException(
                "CSV ended inside a quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
