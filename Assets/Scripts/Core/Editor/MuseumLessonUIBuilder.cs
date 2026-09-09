using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Museum.Core;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Builds the museum's lesson panels: one GameLessonData asset per game, and one
    /// world-space panel hung beside that game's exhibit screen, paged by the trigger volume
    /// that already starts the video.
    ///
    /// Re-runnable. Panel *contents* are regenerated every time; panel *placement* is adopted
    /// from whatever is in the scene, because where a panel hangs is authored by hand.
    /// </summary>
    /// <remarks>
    /// The games are data, not code — see <see cref="LessonContent"/>, which is generated from
    /// the curriculum spreadsheet. This file only decides how a panel is built and where a new
    /// one lands.
    /// </remarks>
    public static class MuseumLessonUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Museum.unity";
        private const string LessonFolder = "Assets/Resources/lessons";

        /// <summary>Scene-root parent for everything this builder generates.</summary>
        private const string ContainerName = "Lessons (Generated)";

        // The panel is authored in the project's 800×600 design units (ui-style.md §1) and
        // then scaled down to the size it wants to be on the wall.
        private static readonly Vector2 PanelSize = new Vector2(820f, 520f);
        private const float PanelPad = 32f;

        /// <summary>Panel height as a fraction of the video frame's, so it reads as its equal.</summary>
        private const float VideoHeightRatio = 1.12f;

        /// <summary>Metres of wall between the video frame and the panel.</summary>
        private const float VideoGap = 0.4f;

        /// <summary>How far the reader's trigger may sit from the panel before it is suspicious.</summary>
        private const float ReaderReach = 6f;

        /// <summary>
        /// Metres from the floor a visitor reads from to the centre of the panel. Eye height is
        /// the same on every floor of the building, so how high a plaque hangs is a rule rather
        /// than a per-panel judgement — Dakon's was placed by hand, and the other seventeen,
        /// derived from screens hung at their own heights, used to sit half a metre above it.
        /// </summary>
        private const float ReadingHeight = 1.88f;

        // The Museum's KONTROL card, in its own 800×600 units. Read off the existing rows:
        // 14×14 caps, 16 apart, one row per divider.
        private static readonly Vector2 HudKeySize = new Vector2(14f, 14f);
        private const float HudKeyCapSize = 9f;
        private const float HudActionSize = 8.5f;
        private const float HudActionInset = 4f;
        private const float HudRowY = -35f;
        private const float HudRowKeyX = -48f;
        private const float HudKeyStep = 16f;
        private const float HudRowLabelX = 11f;

        // Read from standing distance rather than from a desk, so the type runs about 2x the
        // screen-UI table in ui-style.md 4. The panel is a fixed 2.9 m of wall, so this is the
        // only dial that makes the copy physically larger — growing the rect at the same metres
        // would shrink it. It is paid for in pages, and paging is a keypress.
        private const float TitleSize = 38f;
        private const float EyebrowSize = 14f;
        private const float TabSize = 18f;
        private const float BodySize = 25f;
        private const float FooterSize = 16f;
        private const float KeycapSize = 18f;

        /// <summary>The museum's own surface, as the main menu wears it. Opaque, so the copy reads
        /// against the lit interior without a scrim.</summary>
        private const string BackgroundSprite = "Assets/Texture2D/Main Menu BG.png";

        /// <summary>Native aspect of that art. Used to cover the panel rather than squash it.</summary>
        private const float BackgroundAspect = 1920f / 1080f;

        /// <summary>
        /// How far the art overscans the panel. The menu's "16 Permainan Tradisional Indonesia"
        /// caption is baked into the bottom of that image and belongs on the menu, not on a
        /// plaque — overscanning pushes it past the mask, at either edge.
        /// </summary>
        private const float BackgroundZoom = 1.3f;

        /// <summary>The strip of section tabs, and the divider it stands on.</summary>
        private const float TabStripY = -122f;
        private const float TabActiveHeight = 40f;
        private const float TabIdleHeight = 33f;

        /// <summary>Where a panel hangs. Authored by hand in the scene, or derived on first build.</summary>
        private struct PanelPose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public string Source;
        }

        [MenuItem("Museum/Rebuild UI/Lesson Panels")]
        public static void Rebuild()
        {
            Scene scene = OpenScene();
            if (!scene.IsValid()) return;

            // The readers live on trigger volumes this builder does not own, so they cannot be
            // cleaned up by destroying a panel root. Strip them all and re-attach below.
            foreach (LessonReader stale in Object.FindObjectsByType<LessonReader>(FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(stale);
            }

            Transform container = EnsureContainer(scene);
            int built = 0;

            foreach (LessonSource source in LessonContent.All())
            {
                GameLessonData lesson = WriteLesson(source);

                GameObject group = FindInScene(scene, source.VideoGroup);
                if (group == null)
                {
                    Debug.LogWarning($"MuseumLessonUIBuilder: no '{source.VideoGroup}' group in the " +
                                     $"Museum scene; the {source.DisplayName} lesson asset was written " +
                                     "but no panel was built.");
                    continue;
                }

                BuildPlaque(scene, container, group, source, lesson);
                built++;
            }

            StyleControlsRow(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"MuseumLessonUIBuilder: {built} of {LessonContent.All().Length} lesson " +
                      "panels built and saved.");
        }

        /// <summary>
        /// One scene-root parent for every generated panel, the way the lighting builder keeps
        /// its output under `Lighting (Generated)`. Kept at identity so nothing inherits a
        /// scale: the exhibit props around here are scaled non-uniformly and would shear a
        /// canvas parented to them.
        /// </summary>
        private static Transform EnsureContainer(Scene scene)
        {
            GameObject existing = FindInScene(scene, ContainerName);
            if (existing != null) return existing.transform;

            var container = new GameObject(ContainerName);
            Undo.RegisterCreatedObjectUndo(container, UndoLabel);
            return container.transform;
        }

        // --- content -------------------------------------------------------------------

        private static GameLessonData WriteLesson(LessonSource source)
        {
            if (!AssetDatabase.IsValidFolder(LessonFolder))
            {
                Directory.CreateDirectory(LessonFolder);
                AssetDatabase.Refresh();
            }

            string path = $"{LessonFolder}/{source.GameKey}.asset";
            var lesson = AssetDatabase.LoadAssetAtPath<GameLessonData>(path);

            if (lesson == null)
            {
                lesson = ScriptableObject.CreateInstance<GameLessonData>();
                AssetDatabase.CreateAsset(lesson, path);
            }

            lesson.gameKey = source.GameKey;
            lesson.displayName = source.DisplayName;
            lesson.sections = new[]
            {
                Section(LessonContent.HeadingSains, source.Sains),
                Section(LessonContent.HeadingSportScience, source.SportScience),
                Section(LessonContent.HeadingAsalUsul, source.AsalUsul),
                Section(LessonContent.HeadingSeniBudaya, source.SeniBudaya),
            };

            EditorUtility.SetDirty(lesson);
            return lesson;
        }

        private static LessonSection Section(string heading, string body) =>
            new LessonSection { heading = heading, body = body };

        // --- placement -----------------------------------------------------------------

        /// <summary>
        /// Everything is anchored on the game's exhibit screen: the panel hangs beside it, and
        /// the reader rides the trigger volume that already starts that video. Both already
        /// exist for all eighteen games, so a lesson adds no collider and no new geometry.
        /// </summary>
        private static void BuildPlaque(Scene scene, Transform container, GameObject group,
                                        LessonSource source, GameLessonData lesson)
        {
            Transform screen = FindScreenIn(group);

            if (screen == null)
            {
                Debug.LogWarning($"MuseumLessonUIBuilder: '{group.name}' holds no StreamedVideoScreen, " +
                                 $"so the {source.DisplayName} panel has nothing to hang beside.", group);
                return;
            }

            string rootName = $"{source.DisplayName} Lesson";
            string panelName = $"Lesson Panel ({source.DisplayName})";

            // Found anywhere in the scene, not just under the container: an author may have
            // dragged a panel elsewhere, and their placement still has to survive a rebuild.
            GameObject existingPanel = FindInScene(scene, panelName);
            PanelPose? authored = CapturePose(existingPanel);

            GameObject existingRoot = FindInScene(scene, rootName);
            if (existingRoot != null) Undo.DestroyObjectImmediate(existingRoot);

            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, UndoLabel);
            root.transform.SetParent(container, false);

            GameObject volume = FindTriggerFor(screen);

            PanelPose pose = authored ?? PoseBesideScreen(screen);
            pose = AtReadingHeight(pose, volume);

            LessonPanel panel = BuildPanel(root.transform, source, lesson, panelName, pose);
            AttachReader(source, panel, volume);
        }

        /// <summary>
        /// Drops the panel to <see cref="ReadingHeight"/> above the floor of the volume it is
        /// read from. Which wall a plaque hangs on and which way it faces stay authored; how high
        /// it hangs does not, because a visitor's eyes are at the same height in every room and
        /// eighteen plaques at eighteen elevations read as a mistake rather than as a choice.
        /// </summary>
        /// <remarks>
        /// The floor comes from the trigger volume rather than from the panel's own storey: that
        /// collider covers the ground a visitor actually stands on, which is the thing the height
        /// is relative to. Without one, the authored height is left alone — there is nothing to
        /// measure against, and guessing would be worse than an inconsistent panel.
        /// </remarks>
        private static PanelPose AtReadingHeight(PanelPose pose, GameObject volume)
        {
            var collider = volume == null ? null : volume.GetComponent<Collider>();
            if (collider == null) return pose;

            float levelled = collider.bounds.min.y + ReadingHeight;
            if (Mathf.Approximately(levelled, pose.Position.y)) return pose;

            float shift = levelled - pose.Position.y;
            pose.Source += $", {(shift < 0f ? "lowered" : "raised")} {Mathf.Abs(shift):F2} m to reading height";
            pose.Position = new Vector3(pose.Position.x, levelled, pose.Position.z);
            return pose;
        }

        // --- the museum's KONTROL panel --------------------------------------------------

        /// <summary>
        /// Adds the `Q E — Ganti halaman` row to the Museum's KONTROL card, so the paging keys
        /// are advertised in the same place as WASD and the mouse. Idempotent: it adopts the
        /// orphan `E` cap that was already sitting in that row unlabelled.
        /// </summary>
        /// <remarks>
        /// Lives here rather than in MuseumUIBuilder because the row exists for the lessons —
        /// one menu item should leave the feature complete, HUD copy included.
        /// </remarks>
        private static void StyleControlsRow(Scene scene)
        {
            GameObject tutorial = FindInScene(scene, "Tutorial");

            if (tutorial == null)
            {
                Debug.LogWarning("MuseumLessonUIBuilder: no 'Tutorial' panel in the Museum HUD, so " +
                                 "the Q/E keys are not advertised anywhere.");
                return;
            }

            Transform panel = tutorial.transform;

            // The row already holds one unlabelled cap; it becomes the E of the pair.
            Transform existingCap = panel.Find("E");
            Image nextCap = existingCap != null
                ? StyleHudKeycap(existingCap.gameObject, "E")
                : StyleHudKeycap(CreateUI("E", panel, typeof(Image)), "E");
            Place(nextCap.rectTransform, new Vector2(HudRowKeyX + HudKeyStep, HudRowY));

            Transform prev = panel.Find("Q");
            Image prevCap = prev != null
                ? StyleHudKeycap(prev.gameObject, "Q")
                : StyleHudKeycap(CreateUI("Q", panel, typeof(Image)), "Q");
            Place(prevCap.rectTransform, new Vector2(HudRowKeyX, HudRowY));

            Transform existingLabel = panel.Find("Text (TMP) (3)");
            GameObject labelObject = existingLabel != null
                ? existingLabel.gameObject
                : CreateUI("Text (TMP) (3)", panel, typeof(TextMeshProUGUI));

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            StyleText(label, RegularFont, HudActionSize, Color.white);
            label.text = "Ganti halaman";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(HudActionInset, 0f, 0f, 0f);
            Place(label.rectTransform, new Vector2(HudRowLabelX, HudRowY));
            label.rectTransform.sizeDelta = new Vector2(64f, 14f);

            EditorUtility.SetDirty(tutorial);
        }

        private static Image StyleHudKeycap(GameObject go, string cap)
        {
            var frame = go.GetComponent<Image>() ?? Undo.AddComponent<Image>(go);
            SetFrame(frame, FrameSprite(), Color.white, ChipPixelsPerUnitMultiplier);
            frame.rectTransform.sizeDelta = HudKeySize;

            Transform child = go.transform.Find("Text (TMP)");
            GameObject textObject = child != null
                ? child.gameObject
                : CreateUI("Text (TMP)", go.transform, typeof(TextMeshProUGUI));

            var text = textObject.GetComponent<TextMeshProUGUI>();
            Stretch(text.rectTransform);
            StyleText(text, BoldFont, HudKeyCapSize, Color.white);
            text.text = cap;
            text.alignment = TextAlignmentOptions.Center;
            return frame;
        }

        private static void Place(RectTransform rect, Vector2 position)
        {
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = position;
        }

        // --- pose ------------------------------------------------------------------------

        /// <summary>
        /// Reads back a panel that is already in the scene. Placement is a judgement about the
        /// room — where the wall is, what it hangs beside, how big it wants to be next to the
        /// video frame — so once someone has placed one by hand, that wins over anything this
        /// builder would compute. Only the contents are regenerated.
        /// </summary>
        /// <summary>
        /// Reads back a panel that is already in the scene. Placement is a judgement about the
        /// room — which wall, what it hangs beside, how big it wants to be next to the video
        /// frame, and which way it reads from — so once someone has placed one by hand, that
        /// wins over anything this builder would compute. Only the contents are regenerated.
        /// </summary>
        private static PanelPose? CapturePose(GameObject panel)
        {
            if (panel == null) return null;

            // The authored value is a scale, but what was authored is a *size on the wall*. When
            // the design-unit rect changes — as it did when the type grew — keeping the scale
            // would silently resize every plaque in the building, so the metres are preserved
            // and the scale is re-derived from them.
            var rect = panel.GetComponent<RectTransform>();
            float authored = panel.transform.localScale.x;
            float scale = rect != null && rect.sizeDelta.y > 0f
                ? authored * rect.sizeDelta.y / PanelSize.y
                : authored;

            return new PanelPose
            {
                Position = panel.transform.position,
                Rotation = panel.transform.rotation,
                Scale = scale,
                Source = Mathf.Approximately(scale, authored)
                    ? "adopted from the scene"
                    : $"adopted from the scene, rescaled {authored:F4} to {scale:F4} to hold its size",
            };
        }

        /// <summary>
        /// First-build placement: beside the game's exhibit screen, a little taller than it, on
        /// the same wall and at the same height, facing the same way. A lesson is the reading
        /// companion to that footage, so it belongs in the same band of wall.
        /// </summary>
        /// <remarks>
        /// The rotation is copied from the screen rather than derived. Which yaw reads correctly
        /// depends on the viewing side and the rotation together, and the screens are already
        /// hung to face their room — so matching them is more reliable than any rule about
        /// canvas normals.
        /// </remarks>
        private static PanelPose PoseBesideScreen(Transform screen)
        {
            var canvas = screen.GetComponentInParent<Canvas>();
            var canvasRect = (RectTransform)canvas.transform;

            float videoWidth = canvasRect.sizeDelta.x * canvas.transform.lossyScale.x;
            float videoHeight = canvasRect.sizeDelta.y * canvas.transform.lossyScale.y;

            float scale = videoHeight * VideoHeightRatio / PanelSize.y;
            float step = videoWidth * 0.5f + PanelSize.x * scale * 0.5f + VideoGap;

            return new PanelPose
            {
                Position = screen.position - screen.right * step,
                Rotation = screen.rotation,
                Scale = scale,
                Source = $"derived beside '{screen.name}'",
            };
        }

        private static Transform FindScreenIn(GameObject group)
        {
            var screen = group.GetComponentInChildren<StreamedVideoScreen>(true);
            return screen == null ? null : screen.transform;
        }

        // --- panel ---------------------------------------------------------------------

        private static LessonPanel BuildPanel(Transform parent, LessonSource source, GameLessonData lesson,
                                              string panelName, PanelPose pose)
        {
            var go = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // No GraphicRaycaster on purpose: the museum locks the cursor, so nothing on this
            // canvas is ever pointed at. LessonReader drives it from the keyboard instead.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = PanelSize;

            go.transform.position = pose.Position;
            go.transform.rotation = pose.Rotation;
            go.transform.localScale = Vector3.one * pose.Scale;
            Debug.Log($"MuseumLessonUIBuilder: {source.DisplayName} panel placed ({pose.Source}) — " +
                      $"{PanelSize.x * pose.Scale:F2} × {PanelSize.y * pose.Scale:F2} m at {pose.Position}.");

            BuildBackground(rect);

            TMP_Text title = AddLabel(rect, "Title", lesson.displayName, DisplayFont, TitleSize, Gold,
                                      new Vector2(PanelPad + 210f, -PanelPad - 24f), new Vector2(420f, 46f),
                                      new Vector2(0f, 1f));
            title.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text eyebrow = AddLabel(rect, "Eyebrow", "MATERI BELAJAR", MediumFont, EyebrowSize, Tan,
                                        new Vector2(-PanelPad - 140f, -PanelPad - 22f),
                                        new Vector2(280f, 22f), new Vector2(1f, 1f));
            eyebrow.alignment = TextAlignmentOptions.MidlineRight;
            eyebrow.characterSpacing = 6f;

            // The rule the tabs stand on. Drawn before them so the active tab, which overlaps it,
            // covers the line and reads as merged into the page below.
            AddRule(rect, "Divider", TabStripY);

            BuildTabs(rect, lesson, out Image[] tabFrames, out TMP_Text[] tabLabels,
                      out Image[] tabAccents, out LayoutElement[] tabHeights);

            TMP_Text body = BuildBody(rect);

            AddRule(rect, "Footer Divider", -(PanelSize.y - 114f));

            Image prevCap = BuildKeycap(rect, "Prev Keycap", "Q", new Vector2(0f, 0f),
                                        new Vector2(PanelPad + 23f, 74f));
            Image nextCap = BuildKeycap(rect, "Next Keycap", "E", new Vector2(1f, 0f),
                                        new Vector2(-PanelPad - 23f, 74f));

            RectTransform dots = BuildDots(rect, out Image dotTemplate);

            // Clear of the E keycap rather than tucked against it.
            TMP_Text page = AddLabel(rect, "Page Label", "1/1", MediumFont, FooterSize, Tan,
                                     new Vector2(-140f, 74f), new Vector2(90f, 22f), new Vector2(1f, 0f));
            page.alignment = TextAlignmentOptions.MidlineRight;

            TMP_Text hint = AddLabel(rect, "Hint", "Dekati untuk membaca", RegularFont, FooterSize, Tan,
                                     new Vector2(0f, 38f), new Vector2(560f, 22f), new Vector2(0.5f, 0f));

            var panel = go.AddComponent<LessonPanel>();
            var serialized = new SerializedObject(panel);
            Set(serialized, "lesson", lesson);
            serialized.FindProperty("lessonKey").stringValue = lesson.gameKey;
            Set(serialized, "titleLabel", title);
            Set(serialized, "bodyLabel", body);
            Set(serialized, "pageLabel", page);
            Set(serialized, "hintLabel", hint);
            Set(serialized, "dotsRoot", dots);
            Set(serialized, "dotTemplate", dotTemplate);
            Set(serialized, "prevKeycap", prevCap);
            Set(serialized, "nextKeycap", nextCap);
            FillArray(serialized.FindProperty("tabFrames"), tabFrames);
            FillArray(serialized.FindProperty("tabLabels"), tabLabels);
            FillArray(serialized.FindProperty("tabAccents"), tabAccents);
            FillArray(serialized.FindProperty("tabHeights"), tabHeights);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        /// <summary>
        /// The main menu's batik surface, opaque, cropped to the panel. Full-bleed art is Simple
        /// and untinted (ui-style.md 6) — and because it covers rather than stretches, the panel
        /// keeps the artwork's proportions instead of squashing the pattern into ovals.
        /// </summary>
        private static void BuildBackground(RectTransform parent)
        {
            GameObject backdrop = CreateUI("Backdrop", parent);
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            Stretch(backdropRect);
            backdrop.AddComponent<RectMask2D>();

            GameObject art = CreateUI("BG", backdrop.transform, typeof(Image));
            RectTransform artRect = art.GetComponent<RectTransform>();
            Anchor(artRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            float artHeight = PanelSize.y * BackgroundZoom;
            artRect.sizeDelta = new Vector2(artHeight * BackgroundAspect, artHeight);
            artRect.anchoredPosition = Vector2.zero;

            var fill = art.GetComponent<Image>();
            fill.sprite = LoadSprite(BackgroundSprite);
            fill.type = Image.Type.Simple;
            fill.color = Color.white;
            fill.raycastTarget = false;

            GameObject frame = CreateUI("Frame", parent, typeof(Image));
            Stretch(frame.GetComponent<RectTransform>());
            SetFrame(frame.GetComponent<Image>(), CornerFrameSprite(), ContainerFrame,
                     ContainerPixelsPerUnitMultiplier);
        }

        /// <summary>
        /// The section tabs, in the order the sections are written. An indicator, not a control:
        /// the museum locks the cursor and this canvas has no raycaster (ui-style.md 6d), so a
        /// visitor reads which of the four parts they are in and steps through them with Q/E.
        /// </summary>
        /// <remarks>
        /// Widths follow the labels — a content-sized fitter per tab — because the four headings
        /// are different lengths and forcing them to a common width would leave "ASAL-USUL"
        /// swimming in its own tab. Heights are left to the runtime: <see cref="LessonPanel"/>
        /// raises the active one, which is what makes the strip read as tabs rather than chips.
        /// </remarks>
        private static void BuildTabs(RectTransform parent, GameLessonData lesson, out Image[] frames,
                                      out TMP_Text[] labels, out Image[] accents,
                                      out LayoutElement[] heights)
        {
            GameObject strip = CreateUI("Tabs", parent);
            RectTransform stripRect = strip.GetComponent<RectTransform>();
            Anchor(stripRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f));
            stripRect.sizeDelta = new Vector2(-PanelPad * 2f, TabActiveHeight);
            stripRect.anchoredPosition = new Vector2(0f, TabStripY);

            var layout = strip.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            // Lower-left: the tabs share a bottom edge on the divider and the active one grows
            // upward. Heights come from each tab's LayoutElement, which the runtime rewrites;
            // widths are measured off each tab's own label, one level down.
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            frames = new Image[lesson.sections.Length];
            labels = new TMP_Text[lesson.sections.Length];
            accents = new Image[lesson.sections.Length];
            heights = new LayoutElement[lesson.sections.Length];

            for (int i = 0; i < lesson.sections.Length; i++)
            {
                frames[i] = BuildTab(strip.transform, lesson.sections[i].heading, out labels[i],
                                     out accents[i], out heights[i]);
            }
        }

        private static Image BuildTab(Transform parent, string heading, out TMP_Text label,
                                      out Image accent, out LayoutElement height)
        {
            GameObject go = CreateUI($"Tab ({heading})", parent, typeof(Image));

            // The height the strip lays this tab out at. A LayoutElement rather than a rect the
            // runtime writes to: the width is measured by the strip, and poking sizeDelta on a
            // laid-out rect makes the two rebuild in the wrong order and collapses the strip.
            height = go.AddComponent<LayoutElement>();
            height.preferredHeight = TabIdleHeight;
            height.minHeight = TabIdleHeight;
            // No flexible share: a tab is as wide as its heading, never as wide as the leftovers.
            height.flexibleWidth = 0f;

            var frame = go.GetComponent<Image>();
            SetFrame(frame, FrameSprite(), InnerFrame, ChipPixelsPerUnitMultiplier);

            // ui-style.md 6c: nothing sits on the frame's bevel. The padding is what gives the tab
            // its width: the strip asks this group for a preferred width, and it answers with the
            // label's own plus this inset.
            var padding = go.AddComponent<HorizontalLayoutGroup>();
            padding.padding = new RectOffset(16, 16, 6, 6);
            padding.childAlignment = TextAnchor.MiddleCenter;
            padding.childControlWidth = true;
            padding.childControlHeight = true;
            padding.childForceExpandWidth = false;
            padding.childForceExpandHeight = true;

            GameObject text = CreateUI("Text (TMP)", go.transform, typeof(TextMeshProUGUI));
            label = text.GetComponent<TextMeshProUGUI>();
            StyleText(label, SemiBoldFont, TabSize, Tan);
            label.text = heading;
            label.alignment = TextAlignmentOptions.Center;
            // One heading, one line — a wrapped tab would push the whole strip off the divider.
            label.textWrappingMode = TextWrappingModes.NoWrap;

            GameObject accentObject = CreateUI("Accent", go.transform, typeof(Image));
            RectTransform accentRect = accentObject.GetComponent<RectTransform>();
            Anchor(accentRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            accentRect.sizeDelta = new Vector2(0f, 2f);
            accentRect.anchoredPosition = Vector2.zero;
            // Outside the padding group's control, or it would be laid out as a second column.
            accentObject.AddComponent<LayoutElement>().ignoreLayout = true;

            accent = accentObject.GetComponent<Image>();
            accent.color = Gold;
            accent.raycastTarget = false;
            accent.enabled = false;

            return frame;
        }

        private static TMP_Text BuildBody(RectTransform parent)
        {
            GameObject go = CreateUI("Body", parent, typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(PanelPad, 132f);
            rect.offsetMax = new Vector2(-PanelPad, -152f);

            var body = go.GetComponent<TextMeshProUGUI>();
            StyleText(body, RegularFont, BodySize, Cream);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 16f;
            body.paragraphSpacing = 14f;

            // The runtime sets this too, but only after it has a lesson — an unpopulated panel
            // in the editor should still show what the pagination will look like.
            body.overflowMode = TextOverflowModes.Page;
            return body;
        }

        private static void AddRule(RectTransform parent, string name, float y)
        {
            GameObject go = CreateUI(name, parent, typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-PanelPad * 2f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);

            var image = go.GetComponent<Image>();
            image.color = InnerFrame;
            image.raycastTarget = false;
        }

        /// <summary>
        /// A key hint, not a button — it carries no Button component and nothing can click it.
        /// LessonPanel tints it to say whether the key is live.
        /// </summary>
        private static Image BuildKeycap(RectTransform parent, string name, string glyph,
                                         Vector2 anchor, Vector2 position)
        {
            GameObject go = CreateUI(name, parent, typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, anchor, anchor, new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(46f, 36f);

            var image = go.GetComponent<Image>();
            SetFrame(image, FrameSprite(), Tan, ChipPixelsPerUnitMultiplier);

            GameObject label = CreateUI("Text (TMP)", go.transform, typeof(TextMeshProUGUI));
            Stretch(label.GetComponent<RectTransform>());
            var tmp = label.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, BoldFont, KeycapSize, Cream);
            tmp.text = glyph;
            tmp.alignment = TextAlignmentOptions.Center;

            return image;
        }

        private static RectTransform BuildDots(RectTransform parent, out Image template)
        {
            GameObject go = CreateUI("Dots", parent);
            RectTransform rect = go.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = new Vector2(0f, 74f);
            rect.sizeDelta = new Vector2(300f, 12f);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // The template stays in the scene so the dot styling is inspectable; LessonPanel
            // disables it and clones one per page.
            GameObject dot = CreateUI("Dot Template", go.transform, typeof(Image));
            dot.GetComponent<RectTransform>().sizeDelta = new Vector2(8f, 8f);
            template = dot.GetComponent<Image>();
            template.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            template.color = Tan;
            template.raycastTarget = false;
            dot.SetActive(false);

            return rect;
        }

        // --- reader ---------------------------------------------------------------------

        /// <summary>
        /// Hangs the reader on the game's existing doorway trigger rather than adding a
        /// collider of its own. Each plaque stands beside the doorway it belongs to, and that
        /// doorway's volume already covers the ground a visitor reads from, so a dedicated
        /// volume would be a second collider over the same floor.
        /// </summary>
        /// <summary>
        /// Hangs the reader on the trigger volume that already starts this game's video, rather
        /// than adding a collider. That volume is the floor a visitor stands on to watch the
        /// screen, which is exactly where they stand to read the panel beside it. For Dakon the
        /// same object is also the game's doorway, so one trigger serves three purposes.
        /// </summary>
        private static void AttachReader(LessonSource source, LessonPanel panel, GameObject volume)
        {
            if (volume == null)
            {
                Debug.LogWarning($"MuseumLessonUIBuilder: nothing triggers the {source.DisplayName} " +
                                 "video, so its panel was built but cannot be paged. Give that screen " +
                                 "a VideoTriggerPlayer, or the panel a trigger of its own.", panel);
                return;
            }

            var collider = volume.GetComponent<Collider>();

            if (collider == null || !collider.isTrigger)
            {
                Debug.LogWarning($"MuseumLessonUIBuilder: '{volume.name}' has no trigger collider; " +
                                 $"the {source.DisplayName} panel cannot be paged.", volume);
                return;
            }

            // The volume does not have to contain the panel — it covers the floor a visitor
            // stands on, a couple of metres out. What would be wrong is the two being in
            // different parts of the building.
            float reach = Vector3.Distance(collider.bounds.ClosestPoint(panel.transform.position),
                                           panel.transform.position);

            if (reach > ReaderReach)
            {
                Debug.LogWarning($"MuseumLessonUIBuilder: the nearest floor covered by the " +
                                 $"{source.DisplayName} video trigger is {reach:F1} m from its panel. " +
                                 "Check a visitor can read it from inside that volume.", volume);
            }

            var reader = Undo.AddComponent<LessonReader>(volume);
            var serialized = new SerializedObject(reader);
            Set(serialized, "panel", panel);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The VideoTriggerPlayer that drives a given screen, found by its own `screen`
        /// reference rather than by proximity — the volumes are all called "Cube" and several
        /// sit within a few metres of each other on the floors above and below.
        /// </summary>
        private static GameObject FindTriggerFor(Transform screen)
        {
            var target = screen.GetComponent<StreamedVideoScreen>();

            foreach (VideoTriggerPlayer trigger in Object.FindObjectsByType<VideoTriggerPlayer>(FindObjectsSortMode.None))
            {
                if (trigger.gameObject.scene != screen.gameObject.scene) continue;

                var serialized = new SerializedObject(trigger);
                if (serialized.FindProperty("screen").objectReferenceValue == target) return trigger.gameObject;
            }

            return null;
        }

        // --- scene helpers -------------------------------------------------------------

        /// <summary>
        /// Uses the Museum scene as it currently stands in the editor, reopening it only when
        /// it is not already the one loaded.
        /// </summary>
        /// <remarks>
        /// Deliberately never reopens a scene that is already open. Placement is authored by
        /// hand and adopted by this builder, so a reopen would discard the unsaved nudge the
        /// author is in the middle of and then faithfully "adopt" the older pose off disk —
        /// silently undoing their work. Reopening also drops unsaved changes made anywhere
        /// else in the scene, which is not this menu item's business.
        /// </remarks>
        private static Scene OpenScene()
        {
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.IsValid() && active.path == ScenePath) return active;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == name) return child.gameObject;
                }
            }

            return null;
        }

        private static void Set(SerializedObject serialized, string field, Object value)
        {
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"MuseumLessonUIBuilder: no field '{field}' on " +
                                 $"{serialized.targetObject.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
        }
    }
}
