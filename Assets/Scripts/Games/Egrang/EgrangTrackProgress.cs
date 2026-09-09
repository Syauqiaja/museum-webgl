using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// How far along the course a racer is: the answer to "where am I between start and finish",
    /// in both normalized and metre form.
    ///
    /// Plain C# with no <c>MonoBehaviour</c> anywhere near it, so the same reading can be taken on
    /// the authority server once the race goes multiplayer — the server will need each player's
    /// progress to rank them, and that has to be the same arithmetic the client draws.
    /// </summary>
    public readonly struct EgrangTrackProgress
    {
        /// <summary>0 at the start line, 1 at the finish. Clamped: standing behind the start or past the finish still reads as the end of the bar.</summary>
        public readonly float Normalized;

        /// <summary>Metres covered along the course.</summary>
        public readonly float TravelledMeters;

        /// <summary>Metres left to the finish, never negative.</summary>
        public readonly float RemainingMeters;

        /// <summary>Length of the course itself.</summary>
        public readonly float TotalMeters;

        public EgrangTrackProgress(float normalized, float travelledMeters, float remainingMeters, float totalMeters)
        {
            Normalized = normalized;
            TravelledMeters = travelledMeters;
            RemainingMeters = remainingMeters;
            TotalMeters = totalMeters;
        }

        /// <summary>True once the racer is at or past the finish line.</summary>
        public bool HasFinished => Normalized >= 1f;

        /// <summary>
        /// Projects <paramref name="position"/> onto the start→finish line and measures how far along
        /// it sits.
        ///
        /// Projection rather than plain distance-to-finish on purpose: the player wanders sideways
        /// across the path, and a straight distance reading would count that drift as progress lost.
        /// Only movement along the course counts, which is also what a race ranking has to mean.
        ///
        /// Height is ignored — the course is measured on the ground plane, so a rise in the terrain
        /// does not read as extra distance covered.
        /// </summary>
        public static EgrangTrackProgress Evaluate(Vector3 start, Vector3 finish, Vector3 position)
        {
            Vector3 course = Flatten(finish - start);
            float total = course.magnitude;

            // A zero-length course has no meaningful "along": report a finished, empty track rather
            // than dividing by zero. Callers get 1 rather than NaN, and the bar pins to the end.
            if (total <= Mathf.Epsilon) return new EgrangTrackProgress(1f, 0f, 0f, 0f);

            Vector3 travel = Flatten(position - start);
            float along = Vector3.Dot(travel, course / total);
            float clamped = Mathf.Clamp(along, 0f, total);

            return new EgrangTrackProgress(clamped / total, clamped, total - clamped, total);
        }

        static Vector3 Flatten(Vector3 value) => new Vector3(value.x, 0f, value.z);
    }
}
