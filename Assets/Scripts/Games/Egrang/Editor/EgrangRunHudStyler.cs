using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Restyles the run HUD — the timing bar, its cursor and the step button — to
    /// <c>Assets/Docs/ui-style.md</c>. The selection panel was generated against those rules; the bar
    /// was hand-built before them, so it arrives at stock Unity defaults: an overlay canvas at Match 0,
    /// a screen-wide track, a grey block for a cursor, and a `UISprite` button lettered in
    /// LiberationSans.
    ///
    /// Layout, bottom-centre and stacked: the track sits on an opaque plate, the button sits under it,
    /// and a caption under that says what to press. The track is bottom-anchored rather than
    /// centre-anchored so it stays the same distance off the bottom edge on a kiosk screen of any
    /// aspect — a centre anchor drifts with the height (ui-style.md §3).
    ///
    /// Re-runnable: it edits the objects already in the scene and creates the plate only if missing, so
    /// running it after moving something by hand restores the styling without duplicating anything.
    /// </summary>
    public static class EgrangRunHudStyler
    {
        // ui-style.md §5.
        static readonly Color Cream = new Color(0.957f, 0.918f, 0.835f, 1f);
        static readonly Color Tan = new Color(0.847f, 0.753f, 0.631f, 1f);

        // ui-style.md §6, the corner-radius ladder.
        const float ButtonPixelsPerUnitMultiplier = 3.13f;
        const float ChipPixelsPerUnitMultiplier = 5.05f;

        const string FrameSpritePath = "Assets/Sprites/frame_default.png";
        const string CornerFrameSpritePath = "Assets/Sprites/frame_with_corner.png";
        const string BoldFont = "Assets/Fonts/Roboto-Bold SDF.asset";
        const string RegularFont = "Assets/Fonts/Roboto-Regular SDF.asset";

        // 800×600 design units. The stack reads upward from the bottom edge: caption, button, track.
        static readonly Vector2 TrackSize = new Vector2(420f, 18f);
        static readonly Vector2 CursorSize = new Vector2(5f, 28f);
        static readonly Vector2 ButtonSize = new Vector2(170f, 40f);
        const float TrackHeight = 112f;
        const float ButtonHeight = 44f;
        const float CaptionHeight = 20f;

        // How far the plate reads out past the track on each side. Enough to be a rim, not a slab.
        const float PlateInset = -9f;

        const string PlateName = "Track Plate";
        const string StepButtonName = "Step Button";
        const string CaptionName = "Step Hint";

        [MenuItem("Museum/Egrang/Style Run HUD")]
        public static void Style()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Egrang", "No scene is open.", "OK");
                return;
            }

            SkillCheckBar bar = FindOne<SkillCheckBar>();
            if (bar == null)
            {
                EditorUtility.DisplayDialog("Egrang", $"No {nameof(SkillCheckBar)} in the scene.", "OK");
                return;
            }

            var report = new StringBuilder();

            Canvas canvas = bar.GetComponentInParent<Canvas>(true);
            if (canvas != null) StyleCanvas(canvas, report);

            RectTransform track = StyleTrack(bar, report);
            StyleCursor(bar, report);
            if (track != null) EnsurePlate(track, report);
            StyleStepButton(bar, report);
            EnsureCaption(bar, report);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Egrang run HUD styled in '{scene.name}':\n{report}Save the scene to keep it.", bar);
        }

        /// <summary>
        /// ui-style.md §1. Screen Space – Camera on the scene camera and Match 0.5, matching the
        /// selection panel's canvas: two canvases on different match modes drift apart from each other
        /// as the window changes shape, which is exactly what a player sees when the panel closes.
        /// </summary>
        static void StyleCanvas(Canvas canvas, StringBuilder report)
        {
            Undo.RecordObject(canvas, "Style Egrang run HUD");

            Camera camera = Camera.main;
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                Undo.RecordObject(scaler, "Style Egrang run HUD");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }

            report.AppendLine(camera != null
                ? $"- canvas '{canvas.name}': Screen Space – Camera on '{camera.name}', 800×600, Match 0.5."
                : $"- canvas '{canvas.name}': no MainCamera found, left in {canvas.renderMode}; scaler set to 800×600, Match 0.5.");
        }

        /// <summary>
        /// Sizes the track and parks it above the bottom edge. The bar bakes its own red/yellow/green
        /// strip into this image on Awake, so no sprite is assigned here — one would only be overwritten.
        /// </summary>
        static RectTransform StyleTrack(SkillCheckBar bar, StringBuilder report)
        {
            RectTransform track = TrackOf(bar);
            if (track == null)
            {
                report.AppendLine("- track: the bar has no track assigned; skipped.");
                return null;
            }

            Undo.RecordObject(track, "Style Egrang run HUD");
            AnchorBottom(track);
            track.anchoredPosition = new Vector2(0f, TrackHeight);
            track.sizeDelta = TrackSize;

            var image = track.GetComponent<Image>();
            if (image != null)
            {
                Undo.RecordObject(image, "Style Egrang run HUD");
                image.color = Color.white;
                image.type = Image.Type.Simple;
            }

            report.AppendLine($"- track: {TrackSize.x}×{TrackSize.y} at bottom-centre +{TrackHeight}.");
            return track;
        }

        /// <summary>
        /// A cream slider, narrow enough that the band under its tip is unambiguous. The old 24-wide
        /// block covered more of the track than the hardest stick's whole green band, so the player
        /// could not see what they were about to score.
        /// </summary>
        static void StyleCursor(SkillCheckBar bar, StringBuilder report)
        {
            RectTransform cursor = CursorOf(bar);
            if (cursor == null)
            {
                report.AppendLine("- cursor: the bar has no cursor assigned; skipped.");
                return;
            }

            Undo.RecordObject(cursor, "Style Egrang run HUD");
            // Centred anchor and pivot: the bar places it by anchoredPosition about the track's middle,
            // so an edge anchor would sit it a half-track off.
            cursor.anchorMin = new Vector2(0.5f, 0.5f);
            cursor.anchorMax = new Vector2(0.5f, 0.5f);
            cursor.pivot = new Vector2(0.5f, 0.5f);
            cursor.sizeDelta = CursorSize;

            var image = cursor.GetComponent<Image>();
            if (image != null)
            {
                Undo.RecordObject(image, "Style Egrang run HUD");
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Cream;
                image.raycastTarget = false;
            }

            report.AppendLine($"- cursor: cream, {CursorSize.x}×{CursorSize.y}.");
        }

        /// <summary>
        /// Puts an opaque plate behind the track. Over a live 3D scene the strip alone has no edge —
        /// pale terrain runs straight up to the red band — and ui-style.md §6b is explicit that content
        /// over gameplay is opaque rather than translucent. It is a sibling drawn before the track
        /// rather than a child, because children always draw over their parent's own graphic.
        /// </summary>
        static void EnsurePlate(RectTransform track, StringBuilder report)
        {
            Transform parent = track.parent;
            if (parent == null)
            {
                report.AppendLine("- plate: the track is a canvas root, so there is nowhere to put a plate behind it.");
                return;
            }

            Transform existing = parent.Find(PlateName);
            bool created = existing == null;

            GameObject plate;
            if (created)
            {
                plate = new GameObject(PlateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(plate, "Style Egrang run HUD");
                plate.layer = track.gameObject.layer;
                plate.transform.SetParent(parent, false);
            }
            else
            {
                plate = existing.gameObject;
                Undo.RecordObject(plate.transform, "Style Egrang run HUD");
            }

            var rect = plate.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Style Egrang run HUD");
            AnchorBottom(rect);
            rect.anchoredPosition = new Vector2(0f, TrackHeight + PlateInset);
            rect.sizeDelta = TrackSize - new Vector2(PlateInset, PlateInset) * 2f;

            var image = plate.GetComponent<Image>();
            Undo.RecordObject(image, "Style Egrang run HUD");
            image.sprite = LoadSprite(CornerFrameSpritePath);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = ChipPixelsPerUnitMultiplier;
            image.color = Color.white;
            image.raycastTarget = false;

            // Drawn before the track, so the strip sits on it rather than under it.
            plate.transform.SetSiblingIndex(track.GetSiblingIndex());

            report.AppendLine(created
                ? $"- plate: created '{PlateName}' behind the track."
                : $"- plate: '{PlateName}' re-fitted behind the track.");
        }

        /// <summary>
        /// The step button, per ui-style.md §6 and §7: the shared frame sprite at the button rung of the
        /// radius ladder, the stock colour tint, and a Roboto-Bold label that does not eat clicks.
        /// Centred under the track, because a button off in the corner is not obviously the thing the
        /// track is asking you to press.
        /// </summary>
        static void StyleStepButton(SkillCheckBar bar, StringBuilder report)
        {
            // Scoped to the bar's own run root, never the whole canvas. The canvas also carries the
            // stick cards and the MULAI button, and this method renames and relabels everything it is
            // given — pointed at the canvas it turns all four into copies of the step button, which is
            // exactly how the scene came to hold three cards titled "JALAN" named "Step Button".
            Transform runRoot = bar.transform.parent != null ? bar.transform.parent : bar.transform;

            Button[] buttons = FindStepButton(runRoot);

            if (buttons.Length == 0)
            {
                report.AppendLine($"- step button: none under '{runRoot.name}'; skipped.");
                return;
            }

            foreach (Button button in buttons)
            {
                Undo.RecordObject(button, "Style Egrang run HUD");

                var rect = button.GetComponent<RectTransform>();
                Undo.RecordObject(rect, "Style Egrang run HUD");
                AnchorBottom(rect);
                rect.anchoredPosition = new Vector2(0f, ButtonHeight);
                rect.sizeDelta = ButtonSize;

                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    Undo.RecordObject(image, "Style Egrang run HUD");
                    image.sprite = LoadSprite(FrameSpritePath);
                    image.type = Image.Type.Sliced;
                    image.pixelsPerUnitMultiplier = ButtonPixelsPerUnitMultiplier;
                    image.color = Color.white;
                    button.targetGraphic = image;
                }

                // ui-style.md §7: the stock ColorBlock, unchanged. Uniform feedback is the point.
                button.transition = Selectable.Transition.ColorTint;
                button.colors = new ColorBlock
                {
                    normalColor = Color.white,
                    highlightedColor = new Color(0.961f, 0.961f, 0.961f, 1f),
                    pressedColor = new Color(0.784f, 0.784f, 0.784f, 1f),
                    selectedColor = new Color(0.961f, 0.961f, 0.961f, 1f),
                    disabledColor = new Color(0.784f, 0.784f, 0.784f, 0.502f),
                    colorMultiplier = 1f,
                    fadeDuration = 0.1f,
                };

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    Undo.RecordObject(label, "Style Egrang run HUD");
                    label.text = "JALAN";
                    StyleText(label, BoldFont, 16f, Color.white);
                    label.alignment = TextAlignmentOptions.Center;

                    var labelRect = label.rectTransform;
                    Undo.RecordObject(labelRect, "Style Egrang run HUD");
                    Stretch(labelRect);
                }

                if (button.name != StepButtonName)
                {
                    Undo.RecordObject(button.gameObject, "Style Egrang run HUD");
                    button.name = StepButtonName;
                }
            }

            report.AppendLine($"- step button: '{string.Join(", ", System.Array.ConvertAll(buttons, b => b.name))}' " +
                              $"framed, stock tint, JALAN in Roboto-Bold 16, centred under the track.");
        }

        /// <summary>
        /// The one button this styler may touch, looked up under the bar's run root: the one already
        /// named "Step Button", or the only button there if it has yet to be named. Ambiguity returns
        /// nothing rather than a guess — restyling the wrong button renames it too, and the damage is
        /// silent until someone opens the scene.
        /// </summary>
        static Button[] FindStepButton(Transform runRoot)
        {
            var buttons = runRoot.GetComponentsInChildren<Button>(true);

            foreach (Button button in buttons)
            {
                if (button.name == StepButtonName) return new[] { button };
            }

            return buttons.Length == 1 ? buttons : new Button[0];
        }

        /// <summary>
        /// One caption line under the button saying what to press. The bar teaches its own rules by
        /// colour, but nothing on screen says a press is what scores them — and a museum visitor at a
        /// kiosk has no reason to guess at the space bar.
        /// </summary>
        static void EnsureCaption(SkillCheckBar bar, StringBuilder report)
        {
            RectTransform track = TrackOf(bar);
            Transform parent = track != null ? track.parent : bar.transform;
            if (parent == null)
            {
                report.AppendLine("- caption: nowhere to parent it; skipped.");
                return;
            }

            Transform existing = parent.Find(CaptionName);
            bool created = existing == null;

            GameObject caption;
            if (created)
            {
                caption = new GameObject(CaptionName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(caption, "Style Egrang run HUD");
                caption.layer = bar.gameObject.layer;
                caption.transform.SetParent(parent, false);
                caption.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                caption = existing.gameObject;
            }

            var rect = caption.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Style Egrang run HUD");
            AnchorBottom(rect);
            rect.anchoredPosition = new Vector2(0f, CaptionHeight);
            rect.sizeDelta = new Vector2(420f, 18f);

            var text = caption.GetComponent<TMP_Text>();
            Undo.RecordObject(text, "Style Egrang run HUD");
            text.text = "Tekan SPASI saat penanda berada di zona hijau";
            StyleText(text, RegularFont, 12f, Tan);
            text.alignment = TextAlignmentOptions.Center;

            report.AppendLine(created ? $"- caption: created '{CaptionName}'." : $"- caption: '{CaptionName}' re-fitted.");
        }

        // ---- helpers ---------------------------------------------------------------------------

        static RectTransform TrackOf(SkillCheckBar bar) => FieldRect(bar, "track");

        static RectTransform CursorOf(SkillCheckBar bar) => FieldRect(bar, "cursor");

        static RectTransform FieldRect(SkillCheckBar bar, string field)
        {
            var so = new SerializedObject(bar);
            return so.FindProperty(field).objectReferenceValue as RectTransform;
        }

        /// <summary>
        /// Bottom-centre anchor with the pivot on the bottom edge, so the element's distance off the
        /// bottom of the screen is the number in <c>anchoredPosition.y</c> and nothing else.
        /// </summary>
        static void AnchorBottom(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// ui-style.md §4: the font asset is assigned explicitly and auto-sizing stays off. TMP's project
        /// default is still LiberationSans, so a label nobody assigned a font to is a label in the wrong
        /// typeface — which is exactly what this button was.
        /// </summary>
        static void StyleText(TMP_Text text, string fontPath, float size, Color color)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font != null) text.font = font;
            else Debug.LogWarning($"Missing font '{fontPath}'; '{text.name}' keeps its current typeface.", text);

            text.enableAutoSizing = false;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
        }

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

        static T FindOne<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }
    }
}
