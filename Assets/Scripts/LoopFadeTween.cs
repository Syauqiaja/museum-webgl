using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loops a fade between two alpha values using LeanTween.
/// Targets a CanvasGroup, SpriteRenderer, or UI Graphic on the same GameObject.
/// </summary>
public class LoopFadeTween : MonoBehaviour
{
    public enum LoopMode
    {
        PingPong,
        Restart
    }

    [Header("Alpha")]
    [Range(0f, 1f)] public float fromAlpha = 1f;
    [Range(0f, 1f)] public float toAlpha = 0.2f;

    [Header("Timing")]
    public float duration = 1f;
    public float delay = 0f;
    public LeanTweenType easeType = LeanTweenType.easeInOutSine;

    [Header("Loop")]
    public LoopMode loopMode = LoopMode.PingPong;
    [Tooltip("Number of loops. -1 loops forever.")]
    public int loopCount = -1;
    public bool playOnEnable = true;

    private CanvasGroup canvasGroup;
    private SpriteRenderer spriteRenderer;
    private Graphic graphic;
    private int tweenId = -1;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        graphic = GetComponent<Graphic>();

        if (canvasGroup == null && spriteRenderer == null && graphic == null)
        {
            Debug.LogWarning($"{name}: LoopFadeTween found no CanvasGroup, SpriteRenderer, or Graphic to fade.", this);
        }
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        Stop();
        SetAlpha(fromAlpha);

        LTDescr tween = LeanTween.value(gameObject, fromAlpha, toAlpha, duration)
            .setDelay(delay)
            .setEase(easeType)
            .setOnUpdate(SetAlpha);

        if (loopMode == LoopMode.PingPong)
        {
            tween.setLoopPingPong(loopCount < 0 ? -1 : loopCount);
        }
        else
        {
            tween.setLoopCount(loopCount < 0 ? -1 : loopCount);
        }

        tweenId = tween.id;
    }

    public void Stop()
    {
        if (tweenId != -1)
        {
            LeanTween.cancel(gameObject, tweenId);
            tweenId = -1;
        }
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        if (graphic != null)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
