using UnityEngine;

/// <summary>
/// Clears LeanTween's static state when Play mode starts.
///
/// This project enters Play mode with <c>DisableDomainReload</c>, so statics survive between
/// sessions. LeanTween's <see cref="LeanTween.init(int,int)"/> only builds its updater when
/// <c>tweens == null</c>, but the "~LeanTween" GameObject that pumps <c>LeanTween.update()</c>
/// dies with the previous Play session. From the second Play onwards LeanTween therefore holds a
/// full tween array and no updater: every tween is created and never advances.
///
/// That reads as a rendering bug rather than an animation one, because <see cref="PopupTween"/>
/// sets the panel to alpha 0 and a shrunken scale *before* handing the animation to LeanTween —
/// so an active panel simply never appears. Symptoms seen: the museum's Enter prompt invisible,
/// the lobby showing only its background. A separate process (a Multiplayer Play Mode virtual
/// player, or a build) gets a fresh domain and is unaffected, which is what makes it look like
/// one client is broken.
///
/// Lives in the default assembly because LeanTween ships without an asmdef.
/// </summary>
public static class LeanTweenPlayModeReset
{
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        // Nulls the tween array and drops the stale updater, so the next tween re-inits both.
        LeanTween.reset();
    }
#endif
}
