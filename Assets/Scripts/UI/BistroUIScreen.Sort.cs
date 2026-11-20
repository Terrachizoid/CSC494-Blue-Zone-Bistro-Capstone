// Shelf sorting (supervisor note 2026-11-13, added 2026-11-20). A collapsible bar above the aisle lets the player
// order the cards by price per serving, appeal or food category. Pressing the active option again reverses it.
// Changing the order rebuilds the shelf as usual; BistroSortSlide then slides each card from its old spot to the
// new one. Sorting is presentation only: prices, stock and checkout stay with the services.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public partial class BistroUIScreen
{
    private enum ShelfSort { Shelf, Price, Appeal, Category }
    private ShelfSort shelfSort = ShelfSort.Shelf;
    private bool shelfSortReversed;
    private bool sortBarOpen;
    private bool shelfSortChanged;
    // Where each card sat in the last shelf drawn (key: store/listing), so a new order can slide from there.
    private readonly Dictionary<string, Vector2> shelfSpots = new Dictionary<string, Vector2>();

    private static string SortName(ShelfSort sort) =>
        sort == ShelfSort.Price ? "Price per serving" : sort == ShelfSort.Appeal ? "Appeal" : sort == ShelfSort.Category ? "Food type" : "Shelf order";

    // Default directions: cheapest first, most appealing first, food types A to Z. Sold-out cards always go last.
    private List<StoreListingData> SortShelf(IEnumerable<StoreListingData> listings)
    {
        var list = listings.ToList();
        if (shelfSort == ShelfSort.Shelf) return shelfSortReversed ? Enumerable.Reverse(list).ToList() : list;
        var original = list.Select((l, i) => new { l, i }).ToDictionary(x => x.l, x => x.i);
        Func<StoreListingData, double> perServing = l => l.servingsPerPackage > 0 ? game.Queries.GetPrice(l).effectivePrice / (double)l.servingsPerPackage : double.MaxValue;
        IOrderedEnumerable<StoreListingData> ordered = list.OrderBy(l => game.Queries.Market.UnavailableReason(l) != null);
        switch (shelfSort)
        {
            case ShelfSort.Price:
                ordered = shelfSortReversed ? ordered.ThenByDescending(perServing) : ordered.ThenBy(perServing);
                break;
            case ShelfSort.Appeal:
                ordered = shelfSortReversed ? ordered.ThenBy(l => l.food.ingredientAppeal) : ordered.ThenByDescending(l => l.food.ingredientAppeal);
                break;
            default:
                Func<StoreListingData, string> type = l => IntroMealPlan.CategoryName(l.food.recipeCategory);
                ordered = shelfSortReversed ? ordered.ThenByDescending(type, StringComparer.OrdinalIgnoreCase) : ordered.ThenBy(type, StringComparer.OrdinalIgnoreCase);
                ordered = ordered.ThenBy(perServing);
                break;
        }
        return ordered.ThenBy(l => original[l]).ToList();
    }

    private string SortArrow(ShelfSort sort)
    {
        if (sort != shelfSort || sort == ShelfSort.Shelf && !shelfSortReversed) return "";
        bool up = sort == ShelfSort.Price || sort == ShelfSort.Category ? !shelfSortReversed : shelfSortReversed;
        return up ? " ↑" : " ↓";
    }

    // The bar: "Sort ▼" opens (▲▼↑↓ are in the body font; ▾▴ are not) the options; the active option is filled green.
    private void ShelfSortBar(Transform parent, Vector2 min, Vector2 max)
    {
        var bar = BistroWidgets.Box(parent, "Sort bar", Color.clear, min, max);
        bar.GetComponent<Image>().raycastTarget = false;
        SortPill(bar, (sortBarOpen ? "Sort ▲" : "Sort ▼") + (sortBarOpen || shelfSort == ShelfSort.Shelf ? "" : " · " + SortName(shelfSort) + SortArrow(shelfSort)),
            0, sortBarOpen ? .16f : .42f, false, () => sortBarOpen = !sortBarOpen);
        if (!sortBarOpen) return;
        var options = new[] { ShelfSort.Shelf, ShelfSort.Price, ShelfSort.Appeal, ShelfSort.Category };
        float x = .175f, width = (1f - x) / options.Length;
        foreach (var option in options)
        {
            var chosen = option;
            SortPill(bar, SortName(option) + SortArrow(option), x, x + width - .01f, option == shelfSort, () =>
            {
                if (chosen == shelfSort) shelfSortReversed = !shelfSortReversed;
                else { shelfSort = chosen; shelfSortReversed = false; }
                shelfSortChanged = true;
            });
            x += width;
        }
    }

    private Button SortPill(RectTransform bar, string label, float from, float to, bool active, Action action)
    {
        var fill = active ? BistroShelfStyle.Green : BistroShelfStyle.Cream;
        var ink = active ? BistroShelfStyle.Cream : BistroShelfStyle.Ink;
        var button = BistroShelfStyle.Button(bar, label, theme, new Vector2(from, 0), new Vector2(to, 1), fill, ink, () => Change(action));
        var text = button.GetComponentInChildren<Text>();
        if (text != null) { text.fontSize = 16; text.resizeTextForBestFit = true; text.resizeTextMinSize = 11; text.resizeTextMaxSize = 16; }
        return button;
    }

    // Call after the shelf cards and BistroShelfLayout exist: records where each card sits, and slides cards to
    // their new spots when the order was just changed.
    private void SlideShelfCards(RectTransform grid, List<KeyValuePair<RectTransform, string>> cards)
    {
        bool animate = shelfSortChanged && !theme.reducedMotion;
        shelfSortChanged = false;
        BistroSortSlide.Play(grid, cards, shelfSpots, animate, .5f);
    }
}

// Slides shelf cards from their previous grid spots to their new ones after a re-sort. Visual only: the grid
// layout still owns the final positions, and if it rebuilds mid-slide the cards simply land early.
public sealed class BistroSortSlide : MonoBehaviour
{
    private readonly List<(RectTransform card, Vector2 from, Vector2 to)> moves = new List<(RectTransform, Vector2, Vector2)>();
    private float started, seconds;

    public static void Play(RectTransform grid, List<KeyValuePair<RectTransform, string>> cards, Dictionary<string, Vector2> spots, bool animate, float seconds)
    {
        if (grid == null || cards == null || spots == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
        var slide = animate ? grid.gameObject.AddComponent<BistroSortSlide>() : null;
        foreach (var pair in cards)
        {
            if (pair.Key == null) continue;
            var to = pair.Key.anchoredPosition;
            if (slide != null && spots.TryGetValue(pair.Value, out var from) && (from - to).sqrMagnitude > 1f)
            {
                slide.moves.Add((pair.Key, from, to));
                pair.Key.anchoredPosition = from;
            }
            spots[pair.Value] = to;
        }
        if (slide == null) return;
        if (slide.moves.Count == 0) { Destroy(slide); return; }
        slide.started = Time.unscaledTime; slide.seconds = Mathf.Max(.05f, seconds);
    }

    private void LateUpdate()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - started) / seconds);
        float eased = 1f - (1f - t) * (1f - t) * (1f - t); // ease-out: quick start, gentle landing
        foreach (var move in moves)
            if (move.card != null) move.card.anchoredPosition = Vector2.LerpUnclamped(move.from, move.to, eased);
        if (t >= 1f) Destroy(this);
    }
}
