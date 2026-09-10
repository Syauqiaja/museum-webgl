using Museum.Core;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Lobby.EditorTools
{
    /// <summary>
    /// Generates the whole Lobby scene: camera, EventSystem, the persistent bootstrap singletons,
    /// and the canvas holding the name / entry / room panels plus the toast — with a fully wired
    /// <see cref="LobbyController"/> and a freshly written seat prefab.
    ///
    /// It is a generator rather than a hand-built scene for the reason Assets/Docs/ui-style.md §9
    /// gives: this is a big static layout whose *wiring* (three panels, two input fields, six
    /// buttons, four seat views, a toast) is the tedious, silently-breakable part. A script that
    /// assigns every serialized field cannot leave one null.
    ///
    /// Every number, font and colour below comes from Assets/Docs/ui-style.md through
    /// <see cref="Museum.Core.EditorTools.MuseumUIStyle"/> — nothing is re-derived here, so the
    /// lobby cannot drift away from the Egrang screen.
    ///
    /// Re-running is safe and total: the scene is rebuilt from an empty scene and overwritten at
    /// <c>Assets/Scenes/Lobby.unity</c>, and the seat prefab is re-saved. Hand edits to either are
    /// lost, so make them in this file instead.
    /// </summary>
    public static class LobbyUIBuilder
    {
        const string ScenePath = "Assets/Scenes/Lobby.unity";
        const string SlotPrefabPath = "Assets/Prefabs/Lobby Slot.prefab";
        const string SceneLoaderPrefabPath = "Assets/Prefabs/Scene Loader.prefab";
        const string LoadingScreenPrefabPath = "Assets/GameObject/LoadingScreen.prefab";

        // Panel geometry, in the 800×600 design units ui-style.md is written in.
        static readonly Vector2 NamePanelSize = new Vector2(440f, 230f);
        static readonly Vector2 EntryPanelSize = new Vector2(460f, 330f);
        static readonly Vector2 RoomPanelSize = new Vector2(660f, 420f);

        [MenuItem("Museum/Build Lobby Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog(
                    "Build Lobby scene",
                    $"'{ScenePath}' already exists and will be rebuilt from scratch. Any hand edits to it are lost.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            Rebuild();
        }

        /// <summary>
        /// The build itself, without the "are you sure" — so it can be driven from a script or a
        /// test, where a modal dialog would hang the editor with nobody to click it.
        /// </summary>
        public static void Rebuild()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            CreateEventSystem();
            CreateBootstrap();

            GameObject canvasObject = CreateCanvas(camera);
            var controller = canvasObject.AddComponent<LobbyController>();

            CreateBackground(canvasObject.transform);

            GameObject namePanel = BuildNamePanel(canvasObject.transform, controller, out TMP_InputField nameInput);
            GameObject entryPanel = BuildEntryPanel(canvasObject.transform, controller,
                                                    out TMP_InputField codeInput, out TMP_Text titleText);
            GameObject roomPanel = BuildRoomPanel(canvasObject.transform, controller, out TMP_Text codeText,
                                                  out Transform slotContainer, out Button startButton,
                                                  out TMP_Text startHint);
            GameObject toast = BuildToast(canvasObject.transform, out TMP_Text toastText);

            // The panels ship switched off so that whichever one Start() shows plays its PopupTween
            // on enable (ui-style.md §8) instead of appearing already-on and un-animated.
            namePanel.SetActive(false);
            entryPanel.SetActive(false);
            roomPanel.SetActive(false);
            toast.SetActive(false);

            LobbySlotView slotPrefab = BuildSlotPrefab();

            var so = new SerializedObject(controller);
            so.FindProperty("namePanel").objectReferenceValue = namePanel;
            so.FindProperty("entryPanel").objectReferenceValue = entryPanel;
            so.FindProperty("roomPanel").objectReferenceValue = roomPanel;
            so.FindProperty("nameInput").objectReferenceValue = nameInput;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("codeInput").objectReferenceValue = codeInput;
            so.FindProperty("codeText").objectReferenceValue = codeText;
            so.FindProperty("slotContainer").objectReferenceValue = slotContainer;
            so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("startHintText").objectReferenceValue = startHint;
            so.FindProperty("toastRoot").objectReferenceValue = toast;
            so.FindProperty("toastText").objectReferenceValue = toastText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Built the lobby into '{ScenePath}' with the seat prefab at '{SlotPrefabPath}'. " +
                      "Add the scene to Build Settings if it is not there yet.", canvasObject);
        }

        // ---- Scene scaffolding -------------------------------------------------------------------

        static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var camera = go.GetComponent<Camera>();
            // A flat 2D screen: nothing is rendered but the canvas, so the camera only supplies a
            // clear colour and something for Screen Space – Camera to hang off.
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            return camera;
        }

        static void CreateEventSystem()
        {
            // ui-style.md §1: the new Input System module, never the legacy StandaloneInputModule.
            new GameObject("EventSystem", typeof(EventSystem),
                           typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        /// <summary>
        /// The three DontDestroyOnLoad singletons the lobby assumes exist. They are here because the
        /// scene must be openable directly in the Editor without walking through MainMenu first;
        /// each destroys itself in Awake if one already came from an earlier scene, so a duplicate
        /// is harmless.
        /// </summary>
        static void CreateBootstrap()
        {
            var go = new GameObject("Bootstrap", typeof(SessionData), typeof(SceneLoader),
                                    typeof(ColyseusNetManager));

            // The loading screen prefab is not re-picked here: it is copied from whatever the shared
            // Scene Loader prefab already points at, so the lobby's fade is the same one every other
            // scene uses even if that prefab is later replaced.
            var sceneLoaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneLoaderPrefabPath);
            var source = sceneLoaderPrefab != null ? sceneLoaderPrefab.GetComponent<SceneLoader>() : null;

            Object loadingScreen = source != null
                ? new SerializedObject(source).FindProperty("loadingScreenPrefab").objectReferenceValue
                : null;

            if (loadingScreen == null)
            {
                // The shared Scene Loader prefab did not survive the 2026-08-19 loss; the loading
                // screen itself did, and it is what MainMenu and Museum point at directly.
                loadingScreen = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingScreenPrefabPath);
            }

            if (loadingScreen == null)
            {
                Debug.LogWarning($"Neither '{SceneLoaderPrefabPath}' nor '{LoadingScreenPrefabPath}' yields a " +
                                 "loading screen; the lobby's SceneLoader has none. Assign it by hand.", go);
                return;
            }

            var to = new SerializedObject(go.GetComponent<SceneLoader>());
            to.FindProperty("loadingScreenPrefab").objectReferenceValue = loadingScreen;
            to.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreateCanvas(Camera camera)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;

            // ui-style.md §1, verbatim.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            return go;
        }

        static void CreateBackground(Transform parent)
        {
            GameObject bg = CreateUI("BG", parent, typeof(Image));
            Stretch(bg.GetComponent<RectTransform>());
            SetFrame(bg.GetComponent<Image>(), FrameSprite(), Color.white, ContainerPixelsPerUnitMultiplier);
        }

        // ---- Panels ------------------------------------------------------------------------------

        /// <summary>
        /// The fallback nickname prompt. Only shown when the visitor reached the lobby without
        /// passing MainMenu, which a museum launch point can do.
        /// </summary>
        static GameObject BuildNamePanel(Transform parent, LobbyController controller,
                                         out TMP_InputField nameInput)
        {
            GameObject panel = CreatePanel("Name Panel", parent, NamePanelSize);

            AddLabel(panel.transform, "Text (TMP)", "Siapa nama kamu?", BoldFont, 18f, Color.white,
                     new Vector2(0f, -40f), new Vector2(380f, 26f), new Vector2(0.5f, 1f));

            nameInput = CreateInputField("Name Input", panel.transform, "Nama kamu…",
                                         PlayerNameRules.MaxLength, new Vector2(320f, 44f),
                                         new Vector2(0.5f, 0.5f), new Vector2(0f, 10f),
                                         TMP_InputField.ContentType.Standard);

            GameObject confirm = CreateButton("Confirm", panel.transform, "Lanjut", 200f, 44f, 16f);
            PlaceButton(confirm, new Vector2(0.5f, 0f), new Vector2(0f, 40f));
            UnityEventTools.AddVoidPersistentListener(confirm.GetComponent<Button>().onClick,
                                                      controller.ConfirmName);

            return panel;
        }

        static GameObject BuildEntryPanel(Transform parent, LobbyController controller,
                                          out TMP_InputField codeInput, out TMP_Text titleText)
        {
            GameObject panel = CreatePanel("Entry Panel", parent, EntryPanelSize);

            // Placeholder copy only: LobbyController overwrites it in Start() from the LobbyRequest,
            // which is what keeps this one scene usable for Dakon as well as Egrang.
            titleText = AddLabel(panel.transform, "Title Text (TMP)", "Egrang — 4 Pemain", BoldFont, 18f,
                                 Color.white, new Vector2(0f, -36f), new Vector2(400f, 26f),
                                 new Vector2(0.5f, 1f));

            GameObject create = CreateButton("Create Room", panel.transform, "Buat Ruangan", 240f, 44f, 16f);
            PlaceButton(create, new Vector2(0.5f, 0.5f), new Vector2(0f, 78f));
            UnityEventTools.AddVoidPersistentListener(create.GetComponent<Button>().onClick,
                                                      controller.CreateRoom);

            // Alphanumeric rather than Standard: room codes are the RoomCode alphabet, so the mobile
            // keyboard should not offer autocorrect or punctuation. RoomCode.Sanitize still cleans
            // whatever arrives — this only makes typing it easier.
            codeInput = CreateInputField("Code Input", panel.transform, "Kode ruangan", 8,
                                         new Vector2(240f, 44f), new Vector2(0.5f, 0.5f),
                                         new Vector2(0f, 16f), TMP_InputField.ContentType.Alphanumeric);

            GameObject join = CreateButton("Join Room", panel.transform, "Gabung", 240f, 44f, 16f);
            PlaceButton(join, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f));
            UnityEventTools.AddVoidPersistentListener(join.GetComponent<Button>().onClick,
                                                      controller.JoinRoom);

            // Back goes to the museum the visitor walked in from, not MainMenu: MainMenu would ask
            // for the platform and name again, which visitors read as being logged out.
            GameObject back = CreateButton("Back", panel.transform, "Kembali ke Museum", 200f, 38f, 14f);
            PlaceButton(back, new Vector2(0.5f, 0f), new Vector2(0f, 36f));
            UnityEventTools.AddVoidPersistentListener(back.GetComponent<Button>().onClick,
                                                      controller.BackToMuseum);

            return panel;
        }

        static GameObject BuildRoomPanel(Transform parent, LobbyController controller, out TMP_Text codeText,
                                         out Transform slotContainer, out Button startButton,
                                         out TMP_Text startHint)
        {
            GameObject panel = CreatePanel("Room Panel", parent, RoomPanelSize);

            // The code is the one thing a player has to read off the screen and say out loud across
            // the room, so it gets the display face at wordmark size (ui-style.md §4).
            codeText = AddLabel(panel.transform, "Code Text (TMP)", "-----", DisplayFont, 56f, Cream,
                                new Vector2(0f, -56f), new Vector2(600f, 66f), new Vector2(0.5f, 1f),
                                FontStyles.Bold);

            GameObject copy = CreateButton("Copy", panel.transform, "Salin Kode", 180f, 34f, 13.5f);
            PlaceButton(copy, new Vector2(0.5f, 1f), new Vector2(0f, -112f));
            UnityEventTools.AddVoidPersistentListener(copy.GetComponent<Button>().onClick,
                                                      controller.CopyCode);

            GameObject slots = CreateUI("Slots", panel.transform);
            RectTransform slotsRect = slots.GetComponent<RectTransform>();
            Anchor(slotsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            slotsRect.anchoredPosition = new Vector2(0f, -12f);
            slotsRect.sizeDelta = new Vector2(600f, 160f);

            var layout = slots.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            slotContainer = slots.transform;

            // Why Mulai is grey. LobbyController rewrites it from LobbyRoomSnapshot.MinPlayersToStart
            // on every update; this is only the shape it ships with.
            startHint = AddLabel(panel.transform, "Start Hint (TMP)",
                                 $"Minimal {LobbyRoomSnapshot.MinPlayersToStart} pemain untuk mulai",
                                 MediumFont, 13f, Tan, new Vector2(0f, 104f), new Vector2(600f, 22f),
                                 new Vector2(0.5f, 0f));

            GameObject start = CreateButton("Start", panel.transform, "Mulai", 200f, 44f, 16f);
            PlaceButton(start, new Vector2(0.5f, 0f), new Vector2(-110f, 44f));
            startButton = start.GetComponent<Button>();
            UnityEventTools.AddVoidPersistentListener(startButton.onClick, controller.StartGame);

            GameObject leave = CreateButton("Leave", panel.transform, "Keluar", 200f, 44f, 16f);
            PlaceButton(leave, new Vector2(0.5f, 0f), new Vector2(110f, 44f));
            UnityEventTools.AddVoidPersistentListener(leave.GetComponent<Button>().onClick,
                                                      controller.LeaveRoom);

            return panel;
        }

        /// <summary>
        /// The error/confirmation chip at the bottom of the screen. The controller only toggles the
        /// GameObject, so the fade has to be on the object itself (ui-style.md §8).
        /// </summary>
        static GameObject BuildToast(Transform parent, out TMP_Text toastText)
        {
            GameObject toast = CreateUI("Toast", parent, typeof(Image), typeof(CanvasGroup));
            RectTransform rect = toast.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = new Vector2(0f, 56f);
            rect.sizeDelta = new Vector2(420f, 44f);
            SetFrame(toast.GetComponent<Image>(), CornerFrameSprite(), Color.white,
                     ChipPixelsPerUnitMultiplier);

            Component fade = AddTween(toast, "FadeTween");
            if (fade != null)
            {
                // FadeTween ships at 0.6, tuned for an empty dim backdrop. The message lives inside
                // this CanvasGroup, so anything below 1 leaves the text permanently washed out
                // (ui-style.md §6b's rule, same reason).
                var fadeObject = new SerializedObject(fade);
                fadeObject.FindProperty("toAlpha").floatValue = 1f;
                fadeObject.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject textObject = CreateUI("Text (TMP)", toast.transform, typeof(TextMeshProUGUI));
            Stretch(textObject.GetComponent<RectTransform>(), 12f);
            var tmp = textObject.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, RegularFont, 14f, Tan);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = string.Empty;
            toastText = tmp;

            return toast;
        }

        // ---- Seat prefab -------------------------------------------------------------------------

        /// <summary>
        /// Writes the one-seat card to <see cref="SlotPrefabPath"/> and returns the prefab's view
        /// component. Four of these are instantiated at runtime by LobbyController, per
        /// ui-style.md §9's "repeated items = prefab + container" rule.
        /// </summary>
        static LobbySlotView BuildSlotPrefab()
        {
            GameObject card = new GameObject("Lobby Slot", typeof(RectTransform), typeof(Image),
                                             typeof(LobbySlotView));
            card.GetComponent<RectTransform>().sizeDelta = new Vector2(138f, 150f);

            var element = card.AddComponent<LayoutElement>();
            element.preferredWidth = 138f;
            element.preferredHeight = 150f;

            SetFrame(card.GetComponent<Image>(), FrameSprite(), Color.white,
                     ContainerPixelsPerUnitMultiplier);

            TMP_Text seatText = AddLabel(card.transform, "Seat Text (TMP)", "Pemain 1", MediumFont, 8f,
                                         Color.white, new Vector2(0f, -18f), new Vector2(120f, 14f),
                                         new Vector2(0.5f, 1f));

            TMP_Text nameText = AddLabel(card.transform, "Name Text (TMP)", "Menunggu…", BoldFont, 13.5f,
                                         Color.white, new Vector2(0f, 6f), new Vector2(124f, 40f),
                                         new Vector2(0.5f, 0.5f));
            nameText.textWrappingMode = TextWrappingModes.Normal;

            GameObject hostBadge = AddLabel(card.transform, "Host Badge", "Host", BoldFont, 10f, Gold,
                                            new Vector2(0f, 40f), new Vector2(120f, 14f),
                                            new Vector2(0.5f, 0f)).gameObject;
            GameObject youBadge = AddLabel(card.transform, "You Badge", "Kamu", BoldFont, 10f, Tan,
                                           new Vector2(0f, 22f), new Vector2(120f, 14f),
                                           new Vector2(0.5f, 0f)).gameObject;

            // Both badges are exceptions, not decoration: LobbySlotView switches them on for the one
            // seat each applies to, so they ship off.
            hostBadge.SetActive(false);
            youBadge.SetActive(false);

            var view = card.GetComponent<LobbySlotView>();
            var so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("seatText").objectReferenceValue = seatText;
            so.FindProperty("hostBadge").objectReferenceValue = hostBadge;
            so.FindProperty("youBadge").objectReferenceValue = youBadge;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card, SlotPrefabPath);
            Object.DestroyImmediate(card);

            return prefab != null ? prefab.GetComponent<LobbySlotView>() : null;
        }

        // ---- Small shared pieces -------------------------------------------------------------------

        /// <summary>A centred dialog panel: cornered frame at the §6 dialog rung, CanvasGroup + PopupTween.</summary>
        static GameObject CreatePanel(string name, Transform parent, Vector2 size)
        {
            GameObject panel = CreateUI(name, parent, typeof(Image), typeof(CanvasGroup));
            RectTransform rect = panel.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            SetFrame(panel.GetComponent<Image>(), CornerFrameSprite(), Color.white, 1f);
            // The panel is the click surface between its own buttons — without this, a click that
            // misses a button falls through to whatever is behind the lobby.
            panel.GetComponent<Image>().raycastTarget = true;

            AddTween(panel, "PopupTween");
            return panel;
        }

        static void PlaceButton(GameObject button, Vector2 anchor, Vector2 position)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            Anchor(rect, anchor, anchor, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = position;
        }

        /// <summary>
        /// A TMP input field composed by hand — there is no style helper for one because the lobby is
        /// the first screen in the project to take typed input. Same frame and stock tint as a button
        /// so the two read as one family.
        /// </summary>
        static TMP_InputField CreateInputField(string name, Transform parent, string placeholder,
                                               int characterLimit, Vector2 size, Vector2 anchor,
                                               Vector2 position, TMP_InputField.ContentType contentType)
        {
            GameObject go = CreateUI(name, parent, typeof(Image), typeof(TMP_InputField));
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, anchor, anchor, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var background = go.GetComponent<Image>();
            SetFrame(background, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
            // SetFrame defaults frames to decorative; this one has to be clickable to focus the field.
            background.raycastTarget = true;

            // TMP_InputField needs a masked viewport of its own, or a long value draws outside the frame.
            GameObject viewport = CreateUI("Text Area", go.transform, typeof(RectMask2D));
            Stretch(viewport.GetComponent<RectTransform>(), 12f);

            GameObject textObject = CreateUI("Text", viewport.transform, typeof(TextMeshProUGUI));
            Stretch(textObject.GetComponent<RectTransform>());
            var text = textObject.GetComponent<TextMeshProUGUI>();
            StyleText(text, BoldFont, 16f, Color.white);
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.text = string.Empty;

            GameObject placeholderObject = CreateUI("Placeholder", viewport.transform, typeof(TextMeshProUGUI));
            Stretch(placeholderObject.GetComponent<RectTransform>());
            var placeholderText = placeholderObject.GetComponent<TextMeshProUGUI>();
            StyleText(placeholderText, RegularFont, 14f, Tan);
            placeholderText.alignment = TextAlignmentOptions.Left;
            placeholderText.textWrappingMode = TextWrappingModes.NoWrap;
            placeholderText.text = placeholder;

            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = viewport.GetComponent<RectTransform>();
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.fontAsset = text.font;
            field.pointSize = text.fontSize;
            field.characterLimit = characterLimit;
            field.contentType = contentType;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.caretColor = Cream;
            field.customCaretColor = true;
            field.targetGraphic = background;
            field.transition = Selectable.Transition.ColorTint;
            field.colors = ColorBlock.defaultColorBlock;
            field.text = string.Empty;

            // Assigning fontAsset/pointSize pushes the field's own face and size onto both child
            // labels, which silently overwrites the placeholder's Roboto-Regular 14 with the value
            // label's Bold 16. Re-styling afterwards sticks, so the placeholder keeps the body role
            // ui-style.md §4 gives it. The value label is restyled too — same order, no surprises if
            // the two ever stop sharing a font.
            StyleText(text, BoldFont, 16f, Color.white);
            StyleText(placeholderText, RegularFont, 14f, Tan);

            return field;
        }
    }
}
