using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Builds the way out of a race that is still running: one button in the top-right corner
    /// of the HUD wired to <see cref="EgrangRace.BackToMuseum"/>.
    ///
    /// The results panel already has an exit, but it only exists once the race is over. A player
    /// who wants to stop mid-run — or who is stuck in a lobby-less race after a failed reconnect —
    /// had no way off the track short of the browser's back button, which leaves the room holding
    /// their seat through the reconnection window.
    ///
    /// Same shape as <see cref="EgrangResultsUIBuilder"/>: the tedious, silently-wrong-when-missed
    /// part is the persistent listener, not the layout. A button with no listener is
    /// indistinguishable from a working one in the scene view.
    ///
    /// Re-runnable — running it again replaces the button, so hand edits to it are lost.
    /// </summary>
    public static class EgrangExitUIBuilder
    {
        const string ButtonName = "Exit Button";
        const string CanvasName = "Egrang UI";

        // Top-right, clear of the roster (top-left) and the progress strip (top-centre), and the
        // same 16 px inset the other HUD chips keep from the edge.
        static readonly Vector2 Inset = new Vector2(-16f, -16f);
        static readonly Vector2 Size = new Vector2(120f, 36f);

        [MenuItem("Museum/Egrang/Build Exit Button")]
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
                    "Egrang exit",
                    $"No canvas to build into. Run 'Museum/Egrang/Build Stick Selection UI' first — it " +
                    $"creates the '{CanvasName}' canvas this button lives on.",
                    "OK");
                return;
            }

            var race = Object.FindFirstObjectByType<EgrangRace>(FindObjectsInactive.Include);
            if (race == null)
            {
                EditorUtility.DisplayDialog("Egrang exit", $"No {nameof(EgrangRace)} in the scene to wire the button to.", "OK");
                return;
            }

            Transform existing = canvas.transform.Find(ButtonName);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            GameObject button = CreateButton(ButtonName, canvas.transform, "KELUAR", Size.x, Size.y, 16f);
            RectTransform rect = button.GetComponent<RectTransform>();
            Anchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            rect.anchoredPosition = Inset;

            // Always on top of the run HUD, and — as a sibling of the panels rather than a child of
            // the run root — reachable during the stilt pick too, which is the other moment a
            // player changes their mind.
            button.transform.SetAsLastSibling();

            var click = button.GetComponent<Button>();
            UnityEventTools.AddPersistentListener(click.onClick, race.BackToMuseum);
            EditorUtility.SetDirty(click);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = button;
            Debug.Log($"Built '{ButtonName}' in scene '{scene.name}', wired to {nameof(EgrangRace)}.{nameof(EgrangRace.BackToMuseum)}. " +
                      "Save the scene to keep it.", button);
        }

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
    }
}
