using System;
using System.Collections.Generic;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// One colour band of the skill-check track, in normalized track space.
    /// Half-open <c>[Start, End)</c>, except that the band ending at the far edge also owns
    /// that edge — see <see cref="SkillCheckZones.Evaluate"/>.
    /// </summary>
    [Serializable]
    public struct SkillCheckZone
    {
        [Tooltip("Left edge of the band, 0 = far left of the track.")]
        [Range(0f, 1f)] public float Start;

        [Tooltip("Right edge of the band, 1 = far right of the track.")]
        [Range(0f, 1f)] public float End;

        [Tooltip("Outcome when the player presses with the cursor inside this band.")]
        public EgrangStepResult Result;

        public SkillCheckZone(float start, float end, EgrangStepResult result)
        {
            Start = start;
            End = end;
            Result = result;
        }
    }

    /// <summary>
    /// The colour layout of the skill-check track: an ordered set of bands over 0..1 that turns a
    /// cursor position into an <see cref="EgrangStepResult"/>.
    ///
    /// Boundaries are authored in the inspector, and <see cref="SkillCheckBar"/> stretches the band
    /// graphics to match them — the table is the truth and the picture follows, so the two cannot
    /// drift apart. Nothing here knows about sprites or transforms, which is what keeps it
    /// unit-testable.
    /// </summary>
    [Serializable]
    public sealed class SkillCheckZones
    {
        [SerializeField] private List<SkillCheckZone> zones = new List<SkillCheckZone>();

        /// <summary>Bands in author order. Empty is legal and evaluates to <see cref="EgrangStepResult.Fail"/>.</summary>
        public IReadOnlyList<SkillCheckZone> Zones => zones;

        public SkillCheckZones() { }

        public SkillCheckZones(params SkillCheckZone[] bands)
        {
            zones = new List<SkillCheckZone>(bands ?? Array.Empty<SkillCheckZone>());
        }

        /// <summary>
        /// Copies an existing set of bands. Used when a stick profile hands its table to the bar:
        /// the bar gets its own list, so retuning the bar at runtime cannot write back into the
        /// profile asset — which in the editor would be a permanent edit to shipped data.
        /// </summary>
        public SkillCheckZones(IEnumerable<SkillCheckZone> bands)
        {
            zones = bands == null ? new List<SkillCheckZone>() : new List<SkillCheckZone>(bands);
        }

        /// <summary>
        /// Total width of every band scoring <paramref name="result"/>, in normalized track space —
        /// the share of the sweep that scores it. This is the honest measure of how forgiving a zone
        /// table is, which is what difficulty tuning and the selection cards' meters compare.
        /// </summary>
        public float TotalWidth(EgrangStepResult result)
        {
            float total = 0f;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Result != result) continue;
                total += Mathf.Max(0f, zones[i].End - zones[i].Start);
            }
            return total;
        }

        /// <summary>
        /// The result for a cursor position in 0..1. Positions outside that range are clamped.
        ///
        /// Bands are half-open so adjoining bands never both claim their shared boundary: the
        /// boundary belongs to the band that starts there. A position that no band covers —
        /// including the far edge when no band reaches it, and any gap left by mis-authored data —
        /// returns <see cref="EgrangStepResult.Fail"/>. Degrading to "the player missed" keeps a
        /// bad zone table from throwing mid-game.
        /// </summary>
        public EgrangStepResult Evaluate(float t)
        {
            int index = IndexOf(t);
            return index < 0 ? EgrangStepResult.Fail : zones[index].Result;
        }

        /// <summary>
        /// The index of the band covering a position in 0..1, or -1 when none does. Same coverage
        /// rules as <see cref="Evaluate"/>, which is written in terms of this. Callers that need to
        /// tell apart two bands sharing a result — the two yellows, say — want this rather than
        /// <see cref="Evaluate"/>.
        /// </summary>
        public int IndexOf(float t)
        {
            if (float.IsNaN(t)) return -1;
            t = Mathf.Clamp01(t);

            for (int i = 0; i < zones.Count; i++)
            {
                SkillCheckZone zone = zones[i];
                if (t >= zone.Start && t < zone.End) return i;
            }

            // Only a position sitting exactly on some band's End reaches here. The band that ends
            // there owns it, which is what gives the far edge of the track a result at all.
            for (int i = 0; i < zones.Count; i++)
            {
                SkillCheckZone zone = zones[i];
                if (t >= zone.Start && t <= zone.End) return i;
            }

            return -1;
        }

        /// <summary>
        /// Checks the table for problems the author can fix: empty, inverted (<c>End &lt;= Start</c>),
        /// out of order, or overlapping bands. Returns true when the table is sound; otherwise
        /// <paramref name="error"/> describes the first problem found.
        ///
        /// Gaps are not an error — they are a legal way to author a dead strip, and they resolve to
        /// <see cref="EgrangStepResult.Fail"/>.
        /// </summary>
        public bool Validate(out string error)
        {
            if (zones.Count == 0)
            {
                error = "No zones defined; every press will read as Fail.";
                return false;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                SkillCheckZone zone = zones[i];
                if (zone.End <= zone.Start)
                {
                    error = $"Zone {i} is empty or inverted: Start {zone.Start} is not below End {zone.End}.";
                    return false;
                }

                if (i > 0 && zone.Start < zones[i - 1].End)
                {
                    error = $"Zone {i} starts at {zone.Start}, before zone {i - 1} ends at {zones[i - 1].End}. " +
                            "Zones must be listed in ascending order and must not overlap.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
