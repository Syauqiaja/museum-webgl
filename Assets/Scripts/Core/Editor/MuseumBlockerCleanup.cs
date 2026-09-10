using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Finds — and, for the known offenders, moves — the props standing between a visitor and
    /// an exhibit screen. The building dresses each spot with a vitrine, a rope barrier and a
    /// few loose props; most of that sits below the picture and does no harm, but a couple of
    /// pieces were placed at eye height square in front of the video.
    /// </summary>
    /// <remarks>
    /// Menu: <c>Museum/Decor/Report Viewing Blockers</c> lists, per screen, every renderer
    /// that cuts a sight line from where a visitor watches to the picture. The test is
    /// geometric, not a physics raycast — half the props have no collider — and it is done
    /// from a grid of eye points (<see cref="EyeHeight"/> up, <see cref="NearStand"/> to
    /// <see cref="FarStand"/> out from the wall, spread across the picture's width, minus any
    /// spot inside a rope enclosure) to a grid of points on the picture. A prop that stands
    /// below every one of those lines, as the rope barrier and the vitrine do once the screen
    /// is hung high enough, is not a blocker; one that clips a few corner lines from right at
    /// the rope is logged as grazing.
    /// <c>Clear Viewing Blockers</c> applies <see cref="Fixes"/> — a short hand-written table,
    /// one entry per offender — and reports whatever is still in the way for a human to judge.
    ///
    /// Everything is done by renderer bounds, never by <c>transform.position</c>: most of the
    /// museum's meshes have their placement baked into vertices, so the transform of a prop can
    /// sit twenty metres from where it renders (see MuseumScreenGeometry). A fix therefore
    /// says where the prop's *bounds centre* should end up and the tool works out the delta.
    /// Every move is undoable and logged; a prop already within a centimetre of its target is
    /// left alone, so the menu is safe to run twice.
    /// </remarks>
    public static class MuseumBlockerCleanup
    {
        /// <summary>A standing visitor's eye over the floor. The rig's camera is 3.57 m up — see FPSController — but visitors read the picture as people, not as the capsule.</summary>
        public const float EyeHeight = 1.6f;

        /// <summary>
        /// Nearest a visitor stands to the wall. The floor between the wall and the rope
        /// enclosure is open, so this is simply where the picture stops being legible.
        /// </summary>
        public const float NearStand = 2.0f;

        /// <summary>Farthest that still counts as watching. Trigger volumes reach 10–14 m; past 9 the picture is a postcard.</summary>
        public const float FarStand = 9.0f;

        /// <summary>Stands are spaced this far apart between <see cref="NearStand"/> and <see cref="FarStand"/>.</summary>
        public const float StandStep = 1.0f;

        /// <summary>A visitor's body, front to back: stands this close to a rope are inside it.</summary>
        public const float BodyDepth = 0.7f;

        /// <summary>Sight lines are tested from stands this far either side of the picture's centre line.</summary>
        public const float StandSpread = 1.5f;

        /// <summary>A prop whose back is within this of the wall hangs on it — batik, plaques — and cannot stand in front of the picture.</summary>
        public const float NearWall = 0.3f;

        /// <summary>A wall-hung prop overlapping the picture's width by less than this is beside it, not over it.</summary>
        public const float WallHungOverlap = 0.1f;

        /// <summary>Below this share of sight lines a prop only grazes a corner; logged, not flagged.</summary>
        public const float GrazeFraction = 0.05f;

        private const int LateralSteps = 3;
        private const int PictureColumns = 5;
        private const int PictureRows = 3;

        private const float PositionTolerance = 0.01f;
        private const float AngleTolerance = 0.5f;

        // Room-scale geometry is never a "blocker" — walls, floors, ceilings, the trigger volumes.
        private const float MaxBlockerHeight = 3.0f;
        private const float MaxBlockerFootprint = 8.0f;

        /// <summary>One prop and where its bounds centre belongs, with an optional new rotation.</summary>
        private struct Fix
        {
            public string Screen;
            public string Name;
            public Vector3 Target;
            public Quaternion? Rotation;
            public string Why;
        }

        /// <summary>
        /// The known offenders. Targets are bounds centres in world space, written against the
        /// prop layout as recovered; a prop that has since been re-dressed by hand will simply
        /// be reported rather than dragged back.
        ///
        /// Egrang: the pair of 2 m stilts stood upright on their plinth, 4.9 m out and dead
        /// centre — laid flat along the plinth top instead, side by side, each foot-peg across
        /// the end of its stilt. (The stilt meshes are modelled lying along Z and stood up by
        /// a 90° roll, so "flat" is the identity rotation.) Dakon: the vase the building parks
        /// on the vitrine's glass sits at picture height 3.8 m out — set down on the rug beside
        /// the pedestal.
        /// </summary>
        private static readonly Fix[] Fixes =
        {
            new Fix { Screen = "Vid Egrang", Name = "R2_Egrang_pole9", Target = new Vector3(8.05f, 8.02f, -6.05f), Rotation = Quaternion.identity, Why = "upright stilt across the picture" },
            new Fix { Screen = "Vid Egrang", Name = "R2_Egrang_peg9", Target = new Vector3(8.05f, 8.05f, -6.55f), Rotation = Quaternion.identity, Why = "foot-peg of the stilt above" },
            new Fix { Screen = "Vid Egrang", Name = "R2_Egrang_pole-9", Target = new Vector3(8.35f, 8.02f, -6.05f), Rotation = Quaternion.identity, Why = "upright stilt across the picture" },
            new Fix { Screen = "Vid Egrang", Name = "R2_Egrang_peg-9", Target = new Vector3(8.35f, 8.05f, -6.55f), Rotation = Quaternion.identity, Why = "foot-peg of the stilt above" },
            new Fix { Screen = "Vid Dakon", Name = "INS_13.001", Target = new Vector3(17.30f, 8.01f, -21.95f), Rotation = null, Why = "vase on the vitrine glass at picture height" },
        };

        [MenuItem("Museum/Decor/Report Viewing Blockers")]
        public static void Report()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            int total = 0;
            foreach (MuseumScreenGeometry.ScreenFrame screen in MuseumScreenGeometry.All(scene))
            {
                total += ReportOne(screen);
            }

            Debug.Log(total == 0
                ? "MuseumBlockerCleanup: every screen has a clear line of sight."
                : $"MuseumBlockerCleanup: {total} renderer(s) cross a viewing corridor — see the lines above.");
        }

        [MenuItem("Museum/Decor/Clear Viewing Blockers")]
        public static void Clear()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            int moved = 0;
            foreach (Fix fix in Fixes)
            {
                if (Apply(fix)) moved++;
            }

            if (moved > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"MuseumBlockerCleanup: {moved} prop(s) moved out of the way.");
            Report();
        }

        /// <summary>Everything cutting a sight line to this screen, logged one line each. Returns the count.</summary>
        private static int ReportOne(MuseumScreenGeometry.ScreenFrame s)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            List<Bounds> enclosures = Enclosures(s, renderers);
            List<Vector3> eyes = EyePoints(s, enclosures);
            List<Vector3> targets = PicturePoints(s);
            int lines = eyes.Count * targets.Count;
            if (lines == 0)
            {
                Debug.LogWarning($"MuseumBlockerCleanup: nowhere to stand in front of '{s.GroupName}'.");
                return 0;
            }

            int count = 0;
            foreach (Renderer r in renderers)
            {
                if (!Blocks(s, r, eyes, targets, out int cut)) continue;

                Bounds b = WorldBounds(r);
                Vector3 d = b.center - s.Centre;
                string where = $"{Vector3.Dot(d, s.Out):F1} m out, {Vector3.Dot(d, s.Right):+0.0;-0.0} m across, " +
                               $"{b.min.y - s.FloorY:F2}–{b.max.y - s.FloorY:F2} m above the floor";

                if (cut < lines * GrazeFraction)
                {
                    Debug.Log($"MuseumBlockerCleanup: '{s.GroupName}' — '{r.name}' grazes {cut} of {lines} sight lines from the rope ({where}).", r);
                    continue;
                }

                Debug.LogWarning($"MuseumBlockerCleanup: '{s.GroupName}' ← '{r.name}' cuts {cut} of {lines} sight lines; {where}.", r);
                count++;
            }

            return count;
        }

        /// <summary>Whether this renderer's bounds cut any eye→picture sight line. <paramref name="cut"/> counts how many.</summary>
        private static bool Blocks(MuseumScreenGeometry.ScreenFrame s, Renderer r, List<Vector3> eyes, List<Vector3> targets, out int cut)
        {
            cut = 0;

            if (!r.enabled || !r.gameObject.activeInHierarchy) return false;
            if (r.transform.IsChildOf(s.Canvas)) return false;
            if (IsGenerated(r.transform)) return false;

            Bounds b = WorldBounds(r);
            if (b.size.y > MaxBlockerHeight || b.size.x > MaxBlockerFootprint || b.size.z > MaxBlockerFootprint) return false;

            // Cheap rejections first: behind the wall, or beyond the farthest stand.
            float along = Vector3.Dot(b.center - s.Centre, s.Out);
            float halfAlong = Extent(b, s.Out);
            if (along + halfAlong < 0f || along - halfAlong > FarStand) return false;

            // Hung on the same wall and beside the picture: the batik, the plaques. Its bounds
            // brush the oblique sight lines from the far side of the room, but nothing on the
            // wall can be in front of what is on the wall.
            if (along - halfAlong < NearWall)
            {
                float lateral = Vector3.Dot(b.center - s.Centre, s.Right);
                float overlap = s.Width * 0.5f + Extent(b, s.Right) - Mathf.Abs(lateral);
                if (overlap < WallHungOverlap) return false;
            }

            foreach (Vector3 eye in eyes)
            {
                foreach (Vector3 target in targets)
                {
                    if (SegmentHitsBox(eye, target, b)) cut++;
                }
            }

            return cut > 0;
        }

        /// <summary>
        /// The rope enclosures on this screen's floor, grown by <see cref="BodyDepth"/>: a visitor
        /// cannot stand inside one. The ropes are the building's own <c>RM_*_Ropes</c> meshes,
        /// hip-high, ringing each vitrine.
        /// </summary>
        private static List<Bounds> Enclosures(MuseumScreenGeometry.ScreenFrame s, Renderer[] renderers)
        {
            var result = new List<Bounds>();
            foreach (Renderer r in renderers)
            {
                if (!r.name.EndsWith("_Ropes")) continue;

                Bounds b = WorldBounds(r);
                if (Mathf.Abs(b.center.y - s.FloorY) > 2f) continue;

                b.Expand(new Vector3(2f * BodyDepth, 10f, 2f * BodyDepth));
                result.Add(b);
            }

            return result;
        }

        /// <summary>Eye points on a grid of standing spots in front of the screen, skipping any inside a rope enclosure.</summary>
        private static List<Vector3> EyePoints(MuseumScreenGeometry.ScreenFrame s, List<Bounds> enclosures)
        {
            var eyes = new List<Vector3>();
            for (float distance = NearStand; distance <= FarStand + 1e-3f; distance += StandStep)
            {
                for (int j = 0; j < LateralSteps; j++)
                {
                    float lateral = Mathf.Lerp(-StandSpread, StandSpread, j / (float)(LateralSteps - 1));
                    Vector3 p = s.FloorPoint(distance, lateral);
                    var eye = new Vector3(p.x, s.FloorY + EyeHeight, p.z);

                    bool roped = false;
                    foreach (Bounds e in enclosures)
                    {
                        if (e.Contains(eye)) { roped = true; break; }
                    }

                    if (!roped) eyes.Add(eye);
                }
            }

            return eyes;
        }

        /// <summary>Points spread over the picture, edges included — the corners are what get clipped first.</summary>
        private static List<Vector3> PicturePoints(MuseumScreenGeometry.ScreenFrame s)
        {
            var points = new List<Vector3>(PictureColumns * PictureRows);
            for (int row = 0; row < PictureRows; row++)
            {
                float v = Mathf.Lerp(-0.5f, 0.5f, row / (float)(PictureRows - 1)) * s.Height;
                for (int col = 0; col < PictureColumns; col++)
                {
                    float u = Mathf.Lerp(-0.5f, 0.5f, col / (float)(PictureColumns - 1)) * s.Width;
                    points.Add(s.Centre + s.Right * u + s.Up * v);
                }
            }

            return points;
        }

        /// <summary>Slab test of a segment against an axis-aligned box.</summary>
        private static bool SegmentHitsBox(Vector3 a, Vector3 b, Bounds box)
        {
            Vector3 d = b - a;
            float tMin = 0f;
            float tMax = 1f;

            for (int axis = 0; axis < 3; axis++)
            {
                float origin = a[axis];
                float dir = d[axis];
                float lo = box.min[axis];
                float hi = box.max[axis];

                if (Mathf.Abs(dir) < 1e-6f)
                {
                    if (origin < lo || origin > hi) return false;
                    continue;
                }

                float t1 = (lo - origin) / dir;
                float t2 = (hi - origin) / dir;
                if (t1 > t2) (t1, t2) = (t2, t1);

                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }

            return true;
        }

        private static bool Apply(Fix fix)
        {
            GameObject go = GameObject.Find(fix.Name);
            var renderer = go == null ? null : go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"MuseumBlockerCleanup: no renderer named '{fix.Name}' for '{fix.Screen}'; skipped.");
                return false;
            }

            Transform t = renderer.transform;
            Quaternion rotation = fix.Rotation ?? t.rotation;

            bool turn = Quaternion.Angle(t.rotation, rotation) > AngleTolerance;
            Vector3 centreNow = WorldBounds(renderer).center;
            if (!turn && Vector3.Distance(centreNow, fix.Target) <= PositionTolerance) return false;

            Undo.RecordObject(t, "Clear Viewing Blocker");

            // Rotate first, then re-read the bounds: the centre shifts when a mesh whose
            // origin is not its middle turns over.
            if (turn) t.rotation = rotation;
            Vector3 delta = fix.Target - WorldBounds(renderer).center;
            t.position += delta;
            EditorUtility.SetDirty(t);

            Debug.Log($"MuseumBlockerCleanup: '{fix.Name}' ({fix.Why}) → {fix.Target}, moved {delta.magnitude:F2} m{(turn ? ", turned" : "")}.", go);
            return true;
        }

        /// <summary>
        /// World-space bounds computed from the mesh, not read from <see cref="Renderer.bounds"/>,
        /// which lags a frame behind a transform change in the editor.
        /// </summary>
        private static Bounds WorldBounds(Renderer r)
        {
            var filter = r.GetComponent<MeshFilter>();
            Mesh mesh = filter == null ? null : filter.sharedMesh;
            if (mesh == null) return r.bounds;

            Bounds local = mesh.bounds;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            var world = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? local.min.x : local.max.x,
                    (i & 2) == 0 ? local.min.y : local.max.y,
                    (i & 4) == 0 ? local.min.z : local.max.z);
                world.Encapsulate(m.MultiplyPoint3x4(corner));
            }

            return world;
        }

        /// <summary>Half-extent of an axis-aligned box along an arbitrary unit direction.</summary>
        private static float Extent(Bounds b, Vector3 dir)
        {
            Vector3 e = b.extents;
            return Mathf.Abs(e.x * dir.x) + Mathf.Abs(e.y * dir.y) + Mathf.Abs(e.z * dir.z);
        }

        private static bool IsGenerated(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.EndsWith("(Generated)")) return true;
            }

            return false;
        }
    }
}
