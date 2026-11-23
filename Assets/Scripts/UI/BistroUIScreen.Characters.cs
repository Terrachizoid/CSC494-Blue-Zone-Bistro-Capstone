// Service stage: who stands behind the counter and with which face. Expressions follow the dialogue: each line's
// emotion picks the art (CharacterExpressionDatabase), a blank emotion keeps the current face, and the Narrator keeps
// whoever is standing there. While the dish result shows, the guest already wears the first emotion of the closing
// dialogue they are about to speak (GameLoop.UpcomingGuestLine), so one rule covers reactions too.
// A new guest slides in; the one before slides out (served or skipped alike). Art falls back to the customer's old
// portrait, then to the silhouette, so the stage works before any expression art exists.
using UnityEngine;
public partial class BistroUIScreen
{
    private const string NarratorId = "Narrator";
    private string stageCharacter, stageEmotion, stageKey, leavingCharacter;
    private BistroCharacterPortrait.Look stageLook, stagePreviousLook, leavingLook;
    private float stageChangedAt = -99, stageArrivedAt = -99, leavingAt = -99;
    private BistroCharacterVideoCache characterVideos;

    // A new phase starts with an empty stage, so the first guest of the next service slides in.
    private void ResetStage()
    {
        stageCharacter = stageEmotion = stageKey = leavingCharacter = null;
        NoteGuestVisible(null);
        stageLook = stagePreviousLook = leavingLook = null;
        stageChangedAt = stageArrivedAt = leavingAt = -99;
    }

    private bool IsCharacter(string id) =>
        !string.IsNullOrWhiteSpace(id) && id != NarratorId &&
        (game.Queries.GetCustomer(id) != null || game.Expressions != null && game.Expressions.HasRows(id));

    private void CharacterStage(RectTransform person)
    {
        var service = game.Loop.Service;
        var playback = game.Loop.Dialogue;
        var line = playback != null && playback.IsActive ? playback.CurrentLine : null;
        string character, emotion = null;
        Sprite overrideArt = null;
        if (line != null && IsCharacter(line.speakerId)) { character = line.speakerId; emotion = line.emotion; overrideArt = line.portrait; }
        else if (line != null)
        {
            // Narrator: the current guest, or a story speaker who is still on stage (a guest who already left goes).
            character = service.CurrentCustomerId ?? (game.Queries.GetCustomer(stageCharacter) == null ? stageCharacter : null);
            overrideArt = line.portrait;
        }
        else
        {
            character = service.CurrentCustomerId;
            if (service.awaitingDishAcknowledgement)
            {
                var upcoming = game.Loop.UpcomingGuestLine;
                emotion = upcoming?.emotion; overrideArt = upcoming?.portrait;
            }
        }
        if (string.IsNullOrWhiteSpace(emotion) && character == stageCharacter) emotion = stageEmotion; // Sticky.
        var look = ResolveLook(character, emotion, overrideArt, out string key);

        // The guest is visible unless the cooking workspace covers the room (meal selection, no dialogue).
        bool covered = line == null && !service.awaitingDishAcknowledgement;
        NoteGuestVisible(covered ? null : character);
        float now = Time.unscaledTime;
        if (character != stageCharacter)
        {
            bool someoneLeaves = stageCharacter != null && stageLook != null;
            if (someoneLeaves) BistroAudio.Play("guest.leave");
            if (character != null && look != null) BistroAudio.Play("guest.arrive");
            if (someoneLeaves) { leavingLook = stageLook; leavingAt = now; leavingCharacter = stageCharacter; }
            stageCharacter = character; stageEmotion = emotion; stageLook = look; stageKey = key; stagePreviousLook = null;
            stageArrivedAt = now + (someoneLeaves ? .2f : 0); stageChangedAt = -99;
        }
        else
        {
            if (key != stageKey) { stagePreviousLook = stageLook; stageLook = look; stageKey = key; stageChangedAt = now; }
            stageEmotion = emotion;
        }

        if (characterVideos == null) characterVideos = GetComponent<BistroCharacterVideoCache>();
        if (characterVideos == null) characterVideos = gameObject.AddComponent<BistroCharacterVideoCache>();
        if (leavingLook != null && now - leavingAt < BistroCharacterPortrait.SlideOutSeconds)
            BistroCharacterPortrait.Add(person, "Leaving guest", leavingLook, null, -99, -99, leavingAt, theme.reducedMotion, characterVideos, GuestGrade(leavingCharacter));
        if (look != null)
            BistroCharacterPortrait.Add(person, "Guest", look, stagePreviousLook, stageChangedAt, stageArrivedAt, float.NaN, theme.reducedMotion, characterVideos, GuestGrade(character));
    }

    // The line's own portrait (an explicit override), else expression art, else the customer's single portrait. Null = no art.
    private BistroCharacterPortrait.Look ResolveLook(string character, string emotion, Sprite overrideArt, out string key)
    {
        key = null;
        if (character == null) return null;
        if (overrideArt != null) { key = character + "/art:" + overrideArt.GetInstanceID(); return new BistroCharacterPortrait.Look { image = overrideArt }; }
        var expressions = game.Expressions;
        var row = expressions != null ? expressions.Find(character, emotion) : null;
        if (row != null)
        {
            // Keyed by the face the emotion asks for, even while it falls back to another image (e.g. only neutral is
            // drawn yet): a face change still bounces, and nothing changes once the real image exists.
            var wanted = expressions.Find(character, emotion, false);
            key = character + "/" + (wanted != null ? wanted.expression : row.expression);
            return new BistroCharacterPortrait.Look { image = row.image, parts = row.parts, video = row.videoPath };
        }
        var customer = game.Queries.GetCustomer(character);
        if (customer?.portrait != null) { key = character + "/portrait"; return new BistroCharacterPortrait.Look { image = customer.portrait }; }
        if (customer == null && (expressions == null || !expressions.HasRows(character))) return null;
        // No art of their own: the base "Default" rows of the CSV. The expression the emotion maps to is kept (art or
        // not) so an expression change still bounces and cross-fades before the art exists.
        var placeholderRow = expressions != null ? expressions.Find(character, emotion, false) : null;
        string expression = placeholderRow != null ? placeholderRow.expression : Neutral(emotion);
        var baseArt = expressions != null ? expressions.Find(CharacterExpressionDatabase.DefaultId, expression) : null;
        if (baseArt != null)
        {
            key = character + "/default/" + expression;
            return new BistroCharacterPortrait.Look { image = baseArt.image, parts = baseArt.parts, video = baseArt.videoPath };
        }
        var baseRow = expressions != null ? expressions.Find(CharacterExpressionDatabase.DefaultId, expression, false) : null;
        key = character + "/silhouette/" + expression;
        return new BistroCharacterPortrait.Look { silhouette = true,
            silhouetteTint = baseRow != null && baseRow.tint.a > 0 ? baseRow.tint : SilhouetteNeutral };
    }

    private static string Neutral(string emotion) => string.IsNullOrWhiteSpace(emotion) ? CharacterExpressionDatabase.Neutral : emotion.Trim().ToLowerInvariant();

    // Only when the CSV has no Default row for the expression: the original silhouette colour.
    private static readonly Color SilhouetteNeutral = new Color(.30f, .39f, .36f);
}
