using System;
using Colyseus;
using Museum.Core;
using Museum.Games.Egrang;
using Museum.Net.State;
using UnityEngine;

namespace Museum.Net
{
    /// <summary>
    /// Reconnects the Egrang scene to the room the player was seated in, and hands the resulting
    /// session to the race.
    ///
    /// The Egrang counterpart of <see cref="DakonNetBootstrap"/>, and deliberately the same shape:
    /// the lobby's room object died with its scene, but <see cref="SessionData"/> still holds that
    /// room's reconnection token, so the player lands back in the same lane on the same race rather
    /// than joining a second one. With no token — scene opened directly, or the window expired —
    /// the race is left alone and runs offline in lane 1, which is what the editor and a kiosk with
    /// no server both need.
    ///
    /// Drop this on an object in the Egrang scene, above the race in execution order (it binds in
    /// Awake; EgrangRace reads its session from Start onward).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class EgrangNetBootstrap : MonoBehaviour
    {
        [Tooltip("The race to drive. Found in the scene if left empty.")]
        [SerializeField] private EgrangRace race;

        private async void Awake()
        {
            // Inactive included: the race object is commonly parked under a disabled run root and
            // switched on by the stick selection, and a null here would read as "no race" when the
            // real answer is "not enabled yet".
            EgrangRace target = race != null
                ? race
                : FindFirstObjectByType<EgrangRace>(FindObjectsInactive.Include);

            if (target == null)
            {
                Debug.LogWarning("EgrangNetBootstrap: no EgrangRace in the scene.", this);
                return;
            }

            // Claimed before anything that can yield or throw. This method is async, so
            // EgrangRace.Start runs while we are still working, and without the claim it would
            // open an offline race — lane 1, its own picking window — that the room then has to
            // replace mid-countdown.
            target.ReserveForNetwork();

            try
            {
                // The lobby handed its live room over on the way out; racing on that socket is
                // the normal path. The token reconnect below is only correct once the old socket
                // is actually gone (a WebGL page reload) — the server holds a seat open for a
                // session it saw leave, and refuses a second connection for one that never did.
                // See ColyseusNetManager.TakeLiveRoom.
                Room<EgrangState> live = ColyseusNetManager.Instance != null
                    ? ColyseusNetManager.Instance.TakeLiveRoom<EgrangState>("egrang")
                    : null;

                if (live != null)
                {
                    target.Bind(new NetEgrangSession(live));
                    return;
                }

                if (SessionData.Instance == null || !SessionData.Instance.CanReconnect)
                {
                    // Nobody sent us here from a lobby: let the race run offline.
                    target.ReleaseNetworkReservation();
                    return;
                }

                if (SessionData.Instance.RoomName != "egrang")
                {
                    Debug.LogWarning(
                        $"EgrangNetBootstrap: the held seat is in a '{SessionData.Instance.RoomName}' room, " +
                        "not egrang — running offline instead of reconnecting into the wrong game.", this);
                    target.ReleaseNetworkReservation();
                    return;
                }

                Room<EgrangState> room = await ColyseusNetManager.Instance.Reconnect<EgrangState>();

                if (room == null)
                {
                    target.ReleaseNetworkReservation();
                    return;
                }

                target.Bind(new NetEgrangSession(room));
            }
            catch (Exception e)
            {
                // A dead room or an expired window is not a crash: the player still gets a race,
                // just a local one. Logged because online was what they asked for.
                Debug.LogWarning($"Egrang net setup failed — racing locally instead: {e}", this);
                target.ReleaseNetworkReservation();
            }
        }
    }
}
