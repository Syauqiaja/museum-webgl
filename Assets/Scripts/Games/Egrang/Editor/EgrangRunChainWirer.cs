using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Connects the run chain in the open scene: input and the step button into the timing bar, and
    /// the bar's timing to the length of a stride.
    ///
    /// It deliberately does *not* connect the bar's result to a racer any more. Which racer a press
    /// moves is a seating question the server answers, so <see cref="EgrangRace"/> owns that link at
    /// runtime; see <see cref="ClearStepResultListeners"/>.
    ///
    /// <see cref="EgrangStickSelectionUIBuilder"/> stops at the selection panel — and when it adopts a
    /// bar that was already hand-built in the scene, as it did here, it deliberately touches nothing
    /// about that bar. What is left is the part that is invisible in the inspector until you press
    /// Space and nothing happens: a <c>UnityEvent</c> with no listeners, an empty action asset field,
    /// and a lockout too short for the step it is meant to cover.
    ///
    /// Re-runnable. Listeners it owns are matched by target and replaced rather than appended, so
    /// running it twice does not step twice per press.
    /// </summary>
    public static class EgrangRunChainWirer
    {
        const string InputAssetPath = "Assets/Scripts/Games/Egrang/EgrangInput.inputactions";
        const string ActionMapName = "Egrang";
        const string StepActionName = "Step";

        [MenuItem("Museum/Egrang/Wire Run Chain")]
        public static void Wire()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Egrang", "No scene is open.", "OK");
                return;
            }

            SkillCheckBar bar = FindOne<SkillCheckBar>();
            EgrangStepMover mover = FindOne<EgrangStepMover>();

            if (bar == null || mover == null)
            {
                EditorUtility.DisplayDialog(
                    "Egrang",
                    $"Need both a {nameof(SkillCheckBar)} and an {nameof(EgrangStepMover)} in the scene. " +
                    $"Found bar: {bar != null}, mover: {mover != null}.",
                    "OK");
                return;
            }

            var report = new StringBuilder();

            WireInput(bar, report);
            ClearStepResultListeners(bar, report);
            WireStepButtons(bar, report);
            MatchLockoutToStep(bar, mover, report);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Egrang run chain wired in '{scene.name}':\n{report}Save the scene to keep it.", bar);
        }

        /// <summary>
        /// Points the bar at the Step action. Without this the bar sweeps but never reads a press, which
        /// looks exactly like a broken bar rather than a missing binding.
        /// </summary>
        static void WireInput(SkillCheckBar bar, StringBuilder report)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputAssetPath);
            if (asset == null)
            {
                report.AppendLine($"- input: '{InputAssetPath}' is missing, so the bar stays button-only.");
                return;
            }

            if (asset.FindActionMap(ActionMapName)?.FindAction(StepActionName) == null)
            {
                report.AppendLine($"- input: '{InputAssetPath}' has no '{ActionMapName}/{StepActionName}' action; left unassigned.");
                return;
            }

            var so = new SerializedObject(bar);
            so.FindProperty("inputAsset").objectReferenceValue = asset;
            so.FindProperty("actionMapName").stringValue = ActionMapName;
            so.FindProperty("stepActionName").stringValue = StepActionName;
            so.ApplyModifiedProperties();

            report.AppendLine($"- input: bar reads {ActionMapName}/{StepActionName} (Space, gamepad South).");
        }

        /// <summary>
        /// Clears the bar's step-result listeners. It does not add one — and that is the fix, not an
        /// omission.
        ///
        /// This used to wire the bar straight to a mover, which was right when the scene had one
        /// racer and no server. With three lanes it is a routing bug: the listener is a *fixed*
        /// mover, so every client's press walked whichever lane the wirer happened to pick
        /// (<see cref="FindOne{T}"/> takes the first of the three), regardless of which seat that
        /// client actually holds. Two players pressing Space walked the same legs, and neither
        /// walked their own. It also bypassed <see cref="EgrangRacer.ApplyStep"/>, so the strides it
        /// played were never banked and every server echo had to snap the lane back.
        ///
        /// The live wiring is <see cref="EgrangRace"/>'s: it subscribes in Awake and routes each
        /// press to <c>LocalRacer</c> — the lane the server actually seated this client in. A
        /// persistent listener here would fire *in addition* to that one, so the honest thing for
        /// this tool to do is take stale ones out and leave the event empty.
        /// </summary>
        static void ClearStepResultListeners(SkillCheckBar bar, StringBuilder report)
        {
            int removed = 0;
            for (int i = bar.OnStepResult.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                Object listener = bar.OnStepResult.GetPersistentTarget(i);
                if (listener is EgrangStepMover || listener is EgrangRace)
                {
                    UnityEventTools.RemovePersistentListener(bar.OnStepResult, i);
                    removed++;
                }
            }

            report.AppendLine(removed > 0
                ? $"- step result: removed {removed} hard-wired listener(s); EgrangRace routes presses to the local lane at runtime."
                : "- step result: nothing hard-wired; EgrangRace routes presses to the local lane at runtime.");
        }

        /// <summary>
        /// Wires the buttons under the bar's run root to <see cref="SkillCheckBar.Press"/> — the touch
        /// path the museum kiosk needs, since a visitor at a screen has no keyboard.
        ///
        /// Scoped to the run root, not to the canvas. Scoping by canvas was correct while the bar had
        /// a canvas of its own, but the generated UI puts the run root and the selection panel under
        /// one canvas — so "every button living with the bar" grew to mean the stick cards, MULAI and
        /// the results Exit as well. Clicking any of them called Press() on a bar that was still
        /// switched off, whose Awake had therefore never built its cursor.
        ///
        /// Any Press listener left on a button outside the run root is stale by definition, so the
        /// whole canvas is swept for them first; otherwise re-running could never undo the damage.
        /// </summary>
        static void WireStepButtons(SkillCheckBar bar, StringBuilder report)
        {
            Transform runRoot = bar.transform.parent != null ? bar.transform.parent : bar.transform;
            Canvas canvas = bar.GetComponentInParent<Canvas>(true);
            int cleared = 0;

            if (canvas != null)
            {
                foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
                {
                    if (button.transform.IsChildOf(runRoot)) continue;
                    cleared += RemoveListenersTargeting(button.onClick, bar);
                }
            }

            var wired = new List<string>();
            foreach (Button button in runRoot.GetComponentsInChildren<Button>(true))
            {
                RemoveListenersTargeting(button.onClick, bar);
                UnityEventTools.AddVoidPersistentListener(button.onClick, bar.Press);
                wired.Add(button.name);
            }

            report.AppendLine(wired.Count == 0
                ? $"- step button: none found under '{runRoot.name}'; keyboard only."
                : $"- step button: {string.Join(", ", wired)} → Press().");

            if (cleared > 0)
            {
                report.AppendLine($"- step button: cleared {cleared} stray Press() listener(s) outside the run root.");
            }
        }

        /// <summary>
        /// Sizes the bar's lockout to the longest step the mover plays. The lockout is the only thing
        /// stopping a second press from interrupting a step in flight, so a lockout shorter than a full
        /// step lets the player cancel their own stride halfway and lose the ground it was carrying.
        /// </summary>
        static void MatchLockoutToStep(SkillCheckBar bar, EgrangStepMover mover, StringBuilder report)
        {
            float required = mover.FullStepSeconds;

            var so = new SerializedObject(bar);
            SerializedProperty lockout = so.FindProperty("lockoutSeconds");
            float current = lockout.floatValue;

            if (current >= required - 0.001f)
            {
                report.AppendLine($"- lockout: {current:0.00}s already covers a full step ({required:0.00}s).");
                return;
            }

            lockout.floatValue = required;
            so.ApplyModifiedProperties();
            report.AppendLine($"- lockout: {current:0.00}s → {required:0.00}s, the length of a full step.");
        }

        /// <summary>
        /// Drops the persistent listeners pointing at <paramref name="target"/>, so re-running replaces
        /// its own wiring instead of stacking a second copy of it. Anything aimed elsewhere is another
        /// author's and is left in place.
        /// </summary>
        static int RemoveListenersTargeting(UnityEventBase unityEvent, Object target)
        {
            int removed = 0;
            for (int i = unityEvent.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                Object listener = unityEvent.GetPersistentTarget(i);
                if (listener != target && !(listener is Component component && component.gameObject == AsGameObject(target)))
                {
                    continue;
                }

                UnityEventTools.RemovePersistentListener(unityEvent, i);
                removed++;
            }
            return removed;
        }

        static GameObject AsGameObject(Object target) =>
            target is Component component ? component.gameObject : target as GameObject;

        static T FindOne<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (found.Length > 1)
            {
                Debug.LogWarning($"Scene holds {found.Length} {typeof(T).Name}s; wiring the first, '{found[0].name}'.", found[0]);
            }

            return found.Length > 0 ? found[0] : null;
        }
    }
}
