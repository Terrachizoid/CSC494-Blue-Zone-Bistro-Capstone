// Fades a UI element in and out by its CanvasGroup: in over fadeIn seconds from startTime, fully visible, then out over
// `duration` seconds ending at endTime (unscaled time). The alpha is applied the moment it is set up and every frame, so
// a screen rebuilt mid-fade never flashes back to fully opaque (used by the plan hint and the guide's chatter).
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public sealed class BistroFade : MonoBehaviour
{
    public float startTime = float.NegativeInfinity, fadeIn;
    public float endTime;
    public float duration = 2f;
    private CanvasGroup group;
    public void Setup(float start, float fadeInSeconds, float end, float fadeOutSeconds)
    {
        startTime = start; fadeIn = fadeInSeconds; endTime = end; duration = fadeOutSeconds; Apply();
    }
    private void Awake() { group = GetComponent<CanvasGroup>(); }
    private void OnEnable() { Apply(); }
    private void Update() { Apply(); }
    private void LateUpdate() { Apply(); }
    private void Apply()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        if (group == null) return;
        float now = Time.unscaledTime;
        float fadeOut = Mathf.Clamp01((endTime - now) / Mathf.Max(.01f, duration));
        float fadeInAlpha = fadeIn <= 0 ? 1 : Mathf.Clamp01((now - startTime) / fadeIn);
        group.alpha = Mathf.Min(fadeOut, fadeInAlpha);
    }
}
