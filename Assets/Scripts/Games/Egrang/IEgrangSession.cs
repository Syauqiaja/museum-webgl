using System;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The race as seen from the scene: who is seated where, what the other racers just did, and a
    /// way to report what this player did.
    ///
    /// Declared here rather than in the networking assembly because `Museum.Net` references the game
    /// assemblies and not the other way round — the same shape Dakon uses. The offline scene runs
    /// with no session at all.
    /// </summary>
    public interface IEgrangSession
    {
        /// <summary>This client's session id.</summary>
        string LocalSessionId { get; }

        /// <summary>Seats known so far, as (sessionId, seat) pairs.</summary>
        System.Collections.Generic.IEnumerable<(string sessionId, int seat)> Seats { get; }

        /// <summary>Banked stride count for a session id, from the synced state. 0 if unseated or unknown.</summary>
        int StepUnitsOf(string sessionId);

        /// <summary>
        /// How long the lane is in strides, from the synced state. 0 before the state has arrived,
        /// and offline, where nothing has said what the server thinks a lane is worth.
        /// </summary>
        int FinishUnits { get; }

        /// <summary>
        /// The stilt a session is racing on, from the synced state. The server holds every racer's
        /// choice, which is what lets the results panel show all three rather than only this
        /// client's. Defaults to <see cref="EgrangStickShape.Persegi"/> for an unknown session —
        /// the same stilt the server defaults a racer row to.
        /// </summary>
        EgrangStickShape StickOf(string sessionId);

        /// <summary>
        /// The nickname a session joined the lobby under, or empty when it is not known. Names are
        /// what the race calls people on screen; a lane number is the fallback, not the label.
        /// </summary>
        string DisplayNameOf(string sessionId);

        /// <summary>Someone took a step: (sessionId, result, stepUnits banked after it).</summary>
        event Action<string, EgrangStepResult, int> StepTaken;

        /// <summary>Seating changed — a player joined, left, or the state first arrived.</summary>
        event Action SeatsChanged;

        /// <summary>The race ended. Places by session id, 0 for anyone who never finished.</summary>
        event Action<System.Collections.Generic.IReadOnlyDictionary<string, int>> RaceOver;

        /// <summary>
        /// Seconds left before steps count, as the server measured them. 0 means the race is open.
        /// The server sends the remainder rather than a wall-clock instant, so a device with a
        /// wrong clock still counts down correctly.
        /// </summary>
        event Action<float> CountdownChanged;

        /// <summary>
        /// Asks the server how long is left. The countdown is also broadcast when the race starts,
        /// but a client loading the game scene — or reconnecting — normally misses that.
        /// </summary>
        void RequestCountdown();

        /// <summary>Report a graded press.</summary>
        void SendStep(EgrangStepResult result);

        /// <summary>Report the chosen stilt.</summary>
        void SendStick(EgrangStickShape shape);

        /// <summary>
        /// Leave the race for good. Consented, so the server withdraws this racer at once and the
        /// other lanes keep running, rather than waiting on a socket that is still open under a
        /// scene that has already gone.
        /// </summary>
        void Leave();
    }
}
