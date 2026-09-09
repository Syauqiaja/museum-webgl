using Museum.Core;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Adds Egrang's touch step: a transparent full-screen button behind the HUD that calls the
    /// same <see cref="SkillCheckBar.Press"/> the Space key calls. Hidden under the desktop scheme
    /// by <see cref="TouchOnly"/>.
    /// </summary>
    /// <remarks>
    /// A full-screen zone rather than a small button because a stride is a rhythm action: the
    /// player is watching the bar, not their thumb. It sits at the back of the canvas so the
    /// stick-selection cards and the results panel still take their own taps first.
    /// </remarks>
    public static class EgrangTouchUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Egrang.unity";
        private const string OverlayName = "Egrang Touch";

        [MenuItem("Museum/Rebuild UI/Egrang Touch")]
        public static void Rebuild()
        {
            // This generator is meant to be re-run often, and it both opens scenes (Single mode,
            // which discards whatever was active with no prompt) and saves them. Either action can
            // destroy a developer's unsaved work — the currently active scene if it isn't
            // Egrang.unity, or unrelated edits already sitting in Egrang.unity if it is. Refuse to
            // touch anything until the active scene is clean.
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                Debug.LogError($"EgrangTouchUIBuilder: '{active.name}' has unsaved changes — " +
                                "save or discard them before rebuilding Egrang Touch.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene =
                active.path == ScenePath
                    ? active
                    : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                          ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            SkillCheckBar bar = FindLiveBar(scene);

            if (bar == null)
            {
                Debug.LogError("EgrangTouchUIBuilder: no SkillCheckBar in Egrang.unity — nothing to tap.");
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == OverlayName) Object.DestroyImmediate(root);
            }

            var overlay = new GameObject(OverlayName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TouchOnly));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(overlay, scene);

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Behind the HUD: the cards and the results panel must win a shared tap.
            canvas.sortingOrder = -10;

            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            GameObject zone = CreateUI("Tap Zone", overlay.transform, typeof(Image), typeof(Button));
            Stretch(zone.GetComponent<RectTransform>());

            var image = zone.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            var button = zone.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            UnityEventTools.AddPersistentListener(button.onClick, bar.Press);

            EditorUtility.SetDirty(button);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("EgrangTouchUIBuilder: tap zone wired to SkillCheckBar.Press.");
        }

        /// <summary>
        /// Finds the bar the running race actually uses. <see cref="EgrangRace"/> is the
        /// authority on which of the scene's (possibly several) <see cref="SkillCheckBar"/>
        /// instances is live — its private serialized <c>bar</c> field is what
        /// <c>EgrangRace</c> subscribes to at runtime, so binding to any other bar found by a
        /// type search can silently wire the tap zone to an orphan that nothing listens to.
        /// </summary>
        private static SkillCheckBar FindLiveBar(UnityEngine.SceneManagement.Scene scene)
        {
            EgrangRace race = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                race = root.GetComponentInChildren<EgrangRace>(true);
                if (race != null) break;
            }

            SkillCheckBar[] allBars = null;
            if (race != null)
            {
                var so = new SerializedObject(race);
                SerializedProperty prop = so.FindProperty("bar");
                var wired = prop?.objectReferenceValue as SkillCheckBar;
                if (wired != null)
                {
                    allBars = FindAllBars(scene);
                    if (allBars.Length > 1)
                    {
                        Debug.LogWarning($"EgrangTouchUIBuilder: {allBars.Length} SkillCheckBar " +
                            $"instances found in Egrang.unity — binding to '{wired.name}', the " +
                            "one EgrangRace.bar points at.");
                    }
                    return wired;
                }

                Debug.LogWarning("EgrangTouchUIBuilder: found EgrangRace but its 'bar' field is " +
                    "unset — falling back to a type search, which may pick an orphan.");
            }
            else
            {
                Debug.LogWarning("EgrangTouchUIBuilder: no EgrangRace found in Egrang.unity — " +
                    "falling back to a type search for SkillCheckBar, which may pick an orphan.");
            }

            allBars ??= FindAllBars(scene);
            if (allBars.Length > 1)
            {
                Debug.LogWarning($"EgrangTouchUIBuilder: {allBars.Length} SkillCheckBar " +
                    $"instances found in Egrang.unity — binding to '{allBars[0].name}' " +
                    "(first found by type search, not verified against any race).");
            }

            return allBars.Length > 0 ? allBars[0] : null;
        }

        private static SkillCheckBar[] FindAllBars(UnityEngine.SceneManagement.Scene scene)
        {
            var found = new System.Collections.Generic.List<SkillCheckBar>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<SkillCheckBar>(true));
            }
            return found.ToArray();
        }
    }
}
