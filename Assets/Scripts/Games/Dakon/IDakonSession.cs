using System;
using System.Collections.Generic;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// What a Dakon game looks like to the view, whether the rules are running locally
    /// (<see cref="LocalDakonSession"/>, hotseat) or on the server (the networked session in
    /// <c>Museum.Net</c>, which is authoritative and merely reports what happened).
    ///
    /// The split exists because the two differ in exactly one way that matters: locally a drop is
    /// resolved the instant it is made, while online it is a request that the server may reject
    /// and whose result arrives later. So the view never reads a return value — it asks for a drop
    /// and renders <see cref="DropApplied"/> when (and if) one comes back.
    ///
    /// No UnityEngine and no Colyseus types here: this is the seam, not either side of it.
    /// </summary>
    public interface IDakonSession
    {
        /// <summary>A drop was accepted and applied. Carries what the animation needs.</summary>
        event Action<DakonDrop> DropApplied;

        /// <summary>A drop was refused. The payload is a <see cref="DakonError"/> for wording.</summary>
        event Action<DakonError> DropRejected;

        /// <summary>The hand was replaced — a new turn's draw. The view re-deals cards.</summary>
        event Action HandChanged;

        /// <summary>
        /// The board itself now exists: holes are typed and the opening position is readable.
        /// Locally that is true before the view ever sees the session, so it never fires; online
        /// the holes arrive with the first patch, which is *after* the view bound. Anything the
        /// view sizes from <see cref="HoleCount"/> must be built (or rebuilt) here rather than at
        /// bind time, or it is sized from an empty board.
        /// </summary>
        event Action BoardReady;

        /// <summary>The match is over; scores are final.</summary>
        event Action GameOver;

        /// <summary>Raised when scores/pool/turn changed without a drop of ours (the opponent moved).</summary>
        event Action StateChanged;

        Phase Phase { get; }

        /// <summary>Seat whose turn it is (0/1).</summary>
        int ActivePlayer { get; }

        /// <summary>This client's seat. Hotseat has no single seat, so it reports the active one.</summary>
        int MySeat { get; }

        /// <summary>True when this client may drop right now.</summary>
        bool IsMyTurn { get; }

        IReadOnlyList<Seed> Hand { get; }
        int HoleCount { get; }

        /// <summary>Holes per seat: ring indices below this are seat 0's, the rest seat 1's.</summary>
        int HolesPerSide { get; }

        SeedCategory HoleTypeAt(int index);

        /// <summary>True when this hole already took a seed in the current turn, so it is closed until the turn passes.</summary>
        bool IsSown(int index);

        int PoolCount { get; }
        int Total(int seat);

        /// <summary>
        /// Display name for a seat — the name that player registered on the way in. Online that
        /// is the server's <c>displayName</c> for whoever holds the seat; hotseat has no
        /// registered opponent and falls back to a seat label.
        /// </summary>
        string DisplayNameOf(int seat);

        /// <summary>Winner seat, or null for a tie (and while the match is unfinished).</summary>
        int? Winner { get; }

        /// <summary>
        /// Ask to drop this seed into this hole. The hole must be one of the active seat's own
        /// and not yet sown this turn; the session (locally the board, online the server)
        /// answers with <see cref="DropApplied"/> or <see cref="DropRejected"/>.
        /// </summary>
        void RequestDrop(string seedId, int holeIndex);
    }

    /// <summary>An applied drop, in the terms the view animates: which seed, where, who scored.</summary>
    public readonly struct DakonDrop
    {
        public readonly string SeedId;
        public readonly int HoleIndex;
        public readonly int ScoringPlayer;
        public readonly SeedCategory Category;

        /// <summary>Species of the dropped seed, for the 3D prefab. Empty if unknown.</summary>
        public readonly string TypeId;

        public readonly bool TurnEnded;
        public readonly bool GameOver;

        public DakonDrop(string seedId, int holeIndex, int scoringPlayer, SeedCategory category,
                         string typeId, bool turnEnded, bool gameOver)
        {
            SeedId = seedId;
            HoleIndex = holeIndex;
            ScoringPlayer = scoringPlayer;
            Category = category;
            TypeId = typeId;
            TurnEnded = turnEnded;
            GameOver = gameOver;
        }
    }
}
