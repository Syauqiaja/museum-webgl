using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot alpha fade with LeanTween, for dim backdrops behind popup dialogs.
/// Plays on enable, so a caller only needs <c>overlay.SetActive(true)</c>.
/// Fades a CanvasGroup when present, otherwise the UI Graphic's colour alpha —
/// so it works whether the overlay is a plain Image or a grouped subtree.
/// Lives in the default assembly on purpose: LeanTween ships without an asmdef, so
/// asmdef-scoped game code (e.g. Museum.Games.Dakon) cannot call it directly.
/// See <see cref="PopupTween"/> for the dialog's own scale-pop, and
/// <see cref="LoopFadeTween"/> for a looping pulse.
/// </summary>
public class FadeTween : MonoBehaviour
{
    [Header("Alpha")]
    [Range(0f, 1f)] public float fromAlpha = 0f;
    [Tooltip("Alpha the overlay settles at. ~0.5-0.7 reads as a dark dim over the scene.")]
    [Range(0f, 1f)] public float toAlpha = 0.6f;

    [Header("Timing")]
    public float fadeInDuration = 0.25f;
    public float fadeInDelay = 0f;
    public LeanTweenType fadeInEase = LeanTweenType.easeOutSine;
    public float fadeOutDuration = 0.2f;
    public LeanTweenType fadeOutEase = LeanTweenType.easeInSine;

    [Header("Behaviour")]
    public bool playOnEnable = true;
    [Tooltip("Keep animating while Time.timeScale is 0 (e.g. a paused game).")]
    public bool ignoreTimeScale = true;

    CanvasGroup _group;
    bool _cached;

    void Awake()
    {
        Cache();
    }

    void Cache()
    {
        if (_cached) return;
        _group = GetComponent<CanvasGroup>();
        _cached = true;

        if (_group == null)
            Debug.LogWarning($"{name}: FadeTween found no CanvasGroup or Graphic to fade.", this);
    }

    void OnEnable()
    {
        if (playOnEnable) FadeIn();
    }

    void OnDisable()
    {
        LeanTween.cancel(gameObject);
    }

    /// <summary>Fades from <see cref="fromAlpha"/> up to <see cref="toAlpha"/>.</summary>
    public void FadeIn()
    {
        Cache();
        LeanTween.cancel(gameObject);
        SetAlpha(fromAlpha);
        Tween(toAlpha, fadeInDuration, fadeInEase).setDelay(fadeInDelay);
    }

    /// <summary>Fades back down to <see cref="fromAlpha"/>, then deactivates the GameObject.</summary>
    public void FadeOut()
    {
        FadeOut(deactivate: true);
    }

    /// <summary>Fades back down to <see cref="fromAlpha"/>; optionally leaves the object active.</summary>
    public void FadeOut(bool deactivate)
    {
        Cache();
        LeanTween.cancel(gameObject);
        Tween(fromAlpha, fadeOutDuration, fadeOutEase)
            .setOnComplete(() =>
            {
                if (deactivate) gameObject.SetActive(false);
            });
    }

    LTDescr Tween(float target, float duration, LeanTweenType ease)
    {
        float dur = Mathf.Max(0.0001f, duration);
        return LeanTween.value(gameObject, CurrentAlpha(), target, dur)
            .setEase(ease)
            .setIgnoreTimeScale(ignoreTimeScale)
            .setOnUpdate(SetAlpha);
    }

    float CurrentAlpha()
    {
        if (_group != null) return _group.alpha;
        return 0f;
    }

    void SetAlpha(float alpha)
    {
        if (_group != null) _group.alpha = alpha;
    }
}
