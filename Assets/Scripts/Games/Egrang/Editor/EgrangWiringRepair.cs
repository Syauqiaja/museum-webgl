using UnityEditor;
using UnityEngine;

namespace Museum.Games.Egrang.EditorTools
{
    /// <summary>
    /// Re-points the Egrang scene's per-lane references — the three racers, their tracks, movers,
    /// stilts and nameplates — plus the race object's own view references.
    ///
    /// Most of these fields document a fallback ("leave empty to find one in the children"), so a
    /// null is not automatically a fault. They are filled anyway: a fallback that searches by name
    /// hides which object it found, and three lanes of identically-named children is exactly where a
    /// silent mis-bind costs an afternoon.
    ///
    /// One null was a real fault. Every <see cref="EgrangStick"/> carried the default
    /// <c>markerFilter</c> of "_L_", including the right-hand stilts, so both components on a racer
    /// bound the same side's markers — the case that field's own tooltip warns about.
    /// </summary>
    public static class EgrangWiringRepair
    {
        private const string ScenePath = "Assets/Scenes/Egrang.unity";

        private static readonly string[] LaneRoots = { "Player 1 Point", "Player 2 Point", "Player 3 Point" };

        /// <summary>
        /// The bodies a walker can wear, one per <c>PlayerAvatars</c> id, matched by name. Jawa's is
        /// required even though Jawa is the authored body: its avatar reads the authored pose.
        /// </summary>
        private static readonly string[] CharacterModels =
        {
            "Assets/Models/ASSET_NUSANTARA/1_Karakter/Char_Jawa_L.fbx",
            "Assets/Models/ASSET_NUSANTARA/1_Karakter/Char_Bali_P.fbx",
            "Assets/Models/ASSET_NUSANTARA/1_Karakter/Char_Bugis_P.fbx",
            "Assets/Models/ASSET_NUSANTARA/1_Karakter/Char_Minang_L.fbx",
        };

        [MenuItem("Museum/Egrang/Wire Scene References")]
        public static void Repair()
        {
            UnityEngine.SceneManagement.Scene scene = OpenScene();
            Camera camera = FindMainCamera(scene);

            for (int lane = 0; lane < LaneRoots.Length; lane++)
            {
                Transform root = FindRoot(scene, LaneRoots[lane]);

                if (root == null)
                {
                    Debug.LogWarning($"EgrangWiringRepair: no '{LaneRoots[lane]}' in the scene.");
                    continue;
                }

                WireLane(root, lane, camera);
            }

            WireRace(scene);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log("EgrangWiringRepair: lane and race references wired.");
        }

        private static void WireLane(Transform root, int lane, Camera camera)
        {
            Transform player = root.Find("Egrang Player");
            Transform trackRoot = root.Find("Race Track");

            if (player == null || trackRoot == null)
            {
                Debug.LogWarning($"EgrangWiringRepair: '{root.name}' is missing its player or track.");
                return;
            }

            Transform start = trackRoot.Find("Start Line");
            Transform finish = trackRoot.Find("Finish Line");
            var mover = player.GetComponent<EgrangStepMover>();
            var track = trackRoot.GetComponent<EgrangRaceTrack>();
            var racer = root.GetComponent<EgrangRacer>();

            if (mover != null)
            {
                Set(mover, "animator", player.GetComponent<Animator>());
            }

            if (track != null)
            {
                Set(track, "start", start);
                Set(track, "finish", finish);
                // The measured object is the one the mover walks, not the lane root, which never moves.
                Set(track, "racer", player);
            }

            if (racer != null)
            {
                var serialized = new SerializedObject(racer);
                serialized.FindProperty("lane").intValue = lane;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Set(racer, "track", track);
                Set(racer, "mover", mover);
                Set(racer, "start", start);
                Set(racer, "finish", finish);
            }

            WireStick(player, "EgrangAnak_Lingkaran_L", "_L_", "LeftHand", "LeftFoot");
            WireStick(player, "EgrangAnak_Lingkaran_R", "_R_", "RightHand", "RightFoot");
            WireBody(player);

            Transform plate = player.Find("Name Plate");
            var nameplate = plate != null ? plate.GetComponent<EgrangNameplate>() : null;
            if (nameplate != null) Set(nameplate, "facing", camera);
        }

        /// <summary>
        /// One stilt: the side's marker filter, the two bones it is welded to, and its own three
        /// markers. The markers are named for the side, so they are found under this stick rather
        /// than searched for across the rig.
        /// </summary>
        private static void WireStick(Transform player, string stickName, string filter,
                                      string handBone, string footBone)
        {
            Transform stickRoot = player.Find(stickName);

            if (stickRoot == null)
            {
                Debug.LogWarning($"EgrangWiringRepair: '{player.parent.name}' has no '{stickName}'.");
                return;
            }

            var stick = stickRoot.GetComponent<EgrangStick>();
            if (stick == null) return;

            var serialized = new SerializedObject(stick);
            serialized.FindProperty("markerFilter").stringValue = filter;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Set(stick, "handBone", FindDescendant(player, handBone));
            Set(stick, "footBone", FindDescendant(player, footBone));
            Set(stick, "footstep", stickRoot.Find($"{stickName}_Foot"));
            Set(stick, "handle", stickRoot.Find($"{stickName}_Grip"));
            Set(stick, "tip", stickRoot.Find($"{stickName}_Tip"));
        }

