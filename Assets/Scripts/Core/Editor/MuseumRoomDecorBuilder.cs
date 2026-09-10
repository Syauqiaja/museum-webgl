using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Dresses each exhibit bay with props that belong to *its* game — a dakon board on a low
    /// table under the Dakon screen, two forts flanking Bentengan, a chalked court beside
    /// Engklek — on top of the building's own generic dressing (vitrine, rope, plinths, batik),
    /// which is the same in all eighteen bays and is what made them read as copies.
    /// </summary>
    /// <remarks>
    /// Menu: <c>Museum/Decor/Build Room Decor</c> (and <c>Clear</c>). Idempotent: one scene-root
    /// container, <c>Decor (Generated)</c>, rebuilt from scratch. Props are the Blender-built
    /// models under <c>Assets/Models/Generated/</c> (see <see cref="GeneratedModelPostprocessor"/>),
    /// so a themed bay costs a few hundred triangles and no new materials.
    ///
    /// Every placement is expressed in a screen's own frame — metres <em>out</em> from the wall
    /// and <em>along</em> it, from <see cref="MuseumScreenGeometry"/> — never in world space, so
    /// re-hanging a screen re-dresses its bay. The slots are the parts of a bay the building
    /// leaves empty and a visitor never looks through: the strip under the info board, and the
    /// two wings beyond the batik and the plinths (|along| ≥ 4 m). Nothing goes inside the
    /// viewing corridor (|along| &lt; <see cref="CorridorHalfWidth"/>, out past
    /// <see cref="CorridorStart"/>): that is what <c>Report Viewing Blockers</c> polices, and
    /// the tool refuses such a slot rather than relying on the report to catch it.
    ///
    /// Before a prop is committed its bounds are tested against the building's renderers —
    /// columns, side walls, the plinths — and nudged outward, then back, until it fits, or
    /// dropped with a warning. Props at least <see cref="ColliderMinHeight"/> tall get a box
    /// collider so the visitor cannot walk through a gong stand; mats and boards do not.
    /// </remarks>
    public static class MuseumRoomDecorBuilder
    {
        public const string RootName = "Decor (Generated)";
        public const string ModelFolder = "Assets/Models/Generated/";

        /// <summary>Half-width of the strip between the visitor and the picture that stays empty.</summary>
        public const float CorridorHalfWidth = 2.1f;

        /// <summary>The strip starts this far out from the wall; closer than that is under the info board, below every sight line.</summary>
        public const float CorridorStart = 1.3f;

        /// <summary>Nothing under the screen may reach higher than this (the info board hangs at ~1.4 m).</summary>
        public const float UnderScreenMaxHeight = 1.25f;

        public const float ColliderMinHeight = 0.4f;

        private const float Clearance = 0.12f;

        /// <summary>(out, along-the-wall) offsets tried in order when a slot is taken; the second axis is signed towards the room's edge.</summary>
        private static readonly Vector2[] Nudges =
        {
            new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0f, -0.6f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, -0.6f), new Vector2(1.0f, 0f), new Vector2(1.0f, -0.8f),
            new Vector2(0f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(1.5f, 0f), new Vector2(1.5f, -0.8f),
        };

        /// <summary>Top surfaces of the two pieces things get stood on.</summary>
        private const float LowTableTop = 0.45f;
        private const float PlinthTop = 0.91f;

        private struct Place
        {
            public string Model;
            public float Along, Lat, Yaw, Scale;

            /// <summary>Another model stood on this one's top, with its own yaw and scale.</summary>
            public string Top;
            public float TopYaw, TopScale, TopHeight;

            public static Place On(string model, float along, float lat, float yaw = 0f, float scale = 1f)
                => new Place { Model = model, Along = along, Lat = lat, Yaw = yaw, Scale = scale, TopScale = 1f };

            public Place With(string top, float scale = 1f, float yaw = 0f)
            {
                Top = top; TopScale = scale; TopYaw = yaw;
                TopHeight = Model == "Plinth_Teak" ? PlinthTop : LowTableTop;
                return this;
            }
        }

        // Slots, in metres from the screen centre: U = under the info board; L1/R1 = by the
        // wall beyond the batik; L2/R2 = a step out, beyond the plinths; L3/R3 = level with
        // the vitrine's far side. Left is -lat.
        private const float U = 0.8f;
        private const float Near = 1.0f, Mid = 2.4f, Far = 4.2f;
        private const float Wing1 = 4.8f, Wing2 = 5.3f;

        private static Place Under(string model) => Place.On(model, U, 0f);
        private static Place L1(string model, float yaw = 0f, float scale = 1f) => Place.On(model, Near, -Wing1, yaw, scale);
        private static Place R1(string model, float yaw = 0f, float scale = 1f) => Place.On(model, Near, Wing1, yaw, scale);
        private static Place L2(string model, float yaw = 0f, float scale = 1f) => Place.On(model, Mid, -Wing2, yaw, scale);
        private static Place R2(string model, float yaw = 0f, float scale = 1f) => Place.On(model, Mid, Wing2, yaw, scale);
        private static Place L3(string model, float yaw = 0f, float scale = 1f) => Place.On(model, Far, -Wing2, yaw, scale);
        private static Place R3(string model, float yaw = 0f, float scale = 1f) => Place.On(model, Far, Wing2, yaw, scale);

        /// <summary>
        /// The bays, keyed like <see cref="LessonContent"/>. The hero piece is always the game's
        /// own apparatus under the screen; the wings mix the courtyard kit (payung, gong, kendhi,
        /// gunungan, bamboo rack, tikar) so no two neighbouring bays share a silhouette.
        /// </summary>
        private static readonly Dictionary<string, Place[]> Themes = new Dictionary<string, Place[]>
        {
            ["dakon"] = new[]
            {
                Under("Low_Table").With("Dakon_Board", 0.65f),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Gong_Stand"), R2("Gunungan_Wayang"),
            },
            ["engklek"] = new[]
            {
                Under("Low_Table").With("Gatheng_Stones", 0.6f),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Gunungan_Wayang"),
                R1("Kendhi_Pot", 0f, 1.2f), Place.On("Engklek_Court", 5.5f, 6.0f, 90f),
            },
            ["cublak_cublak_suweng"] = new[]
            {
                Under("Low_Table").With("Suweng_Cushion"),
                L1("Bamboo_Rack"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Payung_Ceremonial"), R2("Gong_Stand"),
            },
            ["egrang"] = new[]
            {
                Under("Low_Table").With("Egrang_Stilts", 0.35f),
                L1("Bamboo_Rack"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Egrang_Stilts", 15f), R2("Payung_Ceremonial"),
            },
            ["gobak_sodor"] = new[]
            {
                Under("Low_Table").With("Gobak_Flag", 0.35f),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Kendhi_Pot"),
                Place.On("Gobak_Field", 2.8f, Wing2, 90f), Place.On("Gobak_Flag", 1.4f, 4.0f), Place.On("Gobak_Flag", 4.3f, 6.4f),
            },
            ["bentengan"] = new[]
            {
                Under("Low_Table").With("Kendhi_Pot", 0.8f),
                L1("Bentengan_Fort_Red"), L2("Tikar_Mat").With("Gunungan_Wayang"),
                R1("Bentengan_Fort_Indigo"), R2("Payung_Ceremonial"), R3("Gong_Stand"),
            },
            ["benthik"] = new[]
            {
                Under("Low_Table").With("Benthik_Sticks", 0.8f),
                L1("Bamboo_Rack"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Payung_Ceremonial"), R2("Gunungan_Wayang"),
            },
            ["gatheng"] = new[]
            {
                Under("Low_Table").With("Gatheng_Stones", 0.8f),
                L1("Payung_Ceremonial"), L2("Gong_Stand"),
                R1("Plinth_Teak").With("Kendhi_Pot", 0.8f), R2("Tikar_Mat").With("Gunungan_Wayang"),
            },
            ["bekelan"] = new[]
            {
                Under("Low_Table").With("Bekelan_Set", 0.9f),
                L1("Gong_Stand"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Payung_Ceremonial"), R2("Bamboo_Rack"),
            },
            ["lompat_tali"] = new[]
            {
                Under("Gunungan_Wayang"),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Bamboo_Rack"), R2("Lompat_Tali"),
            },
            ["dam_daman"] = new[]
            {
                Under("Low_Table").With("Damdaman_Board", 0.7f),
                L1("Gong_Stand"), L2("Bamboo_Rack"),
                R1("Payung_Ceremonial"), R2("Tikar_Mat").With("Kendhi_Pot"),
            },
            ["cirak"] = new[]
            {
                Under("Low_Table").With("Cirak_Tiles", 0.7f),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Gunungan_Wayang"), R2("Bamboo_Rack"),
            },
            ["dampar"] = new[]
            {
                Under("Dampar_Table"),
                L1("Gong_Stand"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Payung_Ceremonial"), R2("Gunungan_Wayang"),
            },
            ["sluku_sluku_bathok"] = new[]
            {
                Under("Low_Table").With("Bathok_Shells"),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Bamboo_Rack"), R2("Gunungan_Wayang"),
            },
            ["jamuran"] = new[]
            {
                Under("Low_Table").With("Jamuran_Cluster", 0.75f),
                L1("Payung_Ceremonial"), L2("Jamuran_Cluster", 30f, 1.3f),
                R1("Gunungan_Wayang"), R2("Jamuran_Cluster", -60f, 1.3f), R3("Tikar_Mat").With("Kendhi_Pot"),
            },
            ["bitingan"] = new[]
            {
                Under("Low_Table").With("Bitingan_Bundle"),
                L1("Bamboo_Rack"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Payung_Ceremonial"), R2("Gong_Stand"),
            },
            ["ancak_ancak_alis"] = new[]
            {
                Under("Low_Table").With("Ancak_Tray"),
                L1("Payung_Ceremonial"), L2("Tikar_Mat").With("Kendhi_Pot"),
                R1("Gunungan_Wayang"), R2("Gong_Stand"),
            },
            ["blarak_sempal"] = new[]
            {
                Under("Low_Table").With("Blarak_Frond", 0.6f),
                L1("Bamboo_Rack"), L2("Tikar_Mat").With("Blarak_Frond"),
                R1("Payung_Ceremonial"), R2("Tikar_Mat").With("Blarak_Frond", 1f, 180f),
            },
        };

        [MenuItem("Museum/Decor/Build Room Decor")]
        public static void Build()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            MuseumScreenAligner.AlignAll(scene);

            MuseumScreenGeometry.ClearRoot(scene, RootName);
            Transform root = MuseumScreenGeometry.EnsureRoot(scene, RootName);

            var keyByGroup = new Dictionary<string, string>();
            foreach (LessonSource lesson in LessonContent.All()) keyByGroup[lesson.VideoGroup] = lesson.GameKey;

            List<Bounds> obstacles = Obstacles();
            int placed = 0, dropped = 0;

            foreach (MuseumScreenGeometry.ScreenFrame screen in MuseumScreenGeometry.All(scene))
            {
                if (!keyByGroup.TryGetValue(screen.GroupName, out string key) || !Themes.TryGetValue(key, out Place[] places))
                {
                    Debug.LogWarning($"MuseumRoomDecorBuilder: no theme for '{screen.GroupName}'.");
                    continue;
                }

                var bay = new GameObject($"Bay_{MuseumScreenFrameBuilder.Key(screen.GroupName)}");
                Undo.RegisterCreatedObjectUndo(bay, MuseumUIStyle.UndoLabel);
                bay.transform.SetParent(root, false);

                foreach (Place place in places)
                {
                    if (Put(screen, place, bay.transform, obstacles)) placed++;
                    else dropped++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MuseumRoomDecorBuilder: placed {placed} prop(s) under '{RootName}'" +
                      (dropped > 0 ? $", dropped {dropped} that would not fit — see the warnings above." : "."));
        }

        [MenuItem("Museum/Decor/Clear Room Decor")]
        public static void Clear()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            if (!MuseumScreenGeometry.ClearRoot(scene, RootName))
            {
                Debug.Log("MuseumRoomDecorBuilder: nothing to clear.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Instantiates one placement (and whatever stands on it), nudging it clear of the building. False if it had to be dropped.</summary>
        private static bool Put(MuseumScreenGeometry.ScreenFrame s, Place place, Transform bay, List<Bounds> obstacles)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + place.Model + ".fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"MuseumRoomDecorBuilder: no model '{place.Model}' under {ModelFolder}.");
                return false;
            }

            Quaternion rotation = Quaternion.LookRotation(s.Out, s.Up) * Quaternion.Euler(0f, place.Yaw, 0f);
            GameObject go = Spawn(prefab, place.Model, bay, rotation, place.Scale);

            float along = place.Along, lat = place.Lat;
            float outward = Mathf.Sign(place.Lat == 0f ? 1f : place.Lat);
            bool fits = false;
            foreach (Vector2 nudge in Nudges)
            {
                // Under the screen only "out" can give; in the wings, try a step out, then a
                // step in towards the room (Allowed keeps it out of the corridor), then a
                // step further along the wall.
                float tryAlong = along + nudge.x;
                float tryLat = place.Lat == 0f ? lat : lat + nudge.y * outward;
                if (place.Lat == 0f && nudge.y != 0f) continue;

                go.transform.position = s.FloorPoint(tryAlong, tryLat);
                Bounds b = WorldBounds(go);
                fits = Allowed(s, b, place.Lat == 0f) && Clear(b, obstacles);
                if (fits) { along = tryAlong; lat = tryLat; break; }
            }

            if (!fits)
            {
                Debug.LogWarning($"MuseumRoomDecorBuilder: '{s.GroupName}' — no room for '{place.Model}' at {place.Along:F1} m out / {place.Lat:+0.0;-0.0} m along; dropped.");
                Object.DestroyImmediate(go);
                return false;
            }

            Bounds placedBounds = WorldBounds(go);
            obstacles.Add(placedBounds);
            Finish(go, placedBounds);

            if (string.IsNullOrEmpty(place.Top)) return true;

            GameObject topPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + place.Top + ".fbx");
            if (topPrefab == null)
            {
                Debug.LogWarning($"MuseumRoomDecorBuilder: no model '{place.Top}' under {ModelFolder}.");
                return true;
            }

            GameObject top = Spawn(topPrefab, place.Top, go.transform, rotation * Quaternion.Euler(0f, place.TopYaw, 0f), place.TopScale);
            top.transform.position = s.FloorPoint(along, lat) + s.Up * (place.TopHeight * place.Scale);
            Bounds topBounds = WorldBounds(top);
            if (!Allowed(s, topBounds, place.Lat == 0f))
            {
                Debug.LogWarning($"MuseumRoomDecorBuilder: '{s.GroupName}' — '{place.Top}' on '{place.Model}' reaches into the picture; dropped.");
                Object.DestroyImmediate(top);
                return true;
            }

            obstacles.Add(topBounds);
            Finish(top, topBounds);
            return true;
        }

        internal static GameObject Spawn(GameObject prefab, string name, Transform parent, Quaternion rotation, float scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, MuseumUIStyle.UndoLabel);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.rotation = rotation;
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Static flags, no shadows (none in this museum — see lighting.md), a collider if it is tall enough to walk into.</summary>
        internal static void Finish(GameObject go, Bounds world)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
            }

            if (world.size.y < ColliderMinHeight) return;

            var box = go.AddComponent<BoxCollider>();
            Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
            var local = new Bounds(toLocal.MultiplyPoint3x4(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? world.min.x : world.max.x,
                    (i & 2) == 0 ? world.min.y : world.max.y,
                    (i & 4) == 0 ? world.min.z : world.max.z);
                local.Encapsulate(toLocal.MultiplyPoint3x4(corner));
            }

            box.center = local.center;
            box.size = local.size;
        }

        /// <summary>Keeps props out of the viewing corridor, and anything under the screen below the info board.</summary>
        private static bool Allowed(MuseumScreenGeometry.ScreenFrame s, Bounds b, bool underScreen)
        {
            if (underScreen) return b.max.y - s.FloorY <= UnderScreenMaxHeight;

            Vector3 d = b.center - s.Centre;
            float along = Vector3.Dot(d, s.Out), lat = Vector3.Dot(d, s.Right);
            float halfAlong = Extent(b, s.Out), halfLat = Extent(b, s.Right);
            bool inCorridor = Mathf.Abs(lat) - halfLat < CorridorHalfWidth && along + halfAlong > CorridorStart;
            bool flat = b.size.y < 0.05f;
            return flat || !inCorridor;
        }

        internal static bool Clear(Bounds b, List<Bounds> obstacles)
        {
            Bounds grown = b;
            grown.Expand(Clearance * 2f);
            foreach (Bounds o in obstacles)
            {
                if (grown.Intersects(o)) return false;
            }

            return true;
        }

        /// <summary>
        /// The building's renderers a prop must not overlap: everything that is not a floor
        /// slab, a ceiling, or a rug. The generated containers are left out (they are rebuilt
        /// together) and so is anything hung above head height.
        /// </summary>
        internal static List<Bounds> Obstacles()
        {
            var result = new List<Bounds>();
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || IsGenerated(r.transform)) continue;

                Bounds b = r.bounds;
                bool slab = b.size.y < 0.5f && b.size.x * b.size.z > 20f;
                bool rug = b.size.y < 0.05f;
                // Whole-building merged meshes (facade, pilasters, cornices) span every bay;
                // their bounds say nothing about where a prop can stand.
                bool building = b.size.x > 8f && b.size.z > 8f;
                if (slab || rug || building) continue;

                // Ceilings and lanterns: nothing on the floor reaches them.
                float floor = NearestFloor(b.min.y);
                if (b.min.y > floor + 2.6f) continue;

                result.Add(b);
            }

            return result;
        }

        private static float NearestFloor(float y)
        {
            float best = MuseumScreenGeometry.FloorLevels[0];
            foreach (float f in MuseumScreenGeometry.FloorLevels)
            {
                if (f <= y + 0.3f) best = f;
            }

            return best;
        }

        /// <summary>World bounds from the meshes, so a just-moved transform is measured where it is now.</summary>
        internal static Bounds WorldBounds(GameObject go)
        {
            var world = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds local = mf.sharedMesh.bounds;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? local.min.x : local.max.x,
                        (i & 2) == 0 ? local.min.y : local.max.y,
                        (i & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (first) { world = new Bounds(p, Vector3.zero); first = false; }
                    else world.Encapsulate(p);
                }
            }

            return world;
        }

        internal static float Extent(Bounds b, Vector3 dir)
        {
            Vector3 e = b.extents;
            return Mathf.Abs(e.x * dir.x) + Mathf.Abs(e.y * dir.y) + Mathf.Abs(e.z * dir.z);
        }

        internal static bool IsGenerated(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (t.name.EndsWith("(Generated)")) return true;
            }

            return false;
        }
    }
}
