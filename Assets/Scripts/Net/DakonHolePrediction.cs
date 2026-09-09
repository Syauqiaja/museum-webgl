using System.Collections.Generic;

namespace Museum.Net
{
    /// <summary>
    /// Which hole the next drop is aimed at, while the server's own answer is still in the post.
    ///
    /// Drops are pipelined: the player may click a whole hand without waiting for a round trip,
    /// and every `drop_seed` carries the hole it means. The ring advances by exactly one per
    /// accepted drop — anyone's drop, since both players share the ring — so the target is
    /// derivable locally, and a wrong guess is a refusal rather than a divergence.
    ///
    /// The anchor is deliberately the *last accepted drop*, not the synced `nextHoleIndex`.
    /// `drop_applied` is broadcast the instant the server applies a drop; the state patch that
    /// moves `nextHoleIndex` follows on the room's own patch interval. Between the two the synced
    /// index is a hole behind, and a player quick enough to tap inside that window aimed at the
    /// hole that had already been filled — "Lubang itu bukan tujuan berikutnya" on every drop.
    /// On a LAN the window is a millisecond or two and nobody could hit it; over wss from a phone
    /// it is a whole round trip, which is why this only ever showed up on mobile.
    ///
    /// Pure C# and no UnityEngine, like the rest of the model, so the race is asserted in EditMode.
    /// </summary>
    public sealed class DakonHolePrediction
    {
        /// <summary>Seed ids sent and not yet seen come back, in send order.</summary>
        private readonly List<string> _inFlight = new List<string>();

        /// <summary>Hole the last sent drop was aimed at; -1 when nothing is outstanding.</summary>
        private int _predicted = -1;

        /// <summary>Hole of the last drop the server accepted; -1 when that no longer anchors us.</summary>
        private int _confirmed = -1;

        public int InFlightCount => _inFlight.Count;

        /// <summary>
        /// Claim the hole for one outgoing drop. <paramref name="serverNextHole"/> is the synced
        /// <c>nextHoleIndex</c>, used only when neither an outstanding drop nor an accepted one
        /// gives a nearer anchor — at the start of a turn, that is the only truth there is.
        /// </summary>
        public int Aim(string seedId, int serverNextHole, int holeCount)
        {
            int hole =
                _inFlight.Count > 0 ? NextAfter(_predicted, holeCount) :
                _confirmed >= 0 ? NextAfter(_confirmed, holeCount) :
                serverNextHole;

            _predicted = hole;
            _inFlight.Add(seedId);
            return hole;
        }

        /// <summary>
        /// A drop the server accepted — ours or the opponent's, since both move the same ring.
        /// A drop that ended the turn anchors nothing: the server restarts the ring at the other
        /// seat's first hole, which only the next patch can tell us.
        /// </summary>
        public void Applied(string seedId, int holeIndex, bool turnEnded)
        {
            _inFlight.Remove(seedId);
            _confirmed = turnEnded ? -1 : holeIndex;
        }

        /// <summary>
        /// Forget everything and fall back to synced state. Used on a refusal — which invalidates
        /// every guess queued behind it, the server having stopped at the one it rejected — and on
        /// a turn boundary or a board arriving under us.
        /// </summary>
        public void Reset()
        {
            _inFlight.Clear();
            _predicted = -1;
            _confirmed = -1;
        }

        private static int NextAfter(int hole, int holeCount) =>
            holeCount <= 0 ? 0 : (hole + 1) % holeCount;
    }
}
