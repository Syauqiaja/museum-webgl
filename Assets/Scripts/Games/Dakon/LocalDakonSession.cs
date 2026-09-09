using System;
using System.Collections.Generic;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// Hotseat session: the rules run here, in a local <see cref="DakonBoard"/>, and both players
    /// share one screen. This is the museum's offline mode and the Editor's no-server mode.
    ///
    /// Because the board answers immediately, every event is raised synchronously from inside
    /// <see cref="RequestDrop"/> — which is exactly the behaviour the view is written against, so
    /// the online session (whose answers arrive later) needs no special case in the view.
    /// </summary>
    public sealed class LocalDakonSession : IDakonSession
    {
        private readonly DakonBoard _board;

        public LocalDakonSession(DakonConfig config, int rngSeed)
        {
            _board = new DakonBoard(config, rngSeed);
            _board.StartGame();
        }

        public event Action<DakonDrop> DropApplied;
        public event Action<DakonError> DropRejected;
        public event Action HandChanged;
        public event Action GameOver;

        // Nothing changes this game state except our own drops, so there is never an unsolicited
        // update to report. BoardReady is the same story from the other direction: the board is
        // already started by the constructor above, so the view reads a complete position the
        // moment it attaches and has nothing to wait for. Both are kept on the interface for the
        // networked session, which has plenty of the former and genuinely waits for the latter.
#pragma warning disable 67
        public event Action StateChanged;
        public event Action BoardReady;
#pragma warning restore 67

        public Phase Phase => _board.Phase;
        public int ActivePlayer => _board.ActivePlayer;

        /// <summary>Hotseat has no single owner — whoever's turn it is, is sitting there.</summary>
        public int MySeat => _board.ActivePlayer;

        public bool IsMyTurn => _board.Phase == Phase.InProgress;

        public IReadOnlyList<Seed> Hand => _board.Hand;
        public int NextHoleIndex => _board.NextHoleIndex;
        public int HoleCount => _board.HoleCount;
        public SeedCategory HoleTypeAt(int index) => _board.HoleTypeAt(index);
        public int PoolCount => _board.PoolCount;
        public int Total(int seat) => _board.Total(seat);

        /// <summary>
        /// Hotseat has no registered players — both seats are the one person at the screen — so
        /// the label is the seat itself. Reaching for the local nickname here would name one of
        /// the two chairs after whoever happens to be logged in, which is worse than not naming
        /// them, and would drag SessionData into a model that is deliberately Unity-free.
        /// </summary>
        public string DisplayNameOf(int seat) => $"Pemain {seat + 1}";
        public int? Winner => _board.Winner;

        public void RequestDrop(string seedId)
        {
            if (_board.Phase != Phase.InProgress)
            {
                DropRejected?.Invoke(DakonError.InvalidHole);
                return;
            }

            // Captured before the model applies the drop and advances past them.
            int hole = _board.NextHoleIndex;
            Seed? seed = FindHandSeed(seedId);

            DropResult result = _board.DropSeed(_board.ActivePlayer, seedId, hole);

            if (!result.Ok)
            {
                DropRejected?.Invoke(result.Error ?? DakonError.InvalidHole);
                return;
            }

            DropApplied?.Invoke(new DakonDrop(
                seedId,
                hole,
                result.ScoringPlayer,
                result.ScoringCategory,
                seed?.TypeId ?? string.Empty,
                result.TurnEnded,
                result.GameOver));

            if (result.GameOver)
            {
                GameOver?.Invoke();
            }
            else if (result.TurnEnded)
            {
                HandChanged?.Invoke();
            }
        }

        private Seed? FindHandSeed(string seedId)
        {
            foreach (Seed seed in _board.Hand)
            {
                if (seed.Id == seedId)
                {
                    return seed;
                }
            }

            return null;
        }
    }
}
