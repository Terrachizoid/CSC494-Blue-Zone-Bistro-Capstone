// File responsibility: Developer debug panel (F2) for reproducing situations quickly: start any event, end the day
// or jump to a day, add money or popularity, stock or clear the pantry, finish all lessons. IMGUI like the F3
// graphics panel, so it needs no scene setup. Only created in the editor and development builds
// (Debug.isDebugBuild); a release web build never contains it. The rules stay in GameDebugTools and GameLoop.
using System.Linq;
using UnityEngine;

public sealed class BistroDebugPanel : MonoBehaviour
{
    private bool open;
    private Vector2 scroll;
    private string status = "";
    private GUIStyle box, title, label, small, button;
    private GameBootstrap game;
    private BistroUIScreen screen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!Debug.isDebugBuild) return;
        var go = new GameObject("Debug panel (development builds)");
        DontDestroyOnLoad(go);
        go.AddComponent<BistroDebugPanel>();
    }

    private void Styles()
    {
        if (box != null) return;
        box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
        title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
        label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = Color.white } };
        small = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(1, 1, 1, .75f) } };
        button = new GUIStyle(GUI.skin.button) { fontSize = 12 };
    }

    private bool Find()
    {
        if (game == null) game = FindAnyObjectByType<GameBootstrap>();
        if (game != null && screen == null) screen = game.GetComponent<BistroUIScreen>();
        return game != null && game.Loop != null && game.DebugTools != null;
    }

    // After every shortcut: let lessons react and redraw the screen.
    private void Done(string message)
    {
        status = message;
        game.Loop.RefreshTutorials();
        if (screen != null) screen.MarkDirty();
    }

    private void OnGUI()
    {
        var e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F2) { open = !open; e.Use(); }
        if (!open) return;
        float scale = Mathf.Max(1f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        Styles();
        float h = Screen.height / scale, w = Screen.width / scale;
        var area = new Rect(w - 330, 10, 320, h - 20);
        GUI.color = new Color(0, 0, 0, .82f); GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(area.x + 8, area.y + 6, area.width - 16, area.height - 12));
        GUILayout.BeginHorizontal();
        GUILayout.Label("Debug panel (F2)", title);
        if (GUILayout.Button("Close", button, GUILayout.Width(60))) open = false;
        GUILayout.EndHorizontal();
        if (!Find() || game.IsMainMenu)
        {
            GUILayout.Label("Start or resume a game first.", label);
            GUILayout.EndArea();
            return;
        }
        var state = game.State;
        var tools = game.DebugTools;
        GUILayout.Label($"Day {state.currentDay}/{game.Balance.totalDays} · {game.Loop.Phase} · ${state.money:0.00} · popularity {state.popularity:0}", label);
        if (!string.IsNullOrEmpty(status)) GUILayout.Label(status, small);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("Days", title);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("End today", button)) EndDays(1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Go to day", label, GUILayout.Width(70));
        for (int day = state.currentDay + 1; day <= game.Balance.totalDays; day++)
            if (GUILayout.Button(day.ToString(), button, GUILayout.Width(32))) EndDays(day - state.currentDay);
        GUILayout.EndHorizontal();
        GUILayout.Label("Ends days without serving: rent, spoilage, events and saves run as usual.", small);

        GUILayout.Label("Money and popularity", title);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+$10", button)) { tools.AddMoney(10); Done("Added $10."); }
        if (GUILayout.Button("+$50", button)) { tools.AddMoney(50); Done("Added $50."); }
        if (GUILayout.Button("-$20", button)) { tools.AddMoney(-20); Done("Took $20."); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Popularity -100", button)) { tools.AddPopularity(-100); Done("Popularity -100 (guest count updates tomorrow)."); }
        if (GUILayout.Button("+100", button)) { tools.AddPopularity(100); Done("Popularity +100 (guest count updates tomorrow)."); }
        GUILayout.EndHorizontal();

        GUILayout.Label("Pantry", title);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Stock 4 of each type", button)) Done($"Added {tools.StockPantry(4)} green foods, 4 servings each.");
        if (GUILayout.Button("Empty pantry", button)) Done($"Removed {tools.ClearPantry()} batches.");
        GUILayout.EndHorizontal();

        GUILayout.Label("Lessons", title);
        if (GUILayout.Button("Finish all lessons", button)) { game.Loop.Tutorials?.CompleteAll(); Done("All lessons marked finished."); }

        GUILayout.Label("Events (start today)", title);
        var running = tools.ActiveEvents;
        GUILayout.Label(running.Count == 0 ? "None running." : "Running: " + string.Join(", ", running.Select(r => r.eventId)), small);
        if (running.Count > 0 && GUILayout.Button("End all events", button)) Done($"Ended {tools.EndEvents()} event(s).");
        foreach (var definition in tools.Events)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{definition.id}  ({definition.durationDays}d{(definition.easyMode ? "" : ", not in easy mode")})", small);
            if (GUILayout.Button("Start", button, GUILayout.Width(52)))
            {
                string error = tools.StartEvent(definition.id);
                Done(error ?? $"{definition.id} started: {definition.headline}");
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void EndDays(int count)
    {
        string message = "";
        for (int i = 0; i < count; i++)
        {
            var result = game.Loop.DebugEndDay();
            if (!result.success) { message = result.error ?? "Could not end the day."; break; }
            message = result.gameCompleted ? "Run complete." : $"Now day {game.State.currentDay}" + (result.rentCharged > 0 ? $" (rent ${result.rentCharged:0.##})." : ".");
            if (result.gameCompleted) break;
        }
        Done(message);
    }
}
