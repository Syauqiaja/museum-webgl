using UnityEngine;

/// <summary>
/// Pops a UI panel in with a LeanTween scale-overshoot + fade, and (optionally) out again.
/// Plays automatically when the GameObject is enabled, so callers only need
/// <c>panel.SetActive(true)</c> — no reference to this component required.
/// Lives in the default assembly on purpose: LeanTween ships without an asmdef, so
/// asmdef-scoped game code (e.g. Museum.Games.Dakon) cannot call it directly.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PopupTween : MonoBehaviour
{
    [Header("Show")]
    [Tooltip("Scale the panel starts from before overshooting to its authored scale.")]
    [Range(0f, 1.5f)] public float fromScale = 0.6f;
    public float showDuration = 0.35f;
    public float showDelay = 0f;
    public LeanTweenType showEase = LeanTweenType.easeOutBack;
    [Tooltip("Fade the CanvasGroup from 0 to 1 alongside the scale.")]
    public bool fadeIn = true;
    [Tooltip("Fade takes this fraction of showDuration, so alpha lands before the overshoot settles.")]
    [Range(0.1f, 1f)] public float fadeDurationRatio = 0.6f;

    [Header("Hide")]
    [Range(0f, 1.5f)] public float toScale = 0.7f;
    public float hideDuration = 0.2f;
    public LeanTweenType hideEase = LeanTweenType.easeInBack;

    [Header("Behaviour")]
    public bool playOnEnable = true;
    [Tooltip("Keep animating while Time.timeScale is 0 (e.g. a paused game).")]
    public bool ignoreTimeScale = true;

    CanvasGroup _group;
    Vector3 _baseScale;
    bool _captured;

    void Awake()
    {
        CaptureBase();
    }

    void CaptureBase()
    {
        if (_captured) return;
        _group = GetComponent<CanvasGroup>();
        _baseScale = transform.localScale;
        _captured = true;
    }

    void OnEnable()
    {
        if (playOnEnable) Show();
    }

    void OnDisable()
    {
        LeanTween.cancel(gameObject);
    }

    /// <summary>Plays the pop-in from scratch.</summary>
    public void Show()
    {
        CaptureBase();
        LeanTween.cancel(gameObject);

        transform.localScale = _baseScale * fromScale;
        LeanTween.scale(gameObject, _baseScale, Mathf.Max(0.0001f, showDuration))
            .setDelay(showDelay)
            .setEase(showEase)
            .setIgnoreTimeScale(ignoreTimeScale);

        if (!fadeIn || _group == null) return;

        _group.alpha = 0f;
        LeanTween.alphaCanvas(_group, 1f, Mathf.Max(0.0001f, showDuration * fadeDurationRatio))
            .setDelay(showDelay)
            .setEase(LeanTweenType.easeOutSine)
            .setIgnoreTimeScale(ignoreTimeScale);
    }

    /// <summary>Plays the pop-out, then deactivates the GameObject.</summary>
    public void Hide()
    {
        CaptureBase();
        LeanTween.cancel(gameObject);

        float dur = Mathf.Max(0.0001f, hideDuration);
        LeanTween.scale(gameObject, _baseScale * toScale, dur)
            .setEase(hideEase)
            .setIgnoreTimeScale(ignoreTimeScale)
            .setOnComplete(() =>
            {
                transform.localScale = _baseScale;
                if (_group != null) _group.alpha = 1f;
                gameObject.SetActive(false);
            });

        if (_group != null)
        {
            LeanTween.alphaCanvas(_group, 0f, dur)
                .setEase(LeanTweenType.easeInSine)
                .setIgnoreTimeScale(ignoreTimeScale);
        }
    }
}
