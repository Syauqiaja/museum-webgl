using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Marks a button that makes no click or hover sound (<see cref="UiSelectableSound"/> checks
    /// for it when the sound would play). For a button pressed over and over as part of play —
    /// Egrang's JALAN, a stride per press — where a click on every press is noise, not feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SilentButton : MonoBehaviour
    {
    }
}
