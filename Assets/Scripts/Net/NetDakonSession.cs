using System;
using System.Collections.Generic;
using Colyseus;
using Museum.Games.Dakon;
using Museum.Net.State;

namespace Museum.Net
{
    /// <summary>
    /// A Dakon game running on the server, presented to the view as an
    /// <see cref="IDakonSession"/>.
    ///
    /// The server owns every rule; this class owns no game logic at all. It mirrors the synced
    /// <see cref="DakonState"/> for everything the view *reads*, and takes what visibly *happened*
    /// from the server's own `drop_applied` message. State sync tells you what is; an animation
    /// needs to know what changed, and the server already knows — it computed the whole result
    /// when it validated the move, so asking it beats reconstructing it here.
    ///
    /// (An earlier version did reconstruct it, by diffing each patch against the last. It could
    /// not survive a turn: the server refills the hand inside the same drop that empties it, so
    /// the patch after a turn's final drop shows a *bigger* hand and the diff saw no drop at all.
    /// Any patch carrying two drops broke it the same way. Both are gone with the message.)
    ///
    /// A drop is a request: <see cref="RequestDrop"/> sends `drop_seed` and returns. If the server
    /// accepts it, `drop_applied` produces <see cref="DropApplied"/>; if it refuses, an `error`
    /// message produces <see cref="DropRejected"/>. Nothing is drawn optimistically, so the board
    /// on screen is always one the server agrees with.
    ///
    /// One thing *is* predicted, though nothing is drawn from it: the hole a drop is aimed at.
    /// Requests are pipelined — the player can click a whole hand without waiting for a round trip
    /// each time — so the target hole is `nextHoleIndex` plus however many drops are still in
    /// flight (see <see cref="_inFlight"/>). A wrong guess is not a divergence, only a refusal.
    /// </summary>
    public sealed class NetDakonSession : IDakonSession
    {
        private readonly Room<DakonState> _room;
        private readonly string _mySessionId;

        /// <summary>sessionId per seat, in seat order — the server's seating, not ours.</summary>
        private readonly List<string> _seats = new List<string>();

        /// <summary>displayName per seat, kept in step with <see cref="_seats"/>.</summary>
        private readonly List<string> _seatNames = new List<string>();

        /// <summary>
        /// Seed ids we have sent and not yet seen come back, in the order we sent them. This is
        /// what lets the player keep clicking: the hole a drop lands in is not a server secret —
        /// it advances by one per drop and only resets when the hand empties — so the nth
        /// outstanding drop is predictably `nextHoleIndex + n`. Colyseus delivers one client's
        /// messages in order, so the server applies them in the order they were predicted.
        /// </summary>
        private readonly List<string> _inFlight = new List<string>();

        /// <summary>Hole the last sent drop was aimed at. Only meaningful while <see cref="_inFlight"/> is non-empty.</summary>
        private int _predictedHole;

        private readonly List<Seed> _hand = new List<Seed>();
        private readonly List<SeedCategory> _holes = new List<SeedCategory>();
        private readonly Dictionary<string, int> _totals = new Dictionary<string, int>();
        private int _nextHole;
        private int _poolCount;
        private string _activePlayer = string.Empty;
        private string _phase = "waiting";
        private bool _gameOverReported;

        /// <summary>Final scores from `game_over`, which outlive the room's own state.</summary>
        private Dictionary<string, int> _finalScores;
        private string _winnerSessionId;

        public NetDakonSession(Room<DakonState> room)
        {
            _room = room;
            _mySessionId = room.SessionId;

            _room.OnStateChange += (_, __) =>
            {
                _diagPatches++;
                UnityEngine.Debug.Log($"[DAKON-DIAG] OnStateChange #{_diagPatches}");
                Apply();
            };
            _room.OnError += (code, message) =>
                UnityEngine.Debug.LogWarning($"[DAKON-DIAG] room OnError {code}: {message}");
            _room.OnLeave += (code) =>
                UnityEngine.Debug.LogWarning($"[DAKON-DIAG] room OnLeave code={code}");
            _room.OnMessage<DropAppliedPayload>("drop_applied", OnDropApplied);
            _room.OnMessage<GameOverPayload>("game_over", OnGameOver);
            _room.OnMessage<ErrorPayload>("error", OnError);

            // The room already has its state by the time we get here: it arrives in the join
            // handshake, which `Reconnect` awaits, so the first OnStateChange fired before this
            // object existed. Subscribing alone would leave the mirror empty until the *next*
            // patch — and in a turn-based game there is no next patch, because the only thing
            // that moves the state is a drop, and a player with no hand on screen cannot make
            // one. Reading the state once here is what breaks that deadlock.
            //
            // No listener is attached yet (the view binds after this returns), so the events
            // Apply raises go nowhere; that is fine — the view reads this object's properties
            // directly when it attaches.
            Apply();

            // TEMP DIAG — remove once the empty-hand bug is closed.
            UnityEngine.Debug.Log($"[DAKON-DIAG] ctor room={_room.RoomId} me={_mySessionId} " +
                                  $"state={(_room.State == null ? "null" : "present")} " +
                                  $"players={(_room.State?.players == null ? -1 : _room.State.players.Count)} " +
                                  $"holes={(_room.State?.holes == null ? -1 : _room.State.holes.Count)}");
        }

