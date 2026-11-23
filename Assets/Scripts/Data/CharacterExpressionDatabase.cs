// File responsibility: Character art per expression, imported from Blue_Zone_Bistro_CharacterExpressions.csv.
// One row per (character, expression). The whole-image sprite is required for a row to show; separate parts and a
// looping video are optional upgrades: parts are stacked over each other in the listed order, and a video (a file
// under Assets/StreamingAssets) replaces the still once it is ready. A missing piece always falls back to the still.
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CharacterExpression
{
    public string id;            // expression_id, e.g. U001_happy (unique; for lookups and for people reading the CSV).
    public string characterId;   // A customer ID (U001) or a dialogue speaker ID (e.g. "Visiting Chef").
    public string expression;    // neutral, happy, concerned, disappointed, tired (any name works).
    public string[] emotions = Array.Empty<string>(); // Dialogue emotions this image also covers (Pleased, Welcoming).
    public Sprite image;         // Whole character for this expression.
    public Sprite[] parts = Array.Empty<Sprite>(); // Optional same-size layers, back to front (body, face, hair...).
    public string videoPath;     // Optional loop, relative to Assets/StreamingAssets (e.g. Characters/U001_happy.mp4).
    public Color tint = Color.clear; // Optional (alpha 0 = unset): colour of the built-in silhouette for this expression.
    public bool HasArt => image != null || (parts != null && parts.Length > 0);
}

[CreateAssetMenu(fileName = "CharacterExpressionDatabase", menuName = "Blue Zone Bistro/Character Expressions")]
public class CharacterExpressionDatabase : ScriptableObject
{
    public const string Neutral = "neutral";
    // Rows for this character ID are the base for everyone: used when a character has no art of their own.
    // With an image they replace the silhouette for all guests; with no image, their tint colours the built-in one.
    public const string DefaultId = "Default";
    public List<CharacterExpression> expressions = new List<CharacterExpression>();

    public bool HasRows(string characterId) =>
        !string.IsNullOrWhiteSpace(characterId) && !Same(characterId, DefaultId) &&
        expressions.Exists(e => e != null && Same(e.characterId, characterId));

    public bool HasCharacter(string characterId) =>
        !string.IsNullOrWhiteSpace(characterId) && expressions.Exists(e => e != null && Same(e.characterId, characterId) && e.HasArt);

    // The row for a dialogue emotion: its own expression name, then a row listing it in `emotions`, then neutral,
    // then any row. Null when the character has no rows. requireArt=false also finds rows whose art is not made yet
    // (the placeholder silhouette uses it, so expression changes still show while the art is missing).
    public CharacterExpression Find(string characterId, string emotion, bool requireArt = true)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return null;
        var rows = expressions.FindAll(e => e != null && Same(e.characterId, characterId) && (!requireArt || e.HasArt));
        if (rows.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(emotion))
        {
            var exact = rows.Find(e => Same(e.expression, emotion));
            if (exact != null) return exact;
            var covered = rows.Find(e => e.emotions != null && Array.Exists(e.emotions, x => Same(x, emotion)));
            if (covered != null) return covered;
        }
        return rows.Find(e => Same(e.expression, Neutral)) ?? rows[0];
    }

    private static bool Same(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
