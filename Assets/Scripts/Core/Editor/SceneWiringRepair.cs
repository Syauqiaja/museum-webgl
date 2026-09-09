using UnityEditor;
using UnityEngine;
using static Museum.Core.EditorTools.MuseumUIStyle;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Re-points the serialized references that are not part of any one screen's styling: the
    /// bootstrap singletons every scene carries, the museum's player rig, and the doorways that send
    /// the visitor into a game.
    ///
    /// These were missed by the per-scene builders for two reasons worth recording. The bootstrap
    /// object exists in all five scenes, not just the one a builder owns — wiring it in the Museum
    /// builder left MainMenu's copy null, which is what made the first Play press throw. And the
    /// player scripts live in the default assembly rather than a <c>Museum.*</c> one, so an audit
    /// filtered by namespace never looked at them.
    ///
    /// Values that are content rather than plumbing come from the deployed server: dakon seats two,
    /// egrang three. Engklek has no room server-side and is deliberately left unwired.
    /// </summary>
    public static class SceneWiringRepair
    {
        private const string LoadingScreenPrefab = "Assets/GameObject/LoadingScreen.prefab";
        private const string ServerConfigAsset = "Assets/Resources/ServerConfig.asset";
        private const string PlayerInputAsset = "Assets/Scripts/Player/FPSInput.inputactions";
        private const string MuseumScene = "Assets/Scenes/Museum.unity";

        /// <summary>Above every screen it covers; the scenes' own canvases sit at 0.</summary>
        private const int LoadingScreenSortingOrder = 100;

        /// <summary>The menu background's own near-black brown, sampled from the live build.</summary>
        private static readonly Color LoadingBackdrop = new Color(0.122f, 0.063f, 0.027f, 1f);

        private static readonly string[] Scenes =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Lobby.unity",
            "Assets/Scenes/Museum.unity",
            "Assets/Scenes/Dakon.unity",
            "Assets/Scenes/Egrang.unity",
        };

        /// <summary>A doorway's destination, keyed by the object's path in the Museum scene.</summary>
        private struct Doorway
        {
            public string Path;
            public string RoomName;
            public string DisplayName;
            public string Scene;
            public int MaxPlayers;
        }

        private static readonly Doorway[] Doorways =
        {
            Door("Vid LT1/Vid Dakon/Dakon Doorway", "dakon", "Dakon", SceneReference.Dakon, 2),
            Door("Vid LT1/Egrang Doorway", "egrang", "Egrang", SceneReference.Egrang, 3),
            Door("Vid LT2/Vid Egrang/Cube", "egrang", "Egrang", SceneReference.Egrang, 3),
        };

        private static Doorway Door(string path, string room, string display, string scene, int seats) =>
            new Doorway { Path = path, RoomName = room, DisplayName = display, Scene = scene, MaxPlayers = seats };

        [MenuItem("Museum/Rebuild UI/Wire Scene References")]
        public static void Repair()
        {
            var loadingScreen = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingScreenPrefab);
            var serverConfig = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ServerConfigAsset);

            if (loadingScreen == null) Debug.LogWarning($"SceneWiringRepair: missing '{LoadingScreenPrefab}'.");
            if (serverConfig == null) Debug.LogWarning($"SceneWiringRepair: missing '{ServerConfigAsset}'.");

            StyleLoadingScreen(loadingScreen);

            foreach (string scenePath in Scenes)
            {
                UnityEngine.SceneManagement.Scene scene =
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

                WireBootstrap(scene, loadingScreen, serverConfig);

                if (scenePath == MuseumScene)
                {
                    WirePlayer(scene);
                    WireDoorways(scene);
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("SceneWiringRepair: bootstrap, player and doorway references wired.");
        }

        /// <summary>
        /// The transition screen the loader instantiates. It came back from recovery as an untinted
        /// white full-screen Image with an empty label, so every scene change flashed white — the one
        /// moment in the app where a blank graphic is guaranteed to be seen.
        ///
        /// It draws over whatever is on screen while the next scene loads, so it needs a sorting order
        /// above the screens it covers and the same 800×600 scaler as everything else (ui-style.md §1);
        /// the recovered prefab had neither.
        /// </summary>
        private static void StyleLoadingScreen(GameObject prefab)
        {
            if (prefab == null) return;

            var canvas = prefab.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = LoadingScreenSortingOrder;
            }

            var scaler = prefab.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }

            Transform backdrop = prefab.transform.Find("Image");
            var image = backdrop != null ? backdrop.GetComponent<UnityEngine.UI.Image>() : null;

            if (image != null)
            {
                image.sprite = null;
                image.type = UnityEngine.UI.Image.Type.Simple;
                // Opaque, and the same near-black brown the menu background sits at, so a transition
                // reads as the app dimming rather than as a different screen appearing.
                image.color = LoadingBackdrop;
                image.raycastTarget = true;
            }

            var label = prefab.GetComponentInChildren<TMPro.TMP_Text>(true);

            if (label != null)
            {
                StyleText(label, RegularFont, 14f, Tan);
                label.text = "Memuat…";
                label.alignment = TMPro.TextAlignmentOptions.Center;
            }

            PrefabUtility.SavePrefabAsset(prefab);
        }

        /// <summary>
        /// The loader and the net manager, in every scene. Both are singletons that survive scene
        /// loads, but each scene still ships its own copy for the case where it is opened directly —
        /// so each copy needs its own references.
        /// </summary>
        private static void WireBootstrap(UnityEngine.SceneManagement.Scene scene,
                                          GameObject loadingScreen, ScriptableObject serverConfig)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;

                    if (behaviour is SceneLoader) Set(behaviour, "loadingScreenPrefab", loadingScreen);
                    else if (behaviour is ColyseusNetManager) Set(behaviour, "config", serverConfig);
                }
            }
        }

        /// <summary>
        /// The museum's walkable player. Its scripts sit in the default assembly, so they are reached
        /// by type name; the look pivot is the camera under the capsule, and the reader drives the
        /// controller beside it.
        /// </summary>
        private static void WirePlayer(UnityEngine.SceneManagement.Scene scene)
        {
            var input = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(PlayerInputAsset);

            if (input == null)
            {
                Debug.LogWarning($"SceneWiringRepair: missing '{PlayerInputAsset}'; the player cannot move.");
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;

                    string typeName = behaviour.GetType().Name;

                    if (typeName == "FPSController")
                    {
                        Camera camera = behaviour.GetComponentInChildren<Camera>(true);
                        if (camera != null) Set(behaviour, "cameraTransform", camera.transform);
                    }
                    else if (typeName == "FPSInputReader")
                    {
                        Set(behaviour, "inputAsset", input);

                        foreach (MonoBehaviour sibling in behaviour.GetComponents<MonoBehaviour>())
                        {
                            if (sibling != null && sibling.GetType().Name == "FPSController")
                            {
                                Set(behaviour, "controller", sibling);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The doorways, and the prompt they raise. Every one shows the same "ENTER" panel — there is
        /// one prompt on the HUD, not one per door, and the trigger only switches it on and off.
        /// </summary>
        /// <remarks>
        /// <c>promptLabel</c> is wired here alongside <c>promptUI</c> because leaving it null does not
        /// fail loudly: the panel still opens, still says ENTER, and a touch visitor is told to press
        /// a key their device does not have. It was null on all four doorways until 2026-09-10.
        /// </remarks>
        private static void WireDoorways(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject prompt = FindPrompt(scene);

            if (prompt == null)
            {
                Debug.LogWarning("SceneWiringRepair: no 'Enter' prompt under the HUD canvas.");
                return;
            }

            Transform cap = prompt.transform.Find("Text (TMP)");
            Object label = cap == null ? null : cap.GetComponent<TMPro.TMP_Text>();

            if (label == null)
            {
                Debug.LogWarning("SceneWiringRepair: the 'Enter' prompt has no 'Text (TMP)' cap, so " +
                                 "its wording cannot follow the control scheme.");
            }

            foreach (Doorway doorway in Doorways)
            {
                MonoBehaviour trigger = FindTrigger(scene, doorway.Path);

                if (trigger == null)
                {
                    Debug.LogWarning($"SceneWiringRepair: no doorway at '{doorway.Path}'.");
                    continue;
                }

                var serialized = new SerializedObject(trigger);
                serialized.FindProperty("promptUI").objectReferenceValue = prompt;
                if (label != null) serialized.FindProperty("promptLabel").objectReferenceValue = label;
                serialized.FindProperty("useLobby").boolValue = true;
                serialized.FindProperty("roomName").stringValue = doorway.RoomName;
                serialized.FindProperty("displayName").stringValue = doorway.DisplayName;
                serialized.FindProperty("sceneName").stringValue = doorway.Scene;
                serialized.FindProperty("maxPlayers").intValue = doorway.MaxPlayers;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(trigger);
            }

            DisableEngklekDoorway(scene);
        }

        /// <summary>
        /// Engklek has no room on the server and no menu entry, so its doorway would send the visitor
        /// to a lobby for a game that cannot start. Switched off rather than wired to nothing: an
        /// un-wired trigger still fires on Enter and asks the loader for an empty scene name.
        /// </summary>
        private static void DisableEngklekDoorway(UnityEngine.SceneManagement.Scene scene)
        {
            MonoBehaviour trigger = FindTrigger(scene, "Vid LT2/Vid Engklek/Cube");

            if (trigger == null || !trigger.enabled) return;

            trigger.enabled = false;
            EditorUtility.SetDirty(trigger);
            Debug.Log("SceneWiringRepair: Engklek doorway disabled — no such room server-side.");
        }

        private static GameObject FindPrompt(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode == RenderMode.WorldSpace) continue;

                    Transform found = canvas.transform.Find("Enter");
                    if (found != null) return found.gameObject;
                }
            }

            return null;
        }

        private static MonoBehaviour FindTrigger(UnityEngine.SceneManagement.Scene scene, string path)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || behaviour.GetType().Name != "SceneTriggerPrompt") continue;
                    if (PathOf(behaviour.transform) == path) return behaviour;
                }
            }

            return null;
        }

        private static string PathOf(Transform target)
        {
            string path = target.name;

            for (Transform parent = target.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        private static void Set(Object target, string field, Object value)
        {
            if (value == null) return;

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"SceneWiringRepair: {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // A SerializedProperty silently drops a value whose type the field will not accept, so
            // the assignment is checked rather than assumed.
            if (property.objectReferenceValue == null)
            {
                Debug.LogWarning($"SceneWiringRepair: '{value.name}' ({value.GetType().Name}) was " +
                                 $"rejected by {target.GetType().Name}.{field} — wrong type.");
                return;
            }

            EditorUtility.SetDirty(target);
        }
    }
}
