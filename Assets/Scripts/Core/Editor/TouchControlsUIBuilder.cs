using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Builds the museum's touch overlay: a screen-space canvas holding the floating joystick, the
    /// look area, and the Lompat / Interaksi / ‹ › buttons. It hides itself under the desktop
    /// scheme through <see cref="TouchOnly"/>, so it is safe to leave in the scene always.
    ///
    /// Generated rather than hand-wired for the reason ui-style.md §9 gives: inspector values in
    /// this project have been lost once already, and a generator is how they come back.
    /// </summary>
    public static class TouchControlsUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Museum.unity";
        private const string OverlayName = "Touch Controls";

        [MenuItem("Museum/Rebuild UI/Touch Controls")]
        public static void Rebuild()
        {
            // This generator is meant to be re-run often, and it both opens scenes (Single mode,
            // which discards whatever was active with no prompt) and saves them. Either action can
            // destroy a developer's unsaved work — the currently active scene if it isn't
            // Museum.unity, or unrelated edits already sitting in Museum.unity if it is. Refuse to
            // touch anything until the active scene is clean.
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                Debug.LogError($"TouchControlsUIBuilder: '{active.name}' has unsaved changes — " +
                                "save or discard them before rebuilding Touch Controls.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene =
                active.path == ScenePath
                    ? active
                    : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                          ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == OverlayName) Object.DestroyImmediate(root);
            }

            GameObject overlay = Build();
            SceneManagerMove(overlay, scene);
            WirePlayer(scene, overlay);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("TouchControlsUIBuilder: touch overlay rebuilt in Museum.unity.");
        }

        private static void SceneManagerMove(GameObject go, UnityEngine.SceneManagement.Scene scene)
            => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

        private static GameObject Build()
        {
            var overlay = new GameObject(OverlayName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TouchOnly));

            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the museum's own HUD canvases; the overlay must never be occluded by them.
            canvas.sortingOrder = 100;

            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            overlay.AddComponent<TouchInteractRouter>();

            BuildJoystick(overlay.transform);
            BuildLookArea(overlay.transform);
            BuildButtons(overlay.transform, overlay.GetComponent<TouchInteractRouter>());

            return overlay;
        }

        private static void BuildJoystick(Transform parent)
        {
            // Left third of the screen. A fully transparent Image is still a raycast target, which
            // is what makes an empty area of screen draggable.
            GameObject area = CreateUI("Joystick Area", parent, typeof(Image), typeof(TouchJoystick));
            RectTransform rect = area.GetComponent<RectTransform>();
            Anchor(rect, Vector2.zero, new Vector2(0.35f, 1f), new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = area.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0f);
            backdrop.raycastTarget = true;

            GameObject ring = CreateUI("Ring", area.transform, typeof(Image), typeof(CanvasGroup));
            RectTransform ringRect = ring.GetComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(160f, 160f);

            // Home: bottom-left of the joystick area, where a left thumb already rests. The stick
            // still floats to the finger on touch — this is only where it waits, and waiting
            // somewhere visible is what tells a first-time visitor the control exists.
            Anchor(ringRect, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            ringRect.anchoredPosition = new Vector2(150f, 150f);
            var ringImage = ring.GetComponent<Image>();
            ringImage.sprite = FrameSprite();
            ringImage.color = new Color(Cream.r, Cream.g, Cream.b, 0.25f);
            ringImage.raycastTarget = false;

            GameObject knob = CreateUI("Knob", ring.transform, typeof(Image));
            RectTransform knobRect = knob.GetComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(64f, 64f);
            var knobImage = knob.GetComponent<Image>();
            knobImage.sprite = FrameSprite();
            knobImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.65f);
            knobImage.raycastTarget = false;

            // Left active: TouchJoystick rests it in Awake and dims it through the CanvasGroup.
            ring.GetComponent<CanvasGroup>().alpha = 0.4f;

            var stick = area.GetComponent<TouchJoystick>();
            var serialized = new SerializedObject(stick);
            serialized.FindProperty("ring").objectReferenceValue = ringRect;
            serialized.FindProperty("knob").objectReferenceValue = knobRect;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildLookArea(Transform parent)
        {
            GameObject area = CreateUI("Look Area", parent, typeof(Image), typeof(TouchLookArea));
            RectTransform rect = area.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.35f, 0f), Vector2.one, new Vector2(0.5f, 0.5f));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = area.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0f);
            backdrop.raycastTarget = true;

            // Behind the buttons, so a tap on Lompat is a tap on Lompat and not a one-pixel drag.
            area.transform.SetAsFirstSibling();
        }

        private static void BuildButtons(Transform parent, TouchInteractRouter router)
        {
            GameObject jump = CreateButton("Lompat Button", parent, "Lompat", 140f, 64f, 18f);
            PlaceCorner(jump, new Vector2(1f, 0f), new Vector2(-110f, 100f));

            GameObject interact = CreateButton("Interaksi Button", parent, "Interaksi", 180f, 64f, 18f);
            PlaceCorner(interact, new Vector2(1f, 0f), new Vector2(-110f, 180f));
            UnityEventTools.AddPersistentListener(interact.GetComponent<Button>().onClick, router.Interact);
            ShowWhen(interact, InteractionCue.Doorway);

            GameObject prev = CreateButton("Halaman Sebelumnya", parent, "‹", 64f, 64f, 24f);
            PlaceCorner(prev, new Vector2(0.5f, 0f), new Vector2(-60f, 40f));
            UnityEventTools.AddPersistentListener(prev.GetComponent<Button>().onClick, router.PagePrev);
            ShowWhen(prev, InteractionCue.LessonPaging);

            GameObject next = CreateButton("Halaman Berikutnya", parent, "›", 64f, 64f, 24f);
            PlaceCorner(next, new Vector2(0.5f, 0f), new Vector2(60f, 40f));
            UnityEventTools.AddPersistentListener(next.GetComponent<Button>().onClick, router.PageNext);
            ShowWhen(next, InteractionCue.LessonPaging);
        }

        /// <summary>
        /// Gates a button on the router having something for it to do. Lompat is deliberately not
        /// gated — jumping is always available, so its button is always meaningful.
        /// </summary>
        private static void ShowWhen(GameObject button, InteractionCue cue)
        {
            if (button.GetComponent<CanvasGroup>() == null) Undo.AddComponent<CanvasGroup>(button);

            var gate = button.GetComponent<ShownWhenAvailable>() ?? Undo.AddComponent<ShownWhenAvailable>(button);

            var serialized = new SerializedObject(gate);
            serialized.FindProperty("cue").enumValueIndex = (int)cue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gate);
        }

        private static void PlaceCorner(GameObject go, Vector2 anchor, Vector2 offset)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, anchor, anchor, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = offset;
        }

        /// <summary>
        /// Points the player rig at the overlay: the touch source reads the widgets, and the switch
        /// decides which of the two sources is live.
        /// </summary>
        private static void WirePlayer(UnityEngine.SceneManagement.Scene scene, GameObject overlay)
        {
            FPSController controller = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                controller = root.GetComponentInChildren<FPSController>(true);
                if (controller != null) break;
            }

            if (controller == null)
            {
                Debug.LogWarning("TouchControlsUIBuilder: no FPSController in Museum.unity — " +
                                 "the overlay is built but nothing consumes it.");
                return;
            }

            var joystick = overlay.GetComponentInChildren<TouchJoystick>(true);
            var lookArea = overlay.GetComponentInChildren<TouchLookArea>(true);

            TouchInputSource source = controller.GetComponent<TouchInputSource>();
            if (source == null) source = Undo.AddComponent<TouchInputSource>(controller.gameObject);

            var sourceSerialized = new SerializedObject(source);
            sourceSerialized.FindProperty("controller").objectReferenceValue = controller;
            sourceSerialized.FindProperty("joystick").objectReferenceValue = joystick;
            sourceSerialized.FindProperty("lookArea").objectReferenceValue = lookArea;
            sourceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var reader = controller.GetComponent<FPSInputReader>();
            if (reader == null)
            {
                Debug.LogWarning("TouchControlsUIBuilder: no FPSInputReader on the player — " +
                                  "the desktop control path will be broken until one is added.");
            }

            ControlSchemeSwitch swap = controller.GetComponent<ControlSchemeSwitch>();
            if (swap == null) swap = Undo.AddComponent<ControlSchemeSwitch>(controller.gameObject);

            var swapSerialized = new SerializedObject(swap);
            FillArray(swapSerialized.FindProperty("desktopBehaviours"), new Object[] { reader });
            FillArray(swapSerialized.FindProperty("touchBehaviours"), new Object[] { source });
            swapSerialized.ApplyModifiedPropertiesWithoutUndo();

            // The Lompat button needs the source, which only exists once the player is found.
            Transform jump = overlay.transform.Find("Lompat Button");
            if (jump != null)
            {
                UnityEventTools.AddPersistentListener(jump.GetComponent<Button>().onClick, source.Jump);
                EditorUtility.SetDirty(jump.GetComponent<Button>());
            }

            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(swap);
        }
    }
}
