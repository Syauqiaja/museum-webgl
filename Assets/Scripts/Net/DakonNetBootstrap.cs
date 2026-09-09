using System;
using Colyseus;
using Museum.Core;
using Museum.Games.Dakon;
using Museum.Net.State;
using UnityEngine;

namespace Museum.Net
{
    /// <summary>
    /// Reconnects the Dakon scene to the room the player was seated in, and hands the resulting
    /// session to the view.
    ///
    /// This is the far end of the lobby's handoff: the lobby's live room object died with its
    /// scene, but <see cref="SessionData"/> is still holding that room's reconnection token, so
    /// the player lands back in **the same seat on the same room** rather than joining a second
    /// one. If there is no token — the scene was opened directly, or the reconnect window
    /// expired — the view is left alone and starts its offline hotseat game, which is the right
    /// fallback for a museum kiosk with no server.
    ///
    /// Drop this on an object in the Dakon scene, above the view in execution order (it binds in
    /// Awake; DakonView decides in Start).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class DakonNetBootstrap : MonoBehaviour
    {
        [Tooltip("The view to drive. Found in the scene if left empty.")]
        [SerializeField] private DakonView view;

        private async void Awake()
        {
            // Inactive included on purpose: the view can be disabled in the scene and switched
            // on by whatever composes the board, and a null here would look like "no view" when
            // the real answer is "not enabled yet".
            DakonView target = view != null ? view : FindFirstObjectByType<DakonView>(FindObjectsInactive.Include);

            if (target == null)
            {
                Debug.LogWarning("DakonNetBootstrap: no DakonView in the scene.", this);
                return;
            }

            if (!target.gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    "DakonNetBootstrap: the DakonView object is disabled, so the board will not run. " +
                    "Enable it in the Dakon scene.", target);
            }

            // TEMP DIAG — remove once the empty-hand bug is closed.
            Debug.Log($"[DAKON-DIAG] Bootstrap sessionData={(SessionData.Instance == null ? "null" : "ok")} " +
                      $"canReconnect={(SessionData.Instance != null && SessionData.Instance.CanReconnect)} " +
                      $"roomName={SessionData.Instance?.RoomName}");

            // Claimed before anything that can yield or throw: this method is async, so
            // DakonView.Start runs while we are still working and would otherwise deal a local
            // hotseat hand. Every exit below either binds a session or releases the claim —
            // an unreleased one leaves the scene with no game at all, and a released one that
            // should have been a network game leaves both players in separate offline games,
            // each convinced it is their turn.
            target.ReserveForNetwork();

            try
            {
                // The lobby handed its live room over on the way out. Playing on that socket is
                // the normal path; the token reconnect below is for the case where the socket
                // really did die (a WebGL page reload), because the server only holds a seat
                // open for a session it saw leave. Reconnecting on top of a socket that is still
                // open gets the *new* connection closed (4002) and leaves this scene bound to a
                // room that never receives a patch — an empty board with no error to show for it.
                Room<DakonState> live = ColyseusNetManager.Instance != null
                    ? ColyseusNetManager.Instance.TakeLiveRoom<DakonState>("dakon")
                    : null;

                if (live != null)
                {
                    // TEMP DIAG — remove once the empty-hand bug is closed.
                    Debug.Log($"[DAKON-DIAG] Bootstrap took live room={live.RoomId} session={live.SessionId}");

                    target.Bind(new NetDakonSession(live));
                    return;
                }

                if (SessionData.Instance == null || !SessionData.Instance.CanReconnect)
                {
                    // Nobody sent us here from a lobby: leave the view to play locally.
                    target.ReleaseNetworkReservation();
                    return;
                }

                if (SessionData.Instance.RoomName != "dakon")
                {
                    Debug.LogWarning(
                        $"DakonNetBootstrap: the held seat is in a '{SessionData.Instance.RoomName}' room, " +
                        "not dakon — playing offline instead of reconnecting into the wrong game.", this);
                    target.ReleaseNetworkReservation();
                    return;
                }

                Room<DakonState> room = await ColyseusNetManager.Instance.Reconnect<DakonState>();

                if (room == null)
                {
                    target.ReleaseNetworkReservation();
                    return;
                }

                // TEMP DIAG — remove once the empty-hand bug is closed.
                Debug.Log($"[DAKON-DIAG] Bootstrap bound room={room.Name} roomId={room.RoomId} session={room.SessionId}");

                target.Bind(new NetDakonSession(room));
            }
            catch (Exception e)
            {
                // A dead room or an expired window is not a crash: the player still gets a game,
                // just a local one. Logged because online was what they asked for.
                Debug.LogWarning($"Dakon net setup failed — falling back to a local game: {e}", this);
                target.ReleaseNetworkReservation();
            }
        }
    }
}
