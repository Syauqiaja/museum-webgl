using System;
using System.Collections.Generic;
using Colyseus;
using Museum.Games.Egrang;
using Museum.Net.State;
using UnityEngine;

namespace Museum.Net
{
    /// <summary>
    /// `IEgrangSession` over a live Colyseus room. Follows the same idiom as
    /// <see cref="NetDakonSession"/>: whole-state `OnStateChange` plus typed messages, no callback
    /// proxy, since the state is small enough that diffing it wholesale costs nothing.
    /// </summary>
    public sealed class NetEgrangSession : IEgrangSession
    {
        readonly Room<EgrangState> _room;
        readonly List<(string sessionId, int seat)> _seats = new List<(string, int)>();

        /// <summary>Banked stride count per session id, mirrored from `state.racers` on every patch —
        /// read by <see cref="StepUnitsOf"/> so a reconnecting client can place racers from it.</summary>
        readonly Dictionary<string, int> _stepUnitsBySession = new Dictionary<string, int>();

        /// <summary>Nickname per session id, mirrored from `state.players` on every patch.</summary>
        readonly Dictionary<string, string> _namesBySession = new Dictionary<string, string>();

        /// <summary>Chosen stilt per session id, mirrored from `state.racers` on every patch — the
        /// results panel names every racer's stilt, not only the local one.</summary>
        readonly Dictionary<string, EgrangStickShape> _stickBySession = new Dictionary<string, EgrangStickShape>();

        /// <summary>Lane length in strides, mirrored from `state.finishUnits`.</summary>
        int _finishUnits;

        public NetEgrangSession(Room<EgrangState> room)
        {
            _room = room;

            _room.OnStateChange += (_, __) => ReadSeats();
            _room.OnMessage<StepTakenPayload>("step_taken", OnStepTaken);
            _room.OnMessage<GameOverPlacesPayload>("game_over", OnGameOver);
            _room.OnMessage<CountdownPayload>("countdown", OnCountdown);
            _room.OnMessage<ErrorPayload>("error", OnError);

            ReadSeats();
        }

        public string LocalSessionId => _room.SessionId;

        public IEnumerable<(string sessionId, int seat)> Seats => _seats;

        public event Action<string, EgrangStepResult, int> StepTaken;
        public event Action SeatsChanged;
        public event Action<IReadOnlyDictionary<string, int>> RaceOver;
        public event Action<float> CountdownChanged;

        public void SendStep(EgrangStepResult result) => _room.Send("step", new { result = (int)result });

        public void SendStick(EgrangStickShape shape) => _room.Send("choose_stick", new { shape = (int)shape });

        public void RequestCountdown() => _room.Send("countdown_sync", new { });

        public int FinishUnits => _finishUnits;

        public EgrangStickShape StickOf(string sessionId) =>
            !string.IsNullOrEmpty(sessionId) && _stickBySession.TryGetValue(sessionId, out EgrangStickShape shape)
                ? shape
                : EgrangStickShape.Persegi;

        public int StepUnitsOf(string sessionId) =>
            !string.IsNullOrEmpty(sessionId) && _stepUnitsBySession.TryGetValue(sessionId, out int units) ? units : 0;

        public string DisplayNameOf(string sessionId) =>
            !string.IsNullOrEmpty(sessionId) && _namesBySession.TryGetValue(sessionId, out string name)
                ? name ?? string.Empty
                : string.Empty;

        void ReadSeats()
        {
            EgrangState state = _room.State;
            if (state?.players == null) return;

            _seats.Clear();
            _namesBySession.Clear();
            state.players.ForEach((sessionId, player) =>
            {
                _seats.Add((sessionId, player.seat));
                _namesBySession[sessionId] = player.displayName ?? string.Empty;
            });

            _finishUnits = state.finishUnits;

            _stepUnitsBySession.Clear();
            _stickBySession.Clear();
            state.racers?.ForEach((sessionId, racer) =>
            {
                _stepUnitsBySession[sessionId] = racer.stepUnits;
                _stickBySession[sessionId] = (EgrangStickShape)racer.stick;
            });

            SeatsChanged?.Invoke();
        }

        void OnStepTaken(StepTakenPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.sessionId)) return;

            StepTaken?.Invoke(payload.sessionId, (EgrangStepResult)payload.result, payload.stepUnits);
        }

        void OnGameOver(GameOverPlacesPayload payload)
        {
            RaceOver?.Invoke(payload?.places ?? new Dictionary<string, int>());
        }

        void OnCountdown(CountdownPayload payload)
        {
            // Milliseconds off the server's own clock, so nothing here has to know what time this
            // machine thinks it is.
            CountdownChanged?.Invoke(Mathf.Max(0f, (payload?.remainingMs ?? 0f) / 1000f));
        }

        void OnError(ErrorPayload payload)
        {
            Debug.LogWarning($"Egrang: server rejected the last message ({payload?.code}): {payload?.message}");
        }
    }

    [Serializable]
    public class StepTakenPayload
    {
        public string sessionId;
        public int result;
        public int stepUnits;
    }

    [Serializable]
    public class CountdownPayload
    {
        /// <summary>Server wall-clock instant the race opens. Kept for logging; not used for timing.</summary>
        public double startsAtMs;

        /// <summary>Milliseconds left, measured server-side.</summary>
        public float remainingMs;
    }

    [Serializable]
    public class GameOverPlacesPayload
    {
        public Dictionary<string, int> places;
        public string winner;
    }
}
