using Museum.Core;
using UnityEngine;

/// <summary>
/// Starts a museum screen when the player walks into its trigger and stops it on the way out.
/// The screen itself streams from the CDN — see <see cref="StreamedVideoScreen"/>.
/// </summary>
/// <remarks>
/// Left in the global namespace on purpose: eighteen scene instances serialize this type by
/// name, and moving it into <c>Museum.Core</c> would break every one of those references.
/// </remarks>
[RequireComponent(typeof(Collider))]
public class VideoTriggerPlayer : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private StreamedVideoScreen screen;

    private void Awake()
    {
        if (screen == null)
        {
            screen = transform.parent.GetComponentInChildren<StreamedVideoScreen>(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (screen == null || !other.CompareTag(playerTag)) return;
        screen.RequestPlay();
    }

    private void OnTriggerExit(Collider other)
    {
        if (screen == null || !other.CompareTag(playerTag)) return;
        screen.RequestStop();
    }
}
