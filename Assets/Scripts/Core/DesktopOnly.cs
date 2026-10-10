using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// Deactivates its own GameObject when the visitor chose the touch scheme. The mirror of
    /// <see cref="TouchOnly"/>, for HUD that only means something to a keyboard and mouse — the
    /// museum's KONTROL card being the reason it exists.
    /// </summary>
    /// <remarks>
    /// An unmade choice keeps the object, which is the right default for the same reason
    /// <see cref="TouchOnly"/>'s is the opposite one: <c>Unknown</c> reads as desktop everywhere
    /// in this client, so a scene opened straight from the Editor still shows the desktop HUD.
    /// </remarks>
    public class DesktopOnly : MonoBehaviour
    {
        private void Awake()
        {
            bool touch = SessionData.Instance != null && SessionData.Instance.IsTouch;
            if (touch) gameObject.SetActive(false);
        }
    }
}
