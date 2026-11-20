// File responsibility: idle chatter (2026-11-18). Short, non-blocking lines from the guide, separate from tutorials.
// Lines come from Resources/Idle/IdleChatter.csv: id, kind (click|idle), places (City;Store;Shelf;Sorting;Service;Results
// or Any), condition (optional name from IdleConditions; unknown or failing names drop the line), weight, seconds
// (blank = by length), text. The UI hands in where the player is; this service only picks a line.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum IdleKind { Click, Idle }

public class IdleLine
{
    public string id, condition, text;
    public IdleKind kind;
    public HashSet<string> places = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public float weight = 1, seconds;
    public bool Any => places.Contains("Any");
    // Opaque time: from the sheet, else by length (about 3–8 s).
    public float DisplaySeconds => seconds > 0 ? seconds : Math.Max(3f, Math.Min(8f, 2f + (text?.Length ?? 0) / 18f));
}

// What a condition may look at: a snapshot the UI builds each time a line is picked (nothing here is stored).
public class IdleContext
{
    public GameSessionState state;
    public MealPlanReport plan;
    public int totalDays;
    public string storeId, shelfId;                 // market only
    public int cartPackages; public float cartTotal;
    public int sortPlaced, sortTotal; public SortingResult lastSort;
    public string customerId, mealRecipeId;         // service only
    public CravingTag[] cravings = new CravingTag[0];
    public bool canMakeAny; public int guestsLeft;
    public ServeResult lastDish;                   // set while a dish result is showing
}

// Named checks, asked live when a line is picked (unlike tutorial flags, nothing is stored or kept updated).
// A condition may join several checks with '&'. Checks with a value use "name:value": store:S001, shelf:LUXURY,
// day:1, flag:tutorial.first_purchase, missing:Grain (the plan still needs it), craving:Sweet (current guest),
// recipe:R012 (the dish being built), planned:R012. Unknown names make the line never show.
public static class IdleConditions
{
    private static readonly Dictionary<string, Func<IdleContext, bool>> checks = new Dictionary<string, Func<IdleContext, bool>>(StringComparer.OrdinalIgnoreCase)
    {
        { "money_low", c => c.state.money < 5 },
        { "money_negative", c => c.state.money < 0 },
        { "money_rich", c => c.state.money >= 40 },
        { "pantry_empty", c => !c.state.inventory.Any(MealAllocation.Usable) },
        { "pantry_full", c => c.state.inventory.Count(MealAllocation.Usable) >= 10 },
        { "expiring", c => c.state.inventory.Any(e => MealAllocation.Usable(e) && e.daysRemaining == 1) },
        { "no_plan", c => c.plan == null || c.plan.Empty },
        { "plan_missing", c => c.plan != null && c.plan.Missing > 0 },
        { "plan_ready", c => c.plan != null && c.plan.Ready },
        { "plan_full", c => c.plan != null && c.plan.planned > 0 && c.plan.planned >= c.plan.limit },
        { "plan_room", c => c.plan != null && c.plan.planned > 0 && c.plan.planned < c.plan.limit },
        { "cart_empty", c => c.cartPackages == 0 },
        { "cart_some", c => c.cartPackages > 0 },
        { "cart_big", c => c.cartPackages >= 6 },
        { "cart_over_budget", c => c.cartPackages > 0 && c.cartTotal > c.state.money },
        { "sort_none", c => c.sortTotal > 0 && c.sortPlaced == 0 },
        { "sort_some", c => c.sortPlaced > 0 && c.sortPlaced < c.sortTotal },
        { "sort_all", c => c.sortTotal > 0 && c.sortPlaced >= c.sortTotal },
        { "sort_perfect", c => c.lastSort != null && c.lastSort.questionCount > 0 && c.lastSort.correctCount == c.lastSort.questionCount },
        { "sort_mistakes", c => c.lastSort != null && c.lastSort.correctCount < c.lastSort.questionCount },
        { "building", c => !string.IsNullOrEmpty(c.mealRecipeId) },
        { "not_building", c => !string.IsNullOrEmpty(c.customerId) && string.IsNullOrEmpty(c.mealRecipeId) && c.lastDish == null },
        { "guest_request", c => !string.IsNullOrEmpty(c.customerId) && c.cravings.Any(t => t != CravingTag.None) },
        { "guest_no_request", c => !string.IsNullOrEmpty(c.customerId) && !c.cravings.Any(t => t != CravingTag.None) },
        { "cant_cook", c => !string.IsNullOrEmpty(c.customerId) && !c.canMakeAny },
        { "last_guest", c => c.guestsLeft == 1 },
        { "many_guests", c => c.guestsLeft >= 4 },
        { "dish_built", c => c.lastDish != null && c.lastDish.recipeBuilt },
        { "dish_incomplete", c => c.lastDish != null && !c.lastDish.recipeBuilt },
        { "dish_healthy", c => c.lastDish != null && c.lastDish.healthDelta > 0 },
        { "first_day", c => c.state.currentDay == 1 },
        { "last_day", c => c.totalDays > 0 && c.state.currentDay >= c.totalDays },
        { "popularity_high", c => c.state.popularity >= 600 },
        { "popularity_low", c => c.state.popularity < 200 },
        { "store_closed", c => new MarketEffects(c.state).AnyStoreClosed },
    };
    private static readonly Dictionary<string, Func<IdleContext, string, bool>> valued = new Dictionary<string, Func<IdleContext, string, bool>>(StringComparer.OrdinalIgnoreCase)
    {
        { "store", (c, v) => string.Equals(c.storeId, v, StringComparison.OrdinalIgnoreCase) },
        { "shelf", (c, v) => string.Equals(c.shelfId, v, StringComparison.OrdinalIgnoreCase) },
        { "day", (c, v) => int.TryParse(v, out int d) && c.state.currentDay == d },
        { "flag", (c, v) => c.state.flags.HasFlag(v) },
        { "noflag", (c, v) => !c.state.flags.HasFlag(v) },
        { "missing", (c, v) => c.plan != null && Enum.TryParse(v, true, out FoodCategory f) && c.plan.needs.Any(n => n.category == f && n.Missing > 0) },
        { "craving", (c, v) => Enum.TryParse(v, true, out CravingTag t) && c.cravings.Contains(t) },
        { "recipe", (c, v) => string.Equals(c.mealRecipeId, v, StringComparison.OrdinalIgnoreCase) },
        { "planned", (c, v) => c.plan != null && c.plan.dishes.Any(d => string.Equals(d.recipe.id, v, StringComparison.OrdinalIgnoreCase)) },
    };
    private static IEnumerable<string> Parts(string condition) => (condition ?? "").Split('&').Select(p => p.Trim()).Where(p => p.Length > 0);
    public static bool Known(string condition) => Parts(condition).All(p =>
    {
        int colon = p.IndexOf(':');
        return colon < 0 ? checks.ContainsKey(p) : valued.ContainsKey(p.Substring(0, colon));
    });
    // Blank = always. Every part must hold. Unknown or throwing = false, so a broken condition only removes its own lines.
    public static bool Holds(string condition, IdleContext context)
    {
        if (context?.state == null) return string.IsNullOrWhiteSpace(condition);
        foreach (var part in Parts(condition))
        {
            try
            {
                int colon = part.IndexOf(':');
                if (colon < 0) { if (!checks.TryGetValue(part, out var check) || !check(context)) return false; }
                else { if (!valued.TryGetValue(part.Substring(0, colon), out var check) || !check(context, part.Substring(colon + 1).Trim())) return false; }
            }
            catch { return false; }
        }
        return true;
    }
}

