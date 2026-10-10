using Museum.Core;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Restyles the MainMenu screen to ui-style.md and re-wires its two interactions.
    ///
    /// The scene's RectTransforms are native and survived the asset loss intact, so this adopts the
    /// existing hierarchy rather than emitting a rival one (ui-style.md §9): every object below is
    /// looked up by path and only its Image/TMP/Button data — the managed half that was lost — is
    /// written back. Re-running it on an edited scene is safe.
    /// </summary>
    public static class MainMenuUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        // ui-style.md §6. The menu's own skins, distinct from the shared frame ladder.
        private const string BackgroundSprite = "Assets/Texture2D/Main Menu BG.png";
        private const string ButtonFaceSprite = "Assets/Texture2D/Rectangle 2.png";
        private const string ButtonGlowSprite = "Assets/Texture2D/Rectangle 3.png";

        // The avatar row. Portraits are named after the id they stand for: "Jawa.png" is "jawa".
        private const string AvatarPickerName = "Avatar Picker";
        private const string AvatarSpriteFolder = "Assets/Sprites/Char Avatars/";
        private const float AvatarPickerY = -112f;     // centre of the row, under the name field (-42..-2)
        private const float PortraitSize = 72f;
        private const float PortraitSpacing = 88f;
        private const float PortraitInset = 5f;        // portrait art inside the button frame
        private const float SelectedFrameOutset = 5f;  // gold frame outside it

        /// <summary>
        /// "Mulai" moves down to make room for the avatar row. The one RectTransform this builder
        /// writes on an adopted object — ui-style.md §9 otherwise leaves them to the scene — and
        /// written deliberately: the row has no room between the field and the button otherwise.
        /// </summary>
        private const float PlayButtonY = -200f;   // clear of the backdrop's "16 Permainan" line below

        [MenuItem("Museum/Rebuild UI/Main Menu")]
        public static void Rebuild()
        {
            // This generator is meant to be re-run often, and it both opens scenes (Single mode,
            // which discards whatever was active with no prompt) and saves them. Either action can
            // destroy a developer's unsaved work — the currently active scene if it isn't
            // MainMenu.unity, or unrelated edits already sitting in MainMenu.unity if it is. Refuse
            // to touch anything until the active scene is clean.
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                Debug.LogError($"MainMenuUIBuilder: '{active.name}' has unsaved changes — " +
                                "save or discard them before rebuilding Main Menu.");
                return;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManagerOpen();
            Transform canvas = FindRoot(scene, "Canvas");

            if (canvas == null)
            {
                Debug.LogError("MainMenuUIBuilder: no 'Canvas' in MainMenu.unity — nothing to restyle.");
                return;
            }

            StyleBackground(canvas);
            // Before the picker: it collects the objects it covers by name, the row among them.
            StyleAvatarPicker(canvas, scene);
            StylePlatformPicker(canvas, scene);
            StyleTitle(canvas);
            StyleNameInput(canvas, scene);
            StylePlayButton(canvas, scene);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("MainMenuUIBuilder: MainMenu restyled and saved.");
        }

        private static UnityEngine.SceneManagement.Scene EditorSceneManagerOpen()
        {
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            return active.path == ScenePath
                ? active
                : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                      ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        /// <summary>Full-bleed art, Simple, untinted (ui-style.md §6).</summary>
        private static void StyleBackground(Transform canvas)
        {
            var image = Find<Image>(canvas, "BG");
            if (image == null) return;

            image.sprite = LoadSprite(BackgroundSprite);
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
        }

        /// <summary>Eyebrow, wordmark and tagline — the three rows of ui-style.md §4's display stack.</summary>
        private static void StyleTitle(Transform canvas)
        {
            var eyebrow = Find<TextMeshProUGUI>(canvas, "Title/Text (TMP)");
            if (eyebrow != null)
            {
                StyleText(eyebrow, SemiBoldFont, 14f, Gold);
                eyebrow.text = "MUSEUM";
                eyebrow.characterSpacing = 14f;
                eyebrow.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(eyebrow);
            }

            var wordmark = Find<TextMeshProUGUI>(canvas, "Title/Text (TMP) (1)");
            if (wordmark != null)
            {
                StyleText(wordmark, DisplayFont, 56f, Cream);
                wordmark.text = "Wiraga";
                wordmark.fontStyle = FontStyles.Bold;
                wordmark.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(wordmark);
            }

            var tagline = Find<TextMeshProUGUI>(canvas, "Title/Text (TMP) (2)");
            if (tagline != null)
            {
                StyleText(tagline, RegularFont, 14f, Tan);
                tagline.text = "Jelajari permainan tradisional Indonesia";
                tagline.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(tagline);
            }
        }

        /// <summary>
        /// The nickname field: dark frame, white typed text, tan placeholder, and a live write into
        /// <see cref="SessionData.PlayerName"/> so the name is stored as it is typed rather than on a
        /// submit the player may never give (the Play button leaves the scene immediately).
        /// </summary>
        private static void StyleNameInput(Transform canvas, UnityEngine.SceneManagement.Scene scene)
        {
            var frame = Find<Image>(canvas, "Name Input");
            if (frame != null)
            {
                SetFrame(frame, FrameSprite(), Color.white, ButtonPixelsPerUnitMultiplier);
                // The field is the click target; SetFrame defaults frames to decorative.
                frame.raycastTarget = true;
                EditorUtility.SetDirty(frame);
            }

            var text = Find<TextMeshProUGUI>(canvas, "Name Input/Text Area/Text");
            if (text != null)
            {
                StyleText(text, BoldFont, 14f, Color.white);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                EditorUtility.SetDirty(text);
            }

            var placeholder = Find<TextMeshProUGUI>(canvas, "Name Input/Text Area/Placeholder");
            if (placeholder != null)
            {
                StyleText(placeholder, RegularFont, 14f, Tan);
                placeholder.text = "Nama kamu";
                placeholder.alignment = TextAlignmentOptions.MidlineLeft;
                EditorUtility.SetDirty(placeholder);
            }

            var field = Find<TMP_InputField>(canvas, "Name Input");
            SessionData session = FindSession(scene);

            if (field == null || session == null) return;

            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = frame;
            field.characterLimit = PlayerNameRules.MaxLength;
            field.text = session.PlayerName;

            WireNameWrite(field, session);
            EditorUtility.SetDirty(field);

            // MainMenu pre-fills the field on the way back from a game; it needs the reference.
            MainMenu menu = FindMenu(scene);
            if (menu != null)
            {
                var serialized = new SerializedObject(menu);
                serialized.FindProperty("nameInput").objectReferenceValue = field;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(menu);
            }
        }

        /// <summary>
        /// Points <c>onValueChanged</c> at the <see cref="SessionData.PlayerName"/> setter.
        ///
        /// Written through SerializedProperty rather than <see cref="UnityEventTools"/> because the
        /// target is a property setter, which has no <c>UnityAction</c> to hand it. Existing calls are
        /// cleared first so re-running cannot stack duplicates.
        /// </summary>
        private static void WireNameWrite(TMP_InputField field, SessionData session)
        {
            var serialized = new SerializedObject(field);
            SerializedProperty calls =
                serialized.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls");

            if (calls == null) return;

            calls.arraySize = 1;
            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = session;
            call.FindPropertyRelative("m_MethodName").stringValue = "set_PlayerName";
            // EventDefined: take the string the event itself carries.
            call.FindPropertyRelative("m_Mode").enumValueIndex = 0;
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly

            SerializedProperty targetType = call.FindPropertyRelative("m_TargetAssemblyTypeName");
            if (targetType != null) targetType.stringValue = typeof(SessionData).AssemblyQualifiedName;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The composed button of ui-style.md §2: pulsing glow, gold face, stretched label.</summary>
        private static void StylePlayButton(Transform canvas, UnityEngine.SceneManagement.Scene scene)
        {
            var glow = Find<Image>(canvas, "Play Button/Glow");
            if (glow != null)
            {
                SetFrame(glow, LoadSprite(ButtonGlowSprite), Color.white, 1f);
                EditorUtility.SetDirty(glow);
                StyleGlowTween(glow.gameObject);
            }

            var face = Find<Image>(canvas, "Play Button/BG");
            if (face != null)
            {
                SetFrame(face, LoadSprite(ButtonFaceSprite), Color.white, ButtonPixelsPerUnitMultiplier);
                face.raycastTarget = true;
                EditorUtility.SetDirty(face);
            }

            var label = Find<TextMeshProUGUI>(canvas, "Play Button/Text (TMP)");
            if (label != null)
            {
                StyleText(label, BoldFont, 20f, Color.white);
                label.text = "Mulai";
                label.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(label);
            }

            var button = Find<Button>(canvas, "Play Button");
            if (button == null) return;

            // Composed-button shape: the target graphic is the child face, not the button's own object.
            button.targetGraphic = face;
            ApplyStockTint(button);

            MainMenu menu = FindMenu(scene);
            if (menu != null)
            {
                for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }

                UnityEventTools.AddPersistentListener(button.onClick, menu.GoToMuseum);
            }

            EditorUtility.SetDirty(button);
        }

        /// <summary>
        /// Builds the platform picker as a sibling drawn over the menu, and points MainMenu at both
        /// it and the objects it covers. Re-runnable: an existing panel is replaced.
        /// </summary>
        private static void StylePlatformPicker(Transform canvas, UnityEngine.SceneManagement.Scene scene)
        {
            Transform existing = canvas.Find("Platform Panel");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject panel = CreateUI("Platform Panel", canvas, typeof(Image));
            Stretch(panel.GetComponent<RectTransform>());
            var scrim = panel.GetComponent<Image>();
            scrim.sprite = null;
            scrim.color = Scrim;
            scrim.raycastTarget = true;
            panel.transform.SetAsLastSibling();

            AddLabel(panel.transform, "Heading", "Kamu main pakai apa?", DisplayFont, 32f, Cream,
                     new Vector2(0f, 90f), new Vector2(600f, 60f), new Vector2(0.5f, 0.5f));

            GameObject touch = CreateButton("Touch Button", panel.transform, "HP / Layar Sentuh", 260f, 64f, 18f);
            RectTransform touchRect = touch.GetComponent<RectTransform>();
            Anchor(touchRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            touchRect.anchoredPosition = new Vector2(-150f, 0f);

            GameObject desktop = CreateButton("Desktop Button", panel.transform, "Komputer", 260f, 64f, 18f);
            RectTransform desktopRect = desktop.GetComponent<RectTransform>();
            Anchor(desktopRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            desktopRect.anchoredPosition = new Vector2(150f, 0f);

            GameObject touchHint = HintTag(touch.transform);
            GameObject desktopHint = HintTag(desktop.transform);

            MainMenu menu = FindMenu(scene);
            if (menu == null)
            {
                Debug.LogWarning("MainMenuUIBuilder: no MainMenu component — the picker is built but unwired.");
                return;
            }

            UnityEventTools.AddPersistentListener(touch.GetComponent<Button>().onClick, menu.ChooseTouch);
            UnityEventTools.AddPersistentListener(desktop.GetComponent<Button>().onClick, menu.ChooseDesktop);

            var serialized = new SerializedObject(menu);
            serialized.FindProperty("platformPanel").objectReferenceValue = panel;
            serialized.FindProperty("touchHint").objectReferenceValue = touchHint;
            serialized.FindProperty("desktopHint").objectReferenceValue = desktopHint;

            // Everything the picker covers. BG stays on: it is the screen's backdrop, not menu chrome.
            var covered = new System.Collections.Generic.List<Object>();
            foreach (string name in new[] { "Title", "Name Input", AvatarPickerName, "Play Button" })
            {
                Transform found = canvas.Find(name);
                if (found != null) covered.Add(found.gameObject);
                else Debug.LogWarning($"MainMenuUIBuilder: no '{name}' under Canvas to hide behind the picker.");
            }
            FillArray(serialized.FindProperty("menuObjects"), covered.ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(menu);
        }

        /// <summary>
        /// The "Pilih karakter" row between the name field and "Mulai": one portrait button per
        /// <see cref="PlayerAvatars.Ids"/>, a gold corner frame on the chosen one (the Egrang stilt
        /// cards' selected state, ui-style.md §6), and the character's name under each. Each button
        /// calls <see cref="MainMenu.SelectAvatar"/> with its index. Re-runnable: an existing row is
        /// replaced.
        /// </summary>
        private static void StyleAvatarPicker(Transform canvas, UnityEngine.SceneManagement.Scene scene)
        {
            Transform existing = canvas.Find(AvatarPickerName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject row = CreateUI(AvatarPickerName, canvas);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            Anchor(rowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rowRect.sizeDelta = new Vector2(PortraitSpacing * PlayerAvatars.Ids.Count, 130f);
            rowRect.anchoredPosition = new Vector2(0f, AvatarPickerY);

            // Keep it with the menu chrome, behind the platform picker drawn over everything.
            Transform play = canvas.Find("Play Button");
            if (play != null) row.transform.SetSiblingIndex(play.GetSiblingIndex());

            TMP_Text caption = AddLabel(row.transform, "Caption", "PILIH KARAKTER", SemiBoldFont, 12f, Gold,
                                        new Vector2(0f, 55f), new Vector2(360f, 20f), new Vector2(0.5f, 0.5f));
            caption.characterSpacing = 10f;

            MainMenu menu = FindMenu(scene);
            var frames = new Object[PlayerAvatars.Ids.Count];
            float left = -PortraitSpacing * (PlayerAvatars.Ids.Count - 1) * 0.5f;

            for (int i = 0; i < PlayerAvatars.Ids.Count; i++)
            {
                string display = PlayerAvatars.DisplayName(PlayerAvatars.Ids[i]);
                float x = left + PortraitSpacing * i;

                GameObject button = CreateButton("Avatar " + display, row.transform, string.Empty, PortraitSize, PortraitSize, 12f);
                RectTransform rect = button.GetComponent<RectTransform>();
                Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                rect.anchoredPosition = new Vector2(x, 6f);

                // An empty label is still the button's single text child (ui-style.md §2); nothing to read.
                Transform emptyLabel = button.transform.Find("Text (TMP)");
                if (emptyLabel != null) emptyLabel.gameObject.SetActive(false);

                GameObject portrait = CreateUI("Portrait", button.transform, typeof(Image));
                Stretch(portrait.GetComponent<RectTransform>(), PortraitInset);
                var art = portrait.GetComponent<Image>();
                art.sprite = LoadSprite(AvatarSpriteFolder + display + ".png");
                art.type = Image.Type.Simple;
                art.preserveAspect = true;
                art.raycastTarget = false;
                if (art.sprite == null) Debug.LogWarning($"MainMenuUIBuilder: no portrait at '{AvatarSpriteFolder}{display}.png'.");

                GameObject frame = CreateUI("Selected Frame", button.transform, typeof(Image));
                Stretch(frame.GetComponent<RectTransform>(), -SelectedFrameOutset);
                SetFrame(frame.GetComponent<Image>(), CornerFrameSprite(), Gold, ContainerPixelsPerUnitMultiplier);
                frame.transform.SetAsFirstSibling();
                frame.SetActive(PlayerAvatars.Ids[i] == PlayerAvatars.Default);
                frames[i] = frame;

                AddLabel(row.transform, "Name " + display, display, MediumFont, 12f, Tan,
                         new Vector2(x, -42f), new Vector2(PortraitSpacing, 18f), new Vector2(0.5f, 0.5f));

                if (menu != null)
                {
                    UnityEventTools.AddIntPersistentListener(button.GetComponent<Button>().onClick, menu.SelectAvatar, i);
                }
            }

            if (play != null)
            {
                ((RectTransform)play).anchoredPosition = new Vector2(((RectTransform)play).anchoredPosition.x, PlayButtonY);
                EditorUtility.SetDirty(play);
            }

            if (menu == null)
            {
                Debug.LogWarning("MainMenuUIBuilder: no MainMenu component — the avatar row is built but unwired.");
                return;
            }

            var serialized = new SerializedObject(menu);
            FillArray(serialized.FindProperty("avatarFrames"), frames);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);
        }

        /// <summary>The small "Disarankan" tag under a picker button. Toggled at runtime by MainMenu.</summary>
        private static GameObject HintTag(Transform button)
        {
            TMP_Text tag = AddLabel(button, "Disarankan", "Disarankan", MediumFont, 12f, Gold,
                                    new Vector2(0f, -46f), new Vector2(260f, 20f), new Vector2(0.5f, 0.5f));
            return tag.gameObject;
        }

        /// <summary>
        /// ui-style.md §8's attention pulse. Set through SerializedObject: the tween components live in
        /// Assembly-CSharp, which this asmdef cannot reference.
        /// </summary>
        private static void StyleGlowTween(GameObject go)
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != "LoopFadeTween") continue;

                var serialized = new SerializedObject(component);
                SetFloat(serialized, "fromAlpha", 0.7f);
                SetFloat(serialized, "toAlpha", 0.2f);
                SetFloat(serialized, "duration", 1f);
                SerializedProperty loops = serialized.FindProperty("loopCount");
                if (loops != null) loops.intValue = -1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null) property.floatValue = value;
        }

        private static Transform FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }

            return null;
        }

        private static T Find<T>(Transform canvas, string path) where T : Component
        {
            Transform found = canvas.Find(path);

            if (found == null)
            {
                Debug.LogWarning($"MainMenuUIBuilder: '{path}' not found under the Canvas; skipped.");
                return null;
            }

            var component = found.GetComponent<T>();

            if (component == null)
            {
                Debug.LogWarning($"MainMenuUIBuilder: '{path}' has no {typeof(T).Name}; skipped.");
            }

            return component;
        }

        private static SessionData FindSession(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<SessionData>(true);
                if (found != null) return found;
            }

            Debug.LogWarning("MainMenuUIBuilder: no SessionData in the scene; the name field is not wired.");
            return null;
        }

        private static MainMenu FindMenu(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<MainMenu>(true);
                if (found != null) return found;
            }

            Debug.LogWarning("MainMenuUIBuilder: no MainMenu component in the scene; Play is not wired.");
            return null;
        }
    }
}
