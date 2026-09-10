using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Re-hangs the exhibit screens where the building meant them to go. As recovered, every
    /// one of the eighteen video canvases is yawed 15° off its wall and drifts up to 70 cm from
    /// the architect's picture spot, so the placeholder poster the building ships there
    /// (<c>R2_Egrang_gimg</c>, <c>DK_Graphic_img</c>, …) pokes through the tilted video.
    /// </summary>
    /// <remarks>
    /// Menu: <c>Museum/Decor/Align Screens To Walls</c>; <see cref="MuseumScreenFrameBuilder"/>
    /// runs it first. The building's own <c>*_gimg</c> quad is the authority: the canvas is
    /// snapped to the nearest 90° yaw and moved to sit a centimetre in front of that quad, then
    /// the quad and its brass <c>*_gframe</c> are switched off — the generated bezel takes the
    /// frame's place. A screen with no poster nearby is instead pushed to
    /// <see cref="WallGap"/> off the nearest wall slab behind it. Two posters (Bekelan's on
    /// LT2, Engklek's on LT1) are glued to the far face of their wall, so a screen sat on them
    /// would show its back to the room through 30 cm of slab; those are brought forward to the
    /// wall's room face instead.
    ///
    /// Height is not taken from the poster. The building hangs every picture with its bottom
    /// edge 1.5 m off the floor and parks a 1.8 m glass vitrine 4–5 m in front of it, so from
    /// behind the rope barrier the case's top rail cuts across the lower third of the video.
    /// Each screen is raised until its bottom edge clears <see cref="BottomAboveFloor"/>,
    /// which puts it above the case rails and leaves room for the signboard above the bezel
    /// under the 5 m ceiling. Lesson plaques hang beside the screens and were generated with
    /// the same tilt copied from them, so each is snapped to the same yaw, slid onto its
    /// screen's plane and centred at the same height, so plaque and video read as one band.
    /// The building's batik hanging beside each picture is then kept off both: slid along the
    /// wall if the raised bezel cuts into it, and mirrored to the far side of the screen if it
    /// shares that side with the plaque.
    ///
    /// Only the Canvas, plaque and hanging transforms move — trigger volumes, video wiring and
    /// the <c>Vid *</c> groups are untouched — and a screen already within a degree and two
    /// centimetres of its spot is left alone, so running it twice changes nothing. Each move is
    /// logged and undoable.
    /// </remarks>
    public static class MuseumScreenAligner
    {
        /// <summary>Gap from a wall face to the canvas plane when there is no poster to sit on.</summary>
        public const float WallGap = 0.05f;

        /// <summary>Gap from the poster quad's front face to the canvas plane.</summary>
        public const float PosterGap = 0.01f;

        /// <summary>
        /// Height of the picture's bottom edge over the visitor's floor. The vitrine rails top out
        /// at 1.86 m, 4.9 m out, and the rope keeps a visitor 6.3 m back: at a 1.6 m eye, a
        /// 2.4 m sill clears the rails from 7 m onward and only the bottom hand's-breadth is
        /// clipped from right at the rope. The ceiling is 4.6 m up, which leaves half a metre
        /// over the bezel for the signboard.
        /// </summary>
        public const float BottomAboveFloor = 2.4f;

        /// <summary>Clear wall between the bezel and a batik hanging beside it.</summary>
        public const float HangingGap = 0.25f;

        /// <summary>
        /// Where a hanging's back face sits behind the screen plane. Sixteen of the eighteen
        /// cloths hang with their rod on the wall face, 16 cm behind the poster the screen sits on.
        /// </summary>
        public const float ClothBackFaceGap = 0.16f;

        private const float PosterReach = 1.5f;

        /// <summary>How far past a wall's room face a poster may sit and still count as that wall's.</summary>
        private const float BuriedReach = 0.8f;
        private const float MinWallHeight = 2.0f;
        private const float MaxWallThickness = 0.6f;
        private const float MaxWallReach = 3.0f;
        private const float PlaqueReach = 6.0f;

        // Tolerances below which a canvas counts as already aligned.
        private const float YawTolerance = 1.0f;
        private const float PositionTolerance = 0.02f;

        [MenuItem("Museum/Decor/Align Screens To Walls")]
        public static void AlignMenu()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            int moved = AlignAll(scene);
            if (moved == 0)
            {
                Debug.Log("MuseumScreenAligner: every screen is already flat on its wall.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Aligns every screen and lesson plaque in the scene; returns how many moved.</summary>
        public static int AlignAll(Scene scene)
        {
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<MuseumScreenGeometry.ScreenFrame> screens = MuseumScreenGeometry.All(scene);
            int moved = 0;

            var planes = new List<(Vector3 position, Quaternion rotation)>();
            foreach (MuseumScreenGeometry.ScreenFrame screen in screens)
            {
                if (AlignScreen(screen, renderers, out Vector3 position, out Quaternion rotation)) moved++;
                planes.Add((position, rotation));
            }

            var plaques = new List<Transform>();
            foreach (LessonPanel panel in Object.FindObjectsByType<LessonPanel>(FindObjectsSortMode.None))
            {
                if (panel.gameObject.scene != scene) continue;
                if (AlignPlaque(panel.transform, planes)) moved++;
                plaques.Add(panel.transform);
            }

            foreach (MuseumScreenGeometry.ScreenFrame screen in screens)
            {
                if (MirrorHangingOffPlaque(screen, plaques, renderers)) moved++;
            }

            return moved;
        }

        /// <summary>
        /// Moves the batik hanging to the far side of the screen when it shares a wall with the
        /// lesson plaque. Eleven of the eighteen hangings were placed left of the picture, which
        /// is the wall the plaques were later generated onto, so the cloth hung across the copy.
        /// Every exhibit wall is symmetric about its picture — seven rooms already carry the
        /// cloth on the right at the same offset — so the mirrored spot is real wall.
        /// </summary>
        private static bool MirrorHangingOffPlaque(MuseumScreenGeometry.ScreenFrame screen, List<Transform> plaques, Renderer[] renderers)
        {
            Vector3 centre = screen.Canvas.position;
            Vector3 right = screen.Canvas.right;

            if (!TryFindPlaque(screen, plaques, out RectTransform plaque)) return false;

            float plaqueLateral = Vector3.Dot(plaque.position - centre, right);
            float plaqueHalf = plaque.sizeDelta.x * plaque.lossyScale.x * 0.5f;
            bool moved = false;

            foreach (Renderer r in renderers)
            {
                if (!r.name.EndsWith("_Batik") || !r.gameObject.activeInHierarchy) continue;

                Bounds b = r.bounds;
                Vector3 d = b.center - centre;
                if (Mathf.Abs(Vector3.Dot(d, screen.Into)) > 1f || Mathf.Abs(d.y) > 1.5f) continue;

                float lateral = Vector3.Dot(d, right);
                if (Mathf.Abs(lateral) > PlaqueReach) continue;

                float halfCloth = Mathf.Abs(Vector3.Dot(b.size, right)) * 0.5f;
                if (Mathf.Abs(lateral - plaqueLateral) >= plaqueHalf + halfCloth + HangingGap) continue;

                Vector3 shift = right * (-2f * lateral);
                Transform t = r.transform;
                Undo.RecordObject(t, "Align To Wall");
                t.position += shift;
                EditorUtility.SetDirty(t);
                Debug.Log($"MuseumScreenAligner: mirrored '{r.name}' to the far side of '{screen.GroupName}', off its lesson plaque.");
                moved = true;
            }

            return moved;
        }

        /// <summary>The lesson plaque on this screen's wall band, if one hangs there.</summary>
        private static bool TryFindPlaque(MuseumScreenGeometry.ScreenFrame screen, List<Transform> plaques, out RectTransform plaque)
        {
            plaque = null;
            float best = PlaqueReach;
            Vector3 centre = screen.Canvas.position;

            foreach (Transform t in plaques)
            {
                Vector3 d = t.position - centre;
                if (Mathf.Abs(Vector3.Dot(d, screen.Into)) > 0.3f || Mathf.Abs(d.y) > 0.3f) continue;

                float lateral = Mathf.Abs(Vector3.Dot(d, screen.Canvas.right));
                if (lateral >= best) continue;

                best = lateral;
                plaque = t as RectTransform;
            }

            return plaque != null;
        }

        private static bool AlignScreen(MuseumScreenGeometry.ScreenFrame screen, Renderer[] renderers,
            out Vector3 position, out Quaternion rotation)
        {
            Transform canvas = screen.Canvas;
            Vector3 centre = canvas.position;
            position = centre;
            rotation = canvas.rotation;

            Quaternion target = SnapYaw(canvas);
            Vector3 into = target * Vector3.forward;
            Vector3 right = target * Vector3.right;

            string anchor;
            if (TryFindPoster(centre, into, renderers, out Renderer poster))
            {
                float thickness = Mathf.Abs(Vector3.Dot(poster.bounds.size, into));
                float along = Vector3.Dot(poster.bounds.center - centre, into);
                position = centre + into * (along - thickness * 0.5f - PosterGap);
                anchor = poster.name;

                // Two posters (Bekelan's, Engklek's) are stuck to the far face of their wall, so
                // sitting on them buries the screen in the slab. Step back out to the room side.
                if (TryUnbury(position, into, right, canvas, renderers, out Renderer slab, out float outward))
                {
                    position -= into * outward;
                    anchor = $"{slab.name}, poster {poster.name} being on its far face";
                }
            }
            else if (TryFindWall(centre, into, right, canvas, renderers, out Renderer wall, out float faceDistance))
            {
                position = centre + into * (faceDistance - WallGap);
                anchor = wall.name;
            }
            else
            {
                Debug.LogWarning($"MuseumScreenAligner: nothing to hang '{screen.GroupName}' on; left as is.");
                return false;
            }

            position.y = screen.FloorY + BottomAboveFloor + screen.Height * 0.5f;
            rotation = target;
            HidePlaceholderGraphics(screen.GroupName, position, renderers);
            bool moved = Move(canvas, screen.GroupName, position, target, anchor);
            if (ClearWallHangings(screen, position, right, renderers)) moved = true;
            return moved;
        }

        /// <summary>
        /// Slides the building's batik hanging (<c>RM_*_Batik</c>) along the wall when the raised
        /// picture now overlaps it, and pulls it onto the screen's wall plane when the screen was
        /// moved off the poster's. The hangings were placed beside the poster at its original
        /// height; two of them (Dakon's <c>RM_src_Batik</c>, Gobak's) were tucked so close that
        /// the bezel would cut into the cloth.
        /// </summary>
        private static bool ClearWallHangings(MuseumScreenGeometry.ScreenFrame screen, Vector3 centre, Vector3 right, Renderer[] renderers)
        {
            float halfPicture = screen.Width * 0.5f + MuseumScreenFrameBuilder.Overhang + HangingGap;
            float halfHeight = screen.Height * 0.5f + MuseumScreenFrameBuilder.Overhang;
            bool moved = false;

            foreach (Renderer r in renderers)
            {
                if (!r.name.EndsWith("_Batik") || !r.gameObject.activeInHierarchy) continue;

                Bounds b = r.bounds;
                if (b.max.y < centre.y - halfHeight || b.min.y > centre.y + halfHeight) continue;

                Vector3 d = b.center - centre;
                if (Mathf.Abs(Vector3.Dot(d, screen.Into)) > 1f) continue;

                float lateral = Vector3.Dot(d, right);
                if (Mathf.Abs(lateral) > PlaqueReach) continue; // another exhibit's cloth down the same wall

                float halfCloth = Mathf.Abs(Vector3.Dot(b.size, right)) * 0.5f;
                float overlap = Mathf.Max(0f, halfPicture + halfCloth - Mathf.Abs(lateral));

                // The cloth's back face sits where the poster used to: on the screen's wall plane.
                // A screen pulled out of its slab takes the hanging with it, or the cloth stays
                // half-buried (Bekelan) or floating in front of a window (Engklek).
                float clothDepth = Mathf.Abs(Vector3.Dot(b.size, screen.Into));
                float backFace = Vector3.Dot(d, screen.Into) + clothDepth * 0.5f;
                float depthShift = ClothBackFaceGap - backFace;

                if (overlap <= PositionTolerance && Mathf.Abs(depthShift) <= PositionTolerance) continue;

                Vector3 shift = right * (Mathf.Sign(lateral) * overlap) + screen.Into * depthShift;
                Transform t = r.transform;
                Undo.RecordObject(t, "Align To Wall");
                t.position += shift;
                EditorUtility.SetDirty(t);
                Debug.Log($"MuseumScreenAligner: moved '{r.name}' {overlap:F2} m along and {depthShift:F2} m into the wall, clear of '{screen.GroupName}'.");
                moved = true;
            }

            return moved;
        }

        /// <summary>
        /// Snaps the plaque's yaw, slides it onto the plane of the nearest screen facing the
        /// same way and centres it at that screen's height, so plaque and video read as one
        /// band on the wall.
        /// </summary>
        private static bool AlignPlaque(Transform plaque, List<(Vector3 position, Quaternion rotation)> planes)
        {
            Quaternion target = SnapYaw(plaque);
            Vector3 into = target * Vector3.forward;

            float bestDistance = PlaqueReach;
            bool found = false;
            Vector3 onPlane = plaque.position;

            foreach ((Vector3 position, Quaternion rotation) plane in planes)
            {
                if (Quaternion.Angle(plane.rotation, target) > YawTolerance) continue;

                float d = Vector3.Distance(plane.position, plaque.position);
                if (d >= bestDistance) continue;

                bestDistance = d;
                found = true;
                onPlane = plaque.position + into * Vector3.Dot(plane.position - plaque.position, into);
                onPlane.y = plane.position.y;
            }

            if (!found)
            {
                Debug.LogWarning($"MuseumScreenAligner: no screen within {PlaqueReach} m of '{plaque.name}'; only its yaw was snapped.");
            }

            return Move(plaque, plaque.name, onPlane, target, found ? "its screen's plane" : "yaw only");
        }

        private static bool Move(Transform t, string label, Vector3 position, Quaternion rotation, string anchor)
        {
            bool yawOff = Quaternion.Angle(t.rotation, rotation) > YawTolerance;
            bool posOff = Vector3.Distance(t.position, position) > PositionTolerance;
            if (!yawOff && !posOff) return false;

            float distance = Vector3.Distance(t.position, position);
            Undo.RecordObject(t, "Align To Wall");
            t.SetPositionAndRotation(position, rotation);
            EditorUtility.SetDirty(t);

            Debug.Log($"MuseumScreenAligner: '{label}' → yaw {rotation.eulerAngles.y:F0}° on {anchor}, moved {distance:F2} m.");
            return true;
        }

        private static Quaternion SnapYaw(Transform t)
        {
            float yaw = Mathf.Round(t.eulerAngles.y / 90f) * 90f;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// The building's poster quad for this spot: a renderer named <c>*_gimg</c> or
        /// <c>*_Graphic_img</c>, thin along <paramref name="into"/>, within reach of the canvas.
        /// </summary>
        private static bool TryFindPoster(Vector3 centre, Vector3 into, Renderer[] renderers, out Renderer poster)
        {
            poster = null;
            float best = PosterReach;

            foreach (Renderer r in renderers)
            {
                if (!IsPosterImage(r.name)) continue;
                if (Mathf.Abs(Vector3.Dot(r.bounds.size, into)) > 0.1f) continue;

                float d = Vector3.Distance(r.bounds.center, centre);
                if (d >= best) continue;

                best = d;
                poster = r;
            }

            return poster != null;
        }

        /// <summary>
        /// The nearest wall-like renderer behind the screen along <paramref name="into"/>: at least
        /// 2 m tall, at most 60 cm thick, overlapping the screen laterally. The museum's walls are
        /// 30 cm slabs (<c>WN.*</c>, <c>WE.*</c>, <c>WS_*</c>, <c>WW.*</c>).
        /// <paramref name="faceDistance"/> is from the canvas centre to the wall's near face.
        /// </summary>
        private static bool TryFindWall(Vector3 centre, Vector3 into, Vector3 right, Transform canvas,
            Renderer[] renderers, out Renderer wall, out float faceDistance)
        {
            wall = null;
            faceDistance = float.MaxValue;

            foreach (Renderer r in renderers)
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (r.transform.IsChildOf(canvas)) continue;
                if (IsGenerated(r.transform)) continue;

                Bounds b = r.bounds;
                if (b.size.y < MinWallHeight) continue;

                float thickness = Mathf.Abs(Vector3.Dot(b.size, into));
                if (thickness > MaxWallThickness) continue;

                float lateral = Vector3.Dot(b.center - centre, right);
                float halfWidth = Mathf.Abs(Vector3.Dot(b.size, right)) * 0.5f;
                if (Mathf.Abs(lateral) > halfWidth + 0.5f) continue;
                if (centre.y < b.min.y - 0.5f || centre.y > b.max.y + 0.5f) continue;

                float d = Vector3.Dot(b.center - centre, into) - thickness * 0.5f;
                if (d < -0.5f || d > MaxWallReach) continue;

                if (d < faceDistance)
                {
                    faceDistance = d;
                    wall = r;
                }
            }

            return wall != null;
        }

        /// <summary>
        /// When a wall slab stands between <paramref name="position"/> and the room — the point
        /// is inside the slab or just beyond its far face — <paramref name="outward"/> is how far
        /// to move back along <c>-into</c> to sit <see cref="WallGap"/> in front of the slab's
        /// room face. Same wall test as <see cref="TryFindWall"/>: tall, thin, laterally
        /// overlapping the screen — which also drops the facade skins, metres deep.
        /// </summary>
        private static bool TryUnbury(Vector3 position, Vector3 into, Vector3 right, Transform canvas,
            Renderer[] renderers, out Renderer slab, out float outward)
        {
            slab = null;
            outward = 0f;

            foreach (Renderer r in renderers)
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (r.transform.IsChildOf(canvas)) continue;
                if (IsGenerated(r.transform)) continue;

                Bounds b = r.bounds;
                if (b.size.y < MinWallHeight) continue;
                if (position.y < b.min.y || position.y > b.max.y) continue;

                float thickness = Mathf.Abs(Vector3.Dot(b.size, into));
                if (thickness > MaxWallThickness) continue;

                float lateral = Vector3.Dot(b.center - position, right);
                float halfWidth = Mathf.Abs(Vector3.Dot(b.size, right)) * 0.5f;
                if (Mathf.Abs(lateral) > halfWidth) continue;

                // Room face of the slab, measured along `into` from the screen: negative means the
                // face is on the room side of the screen, i.e. the wall is in front of it.
                float roomFace = Vector3.Dot(b.center - position, into) - thickness * 0.5f;
                if (roomFace >= 0f || roomFace < -BuriedReach) continue;

                float candidate = -roomFace + WallGap;
                if (candidate <= outward) continue;

                outward = candidate;
                slab = r;
            }

            return slab != null;
        }

        /// <summary>
        /// Switches off the poster quad and brass frame the building ships at this spot; the
        /// video now covers the one and the generated bezel replaces the other.
        /// </summary>
        private static void HidePlaceholderGraphics(string groupName, Vector3 position, Renderer[] renderers)
        {
            var slab = new Bounds(position, new Vector3(3.5f, 2.5f, 3.5f));

            foreach (Renderer r in renderers)
            {
                if (!IsPosterImage(r.name) && !IsPosterFrame(r.name)) continue;
                if (!r.bounds.Intersects(slab)) continue;
                if (!r.gameObject.activeSelf) continue;

                Undo.RecordObject(r.gameObject, "Hide placeholder graphic");
                r.gameObject.SetActive(false);
                EditorUtility.SetDirty(r.gameObject);
                Debug.Log($"MuseumScreenAligner: hid placeholder '{r.name}' behind '{groupName}'.");
            }
        }

        private static bool IsPosterImage(string name) => name.EndsWith("_gimg") || name.EndsWith("_Graphic_img");
        private static bool IsPosterFrame(string name) => name.EndsWith("_gframe") || name.EndsWith("_Graphic_frame");

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
