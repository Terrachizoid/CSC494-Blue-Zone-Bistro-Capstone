// Colour grade wiring (2026-11-22, LUT version). Scenery (city map, store interiors, bistro room and counter) drifts
// toward the mood of the ending the player is heading for: four LUT moods in the UI Theme (Blue, Popular, Niche,
// Struggling) blended by popularity and community wellbeing; at their starting values the art is exactly as drawn.
// Paper panels and rails get the same mood at theme.panelMoodStrength. Each guest follows their displayed health
// (health plus the rush/crash offset): health 50 is the original, lower is greyer, higher is more vibrant, never green.
// Materials persist across screen rebuilds and ease toward their targets in LateUpdate over theme.gradeSeconds.
// Text, food and recipe card art, and colour-coded buttons, rims and bins never use these materials.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public partial class BistroUIScreen
{
    private sealed class GradeState { public BistroColorGrade grade; public float shown = -1; }
    private GradeState sceneryGrade, panelGrade;
    private readonly Dictionary<string, GradeState> guestGrades = new Dictionary<string, GradeState>(StringComparer.OrdinalIgnoreCase);
    private bool gradeUnavailable;
    private Color[][] moodLuts;
    private readonly Color[] blendedLut = new Color[BistroLut.Count];
    private float lutU = float.NaN, lutV = float.NaN, popularityShown = -1;

    // Scenery images call this after their sprite is set.
    private void GradeScenery(Image image)
    {
        var state = SceneryGrade();
        if (image != null && state != null) image.material = state.grade.Material;
    }

    private GradeState SceneryGrade()
    {
        if (!theme.colorGrade || gradeUnavailable || game == null || game.IsMainMenu) return null;
        if (sceneryGrade == null)
        {
            var grade = BistroColorGrade.Create("Scenery grade");
            if (grade == null) { gradeUnavailable = true; return null; } // Shader missing or unsupported: plain art.
            sceneryGrade = new GradeState { grade = grade };
            StepGrade(sceneryGrade, SceneryTarget(), true);
        }
        return sceneryGrade;
    }

    // Paper panels, rails, plaques and the top bar take the scenery mood lightly (called after each rebuild). Only these.
    private void GradePanels()
    {
        if (screen == null || theme.panelMoodStrength <= 0 || SceneryGrade() == null) return;
        if (panelGrade == null)
        {
            var grade = BistroColorGrade.Create("Panel grade");
            if (grade == null) return;
            panelGrade = new GradeState { grade = grade };
            panelGrade.grade.Apply(false);
            panelGrade.grade.SetLut(blendedLut, theme.panelMoodStrength);
        }
        var surfaces = new HashSet<Sprite> { theme.panelPaper, theme.guideNotePanel, theme.planPanel, theme.suggestionFrame, theme.shelfRail,
            theme.plaqueLight, theme.plaqueDark };
        surfaces.Remove(null);
        foreach (var image in screen.GetComponentsInChildren<Image>(true))
            if (image.sprite != null ? surfaces.Contains(image.sprite) : image.gameObject.name == "Global toolbar")
                image.material = panelGrade.grade.Material;
    }

    // The material for one guest's portrait (null = draw as is).
    private Material GuestGrade(string customerId)
    {
        if (!theme.colorGrade || gradeUnavailable || string.IsNullOrWhiteSpace(customerId) || game.Queries.GetCustomer(customerId) == null) return null;
        if (!guestGrades.TryGetValue(customerId, out var state))
        {
            var grade = BistroColorGrade.Create("Guest grade " + customerId);
            if (grade == null) { gradeUnavailable = true; return null; }
            state = new GradeState { grade = grade };
            guestGrades[customerId] = state;
            // First seen: start from their health without the rush/crash offset, so the offset plays on screen.
            StepGrade(state, GuestTarget(customerId, false), true, customerId);
        }
        return state.grade.Material;
    }

    private float SceneryTarget() => game.Balance != null ? game.Queries.GetCommunityVisualScore(game.Balance) : .5f;
    private float PopularityTarget() => game.Balance != null && game.Balance.maximumPopularity > 0
        ? Mathf.Clamp01(game.State.popularity / game.Balance.maximumPopularity) : .3f;

    // Displayed health, 0-1: health plus the rush / crash offset (BalanceConfig.rushVisualBoost / crashVisualDrop).
    private float GuestTarget(string customerId, bool withOffset = true)
    {
        var cs = game.State.customers.Find(c => string.Equals(c.customerId, customerId, StringComparison.OrdinalIgnoreCase));
        if (cs == null) return .5f;
        return Mathf.Clamp01((cs.health + (withOffset ? cs.visualScore : 0)) / 100f);
    }

    // A guest's grade only moves while the player can see them, after a short beat (UX choice, 2026-11-22): the
    // stats change the moment a dish is served, but the guest visibly brightens or fades on the dish result, and a
    // crash shows as a drop after they arrive, instead of happening off screen behind the cooking panel.
    private string visibleGuest;
    private float visibleSince;
    private void NoteGuestVisible(string customerId)
    {
        if (string.IsNullOrEmpty(customerId)) { visibleGuest = null; return; }
        if (!string.Equals(customerId, visibleGuest, StringComparison.OrdinalIgnoreCase))
        { visibleGuest = customerId; visibleSince = Time.unscaledTime; }
    }

    private void StepGrade(GradeState state, float target, bool snap, string customerId = null)
    {
        float k = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 4f / Mathf.Max(.05f, theme.gradeSeconds));
        state.shown = snap || state.shown < 0 ? target : Mathf.Lerp(state.shown, target, k);
        bool live = Application.isEditor; // Inspector edits show at once while tuning.
        if (customerId == null)
        {
            float popularity = PopularityTarget();
            popularityShown = snap || popularityShown < 0 ? popularity : Mathf.Lerp(popularityShown, popularity, k);
            var balance = game.Balance;
            float wellbeingStart = balance != null && balance.maximumCommunityWellbeing > 0
                ? balance.startingCommunityWellbeing / balance.maximumCommunityWellbeing : .5f;
            float popularityStart = balance != null && balance.maximumPopularity > 0
                ? balance.startingPopularity / balance.maximumPopularity : .3f;
            float u = MoodPosition(popularityShown, popularityStart, theme.popularityMoodAtZero, theme.popularityMoodAtStart, theme.popularityMoodAtMax);
            float v = MoodPosition(state.shown, wellbeingStart, theme.wellbeingMoodAtZero, theme.wellbeingMoodAtStart, theme.wellbeingMoodAtMax);
            if (live || moodLuts == null)
                moodLuts = new[] { BistroLut.Build(theme.moodBlue), BistroLut.Build(theme.moodPopular),
                    BistroLut.Build(theme.moodNiche), BistroLut.Build(theme.moodStruggling) };
            if (live || float.IsNaN(lutU) || Mathf.Abs(u - lutU) > .002f || Mathf.Abs(v - lutV) > .002f)
            {
                lutU = u; lutV = v;
                BistroLut.Blend(blendedLut, moodLuts[0], moodLuts[1], moodLuts[2], moodLuts[3], u, v);
                state.grade.SetLut(blendedLut, 1);
                panelGrade?.grade.SetLut(blendedLut, theme.panelMoodStrength);
            }
            state.grade.Apply(live);
        }
        else
        {
            // Health 50 is the original; guestGradeStrength pushes the look further from it so changes read clearly.
            float value = Mathf.Clamp01(.5f + (state.shown - .5f) * theme.guestGradeStrength);
            state.grade.Apply(live, new BistroGradeLayer(theme.guestLow, theme.guestStart, theme.guestHigh, .5f, value));
        }
    }

    // 0-1 value -> position between the moods (-1..1): atZero at 0, atStart at the start value, atMax at 1, linear between.
    private static float MoodPosition(float value, float start, float atZero, float atStart, float atMax)
    {
        start = Mathf.Clamp(start, .01f, .99f);
        return value >= start ? Mathf.Lerp(atStart, atMax, (value - start) / (1 - start)) : Mathf.Lerp(atZero, atStart, value / start);
    }

    // Called every frame from LateUpdate; cheap when nothing changed (textures are rebuilt only on change).
    private void TickGrade()
    {
        if (gradeUnavailable || game == null || game.IsMainMenu) return;
        bool snap = theme.reducedMotion || theme.gradeSeconds <= 0;
        if (sceneryGrade != null) StepGrade(sceneryGrade, SceneryTarget(), snap);
        foreach (var pair in guestGrades)
            if (string.Equals(pair.Key, visibleGuest, StringComparison.OrdinalIgnoreCase) &&
                Time.unscaledTime - visibleSince >= theme.guestGradeDelay)
                StepGrade(pair.Value, GuestTarget(pair.Key), snap, pair.Key);
    }

    // On destroy, and whenever the main menu shows (2026-11-22 audit): a new game or a loaded save then starts every
    // grade from its own values instead of easing over from the previous run's colours.
    private void ReleaseGrades()
    {
        sceneryGrade?.grade.Release(); sceneryGrade = null;
        panelGrade?.grade.Release(); panelGrade = null;
        foreach (var state in guestGrades.Values) state.grade.Release();
        guestGrades.Clear();
        lutU = lutV = float.NaN; popularityShown = -1; visibleGuest = null; // a new scenery grade must receive its LUT
    }
}
