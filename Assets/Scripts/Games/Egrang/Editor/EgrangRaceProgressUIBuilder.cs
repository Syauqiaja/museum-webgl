using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Museum.Games.Egrang.EditorTools.EgrangUIStyle;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Builds the course readout: the start–finish strip with the player's marker sliding along it,
    /// plus the <see cref="EgrangRaceTrack"/> markers it measures against.
    ///
    /// The strip goes under the run UI — the same canvas as the timing bar — so it is hidden with it
    /// while the stick-selection panel is up and appears when the run starts.
    ///
    /// A fresh track is laid down the player's own forward axis at <see cref="DefaultCourseMeters"/>,
    /// which is a guess at where the lane ends. Drag the finish marker to the real line; the gizmo in
    /// the Scene view prints the length as you go. An existing track is left exactly where it is.
    /// </summary>
    public static class EgrangRaceProgressUIBuilder
    {
        const string HudName = "Race Progress";
        const string TrackName = "Race Track";

        /// <summary>Where the finish lands on a fresh track, in metres down the player's forward axis. The Egrang lane runs about this far before the fences stop.</summary>
        const float DefaultCourseMeters = 52f;

        [MenuItem("Museum/Egrang/Build Race Progress UI")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Egrang", "No scene is open.", "OK");
                return;
            }

            SkillCheckBar bar = Object.FindFirstObjectByType<SkillCheckBar>(FindObjectsInactive.Include);
            Canvas runCanvas = bar != null ? bar.GetComponentInParent<Canvas>(true) : null;

            if (runCanvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Egrang race progress",
                    "No run canvas found. Build or place the timing bar first — the progress strip is " +
                    "part of the run HUD and hides with it.",
                    "OK");
                return;
            }

            GameObject existing = FindChild(runCanvas.transform, HudName);
            if (existing != null &&
                !EditorUtility.DisplayDialog(
                    "Egrang race progress",
                    $"'{HudName}' already exists under '{runCanvas.name}'. Replace it?",
                    "Replace", "Cancel"))
            {
                return;
            }

            if (existing != null) Undo.DestroyObjectImmediate(existing);

            EgrangRaceTrack track = EnsureTrack();
            GameObject hud = BuildHud(runCanvas.transform, track);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud;
            EditorGUIUtility.PingObject(hud);
            Debug.Log($"Built '{HudName}' under '{runCanvas.name}'. Course is " +
                      $"{track.LengthMeters:0.#} m — drag '{TrackName}/Finish Line' to the real finish.", hud);
        }

        // ---- The course --------------------------------------------------------------------------

        /// <summary>
        /// The scene's track, created down the player's forward axis if there is none. An existing one
        /// is returned untouched: its markers have been placed by hand by then, and moving them back to
        /// a default would be the generator overwriting the level design.
        /// </summary>
        static EgrangRaceTrack EnsureTrack()
        {
            var existing = Object.FindFirstObjectByType<EgrangRaceTrack>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var mover = Object.FindFirstObjectByType<EgrangStepMover>(FindObjectsInactive.Include);
            Transform racer = mover != null ? mover.transform : null;

            Vector3 startPosition = racer != null ? racer.position : Vector3.zero;
            Vector3 forward = racer != null ? racer.forward : Vector3.forward;

            var root = new GameObject(TrackName);
            Undo.RegisterCreatedObjectUndo(root, UndoLabel);
            root.transform.position = startPosition;

            Transform start = CreateMarker("Start Line", root.transform, startPosition);
            Transform finish = CreateMarker("Finish Line", root.transform, startPosition + forward * DefaultCourseMeters);

            var track = Undo.AddComponent<EgrangRaceTrack>(root);
            var so = new SerializedObject(track);
            so.FindProperty("start").objectReferenceValue = start;
            so.FindProperty("finish").objectReferenceValue = finish;
            so.FindProperty("racer").objectReferenceValue = racer;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (racer == null)
            {
                Debug.LogWarning($"No {nameof(EgrangStepMover)} in the scene, so '{TrackName}' has no racer " +
                                 "and sits at the origin. Assign both by hand.", root);
            }

            return track;
        }

        static Transform CreateMarker(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        // ---- The strip ---------------------------------------------------------------------------

        static GameObject BuildHud(Transform parent, EgrangRaceTrack track)
        {
            GameObject hud = CreateUI(HudName, parent, typeof(Image));
            RectTransform hudRect = hud.GetComponent<RectTransform>();
            // Top centre: the run's other readout, the timing bar, owns the bottom of the screen.
            Anchor(hudRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            hudRect.anchoredPosition = new Vector2(0f, -16f);
            hudRect.sizeDelta = new Vector2(420f, 58f);
            SetFrame(hud.GetComponent<Image>(), CornerFrameSprite(), Color.white, ChipPixelsPerUnitMultiplier);

            AddLabel(hud.transform, "Start Label", "MULAI", RegularFont, 11f, Tan,
                     new Vector2(46f, 15f), new Vector2(80f, 16f), new Vector2(0f, 0.5f))
                .alignment = TextAlignmentOptions.MidlineLeft;

            AddLabel(hud.transform, "Finish Label", "FINIS", RegularFont, 11f, Tan,
                     new Vector2(-46f, 15f), new Vector2(80f, 16f), new Vector2(1f, 0.5f))
                .alignment = TextAlignmentOptions.MidlineRight;

            TMP_Text remaining = AddLabel(hud.transform, "Remaining", "-- m lagi", BoldFont, 15f, Cream,
                                          new Vector2(0f, 15f), new Vector2(180f, 20f), new Vector2(0.5f, 0.5f));

            GameObject rail = CreateUI("Rail", hud.transform, typeof(Image));
            RectTransform railRect = rail.GetComponent<RectTransform>();
            Anchor(railRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            railRect.anchoredPosition = new Vector2(0f, -11f);
            railRect.sizeDelta = new Vector2(370f, 6f);
            var railImage = rail.GetComponent<Image>();
            railImage.color = InnerFrame;
            railImage.raycastTarget = false;

            // Left-anchored so only its width has to change as the player advances.
            GameObject fill = CreateUI("Fill", rail.transform, typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Anchor(fillRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 6f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = Tan;
            fillImage.raycastTarget = false;

            // Centred anchor and pivot: the view places it by anchoredPosition about the rail's middle,
            // the same convention SkillCheckBar's cursor uses.
            GameObject marker = CreateUI("Marker", rail.transform, typeof(Image));
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            Anchor(markerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(8f, 18f);
            var markerImage = marker.GetComponent<Image>();
            markerImage.color = Gold;
            markerImage.raycastTarget = false;

            var view = Undo.AddComponent<EgrangProgressView>(hud);
            var so = new SerializedObject(view);
            so.FindProperty("track").objectReferenceValue = track;
            so.FindProperty("rail").objectReferenceValue = railRect;
            so.FindProperty("marker").objectReferenceValue = markerRect;
            so.FindProperty("fill").objectReferenceValue = fillRect;
            so.FindProperty("remainingText").objectReferenceValue = remaining;
            so.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        static GameObject FindChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? child.gameObject : null;
        }
    }
}
