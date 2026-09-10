using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Where each of the eighteen exhibit screens is, which way it faces and where its visitors
    /// stand — read off the scene rather than typed in, so the frame, signage and decor
    /// generators all agree with the video and with each other.
    /// </summary>
    /// <remarks>
    /// Every screen is a world-space Canvas of 2.65 × 1.64 m hung with <c>forward</c> pointing
    /// INTO the wall: the trigger volume that starts the video sits 4–7 m out along
    /// <c>-forward</c>, and a ray along <c>+forward</c> hits plaster where there is any. So
    /// <see cref="ScreenFrame.Out"/> is <c>-forward</c> and is the direction a visitor looks
    /// from. The floor comes from the trigger volume's collider, which covers the ground the
    /// visitor actually stands on; the screens themselves hang 2.3–2.5 m above it.
    ///
    /// Editor-only on purpose: nothing at runtime needs these numbers, and the transforms of the
    /// meshes around here lie (offsets baked into vertices — see MuseumLightingBuilder), so
    /// anything derived from them has to be checked in the editor before it ships.
    /// </remarks>
    public static class MuseumScreenGeometry
    {
        public const string ScenePath = "Assets/Scenes/Museum.unity";

        /// <summary>Walkable floor heights, storey by storey. Mirrors MuseumLightingBuilder.</summary>
        public static readonly float[] FloorLevels = { 1.60f, 7.70f, 12.70f, 17.70f };

        /// <summary>One exhibit screen, resolved to world space.</summary>
        public struct ScreenFrame
        {
            /// <summary>The `Vid &lt;Game&gt;` group the screen lives under — the key everything else joins on.</summary>
            public string GroupName;
            public GameObject Group;
            public StreamedVideoScreen Screen;
            public Transform Canvas;

            /// <summary>Canvas centre, world space.</summary>
            public Vector3 Centre;

            /// <summary>Direction a visitor looks from, i.e. away from the wall (<c>-canvas.forward</c>).</summary>
            public Vector3 Out;

            /// <summary>The canvas's own right, world space — along the wall.</summary>
            public Vector3 Right;
            public Vector3 Up;

            /// <summary>Canvas size in metres.</summary>
            public float Width;
            public float Height;

            /// <summary>Y of the floor the visitor stands on to watch this screen.</summary>
            public float FloorY;

            /// <summary>The trigger volume that starts the video; null when it could not be found.</summary>
            public GameObject Trigger;

            /// <summary>Into the wall.</summary>
            public Vector3 Into => -Out;

            /// <summary>Rotation that makes a +Z-forward object face the visitor.</summary>
            public Quaternion FacingRoom => Quaternion.LookRotation(Into, Up);

            /// <summary>A point on the floor, <paramref name="distance"/> metres out from the wall under the screen centre.</summary>
            public Vector3 FloorPoint(float distance, float lateral = 0f)
            {
                Vector3 p = Centre + Out * distance + Right * lateral;
                return new Vector3(p.x, FloorY, p.z);
            }
        }

        /// <summary>Resolves every screen in the given scene, in no particular order.</summary>
        public static List<ScreenFrame> All(Scene scene)
        {
            var result = new List<ScreenFrame>();

            foreach (StreamedVideoScreen screen in Object.FindObjectsByType<StreamedVideoScreen>(FindObjectsSortMode.None))
            {
                if (screen.gameObject.scene != scene) continue;
                if (TryGet(screen, out ScreenFrame frame)) result.Add(frame);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.GroupName, b.GroupName));
            return result;
        }

        /// <summary>Resolves one screen. False when it has no Canvas — nothing can be hung around it.</summary>
        public static bool TryGet(StreamedVideoScreen screen, out ScreenFrame frame)
        {
            frame = default;

            var canvas = screen.GetComponentInParent<Canvas>();
            if (canvas == null) return false;

            Transform ct = canvas.transform;
            var rect = (RectTransform)ct;

            GameObject group = GroupOf(screen.transform);
            GameObject trigger = FindTriggerFor(screen);

            frame = new ScreenFrame
            {
                GroupName = group != null ? group.name : screen.transform.parent.name,
                Group = group,
                Screen = screen,
                Canvas = ct,
                Centre = ct.position,
                Out = -ct.forward,
                Right = ct.right,
                Up = ct.up,
                Width = rect.sizeDelta.x * ct.lossyScale.x,
                Height = rect.sizeDelta.y * ct.lossyScale.y,
                FloorY = FloorUnder(trigger, ct.position.y),
                Trigger = trigger,
            };

            return true;
        }

        /// <summary>
        /// The nearest `Vid *` ancestor. The hierarchy is `Vid LTn / Vid Game / Canvas / Video
        /// Player`, and "Vid Game" is what the lesson content and signage tables key on.
        /// </summary>
        private static GameObject GroupOf(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                if (p.name.StartsWith("Vid ") && !p.name.StartsWith("Vid LT")) return p.gameObject;
            }

            return null;
        }

        /// <summary>
        /// Floor under the trigger volume, else the storey level just below the screen. A screen
        /// hangs 2.3–2.5 m above its floor, so "the highest floor below the centre" is unambiguous.
        /// </summary>
        private static float FloorUnder(GameObject trigger, float screenY)
        {
            var collider = trigger == null ? null : trigger.GetComponent<Collider>();
            if (collider != null) return collider.bounds.min.y;

            float best = FloorLevels[0];
            foreach (float level in FloorLevels)
            {
                if (level < screenY) best = level;
            }

            return best;
        }

        /// <summary>
        /// The VideoTriggerPlayer driving this screen, by its own `screen` reference — the volumes
        /// are all called "Cube" and several sit within a few metres on the storeys above and below.
        /// </summary>
        public static GameObject FindTriggerFor(StreamedVideoScreen screen)
        {
            foreach (VideoTriggerPlayer trigger in Object.FindObjectsByType<VideoTriggerPlayer>(FindObjectsSortMode.None))
            {
                if (trigger.gameObject.scene != screen.gameObject.scene) continue;

                var serialized = new SerializedObject(trigger);
                if (serialized.FindProperty("screen").objectReferenceValue == screen) return trigger.gameObject;
            }

            return null;
        }

        /// <summary>
        /// The Museum scene as it stands in the editor, reopened only when it is not the active
        /// one. Never reopens an open scene: that would discard unsaved hand placement.
        /// </summary>
        public static Scene OpenMuseum()
        {
            Scene active = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (active.IsValid() && active.path == ScenePath) return active;

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;
            return UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        /// <summary>A scene-root container by name, created (and registered for undo) if missing.</summary>
        public static Transform EnsureRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, MuseumUIStyle.UndoLabel);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        /// <summary>Destroys the scene-root container by name, if any. Returns whether one was there.</summary>
        public static bool ClearRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != name) continue;
                Undo.DestroyObjectImmediate(root);
                return true;
            }

            return false;
        }
    }
}