        /// <summary>TEMP DIAG counter.</summary>
        private int _diagPatches;

        public event Action<DakonDrop> DropApplied;
        public event Action<DakonError> DropRejected;
        public event Action HandChanged;
        public event Action GameOver;
        public event Action StateChanged;
        public event Action BoardReady;

        public Phase Phase =>
            _phase == "in_progress" ? Phase.InProgress :
            _phase == "finished" ? Phase.Finished : Phase.Waiting;

        public int ActivePlayer => Math.Max(0, _seats.IndexOf(_activePlayer));

        public int MySeat => Math.Max(0, _seats.IndexOf(_mySessionId));

        public bool IsMyTurn => Phase == Phase.InProgress && _activePlayer == _mySessionId;

        public IReadOnlyList<Seed> Hand => _hand;
        public int NextHoleIndex => _nextHole;
        public int HoleCount => _holes.Count;
        public SeedCategory HoleTypeAt(int index) => _holes[index];
        public int PoolCount => _poolCount;

        public int Total(int seat)
        {
            string sessionId = seat >= 0 && seat < _seats.Count ? _seats[seat] : null;
            if (sessionId == null) return 0;

            if (_finalScores != null && _finalScores.TryGetValue(sessionId, out int final))
            {
                return final;
            }

            return _totals.TryGetValue(sessionId, out int total) ? total : 0;
        }

        public string DisplayNameOf(int seat)
        {
            string name = seat >= 0 && seat < _seatNames.Count ? _seatNames[seat] : null;

            // The server takes whatever displayName the client sent, empty string included, so a
            // blank is a real possibility rather than a can't-happen. Falling back to the seat
            // label keeps the HUD readable instead of leaving a hole where a name should be.
            return string.IsNullOrEmpty(name) ? $"Pemain {seat + 1}" : name;
        }

        public int? Winner
        {
            get
            {
                if (Phase != Phase.Finished) return null;
                if (string.IsNullOrEmpty(_winnerSessionId)) return null;

                int seat = _seats.IndexOf(_winnerSessionId);
                return seat < 0 ? (int?)null : seat;
            }
        }

        public void RequestDrop(string seedId)
        {
            if (!IsMyTurn)
            {
                DropRejected?.Invoke(DakonError.NotYourTurn);
                return;
            }

            if (_holes.Count == 0)
            {
                // No board yet, so no hole to aim at. Nothing to predict from.
                DropRejected?.Invoke(DakonError.InvalidHole);
                return;
            }

            // With nothing outstanding the server's own index is the truth; inside a burst it is
            // stale by however many drops it has not answered yet, so keep counting from our own
            // last guess. Resyncing whenever the queue drains means a wrong guess cannot compound
            // past the end of one burst.
            int holeIndex = _inFlight.Count == 0 ? _nextHole : (_predictedHole + 1) % _holes.Count;

            _predictedHole = holeIndex;
            _inFlight.Add(seedId);

            _room.Send("drop_seed", new { seedId, holeIndex });
        }

        // --- state ---------------------------------------------------------

        /// <summary>
        /// Fold one patch in: refresh the mirror, then say which kind of change it was. Order
        /// matters — the view reads this object's properties from inside the events, so they must
        /// already describe the new position.
        ///
        /// Note what is *not* here: applied drops. Those arrive as their own message, so a patch
        /// only ever means "the numbers moved", never "a seed was played".
        /// </summary>
        private void Apply()
        {
            DakonState state = _room.State;
            if (state == null) return;

            // The holes are written once, when the match starts — which is after the view bound
            // to us, because the player was still in the lobby when this object was made.
            bool boardArrived = _holes.Count == 0 && state.holes != null && state.holes.Count > 0;

            ReadSeats(state);
            ReadHoles(state);

            string previousActive = _activePlayer;
            int previousPool = _poolCount;

            ReadHand(state);
            ReadTotals(state);

            _nextHole = state.nextHoleIndex;
            _poolCount = state.centerPoolCount;
            _activePlayer = state.activePlayer ?? string.Empty;
            _phase = state.phase ?? "waiting";

            if (boardArrived || _activePlayer != previousActive)
            {
                // A turn boundary (or a board arriving under us) resets the server's hole counter,
                // so anything we were still predicting against the old one is meaningless now.
                _inFlight.Clear();
            }

            // TEMP DIAG — remove once the empty-hand bug is closed.
            UnityEngine.Debug.Log($"[DAKON-DIAG] Apply phase={_phase} holes={_holes.Count} hand={_hand.Count} " +
                                  $"pool={_poolCount} active={_activePlayer} me={_mySessionId} seats={_seats.Count} " +
                                  $"boardArrived={boardArrived}");

            if (boardArrived)
            {
                // The opening position (or a reconnect landing mid-match). The view rebuilds
                // wholesale, including everything it sizes from the hole count.
                BoardReady?.Invoke();
            }
            else if (_poolCount != previousPool || _activePlayer != previousActive)
            {
                // A refill: the turn passed and a fresh grab replaced the hand wholesale.
                HandChanged?.Invoke();
            }
            else
            {
                StateChanged?.Invoke();
            }

            if (_phase == "finished" && !_gameOverReported)
            {
                _gameOverReported = true;
                GameOver?.Invoke();
            }
        }