public class IdleChatterService
{
    private readonly List<IdleLine> lines;
    private readonly Queue<string> recent = new Queue<string>();
    private const int RecentMemory = 6;
    public IReadOnlyList<IdleLine> Lines => lines;
    public IdleChatterService(IEnumerable<IdleLine> lines) { this.lines = (lines ?? Enumerable.Empty<IdleLine>()).Where(l => l != null && !string.IsNullOrWhiteSpace(l.text)).ToList(); }

    // A weighted random line for this kind and place, avoiding the last few shown. Null when none fits.
    public IdleLine Pick(IdleKind kind, string place, IdleContext context, Random random)
    {
        var options = lines.Where(l => l.kind == kind && (l.Any || l.places.Contains(place ?? "")) && l.weight > 0 && IdleConditions.Holds(l.condition, context)).ToList();
        var fresh = options.Where(l => !recent.Contains(l.id)).ToList();
        if (fresh.Count > 0) options = fresh;
        if (options.Count == 0) return null;
        double roll = (random ?? new Random()).NextDouble() * options.Sum(l => l.weight), total = 0;
        var chosen = options.Last();
        foreach (var line in options) { total += line.weight; if (roll < total) { chosen = line; break; } }
        recent.Enqueue(chosen.id); while (recent.Count > RecentMemory) recent.Dequeue();
        return chosen;
    }

    public static List<IdleLine> Parse(string csv)
    {
        var result = new List<IdleLine>();
        var rows = Rows(csv ?? "");
        if (rows.Count == 0) return result;
        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        Func<List<string>, string, string> get = (row, name) => { int i = header.IndexOf(name); return i >= 0 && i < row.Count ? row[i].Trim() : ""; };
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            IdleKind kind;
            if (!Enum.TryParse(get(row, "kind"), true, out kind)) continue;
            float weight, seconds;
            var line = new IdleLine { id = get(row, "id"), kind = kind, condition = get(row, "condition"), text = get(row, "text"),
                weight = float.TryParse(get(row, "weight"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out weight) ? weight : 1,
                seconds = float.TryParse(get(row, "seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out seconds) ? seconds : 0 };
            foreach (var place in get(row, "places").Split(';').Select(p => p.Trim()).Where(p => p.Length > 0)) line.places.Add(place);
            if (line.places.Count == 0) line.places.Add("Any");
            if (string.IsNullOrEmpty(line.id) || string.IsNullOrEmpty(line.text)) continue;
            if (!IdleConditions.Known(line.condition)) UnityEngine.Debug.LogWarning($"Idle line {line.id}: unknown condition '{line.condition}' (line never shows).");
            result.Add(line);
        }
        return result;
    }

    // Minimal CSV: commas, double-quoted fields with "" escapes, newlines inside quotes.
    private static List<List<string>> Rows(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = new List<string>();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
