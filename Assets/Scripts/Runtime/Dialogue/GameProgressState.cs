// File responsibility: Legacy standalone dialogue-demo state used by DialogueRuntimeExample.
// The playable bistro uses GameSessionState; do not instantiate this as a second live progression source.

using System;

[Serializable]
public class GameProgressState
{
    public int currentDay = 1;
    public GameFlagState flags = new GameFlagState();
    public DialogueHistoryState dialogueHistory = new DialogueHistoryState();
}
