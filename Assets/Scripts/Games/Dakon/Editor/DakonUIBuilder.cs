using Museum.Core.EditorTools;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Games.Dakon.EditorTools
{
    /// <summary>
    /// Restores the Dakon screen: the seed catalog, the card prefab, the canvas styling, and every
    /// serialized reference the view and the net bootstrap lost.
    ///
    /// Like the MainMenu builder this adopts the scene rather than regenerating it (ui-style.md §9) —
    /// the RectTransforms, hole anchors and board meshes are native and survived, so only the managed
    /// half is written back. Re-running is safe.
    ///
    /// The seed categories come from the server's <c>DAKON_DEFAULTS</c>, not from botany: the server
    /// mirrors whatever the client authored, and its own comment flags that the authored grouping
    /// looks wrong (corn sits with peanut). Reproducing the odd grouping is what keeps the two ends
    /// agreeing about which store a seed scores into.
    /// </summary>
    public static class DakonUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Dakon.unity";

        /// <summary>Lighting for the board. See <see cref="ApplyEnvironment"/> for why it is so little.</summary>
        private const float SunIntensity = 1.2f;
        private static readonly Color AmbientSky = new Color(0.36f, 0.34f, 0.32f);
        private static readonly Color AmbientEquator = new Color(0.30f, 0.27f, 0.24f);
        private static readonly Color AmbientGround = new Color(0.20f, 0.18f, 0.15f);
        private const string CardPrefabPath = "Assets/GameObject/Card.prefab";
        private const string SeedFolder = "Assets/Resources/seeds";
        private const string SeedArtFolder = "Assets/Texture2D";
        private const string SeedPrefabFolder = "Assets/GameObject";
        private const string TutorialSprite = "Assets/Texture2D/Dakon Tutor.png";
        private const string SettingsIcon = "Assets/Texture2D/setting.png";
        private const string BookIcon = "Assets/Texture2D/book.png";

        // ---------------------------------------------------------------- layout

        /// <summary>
        /// Every rect below is in the 800×600 design units of ui-style.md §1, and is written here
        /// rather than left in the inspector for the same reason the references are: the 2026-08-19
        /// wipe took the inspector with it, and padding that only exists inside a .unity file is
        /// padding this generator cannot restore.
        /// </summary>
        private static readonly Vector2 ScoreChipSize = new Vector2(150f, 30f);
        private static readonly Vector2 TurnChipSize = new Vector2(170f, 46f);
        private static readonly Vector2 IconButtonSize = new Vector2(40f, 40f);

        /// <summary>Frame-to-text inset on a chip; the x is bigger because the corner studs eat into it.</summary>
        private const float ChipPadX = 14f;
        private const float ChipPadY = 5f;

        /// <summary>Frame-to-content inset inside a dialog panel.</summary>
        private const float PanelPad = 20f;

        private static readonly Vector2 PausePanelSize = new Vector2(300f, 200f);
        private static readonly Vector2 DialogButtonSize = new Vector2(220f, 40f);
        private static readonly Vector2 TutorialPanelSize = new Vector2(660f, 440f);

        /// <summary>The tutorial art is 1209×547; this keeps its aspect so nothing is letterboxed.</summary>
        private static readonly Vector2 TutorialImageSize = new Vector2(600f, 271f);
        private static readonly Vector2 TutorialCloseSize = new Vector2(160f, 36f);
        private static readonly Vector2 GameOverSize = new Vector2(320f, 170f);

        /// <summary>Gap between the result text and the exit button under it.</summary>
        private const float GameOverGap = 12f;

        private static readonly Vector2 ToastSize = new Vector2(420f, 44f);

        /// <summary>How far the toast floats above the bottom edge, clear of the hand strip.</summary>
        private const float ToastLift = 150f;

        /// <summary>
        /// One hand card, and the gap between two of them. A hand runs to fifteen cards, which at
        /// this size is far wider than the screen — see <see cref="StyleHand"/> for why that is the
        /// intended shape rather than something to shrink away.
        /// </summary>
        private static readonly Vector2 CardSize = new Vector2(96f, 116f);
        private const float CardSpacing = 10f;
        private const int HandPadX = 14;
        private const int HandPadY = 9;

        /// <summary>
        /// One species: the asset, the id the server hands out, the label on the card, and the art.
        /// Ids and categories are the server's <c>DAKON_DEFAULTS</c> verbatim — the asset filename is
        /// not the id ("Kacang Tanah" is <c>kacang_tanah</c>), so every one is written explicitly.
        /// </summary>
        private struct Species
        {
            public string Asset;
            public string TypeId;
            public string DisplayName;
            public string Art;
            public string Prefab;
            public SeedCategory Category;
        }

        private static readonly Species[] Catalog =
        {
            Make("Beras",        "beras",        "Beras",        "Beras",        "Beras",        SeedCategory.Monocot),
            Make("Gabah",        "gabah",        "Gabah",        "Gabah",        "Gabah",        SeedCategory.Monocot),
            Make("Alpukat",      "alpukat",      "Biji Alpukat", "Alpukat",      "Biji Alpukat", SeedCategory.Monocot),
            Make("Zaitun",       "zaitun",       "Biji Zaitun",  "Zaitun",       "Zaitun",       SeedCategory.Monocot),
            Make("Jagung",       "jagung",       "Biji Jagung",  "Jagung",       "Jagung",       SeedCategory.Dicot),
            Make("Kacang Tanah", "kacang_tanah", "Kacang Tanah", "Kacang Tanah", "Kacang Tanah", SeedCategory.Dicot),
            // The cocoa art and mesh were both filed under other spellings than the asset's.
            Make("Kakao",        "kakao",        "Kakao",        "Kako",         "Coklat",       SeedCategory.Dicot),
            Make("Mangga",       "mangga",       "Biji Mangga",  "Mangga",       "Mangga",       SeedCategory.Dicot),
        };

        private static Species Make(string asset, string typeId, string displayName, string art,
                                    string prefab, SeedCategory category) =>
            new Species
            {
                Asset = asset, TypeId = typeId, DisplayName = displayName,
                Art = art, Prefab = prefab, Category = category
            };

        [MenuItem("Museum/Rebuild UI/Dakon")]
        public static void Rebuild()
        {
            SeedType[] seeds = FillSeedCatalog();
            DakonCard cardPrefab = FixCardPrefab();

            UnityEngine.SceneManagement.Scene scene = OpenScene();
            Transform canvas = FindRoot(scene, "Canvas");

            if (canvas == null)
            {
                Debug.LogError("DakonUIBuilder: no 'Canvas' in Dakon.unity — nothing to restyle.");
                return;
            }

            StyleHud(canvas);
            StyleHand(canvas);
            StyleModals(canvas);
            ApplyEnvironment(scene);
            WireView(scene, canvas, seeds, cardPrefab);
            WireButtons(scene, canvas);
            ReportClickable(canvas, "Setting Button", "Book Button");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("DakonUIBuilder: Dakon restyled, wired and saved.");
        }

        // ---------------------------------------------------------------- seed catalog

        /// <summary>Writes the eight species assets back, in the ring order the view reads them.</summary>
        private static SeedType[] FillSeedCatalog()
        {
            var seeds = new SeedType[Catalog.Length];

            for (int i = 0; i < Catalog.Length; i++)
            {
                Species species = Catalog[i];
                string path = $"{SeedFolder}/{species.Asset}.asset";
                var seed = AssetDatabase.LoadAssetAtPath<SeedType>(path);

                if (seed == null)
                {
                    Debug.LogWarning($"DakonUIBuilder: missing seed asset '{path}'.");
                    continue;
                }

                var serialized = new SerializedObject(seed);
                serialized.FindProperty("typeId").stringValue = species.TypeId;
                serialized.FindProperty("displayName").stringValue = species.DisplayName;
                serialized.FindProperty("category").enumValueIndex = (int)species.Category;
                serialized.FindProperty("cardSprite").objectReferenceValue =
                    LoadSprite($"{SeedArtFolder}/{species.Art}.png");
                serialized.FindProperty("seedPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>($"{SeedPrefabFolder}/{species.Prefab}.prefab");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(seed);
                seeds[i] = seed;
            }

            return seeds;
        }

        /// <summary>
        /// Styles the hand card and points its four references at its own children. The dim group is
        /// added here rather than left to the runtime fallback so the prefab is complete on disk.
        /// </summary>
        private static DakonCard FixCardPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);

            if (prefab == null)
            {
                Debug.LogWarning($"DakonUIBuilder: missing card prefab '{CardPrefabPath}'.");
                return null;
            }

            var frame = prefab.GetComponent<Image>();
            SetFrame(frame, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
            frame.raycastTarget = true;

            // The hand's layout group sizes cards from this, not from the rect: a card whose size is
            // only a serialized rect gets whatever the group decides to share out (see StyleHand).
            var element = Ensure<LayoutElement>(prefab);
            element.minWidth = CardSize.x;
            element.preferredWidth = CardSize.x;
            element.minHeight = CardSize.y;
            element.preferredHeight = CardSize.y;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;

            var cardRect = (RectTransform)prefab.transform;
            cardRect.sizeDelta = CardSize;

            var artwork = prefab.transform.Find("img").GetComponent<Image>();
            artwork.sprite = null;
            artwork.type = Image.Type.Simple;
            artwork.color = Color.white;
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;

            // Art on top, name in the strip under it, both clear of the frame's bevel.
            var artRect = artwork.rectTransform;
            Anchor(artRect, Centre, Centre, Centre);
            artRect.sizeDelta = new Vector2(CardSize.x - 16f, CardSize.y - 46f);
            artRect.anchoredPosition = new Vector2(0f, 12f);

            var label = prefab.transform.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            StyleText(label, BoldFont, 10f, Color.white);
            label.alignment = TextAlignmentOptions.Center;

            // Two-word seed names ("Kacang Tanah") need the second line; the height is for two of
            // them, and the x inset keeps the longest off the frame.
            var labelRect = label.rectTransform;
            Anchor(labelRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            labelRect.offsetMin = new Vector2(6f, 8f);
            labelRect.offsetMax = new Vector2(-6f, 8f + 26f);
            label.textWrappingMode = TextWrappingModes.Normal;

            var button = prefab.GetComponent<Button>();
            button.targetGraphic = frame;
            ApplyStockTint(button);

            CanvasGroup group = prefab.GetComponent<CanvasGroup>();
            if (group == null) group = prefab.AddComponent<CanvasGroup>();

            var card = prefab.GetComponent<DakonCard>();
            var serialized = new SerializedObject(card);
            serialized.FindProperty("label").objectReferenceValue = label;
            serialized.FindProperty("artwork").objectReferenceValue = artwork;
            serialized.FindProperty("button").objectReferenceValue = button;
            serialized.FindProperty("canvasGroup").objectReferenceValue = group;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SavePrefabAsset(prefab);
            return card;
        }

        // ---------------------------------------------------------------- styling

        /// <summary>
        /// The scene's lighting and grading. Written here rather than left in the inspector for the
        /// reason the museum's rig is generated: the 2026-08-19 wipe destroyed every inspector value.
        ///
        /// Dakon is **almost entirely UI** — one 3D `Plane` backdrop, and 17 `Image`s on a
        /// ScreenSpaceCamera canvas. Two consequences that make this scene unlike the museum:
        ///
        /// 1. The lights barely matter. Only the backdrop plane is lit at all; the board, the holes
        ///    and the seeds are sprites.
        /// 2. Post-processing *does* hit the board, because a ScreenSpaceCamera canvas is composited
        ///    before post. Exposure is therefore a blunt instrument here — a big lift washes the whole
        ///    board out along with the backdrop, which is exactly what a first attempt at +0.35 did.
        ///
        /// Tonemapping is Neutral rather than the museum's ACES: ACES desaturates, and on a flat
        /// close-up board it drains the wood and flattens the seeds.
        /// </summary>
        private static void ApplyEnvironment(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type != LightType.Directional) continue;

                    // Authored as 1.08 in Gamma; that reads far dimmer once the project is Linear.
                    light.intensity = SunIntensity;
                    light.shadowStrength = 0.6f;
                    EditorUtility.SetDirty(light);
                }
            }

            // The camera clears to a solid colour, so the skybox is never drawn — but it was still the
            // ambient source, which left a dim blue probe over a warm wooden board.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.6f;
        }

        /// <summary>Score chips, turn chip and the two corner utilities (ui-style.md §3, §4).</summary>
        private static void StyleHud(Transform canvas)
        {
            // The score chips are corner utilities, not centred HUD (ui-style.md §3): anchored to the
            // top-left they keep their inset at any aspect, where the centre-anchored offsets they
            // were rebuilt with slide toward the middle of a wide screen.
            Place(canvas, "Point P1", TopLeft, new Vector2(16f, -16f), ScoreChipSize);
            Place(canvas, "Point P2", TopLeft, new Vector2(16f, -16f - ScoreChipSize.y - 8f), ScoreChipSize);
            Place(canvas, "Turns", TopCentre, new Vector2(0f, -16f), TurnChipSize);
            Place(canvas, "Setting Button", TopRight, new Vector2(-16f, -16f), IconButtonSize);
            Place(canvas, "Book Button", TopRight,
                  new Vector2(-16f, -16f - IconButtonSize.y - 10f), IconButtonSize);

            StyleChip(canvas, "Point P1", TextAlignmentOptions.MidlineLeft);
            StyleChip(canvas, "Point P2", TextAlignmentOptions.MidlineLeft);

            // The turn chip carries two lines, so its label takes the top of the frame and the pool
            // count the bottom. Both are inset — the pool count used to hang below the frame entirely.
            StyleChip(canvas, "Turns", TextAlignmentOptions.Center);
            Inset(canvas, "Turns/Text (TMP)", ChipPadX, ChipPadX, ChipPadY, 20f);

            var pool = Find<TextMeshProUGUI>(canvas, "Turns/Pool Text");
            if (pool != null)
            {
                StyleText(pool, MediumFont, 8f, Color.white);
                pool.alignment = TextAlignmentOptions.Center;
                Inset(pool.rectTransform, ChipPadX, ChipPadX, 26f, ChipPadY);
                EditorUtility.SetDirty(pool);
            }

            // The two icons carry their own frame art, so they are Simple images, not nine-slices.
            StyleIcon(canvas, "Setting Button", SettingsIcon);
            StyleIcon(canvas, "Book Button", BookIcon);
        }

        private static void StyleChip(Transform canvas, string path, TextAlignmentOptions alignment)
        {
            var frame = Find<Image>(canvas, path);
            if (frame != null)
            {
                SetFrame(frame, CornerFrameSprite(), Color.white, ChipPixelsPerUnitMultiplier);
                EditorUtility.SetDirty(frame);
            }

            var label = Find<TextMeshProUGUI>(canvas, $"{path}/Text (TMP)");
            if (label == null) return;

            StyleText(label, BoldFont, 10f, Color.white);
            label.alignment = alignment;
            // A chip label stretched to its frame sits on the frame's own bevel, which is what makes
            // "Pemain 1: 0" read as clipped. The inset is the fix, not a smaller font.
            Inset(label.rectTransform, ChipPadX, ChipPadX, ChipPadY, ChipPadY);
            EditorUtility.SetDirty(label);
        }

        private static void StyleIcon(Transform canvas, string path, string spritePath)
        {
            var image = Find<Image>(canvas, path);
            if (image == null) return;

            image.sprite = LoadSprite(spritePath);
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = true;

            var button = Find<Button>(canvas, path);
            if (button != null)
            {
                button.targetGraphic = image;
                ApplyStockTint(button);
                EditorUtility.SetDirty(button);
            }

            EditorUtility.SetDirty(image);
        }

        /// <summary>
        /// The hand strip: a large container frame over the flat board, so it takes the translucent
        /// container tint of ui-style.md §5 rather than the opaque one reserved for 3D backdrops.
        ///
        /// A Dakon hand is fifteen cards. Fifteen cards do not fit across 800 units at a size where
        /// the seed art and its name are readable, and the scene's answer to that was a layout group
        /// that force-expanded them to fit — which is why every card came out a squeezed sliver with
        /// its label clipped. The cards therefore keep a fixed size and the strip scrolls: the
        /// ScrollRect, the Mask and the ContentSizeFitter the scene already carries are exactly the
        /// pieces that shape needs, and they were left unconfigured rather than being the wrong ones.
        ///
        /// Roles: <c>HandTransform (1)</c> is the framed ScrollRect, <c>Mask</c> is its viewport, and
        /// <c>HandTransform</c> inside is the content the cards are instantiated into.
        /// </summary>
        private static void StyleHand(Transform canvas)
        {
            var outer = Find<Image>(canvas, "Hand Panel/HandTransform (1)");
            if (outer != null)
            {
                SetFrame(outer, FrameSprite(), ContainerFrame, ContainerPixelsPerUnitMultiplier);
                EditorUtility.SetDirty(outer);
            }

            // The mask's image is the stencil shape, not decoration: it has to match the frame's
            // corner radius or the cards square off at the edges.
            var mask = Find<Image>(canvas, "Hand Panel/HandTransform (1)/Mask");
            if (mask != null)
            {
                SetFrame(mask, FrameSprite(), InnerFrame, ContainerPixelsPerUnitMultiplier);
                EditorUtility.SetDirty(mask);
            }

            RectTransform viewport = Find<RectTransform>(canvas, "Hand Panel/HandTransform (1)/Mask");
            RectTransform content =
                Find<RectTransform>(canvas, "Hand Panel/HandTransform (1)/Mask/HandTransform");

            // A viewport that lays its own child out fights the content's size fitter, and the pair
            // of them settle on a zero-width strip. Only the content gets a layout group.
            if (viewport != null) Strip<HorizontalLayoutGroup>(viewport.gameObject);
            if (viewport != null) Inset(viewport, 0f, 0f, 0f, 0f);

            var scroll = Find<ScrollRect>(canvas, "Hand Panel/HandTransform (1)");
            if (scroll != null && viewport != null && content != null)
            {
                scroll.viewport = viewport;
                scroll.content = content;
                scroll.horizontal = true;
                // Vertical scrolling on a single row of cards only lets the player drag them out of
                // sight; the strip is exactly one card tall.
                scroll.vertical = false;
                scroll.movementType = ScrollRect.MovementType.Elastic;
                scroll.elasticity = 0.1f;
                scroll.inertia = true;
                scroll.scrollSensitivity = 20f;
                EditorUtility.SetDirty(scroll);
            }

            if (content != null)
            {
                // Left-anchored and pivoted: the fitter grows the content to the right of a fixed
                // left edge, which is where a hand reads from.
                Anchor(content, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(CardSize.x, 0f);

                var layout = Ensure<HorizontalLayoutGroup>(content.gameObject);
                layout.padding = new RectOffset(HandPadX, HandPadX, HandPadY, HandPadY);
                layout.spacing = CardSpacing;
                layout.childAlignment = TextAnchor.MiddleLeft;
                // Control the size, do not expand it: expansion is what shares one screen width
                // between fifteen cards. The card's own LayoutElement supplies the size.
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                EditorUtility.SetDirty(layout);

                var fitter = Ensure<ContentSizeFitter>(content.gameObject);
                fitter.enabled = true;
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                // Vertical is the viewport's job — a fitter on both axes collapses the strip to the
                // height of an empty hand the moment the last card is played.
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                EditorUtility.SetDirty(fitter);
            }

            var waiting = Find<TextMeshProUGUI>(canvas, "Hand Panel/Waiting For Turn");
            if (waiting != null)
            {
                StyleText(waiting, RegularFont, 12f, Tan);
                waiting.alignment = TextAlignmentOptions.Center;
                Inset(waiting.rectTransform, PanelPad, PanelPad, PanelPad, PanelPad);
                EditorUtility.SetDirty(waiting);
            }
        }

        /// <summary>Pause, tutorial and game-over — scrim plus opaque panel, ui-style.md §6b.</summary>
        private static void StyleModals(Transform canvas)
        {
            StyleScrim(canvas, "Pause Panel");
            StylePanel(canvas, "Pause Panel/Panel", "Jeda", PausePanelSize);
            StyleOutlineButton(canvas, "Pause Panel/Panel/Resume", "Lanjutkan");
            StyleOutlineButton(canvas, "Pause Panel/Panel/Back to Museum", "Kembali ke Museum");

            // Stacked under the title, both the same width, both clear of the frame.
            Place(canvas, "Pause Panel/Panel/Resume", Centre, new Vector2(0f, -6f), DialogButtonSize);
            Place(canvas, "Pause Panel/Panel/Back to Museum", Centre,
                  new Vector2(0f, -6f - DialogButtonSize.y - 12f), DialogButtonSize);

            StyleScrim(canvas, "Tutorial Panel");
            StylePanel(canvas, "Tutorial Panel/Panel", "Cara Bermain", TutorialPanelSize);

            var tutorial = Find<Image>(canvas, "Tutorial Panel/Panel/Image");
            if (tutorial != null)
            {
                tutorial.sprite = LoadSprite(TutorialSprite);
                tutorial.type = Image.Type.Simple;
                tutorial.color = Color.white;
                tutorial.preserveAspect = true;
                tutorial.raycastTarget = false;
                Place(tutorial.rectTransform, Centre, new Vector2(0f, 10f), TutorialImageSize);
                EditorUtility.SetDirty(tutorial);
            }

            EnsureTutorialClose(canvas);

            StylePanel(canvas, "Game Over", null, GameOverSize);
            var gameOver = Find<TextMeshProUGUI>(canvas, "Game Over/Text (TMP)");
            if (gameOver != null)
            {
                StyleText(gameOver, BoldFont, 18f, Color.white);
                gameOver.alignment = TextAlignmentOptions.Center;

                // The bottom inset clears the exit button below, not just the frame: the label
                // is stretched across the whole panel, so the stock PanelPad would run the
                // score line straight under the button.
                Inset(gameOver.rectTransform, PanelPad, PanelPad, PanelPad,
                      PanelPad + DialogButtonSize.y + GameOverGap);
                EditorUtility.SetDirty(gameOver);
            }

            EnsureGameOverExit(canvas);

            // Modals draw last (ui-style.md §2). Without this the score chips, which were re-parented
            // after them at some point, paint over the panel they are supposed to sit under.
            Raise(canvas, "Game Over");
            Raise(canvas, "Tutorial Panel");
            Raise(canvas, "Pause Panel");
        }

        /// <summary>
        /// The tutorial's own way out. The scrim already closes on a click, but a full-bleed diagram
        /// gives the player nothing that looks like a target — the invisible click-outside is a
        /// convenience, not the affordance.
        /// </summary>
        private static void EnsureTutorialClose(Transform canvas)
        {
            Transform panel = canvas.Find("Tutorial Panel/Panel");
            if (panel == null) return;

            Transform close = panel.Find("Close");

            if (close == null)
            {
                GameObject created = CreateButton("Close", panel, "Tutup",
                                                  TutorialCloseSize.x, TutorialCloseSize.y, 13.5f);
                close = created.transform;
            }

            StyleOutlineButton(canvas, "Tutorial Panel/Panel/Close", "Tutup");
            Place((RectTransform)close, Centre,
                  new Vector2(0f, -(TutorialPanelSize.y * 0.5f) + PanelPad + TutorialCloseSize.y * 0.5f),
                  TutorialCloseSize);
        }

        /// <summary>
        /// The results panel's own way out. Until it existed the only exit from a finished match
        /// was the pause menu — which reads as "the game is still going" at exactly the moment it
        /// is not, and which a player who has just been shown a winner has no reason to open.
        /// </summary>
        private static void EnsureGameOverExit(Transform canvas)
        {
            Transform panel = canvas.Find("Game Over");
            if (panel == null) return;

            Transform exit = panel.Find("Exit");

            if (exit == null)
            {
                GameObject created = CreateButton("Exit", panel, "Kembali ke Museum",
                                                  DialogButtonSize.x, DialogButtonSize.y, 13.5f);
                exit = created.transform;
            }

            StyleOutlineButton(canvas, "Game Over/Exit", "Kembali ke Museum");
            Place((RectTransform)exit, Centre,
                  new Vector2(0f, -(GameOverSize.y * 0.5f) + PanelPad + DialogButtonSize.y * 0.5f),
                  DialogButtonSize);
        }

        /// <summary>
        /// The drop-feedback overlay: a full-screen glow the view tints green or red per drop.
        /// Built last in the canvas so it draws over the board and the hand, and left without a
        /// raycast target so it takes no clicks on its way past. The sprite is baked at runtime by
        /// VignetteTexture, so there is nothing here to author or to import.
        /// </summary>
        private static DakonVignette EnsureVignette(Transform canvas)
        {
            Transform vignette = canvas.Find("Vignette");

            if (vignette == null)
            {
                vignette = CreateUI("Vignette", canvas, typeof(Image)).transform;
            }

            Stretch((RectTransform)vignette);

            var image = Ensure<Image>(vignette.gameObject);
            image.sprite = null;             // baked at runtime; an authored sprite would win over it
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);

            // Last sibling = painted over everything else on this canvas. A vignette drawn under
            // the hand panel would be a rectangle of colour behind the cards, not a glow around them.
            vignette.SetAsLastSibling();

            var component = Ensure<DakonVignette>(vignette.gameObject);
            EditorUtility.SetDirty(component);
            return component;
        }

        /// <summary>
        /// The refusal line. Every text node under this canvas is already spoken for by a DakonView
        /// field, so there is nothing to adopt — the toast is built, once, the same shape the lobby
        /// uses (LobbyUIBuilder.BuildToast).
        /// </summary>
        private static TextMeshProUGUI EnsureToast(Transform canvas)
        {
            Transform toast = canvas.Find("Toast");

            if (toast == null)
            {
                GameObject created = CreateUI("Toast", canvas, typeof(Image), typeof(CanvasGroup));
                toast = created.transform;
            }

            var rect = (RectTransform)toast;
            Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = new Vector2(0f, ToastLift);
            rect.sizeDelta = ToastSize;

            var frame = Ensure<Image>(toast.gameObject);
            SetFrame(frame, CornerFrameSprite(), Color.white, ChipPixelsPerUnitMultiplier);

            // A refusal must not eat the click that would fix it — the toast sits over the board.
            frame.raycastTarget = false;
            EditorUtility.SetDirty(frame);

            // ui-style.md 6b: the stock 0.6 is for an empty backdrop. The message lives inside
            // this CanvasGroup, so anything under 1 leaves the text permanently washed out.
            //
            // AddTween adds unconditionally, so re-running the builder would stack a second
            // FadeTween on the same object — hence the guard rather than a bare call.
            if (toast.GetComponent("FadeTween") == null) AddTween(toast.gameObject, "FadeTween");
            SetTweenAlpha(toast.gameObject, 1f);

            Transform textObject = toast.Find("Text (TMP)");
            if (textObject == null)
            {
                textObject = CreateUI("Text (TMP)", toast, typeof(TextMeshProUGUI)).transform;
            }

            Stretch((RectTransform)textObject, 12f);

            var label = Ensure<TextMeshProUGUI>(textObject.gameObject);
            StyleText(label, RegularFont, 14f, Tan);
            label.alignment = TextAlignmentOptions.Center;
            label.text = string.Empty;
            EditorUtility.SetDirty(label);

            // Raised past the modals below, which are themselves raised last: a refusal the
            // player cannot read is the same as no refusal at all.
            Raise(canvas, "Toast");

            // Off in the saved scene. The frame has a background, so an always-on toast is an
            // empty bar hanging over the board from the moment it loads; DakonView switches it
            // on for the two seconds a refusal is on screen and off again afterwards.
            toast.gameObject.SetActive(false);
            EditorUtility.SetDirty(toast.gameObject);

            return label;
        }

        private static void StyleScrim(Transform canvas, string path)
        {
            var image = Find<Image>(canvas, path);
            if (image == null) return;

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Scrim;
            image.raycastTarget = true;
            EditorUtility.SetDirty(image);

            Stretch(image.rectTransform);

            // ui-style.md §7: an invisible click-outside-to-close, so no tint feedback.
            var button = Find<Button>(canvas, path);
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic = image;
                EditorUtility.SetDirty(button);
            }

            // ui-style.md §6b: the stock 0.6 is for a backdrop with nothing in it. The panel lives
            // inside this group, so 0.6 leaves the dialog itself permanently at 60% — which is what
            // an opened panel looked like here.
            SetTweenAlpha(image.gameObject, 1f);
        }

        /// <summary>
        /// A dialog frame: fixed size, centred, its title inset from the frame. The size is passed
        /// in because it is the panel's content that decides it — a tutorial diagram needs more than
        /// two stacked buttons do.
        /// </summary>
        private static void StylePanel(Transform canvas, string path, string title, Vector2 size)
        {
            var frame = Find<Image>(canvas, path);
            if (frame != null)
            {
                SetFrame(frame, CornerFrameSprite(), Color.white, 1f);
                Place(frame.rectTransform, Centre, Vector2.zero, size);
                EditorUtility.SetDirty(frame);
            }

            if (title == null) return;

            var label = Find<TextMeshProUGUI>(canvas, $"{path}/Text (TMP)");
            if (label == null) return;

            StyleText(label, BoldFont, 18f, Color.white);
            label.text = title;
            label.alignment = TextAlignmentOptions.Center;

            // Pinned to the top of the panel with the frame's inset around it, so a longer title
            // cannot walk into the content below it.
            var rect = label.rectTransform;
            Anchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            rect.offsetMin = new Vector2(PanelPad, -(PanelPad + 30f));
            rect.offsetMax = new Vector2(-PanelPad, -PanelPad);
            EditorUtility.SetDirty(label);
        }

        private static void StyleOutlineButton(Transform canvas, string path, string label)
        {
            var frame = Find<Image>(canvas, path);
            if (frame != null)
            {
                SetFrame(frame, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
                frame.raycastTarget = true;
                EditorUtility.SetDirty(frame);
            }

            var button = Find<Button>(canvas, path);
            if (button != null)
            {
                button.targetGraphic = frame;
                ApplyStockTint(button);
                EditorUtility.SetDirty(button);
            }

            var text = Find<TextMeshProUGUI>(canvas, $"{path}/Text (TMP)");
            if (text == null) return;

            StyleText(text, BoldFont, 13.5f, Color.white);
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            Inset(text.rectTransform, 12f, 12f, 4f, 4f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            EditorUtility.SetDirty(text);
        }

        // ---------------------------------------------------------------- rects

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCentre = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);

        /// <summary>
        /// Point-anchor an element: anchor and pivot at the same corner, so the offset is read
        /// straight off that corner and holds at any aspect (ui-style.md §3).
        /// </summary>
        private static void Place(Transform canvas, string path, Vector2 corner, Vector2 offset,
                                  Vector2 size)
        {
            var rect = Find<RectTransform>(canvas, path);
            if (rect != null) Place(rect, corner, offset, size);
        }

        private static void Place(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            Anchor(rect, corner, corner, corner);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
        }

        /// <summary>Stretch to the parent with per-edge padding — the whole of "proper padding".</summary>
        private static void Inset(Transform canvas, string path, float left, float right,
                                  float top, float bottom)
        {
            var rect = Find<RectTransform>(canvas, path);
            if (rect != null) Inset(rect, left, right, top, bottom);
        }

        private static void Inset(RectTransform rect, float left, float right, float top, float bottom)
        {
            Anchor(rect, Vector2.zero, Vector2.one, Centre);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            EditorUtility.SetDirty(rect);
        }

        /// <summary>Modals draw last (ui-style.md §2), whatever order the scene grew into.</summary>
        private static void Raise(Transform canvas, string path)
        {
            Transform found = canvas.Find(path);
            if (found != null) found.SetAsLastSibling();
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static void Strip<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component != null) Undo.DestroyObjectImmediate(component);
        }

        /// <summary>
        /// Sets a <c>FadeTween</c>'s settle alpha. The tweens live in the default assembly (LeanTween
        /// ships without an asmdef, so they cannot sit in one, and an asmdef cannot reference the
        /// default assembly) — hence the component is reached by name and edited as a SerializedObject
        /// rather than compiled against. ui-style.md §9 spells the same constraint out.
        /// </summary>
        private static void SetTweenAlpha(GameObject go, float toAlpha)
        {
            Component tween = go.GetComponent("FadeTween");
            if (tween == null) return;

            var serialized = new SerializedObject(tween);
            SerializedProperty property = serialized.FindProperty("toAlpha");

            if (property == null) return;

            property.floatValue = toAlpha;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tween);
        }

        // ---------------------------------------------------------------- wiring

        private static void WireView(UnityEngine.SceneManagement.Scene scene, Transform canvas,
                                     SeedType[] seeds, DakonCard cardPrefab)
        {
            var view = FindComponent<DakonView>(scene);
            if (view == null)
            {
                Debug.LogWarning("DakonUIBuilder: no DakonView in the scene.");
                return;
            }

            Transform holes = FindRoot(scene, "Holes");
            Transform player = holes != null ? holes.Find("Player Holes") : null;
            Transform opponent = holes != null ? holes.Find("Opponent Holes") : null;

            var serialized = new SerializedObject(view);
            Set(serialized, "highlightMarker", FindRoot(scene, "Highlight"));
            Set(serialized, "handContainer",
                Find<RectTransform>(canvas, "Hand Panel/HandTransform (1)/Mask/HandTransform"));
            Set(serialized, "cardPrefab", cardPrefab);
            Set(serialized, "handRoot", GameObjectAt(canvas, "Hand Panel/HandTransform (1)"));
            Set(serialized, "waitingForTurnPanel", GameObjectAt(canvas, "Hand Panel/Waiting For Turn"));
            Set(serialized, "waitingForTurnLabel",
                Find<TextMeshProUGUI>(canvas, "Hand Panel/Waiting For Turn"));
            Set(serialized, "turnLabel", Find<TextMeshProUGUI>(canvas, "Turns/Text (TMP)"));
            Set(serialized, "poolLabel", Find<TextMeshProUGUI>(canvas, "Turns/Pool Text"));
            Set(serialized, "scoreP0Label", Find<TextMeshProUGUI>(canvas, "Point P1/Text (TMP)"));
            Set(serialized, "scoreP1Label", Find<TextMeshProUGUI>(canvas, "Point P2/Text (TMP)"));
            Set(serialized, "gameOverPanel", GameObjectAt(canvas, "Game Over"));
            Set(serialized, "gameOverLabel", Find<TextMeshProUGUI>(canvas, "Game Over/Text (TMP)"));
            Set(serialized, "gameOverExitButton", Find<Button>(canvas, "Game Over/Exit"));
            Set(serialized, "toastLabel", EnsureToast(canvas));
            Set(serialized, "toastRoot", GameObjectAt(canvas, "Toast"));
            Set(serialized, "boardCamera", FindComponent<Camera>(scene));
            Set(serialized, "vignette", EnsureVignette(canvas));

            FillArray(serialized.FindProperty("seedTypes"), seeds);

            // Ring order: the player's ten holes are 0..9, the opponent's 10..19.
            var anchors = new Object[20];
            for (int i = 0; i < 10; i++)
            {
                anchors[i] = player != null ? player.Find($"p{i}") : null;
                anchors[i + 10] = opponent != null ? opponent.Find($"p{i}") : null;
            }

            FillArray(serialized.FindProperty("holeAnchors"), anchors);

            // Store bins, index = player * 2 + category, and SeedCategory puts Monocot first.
            var stores = new Object[4];
            stores[0] = player != null ? player.Find("SH Monocot") : null;
            stores[1] = player != null ? player.Find("SH Dicot") : null;
            stores[2] = opponent != null ? opponent.Find("SH Monocot") : null;
            stores[3] = opponent != null ? opponent.Find("SH Dicot") : null;
            FillArray(serialized.FindProperty("storeAnchors"), stores);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            var bootstrap = FindComponent<Museum.Net.DakonNetBootstrap>(scene);
            if (bootstrap == null) return;

            var bootstrapSerialized = new SerializedObject(bootstrap);
            Set(bootstrapSerialized, "view", view);
            bootstrapSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        /// <summary>
        /// The modal open/close chain. Both panels are plain <c>SetActive</c> targets — there is no
        /// pause controller in this scene, and the scrim/Resume pair closing the panel they sit in is
        /// the shape ui-style.md §2 describes.
        /// </summary>
        private static void WireButtons(UnityEngine.SceneManagement.Scene scene, Transform canvas)
        {
            GameObject pause = GameObjectAt(canvas, "Pause Panel");
            GameObject tutorial = GameObjectAt(canvas, "Tutorial Panel");

            Toggle(Find<Button>(canvas, "Setting Button"), pause, true);
            Toggle(Find<Button>(canvas, "Pause Panel"), pause, false);
            Toggle(Find<Button>(canvas, "Pause Panel/Panel/Resume"), pause, false);

            Toggle(Find<Button>(canvas, "Book Button"), tutorial, true);
            Toggle(Find<Button>(canvas, "Tutorial Panel"), tutorial, false);
            Toggle(Find<Button>(canvas, "Tutorial Panel/Panel/Close"), tutorial, false);

            var view = FindComponent<DakonView>(scene);
            var back = Find<Button>(canvas, "Pause Panel/Panel/Back to Museum");

            if (view != null && back != null)
            {
                ClearListeners(back);
                UnityEventTools.AddPersistentListener(back.onClick, view.BackToMainMenu);
                EditorUtility.SetDirty(back);
            }

            if (pause != null) pause.SetActive(false);
            if (tutorial != null) tutorial.SetActive(false);
        }

        /// <summary>
        /// Why a wired button can still do nothing when clicked, checked here rather than guessed at
        /// from a screenshot: no listener, no raycast target, a disabled Button — or a graphic that
        /// draws after it and covers it, which is the one that leaves no trace in the inspector.
        /// Logs a warning per fault and stays silent when there is none.
        /// </summary>
        private static void ReportClickable(Transform canvas, params string[] paths)
        {
            var graphics = canvas.GetComponentsInChildren<Graphic>(includeInactive: false);

            foreach (string path in paths)
            {
                var button = Find<Button>(canvas, path);
                if (button == null) continue;

                if (!button.isActiveAndEnabled || !button.interactable)
                    Debug.LogWarning($"DakonUIBuilder: '{path}' is disabled or not interactable.", button);

                if (button.onClick.GetPersistentEventCount() == 0)
                    Debug.LogWarning($"DakonUIBuilder: '{path}' has no click listener.", button);

                var target = button.targetGraphic;
                if (target == null || !target.raycastTarget)
                {
                    Debug.LogWarning($"DakonUIBuilder: '{path}' has no raycast target graphic.", button);
                    continue;
                }

                Rect area = ScreenRect((RectTransform)button.transform);

                foreach (Graphic graphic in graphics)
                {
                    if (graphic == target || !graphic.raycastTarget) continue;
                    if (graphic.transform.IsChildOf(button.transform)) continue;
                    if (!DrawsAfter(graphic.transform, button.transform)) continue;
                    if (!ScreenRect(graphic.rectTransform).Overlaps(area)) continue;

                    Debug.LogWarning($"DakonUIBuilder: '{graphic.name}' draws over '{path}' and takes " +
                                     "its clicks.", graphic);
                }
            }
        }

        /// <summary>Canvas draw order: later siblings paint (and are hit) first, depth by depth.</summary>
        private static bool DrawsAfter(Transform a, Transform b)
        {
            var aChain = Chain(a);
            var bChain = Chain(b);

            for (int i = 0; i < aChain.Count && i < bChain.Count; i++)
            {
                if (aChain[i] == bChain[i]) continue;
                return aChain[i].GetSiblingIndex() > bChain[i].GetSiblingIndex();
            }

            return aChain.Count > bChain.Count;
        }

        private static System.Collections.Generic.List<Transform> Chain(Transform t)
        {
            var chain = new System.Collections.Generic.List<Transform>();
            for (Transform current = t; current != null; current = current.parent) chain.Add(current);
            chain.Reverse();
            return chain;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(Mathf.Min(corners[0].x, corners[2].x), Mathf.Min(corners[0].y, corners[2].y),
                                   Mathf.Max(corners[0].x, corners[2].x), Mathf.Max(corners[0].y, corners[2].y));
        }

        private static void Toggle(Button button, GameObject target, bool active)
        {
            if (button == null || target == null) return;

            ClearListeners(button);
            UnityEventTools.AddBoolPersistentListener(button.onClick, target.SetActive, active);
            EditorUtility.SetDirty(button);
        }

        private static void ClearListeners(Button button)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
        }

        // ---------------------------------------------------------------- lookup

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                Debug.LogWarning($"DakonUIBuilder: no serialized field '{name}'.");
                return;
            }

            if (value == null) Debug.LogWarning($"DakonUIBuilder: '{name}' has no target in the scene.");
            property.objectReferenceValue = value;
        }

        private static UnityEngine.SceneManagement.Scene OpenScene()
        {
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            return active.path == ScenePath
                ? active
                : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                      ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        private static Transform FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }

            return null;
        }

        private static T FindComponent<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject GameObjectAt(Transform canvas, string path)
        {
            Transform found = canvas.Find(path);
            return found == null ? null : found.gameObject;
        }

        private static T Find<T>(Transform canvas, string path) where T : Component
        {
            Transform found = canvas.Find(path);

            if (found == null)
            {
                Debug.LogWarning($"DakonUIBuilder: '{path}' not found under the Canvas; skipped.");
                return null;
            }

            var component = found.GetComponent<T>();

            if (component == null)
            {
                Debug.LogWarning($"DakonUIBuilder: '{path}' has no {typeof(T).Name}; skipped.");
            }

            return component;
        }
    }
}
