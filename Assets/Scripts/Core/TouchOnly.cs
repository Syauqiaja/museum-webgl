using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Deactivates its own GameObject unless the visitor chose the touch scheme. Every touch
    /// overlay in every scene carries one, so no scene needs a script that knows what an overlay
    /// contains.
    /// </summary>
    /// <remarks>
    /// Lives in Museum.Core so Egrang, Dakon and the museum can all use it. An unmade choice
    /// hides the overlay, which is the right default: the museum opened directly in the Editor is
    /// a desktop session.
    /// </remarks>
    public class TouchOnly : MonoBehaviour
    {
        private void Awake()
        {
            bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;
            if (!touch) gameObject.SetActive(false);
        }
    }
}
