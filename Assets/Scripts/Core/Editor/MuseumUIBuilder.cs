using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Restyles the Museum scene's overlay HUD — the minimap, the controls legend, the exit button and
    /// the enter prompt — and re-points the references that went with the managed data: the minimap's
    /// render texture, every video screen's placeholder, and the loader's loading screen.
    ///
    /// Adopts the scene rather than regenerating it (ui-style.md §9); only the lost half is written.
    ///
    /// Two spellings below are the live build's own and are reproduced deliberately: the minimap is
    /// titled "PETA MUESEUM" there. The one place this does *not* follow the screenshot is the third
    /// key cap, which the build renders as a second "W" on a GameObject named "E" — a controls legend
    /// that lists W twice is a defect, not a style, so it is restored as "E".
    /// </summary>
    public static class MuseumUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Museum.unity";
        private const string MinimapTexture = "Assets/RenderTexture/Minimap.renderTexture";
        private const string PlaceholderSprite = "Assets/Texture2D/video-placeholder.png";
        private const string LoadingScreenPrefab = "Assets/GameObject/LoadingScreen.prefab";

        /// <summary>Left/right inset for a panel title, in the 800×600 design units of ui-style.md.</summary>
        private const float TitleInset = 20f;

        /// <summary>Gap between a key cap and the action it describes.</summary>
        private const float ActionInset = 4f;

        // Measured off the live build rather than taken from ui-style.md §4's table: that table's
        // sizes are the full-screen menu scale, and these panels are ~134×100 design units. The
        // build's "KONTROL" is 37.5 units wide and its "Bergerak" 33 — a third of the table's.
        private const float PanelTitleSize = 7f;
        private const float ActionSize = 8.5f;
        private const float KeyCapSize = 9f;

        [MenuItem("Museum/Rebuild UI/Museum")]
        public static void Rebuild()
        {
            UnityEngine.SceneManagement.Scene scene = OpenScene();
            Transform canvas = FindOverlayCanvas(scene);

            if (canvas == null)
            {
                Debug.LogError("MuseumUIBuilder: no screen-space Canvas in Museum.unity.");
                return;
            }

            StyleMinimap(canvas);
            StyleControls(canvas);
            StyleExit(canvas);
            StyleEnterPrompt(canvas);
            WireLooseReferences(scene);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("MuseumUIBuilder: Museum HUD restyled and saved.");
        }

        private static void StyleMinimap(Transform canvas)
        {
            StylePanel(canvas, "Mini Map");
            StyleEyebrow(canvas, "Mini Map/Text (TMP)", "PETA MUESEUM");

            // The mask's image is the stencil: it needs the panel's corner radius or the map squares off.
            var mask = Find<Image>(canvas, "Mini Map/Image");
            if (mask != null)
            {
                SetFrame(mask, FrameSprite(), Color.white, ContainerPixelsPerUnitMultiplier);
                EditorUtility.SetDirty(mask);
            }

            var map = Find<RawImage>(canvas, "Mini Map/Image/Image (1)");
            if (map == null) return;

            map.texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(MinimapTexture);
            map.color = Color.white;
            map.raycastTarget = false;

            if (map.texture == null)
            {
                Debug.LogWarning($"MuseumUIBuilder: missing '{MinimapTexture}'; the map stays blank.");
            }

            EditorUtility.SetDirty(map);
        }

        private static void StyleControls(Transform canvas)
        {
            StylePanel(canvas, "Tutorial");
            StyleEyebrow(canvas, "Tutorial/Text (TMP)", "KONTROL");

            StyleDivider(canvas, "Tutorial/Line");
            StyleDivider(canvas, "Tutorial/Line (1)");
            StyleDivider(canvas, "Tutorial/Line (2)");

            StyleKeyCap(canvas, "Tutorial/WASD/WASD/W", "W");
            StyleKeyCap(canvas, "Tutorial/WASD/WASD/A", "A");
            StyleKeyCap(canvas, "Tutorial/WASD/WASD/S", "S");
            StyleKeyCap(canvas, "Tutorial/WASD/WASD/D", "D");
            StyleAction(canvas, "Tutorial/WASD/Text (TMP) (1)", "Bergerak");

            StyleKeyCap(canvas, "Tutorial/Mouse/Mouse", "Mouse");
            StyleAction(canvas, "Tutorial/Mouse/Text (TMP) (2)", "Lihat sekeliling");

            // Named "E" in the scene; the shipped build's cap art reads "W". See the class summary.
            StyleKeyCap(canvas, "Tutorial/E", "E");
        }

        private static void StyleExit(Transform canvas)
        {
            var frame = Find<Image>(canvas, "Back Button");
            if (frame != null)
            {
                SetFrame(frame, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
                frame.raycastTarget = true;
                EditorUtility.SetDirty(frame);
            }

            var button = Find<Button>(canvas, "Back Button");
            if (button != null)
            {
                button.targetGraphic = frame;
                ApplyStockTint(button);
                EditorUtility.SetDirty(button);
            }

            var label = Find<TextMeshProUGUI>(canvas, "Back Button/Text (TMP)");
            if (label == null) return;

            StyleText(label, BoldFont, 13.5f, Gold);
            label.text = "KELUAR";
            label.characterSpacing = 8f;
            label.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(label);
        }

        private static void StyleEnterPrompt(Transform canvas)
        {
            var frame = Find<Image>(canvas, "Enter");
            if (frame != null)
            {
                SetFrame(frame, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
                EditorUtility.SetDirty(frame);
            }

            var key = Find<TextMeshProUGUI>(canvas, "Enter/Text (TMP)");
            if (key != null)
            {
                StyleText(key, BoldFont, 16f, Color.white);
                key.text = "ENTER";
                key.characterSpacing = 6f;
                key.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(key);
            }

            // The caption sits below the framed key, outside it — a caption, not a button label.
            var caption = Find<TextMeshProUGUI>(canvas, "Enter/Text (TMP) (1)");
            if (caption == null) return;

            StyleText(caption, RegularFont, 12f, Color.white);
            caption.text = "Untuk memulai permainan ini";
            caption.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(caption);
        }

        /// <summary>
        /// References outside the HUD that the same data loss emptied: the video screens' fallback
        /// image and the loader's transition screen. Both have exactly one candidate asset.
        /// </summary>
        private static void WireLooseReferences(UnityEngine.SceneManagement.Scene scene)
        {
            // StreamedVideoScreen.placeholder is a Texture, not a Sprite. Assigning a Sprite to it
            // fails silently — SerializedProperty drops a value of the wrong type without error — so
            // the field has to be loaded as the type the script actually declares.
            var placeholder = AssetDatabase.LoadAssetAtPath<Texture>(PlaceholderSprite);
            int screens = 0;

            if (placeholder == null)
            {
                Debug.LogWarning($"MuseumUIBuilder: missing '{PlaceholderSprite}'.");
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;

                    // Matched by type name: the video screen lives in another assembly this one does
                    // not reference, and the field is reachable through SerializedObject regardless.
                    if (behaviour.GetType().Name == "StreamedVideoScreen" &&
                        SetReference(behaviour, "placeholder", placeholder))
                    {
                        screens++;
                    }
                }
            }

            Debug.Log($"MuseumUIBuilder: placeholder set on {screens} video screens.");
        }

        private static bool SetReference(Object target, string field, Object value)
        {
            if (value == null) return false;

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null) return false;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        // ---------------------------------------------------------------- pieces

        private static void StylePanel(Transform canvas, string path)
        {
            var frame = Find<Image>(canvas, path);
            if (frame == null) return;

            SetFrame(frame, CornerFrameSprite(), Color.white, ContainerPixelsPerUnitMultiplier);
            EditorUtility.SetDirty(frame);
        }

        private static void StyleEyebrow(Transform canvas, string path, string text)
        {
            var label = Find<TextMeshProUGUI>(canvas, path);
            if (label == null) return;

            StyleText(label, SemiBoldFont, PanelTitleSize, Gold);
            label.text = text;
            label.characterSpacing = 8f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            // The label's rect is the full panel width, so left-aligned copy would start on the frame
            // border. The margin is the inset, not a smaller rect — the rect itself is original.
            label.margin = new Vector4(TitleInset, 0f, TitleInset, 0f);
            EditorUtility.SetDirty(label);
        }

        private static void StyleDivider(Transform canvas, string path)
        {
            var line = Find<Image>(canvas, path);
            if (line == null) return;

            line.sprite = null;
            line.type = Image.Type.Simple;
            line.color = InnerFrame;
            line.raycastTarget = false;
            EditorUtility.SetDirty(line);
        }

        private static void StyleKeyCap(Transform canvas, string path, string cap)
        {
            var frame = Find<Image>(canvas, path);
            if (frame != null)
            {
                SetFrame(frame, FrameSprite(), Color.white, ChipPixelsPerUnitMultiplier);
                EditorUtility.SetDirty(frame);
            }

            var label = Find<TextMeshProUGUI>(canvas, $"{path}/Text (TMP)");
            if (label == null) return;

            StyleText(label, BoldFont, KeyCapSize, Color.white);
            label.text = cap;
            label.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(label);
        }

        private static void StyleAction(Transform canvas, string path, string text)
        {
            var label = Find<TextMeshProUGUI>(canvas, path);
            if (label == null) return;

            StyleText(label, RegularFont, ActionSize, Color.white);
            label.text = text;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            // Keeps the description off the key cap it sits beside.
            label.margin = new Vector4(ActionInset, 0f, 0f, 0f);
            EditorUtility.SetDirty(label);
        }

        // ---------------------------------------------------------------- lookup

        private static UnityEngine.SceneManagement.Scene OpenScene()
        {
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            return active.path == ScenePath
                ? active
                : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                      ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        /// <summary>The one overlay canvas; the museum's other eighteen are world-space video screens.</summary>
        private static Transform FindOverlayCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode != RenderMode.WorldSpace) return canvas.transform;
                }
            }

            return null;
        }

        private static T Find<T>(Transform canvas, string path) where T : Component
        {
            Transform found = canvas.Find(path);

            if (found == null)
            {
                Debug.LogWarning($"MuseumUIBuilder: '{path}' not found under the Canvas; skipped.");
                return null;
            }

            var component = found.GetComponent<T>();

            if (component == null)
            {
                Debug.LogWarning($"MuseumUIBuilder: '{path}' has no {typeof(T).Name}; skipped.");
            }

            return component;
        }
    }
}
