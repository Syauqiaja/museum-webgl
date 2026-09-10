using TMPro;
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
    /// egrang three. Engklek has no room server-side: its volume carries a <see cref="ComingSoonNotice"/>
    /// instead of a doorway.
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

        /// <summary>
        /// One doorway per game, on the video trigger volume of that game's exhibit — the same
        /// <c>Cube</c> that starts the video and opens the lesson. All three exhibits with a
        /// trigger sit on the first floor (<c>Vid LT1</c>); the second <c>Egrang Doorway</c>
        /// that used to sit beside the Dakon one was a placeholder overlapping the Egrang volume
        /// and has been deleted.
        /// </summary>
        private static readonly Doorway[] Doorways =
        {
            Door("Vid LT1/Vid Dakon/Dakon Doorway", "dakon", "Dakon", SceneReference.Dakon, 2),
            Door("Vid LT1/Vid Egrang/Cube", "egrang", "Egrang", SceneReference.Egrang, 3),
        };

        /// <summary>The presence component, by name: it lives in <c>Museum.Net</c>, which depends on this assembly's runtime half.</summary>
        private const string PresenceTypeName = "Museum.Net.MuseumPresence";

        private const string EngklekDoorwayPath = "Vid LT1/Vid Engklek/Cube";
        private const string EgrangDoorwayPath = "Vid LT1/Vid Egrang/Cube";

        /// <summary>Near face of <c>L1_wall_E_SR</c>, the wall between the Egrang and Engklek bays; the Egrang trigger stops just short of it.</summary>
        private const float EgrangBayWallZ = 2.4f;

        /// <summary>The floating notice over the Engklek volume, under <c>Vid LT1</c> beside the <c>Vid Engklek</c> group.</summary>
        private const string ComingSoonLabelName = "Segera Hadir (Engklek)";
        private const string ComingSoonText = "Permainan Engklek segera hadir";

        /// <summary>Head height of the notice above LT1's floor — the lobby's station hints use the same.</summary>
        private const float ComingSoonLabelHeight = 2.55f;

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
                    WirePresence(scene);
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
        /// The museum's presence link: one <c>MuseumPresence</c> on the player rig, so visitors
        /// see each other walk the halls. It lives in <c>Museum.Net</c>, which this assembly cannot
        /// reference without a cycle, so it is reached by type name like the player scripts. The
        /// font it names is the same the nameplates in Egrang use.
        /// </summary>
        private static void WirePresence(UnityEngine.SceneManagement.Scene scene)
        {
            System.Type presenceType = null;
            foreach (System.Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type.FullName == PresenceTypeName) { presenceType = type; break; }
            }

            if (presenceType == null)
            {
                Debug.LogWarning($"SceneWiringRepair: no '{PresenceTypeName}' type; visitors will not see each other.");
                return;
            }

            GameObject player = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.CompareTag("Player")) { player = t.gameObject; break; }
                }
                if (player != null) break;
            }

            if (player == null)
            {
                Debug.LogWarning("SceneWiringRepair: no object tagged 'Player' in the Museum; presence not wired.");
                return;
            }

            Component presence = player.GetComponent(presenceType);
            if (presence == null) presence = player.AddComponent(presenceType);

            Set(presence, "player", player.transform);
            Set(presence, "nameFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldFont));
            EditorUtility.SetDirty(player);
        }

        /// <summary>
        /// The doorways, and the prompt they raise. Every one shows the same "ENTER" panel — there is
        /// one prompt on the HUD, not one per door, and the trigger only switches it on and off.
        /// </summary>
        private static void WireDoorways(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject prompt = FindPrompt(scene);

            if (prompt == null)
            {
                Debug.LogWarning("SceneWiringRepair: no 'Enter' prompt under the HUD canvas.");
                return;
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
                serialized.FindProperty("useLobby").boolValue = true;
                serialized.FindProperty("roomName").stringValue = doorway.RoomName;
                serialized.FindProperty("displayName").stringValue = doorway.DisplayName;
                serialized.FindProperty("sceneName").stringValue = doorway.Scene;
                serialized.FindProperty("maxPlayers").intValue = doorway.MaxPlayers;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(trigger);
            }

            TrimEgrangTrigger(scene);
            MakeEngklekComingSoon(scene);
        }

        /// <summary>
        /// Engklek has no room on the server and no menu entry, so its doorway would send the visitor
        /// to a lobby for a game that cannot start. The <c>SceneTriggerPrompt</c> is removed — a
        /// disabled one still fires on Enter and asks the loader for an empty scene name — and a
        /// <see cref="ComingSoonNotice"/> takes its place on the same volume, with a floating
        /// "Segera hadir" label and no key named, because there is nothing to press.
        /// </summary>
        private static void MakeEngklekComingSoon(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject volume = FindByPath(scene, EngklekDoorwayPath);
            if (volume == null)
            {
                Debug.LogWarning($"SceneWiringRepair: no Engklek trigger at '{EngklekDoorwayPath}'.");
                return;
            }

            MonoBehaviour doorway = FindTrigger(scene, EngklekDoorwayPath);
            if (doorway != null) Object.DestroyImmediate(doorway);

            var notice = volume.GetComponent<ComingSoonNotice>();
            if (notice == null) notice = volume.AddComponent<ComingSoonNotice>();

            // The label hangs at head height over the middle of the volume, where the visitor is
            // standing when it appears; ComingSoonNotice turns it to the camera every frame. It is
            // parented beside the `Vid Engklek` group, not inside it: the group is scaled
            // (2.6, 0.04, 1.5) and laid on its side, and a child of that cannot be a readable
            // label at any local scale.
            Transform floor = volume.transform.parent.parent;
            Transform label = floor.Find(ComingSoonLabelName);
            Physics.SyncTransforms();
            var collider = volume.GetComponent<Collider>();
            Vector3 at = collider != null ? collider.bounds.center : volume.transform.position;
            at.y = MuseumScreenGeometry.FloorLevels[1] + ComingSoonLabelHeight;

            if (label == null)
            {
                var go = new GameObject(ComingSoonLabelName, typeof(RectTransform), typeof(TextMeshPro));
                go.transform.SetParent(floor, false);
                label = go.transform;

                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(30f, 6f);

                var text = go.GetComponent<TextMeshPro>();
                text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldFont);
                text.fontSize = 9f;
                text.fontStyle = FontStyles.Bold;
                text.color = Cream;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Overflow;
            }

            label.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.back));
            label.localScale = Vector3.one * 0.1f;
            // A RectTransform under a plain Transform serializes its anchoredPosition, not its
            // localPosition; left derived, x and y came back as 0 on the next scene load.
            ((RectTransform)label).anchoredPosition3D = label.localPosition;
            GameObjectUtility.SetStaticEditorFlags(label.gameObject, 0);

            var serialized = new SerializedObject(notice);
            serialized.FindProperty("notice").objectReferenceValue = label.GetComponent<TMP_Text>();
            serialized.FindProperty("text").stringValue = ComingSoonText;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(volume);
            Debug.Log("SceneWiringRepair: Engklek exhibit marked 'Segera hadir' — no such room server-side.");
        }

        /// <summary>
        /// The Egrang trigger volume ran 23 m along its bay and through the wall into the Engklek
        /// bay, so a visitor standing at the Engklek screen was also "inside" Egrang's doorway
        /// and saw its ENTER prompt. Cut it at the shared wall (<c>L1_wall_E_SR</c>, z ≈ 2.5).
        /// </summary>
        private static void TrimEgrangTrigger(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject volume = FindByPath(scene, EgrangDoorwayPath);
            var box = volume != null ? volume.GetComponent<BoxCollider>() : null;
            if (box == null) return;

            // Local +x runs along world -z; the box is unit-sized and scaled by the transform.
            Transform t = volume.transform;
            float length = box.bounds.size.z;
            float zNear = box.bounds.min.z;
            if (box.bounds.max.z <= EgrangBayWallZ + 0.01f) return;

            float newLength = EgrangBayWallZ - zNear;
            float worldCentreZ = (zNear + EgrangBayWallZ) * 0.5f;
            float localCentreX = (t.position.z - worldCentreZ) / t.lossyScale.x;

            float zFarBefore = box.bounds.max.z;
            Vector3 size = box.size;
            size.x *= newLength / length;
            box.size = size;
            box.center = new Vector3(localCentreX, box.center.y, box.center.z);
            EditorUtility.SetDirty(box);
            Debug.Log($"SceneWiringRepair: Egrang trigger trimmed to z ≤ {EgrangBayWallZ} (ran to {zFarBefore:F1}).");
        }

        private static GameObject FindByPath(UnityEngine.SceneManagement.Scene scene, string path)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (PathOf(t) == path) return t.gameObject;
                }
            }

            return null;
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
