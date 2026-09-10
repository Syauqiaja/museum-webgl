using UnityEngine;
using UnityEngine.EventSystems;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The pause panel's one piece of behaviour: while it is up, the timing bar takes no presses.
    /// Put this on the <c>Pause Panel</c> root (the scrim), which is what the gear, the scrim and
    /// Lanjutkan switch on and off — so the panel's own enable state is the pause state, and there
    /// is no flag anywhere else to fall out of step with it.
    ///
    /// It does not stop the race. Egrang is real-time and its clock is the server's; the other
    /// racers keep walking, and offline the local countdown runs on unscaled time on purpose. What
    /// the menu must prevent is the scrim swallowing clicks while Space still reaches the bar
    /// through the Input System, which would walk the racer behind a menu the player is reading.
    ///
    /// Same shape as Dakon's pause panel otherwise: open/close are plain <c>SetActive</c>
    /// persistent listeners, and the exit is <see cref="EgrangRace.BackToMuseum"/>.
    /// </summary>
    public sealed class EgrangPauseMenu : MonoBehaviour
    {
        [Tooltip("The live timing bar — the one EgrangRace.bar points at, not the orphaned 'Bar' " +
                 "under the inactive Canvas (see scene-setup.md).")]
        [SerializeField] private SkillCheckBar bar;

        /// <summary>Wiring from code — used by the tests.</summary>
        public void Configure(SkillCheckBar bar)
        {
            if (this.bar != null && isActiveAndEnabled) this.bar.InputBlocked = false;

            this.bar = bar;

            if (this.bar != null && isActiveAndEnabled) this.bar.InputBlocked = true;
        }

        void OnEnable()
        {
            // The bar is usually still asleep under the inactive run root while the stilts are
            // being picked. The flag is a plain property, so it holds across the bar waking up.
            if (bar != null) bar.InputBlocked = true;
        }

        void OnDisable()
        {
            if (bar != null) bar.InputBlocked = false;

            // Whatever was clicked to get here (the gear, Lanjutkan) is still the EventSystem's
            // selection, and a key press after the menu closes should go to the race, not to a
            // button that was under the scrim.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