        /// <summary>
        /// Seating, which only ever grows. The server fixes its seat order when the match starts
        /// and keeps it; the synced players map does not, because a player who leaves is removed
        /// from it. Rebuilding from that map would slide the survivor down into the leaver's seat
        /// and hand them the leaver's scores and win — so a seat, once seen, is kept.
        ///
        /// Names are kept for the same reason: the results panel names both players, and it is
        /// shown at exactly the moment an opponent is most likely to have closed their tab.
        /// </summary>
        private void ReadSeats(DakonState state)
        {
            if (state.players == null) return;

            state.players.ForEach((sessionId, player) =>
            {
                while (_seats.Count <= player.seat)
                {
                    _seats.Add(null);
                    _seatNames.Add(null);
                }

                _seats[player.seat] = sessionId;

                if (!string.IsNullOrEmpty(player.displayName))
                {
                    _seatNames[player.seat] = player.displayName;
                }
            });
        }

        private void ReadHoles(DakonState state)
        {
            if (state.holes == null || state.holes.Count == _holes.Count) return;

            _holes.Clear();
            for (int i = 0; i < state.holes.Count; i++)
            {
                _holes.Add(CategoryFrom(state.holes[i]));
            }
        }

        private void ReadHand(DakonState state)
        {
            _hand.Clear();
            if (state.hand == null) return;

            for (int i = 0; i < state.hand.Count; i++)
            {
                DakonSeed seed = state.hand[i];
                _hand.Add(new Seed(seed.id, CategoryFrom(seed.category), seed.typeId));
            }
        }

        private void ReadTotals(DakonState state)
        {
            _totals.Clear();
            if (state.storehouses == null) return;

            state.storehouses.ForEach((sessionId, store) => _totals[sessionId] = store.total);
        }

        private static SeedCategory CategoryFrom(string category) =>
            category == "dicot" ? SeedCategory.Dicot : SeedCategory.Monocot;

        /// <summary>
        /// The server accepted a drop — anyone's, including our own — and is describing it. The
        /// seat that scored comes from the server's own frozen seating, so it needs no lookup
        /// here, and the species id is the one the seed carried before it left the hand.
        /// </summary>
        private void OnDropApplied(DropAppliedPayload payload)
        {
            if (payload == null) return;

            // By seed id, not by count: this message is broadcast for *every* accepted drop,
            // including the opponent's, and it carries no sessionId to tell them apart. Only a
            // seed we sent ourselves can be one of ours in flight.
            _inFlight.Remove(payload.seedId);

            DropApplied?.Invoke(new DakonDrop(
                payload.seedId,
                payload.holeIndex,
                payload.scoringPlayer,
                CategoryFrom(payload.category),
                payload.typeId,
                payload.turnEnded,
                payload.gameOver));
        }

        private void OnGameOver(GameOverPayload payload)
        {
            // Kept because the room's state can go away (or be left) while the results panel is
            // still on screen — these are the numbers it shows.
            _finalScores = payload?.scores;
            _winnerSessionId = payload?.winner;
            _phase = "finished";

            if (!_gameOverReported)
            {
                _gameOverReported = true;
                GameOver?.Invoke();
            }
        }

        private void OnError(ErrorPayload payload)
        {
            // A refusal invalidates every prediction queued behind it — the server stopped at the
            // one it rejected, so the drops after it were aimed one hole too far. Drop them all
            // and let the view re-read the hand from the next patch, which is server truth.
            _inFlight.Clear();

            switch (payload?.code)
            {
                case "not_your_turn":
                    DropRejected?.Invoke(DakonError.NotYourTurn);
                    break;
                case "seed_not_in_hand":
                    DropRejected?.Invoke(DakonError.SeedNotInHand);
                    break;
                default:
                    DropRejected?.Invoke(DakonError.InvalidHole);
                    break;
            }
        }
    }

    /// <summary>
    /// `drop_applied` — one accepted move, as the server resolved it:
    /// { seedId, holeIndex, scoringPlayer (seat), category, typeId, turnEnded, gameOver }.
    /// </summary>
    [Serializable]
    public class DropAppliedPayload
    {
        public string seedId;
        public int holeIndex;
        public int scoringPlayer;
        public string category;
        public string typeId;
        public bool turnEnded;
        public bool gameOver;
    }

    /// <summary>`game_over` — { scores: { sessionId: score }, winner: sessionId | null }.</summary>
    [Serializable]
    public class GameOverPayload
    {
        public Dictionary<string, int> scores;
        public string winner;
    }

    /// <summary>`error` — { code, message }.</summary>
    [Serializable]
    public class ErrorPayload
    {
        public string code;
        public string message;
    }
}
