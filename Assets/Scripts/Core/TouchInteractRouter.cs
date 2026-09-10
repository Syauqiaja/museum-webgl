using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The overlay's Interaksi and ‹ › buttons, routed to whatever the player is standing in
    /// front of. Doorways and plaques register themselves as the player enters their trigger, so
    /// no doorway needs to be wired to the overlay by hand — which matters, because a museum
    /// doorway going unwired is a failure this project has already lived through.
    /// </summary>
    public class TouchInteractRouter : MonoBehaviour
    {
        /// <summary>The doorway the player is inside, or null.</summary>
        public static SceneTriggerPrompt CurrentPrompt { get; private set; }

        /// <summary>The plaque the player is standing at, or null.</summary>
        public static LessonReader CurrentReader { get; private set; }

        /// <summary>The lobby station (gong, gasing, stage) the player is standing at, or null.</summary>
        public static IInteractable CurrentInteractable { get; private set; }

        public static void Register(SceneTriggerPrompt prompt) => CurrentPrompt = prompt;

        public static void Register(LessonReader reader) => CurrentReader = reader;

        public static void Register(IInteractable interactable) => CurrentInteractable = interactable;

        /// <summary>Clears the registration only if this is still the one registered.</summary>
        public static void Unregister(IInteractable interactable)
        {
            if (ReferenceEquals(CurrentInteractable, interactable)) CurrentInteractable = null;
        }

        /// <summary>Clears the registration only if this is still the one registered.</summary>
        public static void Unregister(SceneTriggerPrompt prompt)
        {
            if (CurrentPrompt == prompt) CurrentPrompt = null;
        }

        /// <summary>Clears the registration only if this is still the one registered.</summary>
        public static void Unregister(LessonReader reader)
        {
            if (CurrentReader == reader) CurrentReader = null;
        }

        private void OnDestroy()
        {
            // Statics outlive a scene load; a stale prompt would be a destroyed object.
            CurrentPrompt = null;
            CurrentReader = null;
            CurrentInteractable = null;
        }

        /// <summary>UnityEvent target for the Interaksi button. A doorway wins over a station.</summary>
        public void Interact()
        {
            if (CurrentPrompt != null) CurrentPrompt.Enter();
            else CurrentInteractable?.Interact();
        }

        /// <summary>UnityEvent target for the › button.</summary>
        public void PageNext()
        {
            if (CurrentReader != null) CurrentReader.Next();
        }

        /// <summary>UnityEvent target for the ‹ button.</summary>
        public void PagePrev()
        {
            if (CurrentReader != null) CurrentReader.Prev();
        }
    }
}
