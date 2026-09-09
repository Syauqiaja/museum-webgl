using System.Collections.Generic;

namespace Museum.Lobby
{
    /// <summary>Where a room is in its life, as far as the lobby UI cares.</summary>
    public enum LobbyPhase
    {
        /// <summary>Still filling up; players may join and leave.</summary>
        Waiting,

        /// <summary>Start accepted, game scene not up yet.</summary>
        Starting,

        /// <summary>The match owns the players now — the lobby's job is done.</summary>
        InProgress
    }

    /// <summary>
    /// The whole lobby room as one immutable value: the code, every seat, and whose room it is.
    ///
    /// The UI renders from these and never mutates one, so a late or out-of-order update can only
    /// ever replace the picture wholesale — it can't leave the panel half-describing two rooms.
    /// </summary>
    public sealed class LobbyRoomSnapshot
    {
        /// <summary>Below this many players, the host cannot start.</summary>
        public const int MinPlayersToStart = 2;

        public LobbyRoomSnapshot(string code,
                                 IReadOnlyList<LobbySlot> slots,
                                 LobbyPhase phase,
                                 string mySessionId,
                                 string hostSessionId)
        {
            Code = code ?? string.Empty;
            Slots = slots ?? new List<LobbySlot>();
            Phase = phase;
            MySessionId = mySessionId ?? string.Empty;
            HostSessionId = hostSessionId ?? string.Empty;
        }

        /// <summary>Human-typeable room code.</summary>
        public string Code { get; }

        /// <summary>Every seat in seat order, empty ones included.</summary>
        public IReadOnlyList<LobbySlot> Slots { get; }

        public LobbyPhase Phase { get; }

        /// <summary>This client's session.</summary>
        public string MySessionId { get; }

        /// <summary>Session allowed to start the match.</summary>
        public string HostSessionId { get; }

        /// <summary>Seats actually held by a player.</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    if (!Slots[i].IsEmpty) count++;
                }

                return count;
            }
        }

        public bool IsFull => Slots.Count > 0 && OccupiedCount >= Slots.Count;

        /// <summary>Whether this client is the host.</summary>
        public bool AmHost => !string.IsNullOrEmpty(MySessionId) && MySessionId == HostSessionId;

        /// <summary>
        /// Whether the start button should do anything: the host's call, only while the room is
        /// still waiting, and only once enough players are seated.
        /// </summary>
        public bool CanStart => AmHost
                                && Phase == LobbyPhase.Waiting
                                && OccupiedCount >= MinPlayersToStart;
    }
}
