// File responsibility: Editor-only claim CSV-to-ScriptableObject importer.
// Stable IDs preserve asset identity on reimport; optional presentation references are retained.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ClaimCsvImporter
{
    private static readonly string[] RequiredColumns =
    {
        "claim_id",
        "short_text",
        "codex_text",
        "source_title",
        "source_citation",
        "source_url",
        "usage_tags",
        "tip_day",
        "source_checked",
        "approval_status",
        "review_notes"
    };

    private const string CsvPath =
        "Assets/GameData/CSV/Blue_Zone_Bistro_Claims.csv";

    private const string OutputFolder =
        "Assets/GameData/Claims";

    private const string DatabasePath =
        OutputFolder + "/ClaimDatabase.asset";

    [MenuItem("Tools/Blue Zone Bistro/Import Claims CSV")]
    public static void ImportClaims()
    {
        if (!CsvImportUtility.TryRead(
                CsvPath,
                "Claim",
                RequiredColumns,
                out List<List<string>> rows,
                out Dictionary<string, int> columns))
        {
            return;
        }

        CsvImportUtility.EnsureFolder(OutputFolder);
        HashSet<string> idsSeen = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        List<ClaimData> imported = new List<ClaimData>();
        int created = 0;
        int updated = 0;
        int rejected = 0;

        for (int rowNumber = 1; rowNumber < rows.Count; rowNumber++)
        {
            List<string> row = rows[rowNumber];

            if (CsvImportUtility.IsBlankRow(row))
            {
                continue;
            }

            string id = CsvImportUtility.Get(
                row, columns, "claim_id").Trim();

            if (!CsvImportUtility.IsSafeId(id) || !idsSeen.Add(id))
            {
                Debug.LogError(
                    $"Row {rowNumber + 1}: claim ID '{id}' is invalid " +
                    "or duplicated.");
                rejected++;
                continue;
            }

            ClaimData claim = CsvImportUtility.GetOrCreateAsset<ClaimData>(
                $"{OutputFolder}/{id}.asset",
                out bool isNew);

            claim.id = id;
            claim.shortText = CsvImportUtility.Get(
                row, columns, "short_text");
            claim.codexText = CsvImportUtility.Get(
                row, columns, "codex_text");
            claim.sourceTitle = CsvImportUtility.Get(
                row, columns, "source_title");
            claim.sourceCitation = CsvImportUtility.Get(
                row, columns, "source_citation");
            claim.sourceUrl = CsvImportUtility.Get(
                row, columns, "source_url").Trim();
            claim.usageTags = CsvImportUtility.ParseEnumList(
                CsvImportUtility.Get(row, columns, "usage_tags"),
                ClaimUsageTag.None,
                id,
                "usage_tags");
            claim.tipDay = CsvImportUtility.ParseInt(
                CsvImportUtility.Get(row, columns, "tip_day"),
                0, 0, int.MaxValue, id, "tip_day");
            claim.sourceChecked = CsvImportUtility.ParseBool(
                CsvImportUtility.Get(row, columns, "source_checked"),
                false, id, "source_checked");
            claim.approvalStatus = CsvImportUtility.ParseEnum(
                CsvImportUtility.Get(row, columns, "approval_status"),
                ApprovalStatus.Pending,
                id,
                "approval_status");
            claim.reviewNotes = CsvImportUtility.Get(
                row, columns, "review_notes");

            if (claim.approvalStatus == ApprovalStatus.Approved &&
                !claim.IsReadyForPlayerFacing)
            {
                Debug.LogWarning(
                    $"Claim {id} is marked Approved but is missing checked " +
                    "player-facing text or a source URL.");
            }

            EditorUtility.SetDirty(claim);
            imported.Add(claim);

            if (isNew)
            {
                created++;
            }
            else
            {
                updated++;
            }
        }

        ClaimDatabase database =
            CsvImportUtility.GetOrCreateAsset<ClaimDatabase>(
                DatabasePath,
                out _);
        database.SetItems(imported.OrderBy(claim => claim.id).ToList());
        EditorUtility.SetDirty(database);

        CsvImportUtility.SaveAndReport(
            "Claim", created, updated, rejected);
    }
}