        /// <summary>
        /// The walker's <see cref="EgrangRacerBody"/>, added if missing: both stilts it re-points and
        /// the four character models. Runs after <see cref="WireStick"/> so the stilts already hold
        /// the authored bones it restores to.
        /// </summary>
        private static void WireBody(Transform player)
        {
            var body = player.GetComponent<EgrangRacerBody>();
            if (body == null) body = Undo.AddComponent<EgrangRacerBody>(player.gameObject);

            Transform left = player.Find("EgrangAnak_Lingkaran_L");
            Transform right = player.Find("EgrangAnak_Lingkaran_R");

            SetArray(body, "sticks", new Object[]
            {
                left != null ? left.GetComponent<EgrangStick>() : null,
                right != null ? right.GetComponent<EgrangStick>() : null,
            });

            var models = new Object[CharacterModels.Length];
            for (int i = 0; i < CharacterModels.Length; i++) models[i] = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModels[i]);
            SetArray(body, "characters", models);
        }

        /// <summary>
        /// The race's own views. The bar is taken from the generated run root rather than by a
        /// scene-wide search: a stale second <see cref="SkillCheckBar"/> sits on the old HUD canvas,
        /// and it is the unwired one.
        /// </summary>
        private static void WireRace(UnityEngine.SceneManagement.Scene scene)
        {
            var race = FindComponent<EgrangRace>(scene);

            if (race == null)
            {
                Debug.LogWarning("EgrangWiringRepair: no EgrangRace in the scene.");
                return;
            }

            Transform ui = FindRoot(scene, "Egrang UI");
            SkillCheckBar bar = null;

            if (ui != null)
            {
                Transform runRoot = ui.Find("Run Root");
                if (runRoot != null) bar = runRoot.GetComponentInChildren<SkillCheckBar>(true);
            }

            if (bar == null)
            {
                Debug.LogWarning("EgrangWiringRepair: no SkillCheckBar under 'Egrang UI/Run Root'.");
            }

            Set(race, "bar", bar);
            Set(race, "progressView", FindComponent<EgrangProgressView>(scene));
            Set(race, "stickSelector", FindComponent<EgrangStickSelector>(scene));

            // The two arrays the race indexes by lane, filled from the same lane roots the lanes were
            // just numbered from. The names builder sorts by `EgrangRacer.lane`, but it ran before
            // those numbers were set after the rebuild and shipped the plates as lanes 3, 1, 2 — every
            // name over someone else's walker (found 2026-09-10). The tool that owns the numbers owns
            // what is ordered by them.
            var racers = new Object[LaneRoots.Length];
            var plates = new Object[LaneRoots.Length];

            for (int lane = 0; lane < LaneRoots.Length; lane++)
            {
                Transform root = FindRoot(scene, LaneRoots[lane]);
                Transform plate = root != null ? root.Find("Egrang Player/Name Plate") : null;

                racers[lane] = root != null ? root.GetComponent<EgrangRacer>() : null;
                plates[lane] = plate != null ? plate.GetComponent<EgrangNameplate>() : null;
            }

            SetArray(race, "racers", racers);
            SetArray(race, "nameplates", plates);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Name == "EgrangNetBootstrap")
                    {
                        Set(behaviour, "race", race);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- lookup

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }

            return null;
        }

        private static Camera FindMainCamera(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.name == "Main Camera") return camera;
                }
            }

            return null;
        }

        private static T FindComponent<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }

            return null;
        }

        private static UnityEngine.SceneManagement.Scene OpenScene()
        {
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            return active.path == ScenePath
                ? active
                : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                      ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        private static void Set(Object target, string field, Object value)
        {
            if (value == null)
            {
                Debug.LogWarning($"EgrangWiringRepair: no target for {target.GetType().Name}.{field}.");
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"EgrangWiringRepair: {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// Writes a lane-indexed array whole, or not at all: a half-filled one would put a later
        /// lane's reference in an earlier lane's slot, which is the fault this exists to prevent.
        /// </summary>
        private static void SetArray(Object target, string field, Object[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                {
                    Debug.LogWarning($"EgrangWiringRepair: no target for {target.GetType().Name}.{field}[{i}]; " +
                                     "left the array as it was.");
                    return;
                }
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"EgrangWiringRepair: {target.GetType().Name} has no array '{field}'.");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
