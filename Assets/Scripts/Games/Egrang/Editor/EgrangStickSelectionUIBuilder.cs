using System;
using System.Collections.Generic;
using System.IO;
using Museum.Games.Egrang;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Builds the Egrang stick-selection screen into the open scene: the three cards, the start
    /// button, and the run root holding the timing bar they configure.
    ///
    /// This is a generator, not a runtime class. The layout is authored here rather than shipped as a
    /// prefab because the panel's wiring — which card holds which profile, which bar the selector
    /// configures — is exactly the part that is tedious and easy to get subtly wrong by hand, and a
    /// script that does it is re-runnable when the scene is rebuilt or a fourth stick is added.
    ///
    /// Every number and colour below comes from <c>Assets/Docs/ui-style.md</c>: the 800×600 canvas,
    /// the palette, the font assets, the nine-slice multipliers, the stock button tint. Generated UI
    /// gets no exemption from the style rules — change them there first, then here.
    ///
    /// Once built, everything is plain scene objects: move, restyle and reparent them freely. Running
    /// the item again replaces the whole root, so keep hand edits in mind before re-running.
    /// </summary>
    public static class EgrangStickSelectionUIBuilder
    {
        const string RootName = "Egrang UI";
        const string LegacyRootName = "EgrangUI";
        const string ProfileFolder = "Assets/Data/Egrang";

        // ui-style.md §1. Every screen in this project is authored at 800×600, so the sizes below are
        // only meaningful against this reference resolution.
        static readonly Vector2 ReferenceResolution = new Vector2(800f, 600f);

        // ui-style.md §5. Nothing here invents a colour.
        static readonly Color Gold = new Color(0.757f, 0.624f, 0.380f, 1f);
        static readonly Color Cream = new Color(0.957f, 0.918f, 0.835f, 1f);
        static readonly Color Tan = new Color(0.847f, 0.753f, 0.631f, 1f);
        static readonly Color Scrim = new Color(0f, 0f, 0f, 0.631f);

        /// <summary>Band behind the heading. Shared with the countdown strip so the two meet as one.</summary>
        static readonly Color HeaderPlate = new Color(0.055f, 0.043f, 0.031f, 0.78f);

        /// <summary>Plate behind an unpicked card's text. Dark enough to read white on, over grass.</summary>
        static readonly Color CardPlateIdle = new Color(0.078f, 0.063f, 0.047f, 0.72f);

        /// <summary>Plate behind the picked card: heavier, so the choice reads without hunting for the gold frame.</summary>
        static readonly Color CardPlateSelected = new Color(0.129f, 0.102f, 0.071f, 1f);
        static readonly Color ContainerFrame = new Color(0.594f, 0.594f, 0.594f, 0.353f);
        static readonly Color InnerFrame = new Color(1f, 1f, 1f, 0.263f);

        // ui-style.md §6, the corner-radius ladder. Frames are tinted, never recoloured as new sprites.
        const float ContainerPixelsPerUnitMultiplier = 1.97f;
        const float ButtonPixelsPerUnitMultiplier = 3.13f;
        const float ChipPixelsPerUnitMultiplier = 5.05f;

        const string DisplayFont = "Assets/Fonts/CG-Regular SDF.asset";
        const string BoldFont = "Assets/Fonts/Roboto-Bold SDF.asset";
        const string RegularFont = "Assets/Fonts/Roboto-Regular SDF.asset";

        [MenuItem("Museum/Egrang/Build Stick Selection UI")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Egrang", "No scene is open.", "OK");
                return;
            }

            GameObject existing = FindRootInScene(scene, RootName) ?? FindRootInScene(scene, LegacyRootName);
            if (existing != null &&
                !EditorUtility.DisplayDialog(
                    "Egrang stick selection",
                    $"'{existing.name}' already exists in {scene.name}. Replace it? Any hand edits to it are lost.",
                    "Replace", "Cancel"))
            {
                return;
            }

            EgrangStickProfile[] profiles = EnsureProfiles();

            if (existing != null) Undo.DestroyObjectImmediate(existing);
            EnsureEventSystem();

            GameObject canvasObject = CreateCanvas();
            GameObject runRoot = BuildRunRoot(canvasObject.transform, out SkillCheckBar bar);
            BuildSelectionPanel(canvasObject.transform, profiles, bar, runRoot);

            // The run root is the reason the player cannot press Step through the panel, so it ships
            // switched off; the selector switches it on when a stick is committed.
            runRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = canvasObject;
            EditorGUIUtility.PingObject(canvasObject);
            Debug.Log($"Built '{RootName}' in scene '{scene.name}'. Save the scene to keep it.", canvasObject);
        }

        // ---- Profile assets -------------------------------------------------------------------

        /// <summary>
        /// Returns one profile asset per shape, in difficulty order, creating any that are missing.
        /// Existing assets are reused untouched — a retune done in the inspector must survive a
        /// rebuild of the panel, or the generator would quietly undo the designer's work.
        /// </summary>
        static EgrangStickProfile[] EnsureProfiles()
        {
            EnsureFolder(ProfileFolder);

            var shapes = (EgrangStickShape[])Enum.GetValues(typeof(EgrangStickShape));
            var profiles = new EgrangStickProfile[shapes.Length];

            for (int i = 0; i < shapes.Length; i++)
            {
                string path = $"{ProfileFolder}/EgrangStick_{shapes[i]}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<EgrangStickProfile>(path);

                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<EgrangStickProfile>();
                    profile.ApplyPreset(shapes[i]);
                    AssetDatabase.CreateAsset(profile, path);
                }

                profiles[i] = profile;
            }

            AssetDatabase.SaveAssets();
            return profiles;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---- Scene scaffolding ----------------------------------------------------------------

        static GameObject FindRootInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            // ui-style.md §1: the new Input System module, never the legacy StandaloneInputModule.
            var go = new GameObject("EventSystem", typeof(EventSystem),
                                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
        }

        static GameObject CreateCanvas()
        {
            var go = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Build Egrang stick selection UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1f;

            if (canvas.worldCamera == null)
            {
                Debug.LogWarning("No MainCamera in the scene, so the canvas has no camera to render " +
                                 "through. Assign one, or it falls back to overlay behaviour.", go);
            }

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            return go;
        }

        // ---- Run root: the timing bar -----------------------------------------------------------

        static GameObject BuildRunRoot(Transform parent, out SkillCheckBar bar)
        {
            GameObject runRoot = CreateUI("Run Root", parent);
            Stretch(runRoot.GetComponent<RectTransform>());

            GameObject barRoot = CreateUI("Skill Check Bar", runRoot.transform);
            Stretch(barRoot.GetComponent<RectTransform>());

            GameObject track = CreateUI("Track", barRoot.transform, typeof(Image));
            RectTransform trackRect = track.GetComponent<RectTransform>();
            Anchor(trackRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            trackRect.anchoredPosition = new Vector2(0f, 78f);
            trackRect.sizeDelta = new Vector2(420f, 18f);

            // The bar bakes the red/yellow/green strip from its zone table and assigns it here, so no
            // sprite is authored: whatever were set now would be overwritten on Awake. Full-bleed art
            // is Simple per ui-style.md §6, which is what the bar sets it to.
            var trackImage = track.GetComponent<Image>();
            trackImage.color = Color.white;

            GameObject frame = CreateUI("Frame", track.transform, typeof(Image));
            Stretch(frame.GetComponent<RectTransform>(), -4f);
            var frameImage = frame.GetComponent<Image>();
            SetFrame(frameImage, CornerFrameSprite(), InnerFrame, ChipPixelsPerUnitMultiplier);
            // Behind the gradient, so the chip frame rims the track rather than covering it.
            frame.transform.SetAsFirstSibling();

            GameObject cursor = CreateUI("Cursor", track.transform, typeof(Image));
            RectTransform cursorRect = cursor.GetComponent<RectTransform>();
            // Centred anchor and pivot: SkillCheckBar places it by anchoredPosition about the track's
            // middle, so an edge anchor would put the cursor a half-track off.
            Anchor(cursorRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            cursorRect.anchoredPosition = Vector2.zero;
            cursorRect.sizeDelta = new Vector2(5f, 28f);
            cursor.GetComponent<Image>().color = Cream;

            GameObject stepButton = CreateButton("Step Button", barRoot.transform, "LANGKAH", 170f, 40f, 16f);
            RectTransform stepRect = stepButton.GetComponent<RectTransform>();
            Anchor(stepRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            stepRect.anchoredPosition = new Vector2(0f, 26f);

            bar = Undo.AddComponent<SkillCheckBar>(barRoot);

            var so = new SerializedObject(bar);
            so.FindProperty("track").objectReferenceValue = trackRect;
            so.FindProperty("cursor").objectReferenceValue = cursorRect;
            so.FindProperty("trackImage").objectReferenceValue = trackImage;
            so.ApplyModifiedPropertiesWithoutUndo();

            // No "Egrang/Step" action map exists yet, so the bar is left script-driven and this button
            // is the only way to step. Once the map lands, assign the action asset on the bar and this
            // button becomes the touch fallback for the kiosk.
            UnityEventTools.AddVoidPersistentListener(stepButton.GetComponent<Button>().onClick, bar.Press);

            return runRoot;
        }

        // ---- Selection panel ---------------------------------------------------------------------

        static void BuildSelectionPanel(Transform parent, EgrangStickProfile[] profiles, SkillCheckBar bar, GameObject runRoot)
        {
            GameObject panel = CreateUI("Stick Selection Panel", parent, typeof(Image));
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = Scrim;

            // ui-style.md §8: anything that animates carries a CanvasGroup and exactly one tween that
            // plays on enable, so showing the panel is still just SetActive(true).
            panel.AddComponent<CanvasGroup>();
            AddTween(panel, "FadeTween");

            // A plate under the heading, and the heading itself pushed clear of the run HUD.
            // Both matter over a live 3D scene: the HUD's progress strip owns the top ~80 units, so
            // a title at -58 is simply behind it, and cream text on sunlit grass is unreadable
            // however large it is set. The countdown strip below carries the same colour, so the two
            // read as one band rather than two floating labels.
            GameObject headerPlate = CreateUI("Header Plate", panel.transform, typeof(Image));
            RectTransform headerRect = headerPlate.GetComponent<RectTransform>();
            Anchor(headerRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            headerRect.anchoredPosition = new Vector2(0f, -86f);
            headerRect.sizeDelta = new Vector2(780f, 70f);
            Image headerImage = headerPlate.GetComponent<Image>();
            headerImage.color = HeaderPlate;
            headerImage.raycastTarget = false;
            headerPlate.transform.SetAsFirstSibling();

            AddLabel(panel.transform, "Title", "PILIH EGRANG", DisplayFont, 34f, Cream,
                     new Vector2(0f, -110f), new Vector2(700f, 40f), new Vector2(0.5f, 1f), FontStyles.Bold);
            AddLabel(panel.transform, "Subtitle", "Makin lebar tumpuan, makin stabil langkahmu.",
                     RegularFont, 14f, Tan,
                     new Vector2(0f, -140f), new Vector2(700f, 18f), new Vector2(0.5f, 1f));

            GameObject row = CreateUI("Cards", panel.transform);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            Anchor(rowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            // Clear of the header band above, which is 132 units deep once the countdown strip is in.
            rowRect.anchoredPosition = new Vector2(0f, -70f);
            rowRect.sizeDelta = new Vector2(720f, 320f);

            row.AddComponent<CanvasGroup>();
            AddTween(row, "PopupTween");

            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 20f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            var cards = new List<EgrangStickCard>(profiles.Length);
            foreach (EgrangStickProfile profile in profiles)
            {
                cards.Add(BuildCard(row.transform, profile));
            }

            // No confirm button. The countdown on this panel is what starts the race — it commits
            // whichever card is highlighted when it reaches zero — so a MULAI button would either
            // duplicate that or, worse, let one player start before the server is accepting steps.
            var selector = Undo.AddComponent<EgrangStickSelector>(panel);
            var so = new SerializedObject(selector);
            FillArray(so.FindProperty("profiles"), profiles);
            FillArray(so.FindProperty("cards"), cards.ToArray());
            so.FindProperty("bar").objectReferenceValue = bar;
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("runRoot").objectReferenceValue = runRoot;
            // Left empty on purpose — see above. A card click highlights; the countdown commits.
            so.FindProperty("startButton").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static EgrangStickCard BuildCard(Transform parent, EgrangStickProfile profile)
        {
            GameObject card = CreateUI($"Card {profile.Shape}", parent, typeof(Image), typeof(Button));
            var layoutElement = card.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 220f;
            // Sized to the heading plus three rows. The card used to be 320 and stood two-thirds empty.
            layoutElement.preferredHeight = 187f;

            var background = card.GetComponent<Image>();
            // ui-style.md §6b: opaque over a live 3D scene. The translucent container tint is for a
            // panel sitting on a flat background; here the village reads straight through the card
            // and the specification rows turn to mud against it.
            SetFrame(background, FrameSprite(), Color.white, ContainerPixelsPerUnitMultiplier);
            background.raycastTarget = true;

            // The frame sprite is a frame: its fill is part-transparent, so tinting it white still
            // leaves the grass legible through the card's own numbers. This plate is what the text
            // actually sits on, and its weight is what selection changes.
            GameObject plate = CreateUI("Backdrop", card.transform, typeof(Image));
            Stretch(plate.GetComponent<RectTransform>(), 3f);
            var plateImage = plate.GetComponent<Image>();
            plateImage.color = CardPlateIdle;
            plateImage.raycastTarget = false;
            plate.transform.SetAsFirstSibling();

            var button = card.GetComponent<Button>();
            button.targetGraphic = background;
            ApplyStockTint(button);

            // The frame sits under the card root rather than inside the content column, so the layout
            // group never tries to give it a row of its own. Selection reads as the cornered frame in
            // gold over the plain container frame — one sprite swap, no new colours.
            GameObject frame = CreateUI("Selected Frame", card.transform, typeof(Image));
            Stretch(frame.GetComponent<RectTransform>());
            SetFrame(frame.GetComponent<Image>(), CornerFrameSprite(), Gold, ContainerPixelsPerUnitMultiplier);
            frame.transform.SetAsFirstSibling();
            frame.SetActive(false);

            GameObject content = CreateUI("Content", card.transform);
            Stretch(content.GetComponent<RectTransform>());
            var column = content.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(14, 14, 14, 14);
            column.spacing = 6f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            // The card is a heading over a specification list, not a column of bare values: without
            // the captions the player is asked to guess which of "Persegi" and "Sisi 8 cm" is the
            // shape and which the measurement, and the whole point of the screen is comparing the
            // three sticks row by row.
            TMP_Text nameText = AddColumnLabel(content.transform, "Name", BoldFont, 16f, Color.white, 22f);
            AddDivider(content.transform);

            TMP_Text shapeText = AddSpecRow(content.transform, "Shape", "Bentuk");
            TMP_Text sizeText = AddSpecRow(content.transform, "Size", "Ukuran");
            TMP_Text formulaText = AddSpecRow(content.transform, "Formula", "Rumus");
            // No "Luas" row: the area is the answer the player works out from the formula and the
            // size. Printing it turns the comparison into reading. EgrangStickCard.areaText is
            // optional and Bind() skips a null label, so the profile keeps the value unshown.

            var component = Undo.AddComponent<EgrangStickCard>(card);
            var so = new SerializedObject(component);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("selectedIndicator").objectReferenceValue = frame;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("shapeText").objectReferenceValue = shapeText;
            so.FindProperty("sizeText").objectReferenceValue = sizeText;
            so.FindProperty("formulaText").objectReferenceValue = formulaText;
            so.FindProperty("backdrop").objectReferenceValue = plateImage;
            SetColor(so.FindProperty("idleTint"), CardPlateIdle);
            SetColor(so.FindProperty("selectedTint"), CardPlateSelected);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Author-time preview only. Bind() overwrites all of this at runtime from the profile,
            // which is what keeps the card honest; filling it in now just means the scene view is not
            // three identical blank cards.
            nameText.text = profile.DisplayName;
            shapeText.text = profile.ShapeText;
            sizeText.text = profile.SizeText;
            formulaText.text = profile.FormulaText;

            return component;
        }

        // ---- Small builders ----------------------------------------------------------------------

        static GameObject CreateUI(string name, Transform parent, params Type[] components)
        {
            // RectTransform first and by itself: Image and the rest require one, so listing them in the
            // constructor would have Unity add it for us and the explicit add would then fail.
            var go = new GameObject(name, typeof(RectTransform));
            foreach (Type type in components) go.AddComponent(type);

            Undo.RegisterCreatedObjectUndo(go, "Build Egrang stick selection UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject CreateButton(string name, Transform parent, string label, float width, float height, float fontSize)
        {
            GameObject go = CreateUI(name, parent, typeof(Image), typeof(Button));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<Image>();
            SetFrame(image, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ApplyStockTint(button);

            // ui-style.md §2: the single label inside a button is named "Text (TMP)".
            GameObject text = CreateUI("Text (TMP)", go.transform, typeof(TextMeshProUGUI));
            Stretch(text.GetComponent<RectTransform>());
            var tmp = text.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, BoldFont, fontSize, Color.white);
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }

        static TMP_Text AddLabel(Transform parent, string name, string text, string fontPath, float size,
                                 Color color, Vector2 position, Vector2 rectSize, Vector2 anchor,
                                 FontStyles style = FontStyles.Normal)
        {
            GameObject go = CreateUI(name, parent, typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, anchor, anchor, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, fontPath, size, color);
            tmp.text = text;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        static TMP_Text AddColumnLabel(Transform parent, string name, string fontPath, float size, Color color, float height)
        {
            GameObject go = CreateUI(name, parent, typeof(TextMeshProUGUI));
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, fontPath, size, color);
            tmp.text = name;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        /// <summary>
        /// ui-style.md §4: an explicit font asset on every label (TMP's project default is still
        /// LiberationSans), auto-sizing off, and no raycasts from decorative copy.
        /// </summary>
        /// <summary>
        /// One line of the specification list: a fixed caption on the left, the value on the right.
        /// Returns the value label, since that is the half the card binds at runtime.
        /// </summary>
        static TMP_Text AddSpecRow(Transform parent, string name, string caption,
                                   string valueFont = null, Color? valueColor = null)
        {
            GameObject rowObject = CreateUI($"{name} Row", parent);
            var element = rowObject.AddComponent<LayoutElement>();
            element.preferredHeight = 22f;
            element.minHeight = 22f;

            var layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            GameObject captionObject = CreateUI("Caption", rowObject.transform, typeof(TextMeshProUGUI));
            var captionElement = captionObject.AddComponent<LayoutElement>();
            captionElement.preferredWidth = 54f;
            captionElement.minWidth = 54f;
            var captionText = captionObject.GetComponent<TextMeshProUGUI>();
            StyleText(captionText, RegularFont, 12f, Tan);
            captionText.text = caption;
            captionText.alignment = TextAlignmentOptions.MidlineLeft;
            // One word, one line. A wrapped caption reads as a rendering fault.
            captionText.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject valueObject = CreateUI("Value", rowObject.transform, typeof(TextMeshProUGUI));
            var valueElement = valueObject.AddComponent<LayoutElement>();
            valueElement.flexibleWidth = 1f;
            var value = valueObject.GetComponent<TextMeshProUGUI>();
            StyleText(value, valueFont ?? BoldFont, 13f, valueColor ?? Color.white);
            value.alignment = TextAlignmentOptions.MidlineRight;
            value.textWrappingMode = TextWrappingModes.NoWrap;
            return value;
        }

        /// <summary>A hairline between the card's heading and the rows under it.</summary>
        static void AddDivider(Transform parent)
        {
            GameObject divider = CreateUI("Divider", parent, typeof(Image));
            var element = divider.AddComponent<LayoutElement>();
            element.preferredHeight = 1f;
            element.minHeight = 1f;

            var image = divider.GetComponent<Image>();
            image.color = InnerFrame;
            image.raycastTarget = false;
        }

        static void StyleText(TMP_Text text, string fontPath, float size, Color color)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);

            if (font != null)
            {
                text.font = font;
            }
            else
            {
                Debug.LogWarning($"Missing font asset '{fontPath}'; the label keeps TMP's default font.");
            }

            text.enableAutoSizing = false;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
        }

        /// <summary>ui-style.md §6: neutral frame sprite, Sliced, tinted, corner radius by multiplier.</summary>
        static void SetFrame(Image image, Sprite sprite, Color tint, float pixelsPerUnitMultiplier)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = tint;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.raycastTarget = false;
        }

        /// <summary>ui-style.md §7: the stock ColorBlock, unchanged, on every button.</summary>
        static void ApplyStockTint(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ColorBlock.defaultColorBlock;
        }

        /// <summary>
        /// Adds one of the shared tween components by name. They live in the default assembly (LeanTween
        /// ships without an asmdef, so the tweens cannot sit in one either), and an asmdef cannot
        /// reference the default assembly — so the type is fetched by name rather than compiled against.
        /// </summary>
        static void AddTween(GameObject go, string typeName)
        {
            Type type = Type.GetType($"{typeName}, Assembly-CSharp");

            if (type == null)
            {
                Debug.LogWarning($"Could not find '{typeName}' in Assembly-CSharp; '{go.name}' is built " +
                                 "without its tween. Add the component by hand.", go);
                return;
            }

            Undo.AddComponent(go, type);
        }

        static Sprite FrameSprite() => LoadSprite("Assets/Sprites/frame_default.png");

        static Sprite CornerFrameSprite() => LoadSprite("Assets/Sprites/frame_with_corner.png");

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogWarning($"Missing sprite '{path}'; falling back to the built-in UI sprite.");
                return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }

            return sprite;
        }

        static void FillArray(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        static void SetColor(SerializedProperty property, Color value)
        {
            if (property != null) property.colorValue = value;
        }

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
