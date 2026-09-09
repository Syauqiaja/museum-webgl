using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// <c>Assets/Docs/ui-style.md</c> expressed as code: the palette, the font assets, the nine-slice
    /// ladder and the small builders that apply them. Every Egrang UI generator goes through here, so
    /// two generated screens cannot quietly disagree about what a button or a caption is.
    ///
    /// Meant to be pulled in with <c>using static</c>, which is why the members read as bare verbs at
    /// the call site.
    /// </summary>
    public static class EgrangUIStyle
    {
        /// <summary>ui-style.md §1. Every screen is authored at 800×600; the sizes elsewhere only mean anything against this.</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(800f, 600f);

        // ui-style.md §5. Nothing here invents a colour.
        public static readonly Color Gold = new Color(0.757f, 0.624f, 0.380f, 1f);
        public static readonly Color Cream = new Color(0.957f, 0.918f, 0.835f, 1f);
        public static readonly Color Tan = new Color(0.847f, 0.753f, 0.631f, 1f);
        public static readonly Color Scrim = new Color(0f, 0f, 0f, 0.631f);
        public static readonly Color ContainerFrame = new Color(0.594f, 0.594f, 0.594f, 0.353f);
        public static readonly Color InnerFrame = new Color(1f, 1f, 1f, 0.263f);

        // ui-style.md §6, the corner-radius ladder. Frames are tinted, never recoloured as new sprites.
        public const float ContainerPixelsPerUnitMultiplier = 1.97f;
        public const float ButtonPixelsPerUnitMultiplier = 3.13f;
        public const float ChipPixelsPerUnitMultiplier = 5.05f;

        public const string DisplayFont = "Assets/Fonts/CG-Regular SDF.asset";
        public const string BoldFont = "Assets/Fonts/Roboto-Bold SDF.asset";
        public const string SemiBoldFont = "Assets/Fonts/Roboto-SemiBold SDF.asset";
        public const string RegularFont = "Assets/Fonts/Roboto-Regular SDF.asset";

        public const string UndoLabel = "Build Egrang UI";

        public static GameObject CreateUI(string name, Transform parent, params Type[] components)
        {
            // RectTransform first and by itself: Image and the rest require one, so listing them in the
            // constructor would have Unity add it for us and the explicit add would then fail.
            var go = new GameObject(name, typeof(RectTransform));
            foreach (Type type in components) go.AddComponent(type);

            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject CreateButton(string name, Transform parent, string label, float width,
                                              float height, float fontSize)
        {
            GameObject go = CreateUI(name, parent, typeof(Image), typeof(Button));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);

            var image = go.GetComponent<Image>();
            SetFrame(image, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
            // SetFrame defaults frames to decorative; this one is the click target.
            image.raycastTarget = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ApplyStockTint(button);

            // ui-style.md §2: the single label inside a button is named "Text (TMP)".
            GameObject text = CreateUI("Text (TMP)", go.transform, typeof(TextMeshProUGUI));
            Stretch(text.GetComponent<RectTransform>());
            var tmp = text.GetComponent<TextMeshProUGUI>();
            StyleText(tmp, BoldFont, fontSize, Gold);
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }

        public static TMP_Text AddLabel(Transform parent, string name, string text, string fontPath, float size,
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

        public static TMP_Text AddColumnLabel(Transform parent, string name, string fontPath, float size,
                                              Color color, float height)
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
        public static void StyleText(TMP_Text text, string fontPath, float size, Color color)
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
        public static void SetFrame(Image image, Sprite sprite, Color tint, float pixelsPerUnitMultiplier)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = tint;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.raycastTarget = false;
        }

        /// <summary>ui-style.md §7: the stock ColorBlock, unchanged, on every button.</summary>
        public static void ApplyStockTint(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.colors = ColorBlock.defaultColorBlock;
        }

        /// <summary>
        /// Adds one of the shared tween components by name. They live in the default assembly (LeanTween
        /// ships without an asmdef, so the tweens cannot sit in one either), and an asmdef cannot
        /// reference the default assembly — so the type is fetched by name rather than compiled against.
        /// </summary>
        public static Component AddTween(GameObject go, string typeName)
        {
            Type type = Type.GetType($"{typeName}, Assembly-CSharp");

            if (type == null)
            {
                Debug.LogWarning($"Could not find '{typeName}' in Assembly-CSharp; '{go.name}' is built " +
                                 "without its tween. Add the component by hand.", go);
                return null;
            }

            return Undo.AddComponent(go, type);
        }

        /// <summary>The plain nine-sliced wooden panel used for cards and buttons.</summary>
        public static Sprite FrameSprite() => LoadSprite("Assets/Sprites/frame_default.png");

        /// <summary>The same panel with lit corner studs — HUD chips and the selected card.</summary>
        public static Sprite CornerFrameSprite() => LoadSprite("Assets/Sprites/frame_with_corner.png");

        public static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogWarning($"Missing sprite '{path}'; falling back to the built-in UI sprite.");
                return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }

            return sprite;
        }

        public static void FillArray(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
