using UnityEngine;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// Deterministic seed-pile layout. Given the nth seed dropped into a bin (a hole or a
    /// storehouse), returns a local offset from the bin anchor so seeds scatter without
    /// z-fighting and stack as the pile grows. Pure/static — no scene state, unit-testable.
    /// Used for both hole piles and storehouse piles.
    /// </summary>
    public static class DakonPile
    {
        // Golden-angle spiral: even packing per layer, no clustering.
        const float GoldenAngle = 2.39996323f;
        const int RingSize = 12;

        /// <summary>
        /// Local offset for the <paramref name="n"/>th seed (0-based) in a bin of the given
        /// scatter <paramref name="radius"/>. Horizontal distance from center is always
        /// &lt;= radius; y is non-decreasing in n so later seeds rest on top.
        /// </summary>
        public static Vector3 OffsetFor(int n, float radius)
        {
            if (n <= 0) return Vector3.zero;

            int inRing = n % RingSize;
            int layer = n / RingSize;

            float angle = n * GoldenAngle;
            float rr = radius * Mathf.Sqrt(inRing / (float)RingSize);
            float x = rr * Mathf.Cos(angle);
            float z = rr * Mathf.Sin(angle);
            float y = layer * radius * 0.5f + inRing * radius * 0.02f;

            return new Vector3(x, y, z);
        }
    }
}
