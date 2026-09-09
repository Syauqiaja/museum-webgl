using System.Collections.Generic;
using Museum.Games.Egrang;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Builds the Egrang results panel into the open scene: the placing line, the run's numbers, the
    /// lane standings and the way out to the museum.
    ///
    /// A generator for the same reason <see cref="EgrangStickSelectionUIBuilder"/> is one — the
    /// tedious, silently-wrong-when-missed part is the wiring, not the layout: eight labels onto
    /// <see cref="EgrangResultsView"/> and the view itself onto the scene's <see cref="EgrangRace"/>.
    /// A panel that is built but not wired to the race looks exactly like the bug this is fixing.
    ///
    /// Every number and colour comes from <c>Assets/Docs/ui-style.md</c> §6b: scrim first, opaque
    /// content frame over it, one tween each.
    ///
    /// Re-runnable — running it again replaces the whole panel, so hand edits to it are lost.
    /// </summary>
    public static class EgrangResultsUIBuilder
    {
        const string PanelName = "Results Panel";
        const string CanvasName = "Egrang UI";
        const int StandingRows = 3;

        [MenuItem("Museum/Egrang/Build Results UI")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Egrang", "No scene is open.", "OK");
                return;
            }

            Canvas canvas = FindCanvas(scene);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Egrang results",
                    $"No canvas to build into. Run 'Museum/Egrang/Build Stick Selection UI' first — it " +
                    $"creates the '{CanvasName}' canvas this panel lives on.",
                    "OK");
                return;
            }

            Transform existing = canvas.transform.Find(PanelName);
            if (existing != null &&
                !EditorUtility.DisplayDialog(
                    "Egrang results",
                    $"'{PanelName}' already exists under '{canvas.name}'. Replace it? Any hand edits to it are lost.",
                    "Replace", "Cancel"))
            {
                return;
            }

            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            EgrangResultsView view = BuildPanel(canvas.transform);
            string wiring = WireToRace(view);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = view.gameObject;
            EditorGUIUtility.PingObject(view.gameObject);
            Debug.Log($"Built '{PanelName}' in scene '{scene.name}'. {wiring} Save the scene to keep it.",
                      view.gameObject);
        }

        /// <summary>
        /// The canvas the Egrang UI lives on: the generated one by name, or any canvas in the scene.
        /// Falling back keeps the item usable in a scene whose canvas was renamed by hand rather than
        /// silently building a second one that renders over the first.
        /// </summary>
        static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != CanvasName) continue;

                var named = root.GetComponent<Canvas>();
                if (named != null) return named;
            }

            return Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        static EgrangResultsView BuildPanel(Transform parent)
        {
            // ui-style.md §6b: the scrim is a full-screen Image at 63% black, and it — not the content
            // frame — is what dims the race still visible underneath.
            GameObject panel = CreateUI(PanelName, parent, typeof(Image));
            Stretch(panel.GetComponent<RectTransform>());
            var scrim = panel.GetComponent<Image>();
            scrim.color = Scrim;
            // The panel covers a live race: clicks must stop here rather than reaching the Step button
            // that is still sitting under it.
            scrim.raycastTarget = true;

            panel.AddComponent<CanvasGroup>();
            Component fade = AddTween(panel, "FadeTween");
            if (fade != null)
            {
                // Same reason as the selection panel: FadeTween ships tuned to stop at 60% for a
                // backdrop, and the content lives inside this group.
                var fadeObject = new SerializedObject(fade);
                fadeObject.FindProperty("toAlpha").floatValue = 1f;
                fadeObject.ApplyModifiedPropertiesWithoutUndo();
            }

            TMP_Text title = AddLabel(panel.transform, "Title", "SELESAI!", DisplayFont, 56f, Cream,
                                      new Vector2(0f, -64f), new Vector2(700f, 64f), new Vector2(0.5f, 1f),
                                      FontStyles.Bold);

            GameObject card = BuildCard(panel.transform);
            Transform content = card.transform.Find("Content");

            TMP_Text place = AddColumnLabel(content, "Place", BoldFont, 20f, Gold, 28f);
            AddDivider(content);

            TMP_Text time = AddSpecRow(content, "Waktu", "Waktu", BoldFont, Cream, 70f);
            TMP_Text steps = AddSpecRow(content, "Langkah", "Langkah", captionWidth: 70f);
            TMP_Text fails = AddSpecRow(content, "Gagal", "Gagal", captionWidth: 70f);
            TMP_Text stick = AddSpecRow(content, "Egrang", "Egrang", captionWidth: 70f);

            GameObject standings = BuildStandings(content, out TMP_Text[] rows);

            GameObject exitButton = CreateButton("Exit Button", panel.transform, "KEMBALI KE MUSEUM", 260f, 44f, 18f);
            RectTransform exitRect = exitButton.GetComponent<RectTransform>();
            Anchor(exitRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            exitRect.anchoredPosition = new Vector2(0f, 40f);

            var view = Undo.AddComponent<EgrangResultsView>(panel);
            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("placeText").objectReferenceValue = place;
            so.FindProperty("timeText").objectReferenceValue = time;
            so.FindProperty("stepsText").objectReferenceValue = steps;
            so.FindProperty("failsText").objectReferenceValue = fails;
            so.FindProperty("stickText").objectReferenceValue = stick;
            so.FindProperty("standingsRoot").objectReferenceValue = standings;
            FillArray(so.FindProperty("standingRows"), rows);
            so.FindProperty("exitButton").objectReferenceValue = exitButton.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Author-time preview so the scene view is not a stack of blank rows. The view overwrites
            // every one of these from the summary the race hands it.
            place.text = "Juara 1 dari 3";
            time.text = EgrangRunSummary.NoTime;
            steps.text = "0 penuh / 0 setengah";
            fails.text = "0";
            stick.text = "Egrang Persegi (Sisi 8 cm)";

            // Down until the race ends. The view enforces this on Awake too, so a panel left visible
            // by a hand edit still cannot cover the run.
            panel.SetActive(false);

            return view;
        }

        /// <summary>The opaque frame the numbers sit in, laid out as one column.</summary>
        static GameObject BuildCard(Transform parent)
        {
            GameObject card = CreateUI("Card", parent, typeof(Image));
            RectTransform rect = card.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(360f, 300f);

            // ui-style.md §6b: white, i.e. the frame sprite as drawn. Translucent over a live 3D scene
            // is what makes body copy unreadable.
            SetFrame(card.GetComponent<Image>(), FrameSprite(), Color.white, ContainerPixelsPerUnitMultiplier);

            card.AddComponent<CanvasGroup>();
            AddTween(card, "PopupTween");

            GameObject content = CreateUI("Content", card.transform);
            Stretch(content.GetComponent<RectTransform>());
            var column = content.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(20, 20, 18, 18);
            column.spacing = 6f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            return card;
        }

        /// <summary>
        /// The lane table. Built with a fixed row per lane rather than an instantiated prefab: three
        /// lanes is the whole race, and a prefab would put the panel's only runtime allocation in the
        /// frame the race ends on.
        /// </summary>
        static GameObject BuildStandings(Transform parent, out TMP_Text[] rows)
        {
            GameObject root = CreateUI("Standings", parent);
            var column = root.AddComponent<VerticalLayoutGroup>();
            column.spacing = 2f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            AddDivider(root.transform);

            var built = new List<TMP_Text>(StandingRows);
            for (int i = 0; i < StandingRows; i++)
            {
                TMP_Text row = AddColumnLabel(root.transform, $"Standing {i + 1}", RegularFont, 13f, Tan, 20f);
                row.text = $"Lane {i + 1} — juara {i + 1}";
                built.Add(row);
            }

            rows = built.ToArray();
            return root;
        }

        /// <summary>
        /// Hands the panel to the race in the scene. Without this the panel exists and nothing ever
        /// shows it, which is indistinguishable from not having built it at all.
        /// </summary>
        static string WireToRace(EgrangResultsView view)
        {
            var race = Object.FindFirstObjectByType<EgrangRace>(FindObjectsInactive.Include);
            if (race == null)
            {
                return $"No {nameof(EgrangRace)} in the scene, so nothing will show it yet — " +
                       "assign it to the race's Results View field once there is one.";
            }

            var so = new SerializedObject(race);
            so.FindProperty("resultsView").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();

            return $"Wired to '{race.name}'.";
        }
    }
}
