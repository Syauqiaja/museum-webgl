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
    /// Builds everything that puts a name on a racer, plus the countdown that opens the race:
    ///
    /// - the countdown strip on the stick-selection panel ("MULAI DALAM 12") with the room's roster
    ///   under it, so a player picking a stilt can see who they are racing and how long they have;
    /// - the HUD roster on the run canvas — one named row per lane with a progress bar, which is the
    ///   "am I winning" readout the single-lane progress strip cannot give;
    /// - a name plate floating over each lane's stilt-walker.
    ///
    /// Then it wires all three into the scene's <see cref="EgrangRace"/>, which is the part that is
    /// invisible when it is missing: the views exist, and nothing ever fills them in.
    ///
    /// Re-runnable, and deliberately silent about it: it destroys and rebuilds only the objects it
    /// owns (named below), so hand edits to *those* are lost while the rest of the panel, the HUD and
    /// the lanes are left alone.
    /// </summary>
    public static class EgrangRaceNamesUIBuilder
    {
        const string CountdownName = "Countdown";
        const string RosterName = "Roster";
        const string PlateName = "Name Plate";
        const int Lanes = 3;

        [MenuItem("Museum/Egrang/Build Race Names UI")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Egrang", "No scene is open.", "OK");
                return;
            }

            var race = Object.FindFirstObjectByType<EgrangRace>(FindObjectsInactive.Include);
            var selector = Object.FindFirstObjectByType<EgrangStickSelector>(FindObjectsInactive.Include);
            SkillCheckBar bar = Object.FindFirstObjectByType<SkillCheckBar>(FindObjectsInactive.Include);
            Canvas runCanvas = bar != null ? bar.GetComponentInParent<Canvas>(true) : null;

            if (race == null || selector == null || runCanvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Egrang names",
                    $"Need the race, the selection panel and the run canvas in the scene. Found race: " +
                    $"{race != null}, selector: {selector != null}, run canvas: {runCanvas != null}. " +
                    "Run 'Build Stick Selection UI' first.",
                    "OK");
                return;
            }

            EgrangCountdownView countdown = BuildCountdown(selector.transform);
            EgrangRosterView roster = BuildRoster(runCanvas.transform);
            EgrangNameplate[] plates = BuildNameplates();

            var so = new SerializedObject(race);
            so.FindProperty("countdownView").objectReferenceValue = countdown;
            so.FindProperty("rosterView").objectReferenceValue = roster;
            FillArray(so.FindProperty("nameplates"), plates);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = countdown.gameObject;
            EditorGUIUtility.PingObject(countdown.gameObject);
            Debug.Log($"Built the Egrang countdown, HUD roster and {plates.Length} name plate(s), wired to " +
                      $"'{race.name}'. Save the scene to keep it.", race);
        }

        // ---- Countdown strip on the selection panel ----------------------------------------------

        static EgrangCountdownView BuildCountdown(Transform panel)
        {
            Replace(panel, CountdownName);

            GameObject root = CreateUI(CountdownName, panel, typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();

            // A plate, not just text. The countdown and the roster sit over the village at whatever
            // brightness the sun happens to be, and gold-on-grass is the one line on this screen a
            // player has to read at a glance.
            var plate = root.GetComponent<Image>();
            plate.color = new Color(0.055f, 0.043f, 0.031f, 0.78f);
            plate.raycastTarget = false;
            // Under the title/subtitle, above the cards: the two things a waiting player needs are the
            // time left and who else is here, and both belong at the top of the screen they are on.
            Anchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            // Flush under the heading's plate (EgrangStickSelectionUIBuilder puts that at -86, 70
            // deep) and the same width, so the heading and the clock read as one band.
            rect.anchoredPosition = new Vector2(0f, -156f);
            rect.sizeDelta = new Vector2(780f, 62f);

            TMP_Text countdownText = AddLabel(root.transform, "Countdown Text", "MULAI DALAM 15",
                                              BoldFont, 24f, Gold,
                                              new Vector2(0f, -12f), new Vector2(420f, 28f),
                                              new Vector2(0.5f, 1f), FontStyles.Bold);

            GameObject rosterRoot = CreateUI("Racers", root.transform);
            RectTransform rosterRect = rosterRoot.GetComponent<RectTransform>();
            Anchor(rosterRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            rosterRect.anchoredPosition = new Vector2(0f, -32f);
            rosterRect.sizeDelta = new Vector2(520f, 22f);

            // A row of names side by side rather than a column: three names fit across, and a column
            // here would push the cards down the screen.
            var layout = rosterRoot.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var rows = new List<TMP_Text>(Lanes);
            for (int i = 0; i < Lanes; i++)
            {
                TMP_Text row = AddColumnLabel(rosterRoot.transform, $"Racer {i + 1}", RegularFont, 13f, Cream, 20f);
                row.text = $"Pemain {i + 1}";
                rows.Add(row);
            }

            var view = Undo.AddComponent<EgrangCountdownView>(root);
            var so = new SerializedObject(view);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("countdownText").objectReferenceValue = countdownText;
            so.FindProperty("rosterRoot").objectReferenceValue = rosterRoot;
            FillArray(so.FindProperty("rosterRows"), rows.ToArray());
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        // ---- HUD roster on the run canvas --------------------------------------------------------

        static EgrangRosterView BuildRoster(Transform runCanvas)
        {
            Replace(runCanvas, RosterName);

            GameObject root = CreateUI(RosterName, runCanvas, typeof(Image));
            RectTransform rect = root.GetComponent<RectTransform>();
            // Top left, clear of the progress strip in the top centre and the bar along the bottom.
            Anchor(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(190f, 78f);
            SetFrame(root.GetComponent<Image>(), CornerFrameSprite(), Color.white, ChipPixelsPerUnitMultiplier);

            GameObject content = CreateUI("Content", root.transform);
            Stretch(content.GetComponent<RectTransform>(), 8f);
            var column = content.AddComponent<VerticalLayoutGroup>();
            column.spacing = 4f;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var view = Undo.AddComponent<EgrangRosterView>(root);
            var so = new SerializedObject(view);
            SerializedProperty rows = so.FindProperty("rows");
            rows.arraySize = Lanes;

            for (int lane = 0; lane < Lanes; lane++)
            {
                GameObject row = CreateUI($"Lane {lane + 1}", content.transform);
                var element = row.AddComponent<LayoutElement>();
                element.preferredHeight = 18f;
                element.minHeight = 18f;

                TMP_Text name = AddLabel(row.transform, "Name", $"Pemain {lane + 1}", RegularFont, 11f, Cream,
                                         new Vector2(0f, 4f), new Vector2(170f, 12f), new Vector2(0f, 0.5f));
                name.alignment = TextAlignmentOptions.MidlineLeft;
                Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                name.rectTransform.anchoredPosition = new Vector2(0f, 4f);

                GameObject rail = CreateUI("Rail", row.transform, typeof(Image));
                RectTransform railRect = rail.GetComponent<RectTransform>();
                Anchor(railRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                railRect.anchoredPosition = new Vector2(0f, -7f);
                railRect.sizeDelta = new Vector2(170f, 3f);
                var railImage = rail.GetComponent<Image>();
                railImage.color = InnerFrame;
                railImage.raycastTarget = false;

                // Left-anchored, like the big progress strip's fill: only the width changes as that
                // lane advances, so a lane at 0 % is an empty bar rather than a centred sliver.
                GameObject fill = CreateUI("Fill", rail.transform, typeof(Image));
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                Anchor(fillRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
                fillRect.anchoredPosition = Vector2.zero;
                // Empty until the race moves it: the view measures the rail behind it for full width.
                fillRect.sizeDelta = new Vector2(0f, 3f);
                var fillImage = fill.GetComponent<Image>();
                fillImage.color = lane == 0 ? Gold : Tan;
                fillImage.raycastTarget = false;

                SerializedProperty entry = rows.GetArrayElementAtIndex(lane);
                entry.FindPropertyRelative("nameText").objectReferenceValue = name;
                entry.FindPropertyRelative("fill").objectReferenceValue = fillRect;
                entry.FindPropertyRelative("root").objectReferenceValue = row;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ---- Name plates over the racers ---------------------------------------------------------

        static EgrangNameplate[] BuildNameplates()
        {
            var racers = new List<EgrangRacer>(
                Object.FindObjectsByType<EgrangRacer>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            // Lane order, not scene order: the array this fills is indexed by lane.
            racers.Sort((a, b) => a.Lane.CompareTo(b.Lane));

            var plates = new EgrangNameplate[racers.Count];

            for (int i = 0; i < racers.Count; i++)
            {
                plates[i] = BuildNameplate(racers[i]);
            }

            return plates;
        }

        static EgrangNameplate BuildNameplate(EgrangRacer racer)
        {
            // Parented to the mover rather than the lane root, so the plate travels with the walker
            // instead of hanging over the starting line all race.
            var mover = racer.GetComponentInChildren<EgrangStepMover>(true);
            Transform parent = mover != null ? mover.transform : racer.transform;

            Replace(parent, PlateName);

            GameObject root = new GameObject(PlateName, typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(root, UndoLabel);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 2.4f, 0f);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // 1 unit of UI = 1 cm of world, so the 200×40 rect below is a 2 m × 0.4 m sign — readable
            // from the camera's chase distance without dwarfing the racer.
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 40f);
            rect.localScale = Vector3.one * 0.01f;

            GameObject textObject = CreateUI("Text (TMP)", root.transform, typeof(TextMeshProUGUI));
            Stretch(textObject.GetComponent<RectTransform>());
            var text = textObject.GetComponent<TextMeshProUGUI>();
            StyleText(text, BoldFont, 24f, Cream);
            text.alignment = TextAlignmentOptions.Center;
            text.text = string.Empty;

            var plate = Undo.AddComponent<EgrangNameplate>(root);
            var so = new SerializedObject(plate);
            so.FindProperty("label").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            return plate;
        }

        static void Replace(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
        }
    }
}
