using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Hangs the curriculum signage on every exhibit: a teak name plate above the screen's bezel
    /// carrying the game's number and name, and an info board under the screen with the three
    /// columns the curriculum sheet gives each game — biology concept, science-literacy
    /// indicator, virtual-game idea (<see cref="GameSignageContent"/>).
    /// </summary>
    /// <remarks>
    /// Menu: <c>Museum/Decor/Build Signage</c> (and <c>Clear</c>). Idempotent: one scene-root
    /// container, <c>Signage (Generated)</c>, rebuilt from scratch. Placement is derived from
    /// <see cref="MuseumScreenGeometry"/> and the bezel's <see cref="MuseumScreenFrameBuilder.Overhang"/>,
    /// never authored by hand, so re-hanging a screen re-hangs its signs.
    ///
    /// Above and below rather than beside: the lesson plaque owns the wall to the left of every
    /// screen and the batik hangs to the right of a third of them, while the band under the
    /// screen — bezel bottom at 2.29 m, eye height 1.6 m — is bare wall on all eighteen. The
    /// storeys' ceilings are ~1.4 m above screen centre, which leaves the name plate ~0.1 m of
    /// air above its lip.
    ///
    /// World-space canvases without a GraphicRaycaster, like the lesson plaques: the museum locks
    /// the cursor, nothing on these is ever pointed at. No colliders on the boards either — they
    /// are flush wall dressing, and a collider there would catch the screens' trigger raycasts.
    /// </remarks>
    public static class MuseumSignageBuilder
    {
        public const string RootName = "Signage (Generated)";

        // --- name plate (above the bezel) ---

        /// <summary>Metres of wall, plate outer size.</summary>
        private const float PlateWidth = 1.9f;
        private const float PlateHeight = 0.3f;

        /// <summary>Air between the bezel's top bar and the plate's bottom lip.</summary>
        private const float PlateGap = 0.06f;

        /// <summary>The plate's canvas in design units; scaled to <see cref="PlateWidth"/>.</summary>
        private static readonly Vector2 PlateCanvas = new Vector2(1000f, 158f);
        private const float PlateTitleSize = 78f;
        private const float PlateEyebrowSize = 17f;
        private const float PlateNumberSize = 44f;

        // --- info board (under the bezel) ---

        private const float BoardWidth = 2.6f;
        private const float BoardHeight = 0.8f;
        private const float BoardGap = 0.12f;

        private static readonly Vector2 BoardCanvas = new Vector2(1500f, 462f);
        private const float BoardPad = 30f;
        private const float ColumnGap = 26f;
        private const float HeadingSize = 19f;
        private const float BodyMaxSize = 31f;
        private const float BodyMinSize = 18f;
        private const float HeadingHeight = 26f;
        private const float HeadingToBody = 10f;

        // --- carpentry ---

        private const float BoardDepth = 0.05f;
        private const float LipThickness = 0.02f;
        private const float LipDepth = 0.03f;

        /// <summary>The canvas sits this far in front of the board's face.</summary>
        private const float CanvasLift = 0.008f;

        /// <summary>Same batik surface as the lesson plaques and the main menu, cropped to the panel.</summary>
        private const string BackgroundSprite = "Assets/Texture2D/Main Menu BG.png";
        private const float BackgroundAspect = 1920f / 1080f;
        private const float BackgroundZoom = 1.3f;

        private static readonly string[] Headings = { "KONSEP BIOLOGI", "INDIKATOR LITERASI SAINS", "IDE GAME VIRTUAL" };

        [MenuItem("Museum/Decor/Build Signage")]
        public static void Build()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            Material wood = MuseumDecorMaterials.FrameWood();
            Material gold = MuseumDecorMaterials.FrameGold();
            if (wood == null || gold == null) return;

            // Same footing as the bezels: a sign derived from a tilted screen would tilt with it.
            MuseumScreenAligner.AlignAll(scene);

            MuseumScreenGeometry.ClearRoot(scene, RootName);
            Transform root = MuseumScreenGeometry.EnsureRoot(scene, RootName);

            Dictionary<string, string> keyByGroup = KeyByVideoGroup();
            int built = 0;

            foreach (MuseumScreenGeometry.ScreenFrame screen in MuseumScreenGeometry.All(scene))
            {
                if (!keyByGroup.TryGetValue(screen.GroupName, out string gameKey) ||
                    !GameSignageContent.TryGet(gameKey, out SignageSource source))
                {
                    Debug.LogWarning($"MuseumSignageBuilder: no signage entry for '{screen.GroupName}'.");
                    continue;
                }

                BuildOne(screen, source, root, wood, gold);
                built++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MuseumSignageBuilder: signed {built} exhibits under '{RootName}'.");
        }

        [MenuItem("Museum/Decor/Clear Signage")]
        public static void Clear()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            if (!MuseumScreenGeometry.ClearRoot(scene, RootName))
            {
                Debug.Log("MuseumSignageBuilder: nothing to clear.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The exhibit group each game hangs in, as the lesson content already records it.</summary>
        private static Dictionary<string, string> KeyByVideoGroup()
        {
            var map = new Dictionary<string, string>();
            foreach (LessonSource lesson in LessonContent.All()) map[lesson.VideoGroup] = lesson.GameKey;
            return map;
        }

        private static void BuildOne(MuseumScreenGeometry.ScreenFrame s, SignageSource source, Transform root,
                                     Material wood, Material gold)
        {
            string key = MuseumScreenFrameBuilder.Key(s.GroupName);
            float bezel = MuseumScreenFrameBuilder.Overhang;

            // Name plate: centred over the bezel's top bar.
            Vector3 plateCentre = s.Centre + s.Up * (s.Height * 0.5f + bezel + PlateGap + PlateHeight * 0.5f);
            Transform plate = Holder($"Sign_{key}", root, plateCentre, s.FacingRoom);
            Carpentry(plate, wood, gold, PlateWidth, PlateHeight);
            BuildPlateCanvas(plate, source);

            // Info board: hung under the bezel's bottom bar, reading height.
            Vector3 boardCentre = s.Centre - s.Up * (s.Height * 0.5f + bezel + BoardGap + BoardHeight * 0.5f);
            Transform board = Holder($"Info_{key}", root, boardCentre, s.FacingRoom);
            Carpentry(board, wood, gold, BoardWidth, BoardHeight);
            BuildBoardCanvas(board, source);
        }

        private static Transform Holder(string name, Transform root, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, rotation);
            return go.transform;
        }

        /// <summary>A teak board with a gold lip round its face; local +Z is into the wall.</summary>
        private static void Carpentry(Transform holder, Material wood, Material gold, float width, float height)
        {
            MuseumScreenFrameBuilder.Bar(holder, "Board", wood,
                new Vector3(0f, 0f, BoardDepth * 0.5f),
                new Vector3(width, height, BoardDepth));

            MuseumScreenFrameBuilder.Ring(holder, "Lip", gold, width, height, LipThickness, LipDepth,
                -LipDepth * 0.5f + 0.002f);
        }

        // --- name plate ---------------------------------------------------------------

        private static void BuildPlateCanvas(Transform holder, SignageSource source)
        {
            float scale = (PlateWidth - 2f * LipThickness) / PlateCanvas.x;
            RectTransform rect = WorldCanvas("Plate", holder, PlateCanvas, scale);

            BuildBackground(rect);

            // Number roundel on the left, framed like a keycap.
            GameObject roundel = CreateUI("Number", rect, typeof(Image));
            RectTransform roundelRect = roundel.GetComponent<RectTransform>();
            Anchor(roundelRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            roundelRect.anchoredPosition = new Vector2(28f, 0f);
            roundelRect.sizeDelta = new Vector2(96f, 96f);
            SetFrame(roundel.GetComponent<Image>(), FrameSprite(), Gold, ChipPixelsPerUnitMultiplier);

            GameObject numberText = CreateUI("Text (TMP)", roundel.transform, typeof(TextMeshProUGUI));
            Stretch(numberText.GetComponent<RectTransform>());
            var number = numberText.GetComponent<TextMeshProUGUI>();
            StyleText(number, BoldFont, PlateNumberSize, Cream);
            number.text = source.Number.ToString("00");
            number.alignment = TextAlignmentOptions.Center;

            // Eyebrow above, name below — both left-aligned off the roundel.
            const float textLeft = 150f;
            float textWidth = PlateCanvas.x - textLeft - 28f;

            TMP_Text eyebrow = AddLabel(rect, "Eyebrow", "PERMAINAN TRADISIONAL JAWA", MediumFont, PlateEyebrowSize, Tan,
                                        new Vector2(textLeft + textWidth * 0.5f, -22f),
                                        new Vector2(textWidth, 22f), new Vector2(0f, 1f));
            eyebrow.alignment = TextAlignmentOptions.MidlineLeft;
            eyebrow.characterSpacing = 8f;

            TMP_Text title = AddLabel(rect, "Title", source.DisplayName, DisplayFont, PlateTitleSize, Gold,
                                      new Vector2(textLeft + textWidth * 0.5f, 56f),
                                      new Vector2(textWidth, 96f), new Vector2(0f, 0f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 40f;
            title.fontSizeMax = PlateTitleSize;
        }

        // --- info board ---------------------------------------------------------------

        private static void BuildBoardCanvas(Transform holder, SignageSource source)
        {
            float scale = (BoardWidth - 2f * LipThickness) / BoardCanvas.x;
            RectTransform rect = WorldCanvas("Board Canvas", holder, BoardCanvas, scale);

            BuildBackground(rect);

            string[] bodies = { source.Konsep, source.Indikator, source.Ide };
            float innerWidth = BoardCanvas.x - 2f * BoardPad;
            float columnWidth = (innerWidth - 2f * ColumnGap) / 3f;
            float columnHeight = BoardCanvas.y - 2f * BoardPad;

            for (int i = 0; i < 3; i++)
            {
                float left = BoardPad + i * (columnWidth + ColumnGap);
                BuildColumn(rect, Headings[i], bodies[i], left, columnWidth, columnHeight);

                if (i > 0) AddVerticalRule(rect, $"Rule {i}", left - ColumnGap * 0.5f, columnHeight);
            }
        }

        private static void BuildColumn(RectTransform parent, string heading, string body, float left, float width, float height)
        {
            GameObject column = CreateUI($"Column ({heading})", parent);
            RectTransform rect = column.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(left, -BoardPad);
            rect.sizeDelta = new Vector2(width, height);

            TMP_Text head = AddLabel(rect, "Heading", heading, MediumFont, HeadingSize, Tan,
                                     new Vector2(width * 0.5f, -HeadingHeight * 0.5f),
                                     new Vector2(width, HeadingHeight), new Vector2(0f, 1f));
            head.alignment = TextAlignmentOptions.MidlineLeft;
            head.characterSpacing = 5f;
            head.textWrappingMode = TextWrappingModes.NoWrap;

            // A short gold underline beneath the heading, the plaque's accent.
            GameObject accent = CreateUI("Accent", rect, typeof(Image));
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            Anchor(accentRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            accentRect.anchoredPosition = new Vector2(0f, -HeadingHeight - 2f);
            accentRect.sizeDelta = new Vector2(48f, 2f);
            var accentImage = accent.GetComponent<Image>();
            accentImage.color = Gold;
            accentImage.raycastTarget = false;

            GameObject text = CreateUI("Body", rect, typeof(TextMeshProUGUI));
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = new Vector2(0f, -(HeadingHeight + HeadingToBody + 4f));

            var tmp = text.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, RegularFont, BodyMaxSize, Cream);
            tmp.text = body;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.lineSpacing = 8f;
            // The indicator column runs to ~260 characters on the longest row; sizing down beats
            // a plaque whose last sentence is missing.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = BodyMinSize;
            tmp.fontSizeMax = BodyMaxSize;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        private static void AddVerticalRule(RectTransform parent, string name, float x, float height)
        {
            GameObject go = CreateUI(name, parent, typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f));
            rect.anchoredPosition = new Vector2(x, -BoardPad);
            rect.sizeDelta = new Vector2(1f, height);

            var image = go.GetComponent<Image>();
            image.color = InnerFrame;
            image.raycastTarget = false;
        }

        // --- shared -------------------------------------------------------------------

        private static RectTransform WorldCanvas(string name, Transform holder, Vector2 size, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(holder, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            go.transform.localPosition = new Vector3(0f, 0f, -CanvasLift);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            return rect;
        }

        /// <summary>The menu's batik art, covered and cropped — the same surface the lesson plaques wear.</summary>
        private static void BuildBackground(RectTransform parent)
        {
            GameObject backdrop = CreateUI("Backdrop", parent);
            Stretch(backdrop.GetComponent<RectTransform>());
            backdrop.AddComponent<RectMask2D>();

            GameObject art = CreateUI("BG", backdrop.transform, typeof(Image));
            RectTransform artRect = art.GetComponent<RectTransform>();
            Anchor(artRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // Cover: tall enough for the panel, wide enough for it, whichever binds.
            float artHeight = Mathf.Max(parent.sizeDelta.y, parent.sizeDelta.x / BackgroundAspect) * BackgroundZoom;
            artRect.sizeDelta = new Vector2(artHeight * BackgroundAspect, artHeight);
            artRect.anchoredPosition = Vector2.zero;

            var fill = art.GetComponent<Image>();
            fill.sprite = LoadSprite(BackgroundSprite);
            fill.type = Image.Type.Simple;
            fill.color = Color.white;
            fill.raycastTarget = false;

            GameObject scrim = CreateUI("Scrim", parent, typeof(Image));
            Stretch(scrim.GetComponent<RectTransform>());
            var scrimImage = scrim.GetComponent<Image>();
            scrimImage.color = new Color(0f, 0f, 0f, 0.28f);
            scrimImage.raycastTarget = false;

            GameObject frame = CreateUI("Frame", parent, typeof(Image));
            Stretch(frame.GetComponent<RectTransform>());
            SetFrame(frame.GetComponent<Image>(), CornerFrameSprite(), ContainerFrame, ContainerPixelsPerUnitMultiplier);
        }
    }
}
