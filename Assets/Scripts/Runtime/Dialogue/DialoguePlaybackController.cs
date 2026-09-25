// File responsibility: Plays one eligible sequence in line order and completes effects exactly once at its end.
// Presentation consumes CurrentText/CurrentLine; advancing a line never recalculates a meal.

using System;
using System.Linq;

// Owns line position, not game progression. Effects commit only on the last Next.
public class DialoguePlaybackController
{
    private readonly DialogueService service;
    private DialogueRuntimeContext context;
    private DialogueLineData[] lines = Array.Empty<DialogueLineData>();
    public DialogueSequenceData Sequence { get; private set; }
    public int LineIndex { get; private set; }
    public int LineCount => lines.Length;
    public bool IsActive => Sequence != null;
    public DialogueLineData CurrentLine => IsActive ? lines[LineIndex] : null;
    public string CurrentText => IsActive ? service.ResolveText(CurrentLine, context) : string.Empty;

    public DialoguePlaybackController(DialogueService service) { this.service = service; }
    public bool Start(DialogueSequenceData sequence, DialogueRuntimeContext context)
    {
        if (IsActive || sequence == null) return false;
        lines = (sequence.lines ?? Array.Empty<DialogueLineData>())
            .Where(l => l != null && !string.IsNullOrWhiteSpace(l.textTemplate)).OrderBy(l => l.lineOrder).ToArray();
        if (lines.Length == 0) return false;
        this.context = context;
        Sequence = sequence;
        LineIndex = 0;
        return true;
    }
    // True only when a whole sequence has just completed.
    public bool Next()
    {
        if (!IsActive) return false;
        if (++LineIndex < lines.Length) return false;
        var completed = Sequence;
        Sequence = null;
        service.Complete(completed, context.currentDay);
        return true;
    }
}
