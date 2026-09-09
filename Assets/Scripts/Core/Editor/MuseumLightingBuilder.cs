using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Lights the Museum scene: a cool daylight key, a warm gradient ambient, one warm point light
    /// inside every lantern fixture, and a soft accent over every exhibit podium.
    ///
    /// The look is authored here rather than in the scene for the reason the rest of this folder
    /// exists — the project was destroyed on 2026-08-19 and every inspector value was lost. A number
    /// that only lives in a .unity file is a number that does not survive the next rebuild.
    ///
    /// Lighting is deliberately **all realtime**. The museum's 954 meshes are compressed
    /// <c>Assets/Mesh/*.asset</c> files with a single UV set and no lightmap UV2, and being raw Mesh
    /// assets they have no model importer that could generate one; baking would mean unwrapping all
    /// 954 and adding lightmap atlases to a WebGL payload already near 110 MB. See
    /// <c>Assets/Docs/lighting.md</c>.
    ///
    /// Two traps this generator is written around, both learned the hard way from the scene file:
    ///
    /// 1. <b>Transforms lie.</b> 305 of the museum's 1052 transforms sit at the origin because their
    ///    offset is baked into the mesh vertices — every <c>Podium_*</c> and every <c>*_Plinth*</c>
    ///    among them. Positions therefore come from <see cref="Renderer.bounds"/>, which is
    ///    world-space and correct either way, never from <c>transform.position</c>.
    /// 2. <b>Additional lights get no shadows.</b> <c>Mobile_RPAsset</c> disables them pipeline-wide,
    ///    so asking for them here would cost nothing and buy nothing.
    /// </summary>
    public static class MuseumLightingBuilder
    {
        private const string ScenePath = "Assets/Scenes/Museum.unity";

        /// <summary>
        /// Everything this generator creates lives under this one root. Re-running clears the root
        /// and rebuilds it, so a re-run is safe on a scene someone else has been editing
        /// (ui-style.md §9) — anything hand-placed outside the root is never touched.
        /// </summary>
        private const string GeneratedRoot = "Lighting (Generated)";

        /// <summary>
        /// The minimap draws its markers with a camera of its own and no lighting; a scene light that
        /// reached them would shade one flat icon differently from the rest.
        /// </summary>
        private const int MinimapOnlyLayer = 7;

        private static int LitLayers => ~(1 << MinimapOnlyLayer);

        // ------------------------------------------------------------------ the look

        /// <summary>The daylight key. Cool, so the warm lanterns read as the interior's own light.</summary>
        private const float SunKelvin = 6500f;
        private const float SunIntensity = 2.2f;
        private const float SunShadowStrength = 0.55f;

        /// <summary>
        /// The sun's aim, and it matters more than its intensity. Every window in the building is on
        /// the SOUTH face (z ~ +13.2): eight `Relief_lo/hi_*` panels, the `Glass_header` and the
        /// entrance curtain wall. The recovered rotation was yaw -30°, whose forward vector is
        /// (-0.32, -0.77, +0.56) — pointing from the north, into the back of a solid plaster wall, so
        /// no daylight could enter the building at all. Yaw 180° rakes it straight in through the
        /// glazing and down the atrium.
        /// </summary>
        private static readonly Vector3 SunEuler = new Vector3(38f, 180f, 0f);

        /// <summary>
        /// Ambient is switched off the skybox and onto the three-colour gradient, whose values the
        /// scene already carried unused. Warm, and the floor the lanterns build on.
        ///
        /// This is the number that decides whether the museum is walkable. The sun is blocked by the
        /// roof almost everywhere inside, so on the ground floor ambient is doing *all* of the fill —
        /// the first pass set it around 0.16/0.10/0.045 and the interior came out near black.
        /// Raising it is also the fastest way to flatten the lanterns back out, so it is deliberately
        /// warm and still well below 0.5.
        /// </summary>
        private static readonly Color AmbientSky = new Color(0.50f, 0.48f, 0.46f);
        private static readonly Color AmbientEquator = new Color(0.44f, 0.41f, 0.38f);
        private static readonly Color AmbientGround = new Color(0.34f, 0.31f, 0.28f);

        /// <summary>Depth for the atrium's long sight lines, kept well inside the 50 m shadow distance.</summary>
        private static readonly Color FogColor = new Color(0.16f, 0.13f, 0.10f);
        private const float FogDensity = 0.0025f;

        /// <summary>How much of the skybox the interior is allowed to mirror. See ApplyEnvironment.</summary>
        private const float ReflectionIntensity = 0.8f;

        // ------------------------------------------------------------------ the building

        /// <summary>
        /// Walkable floor heights. The plate is 48 x 40 m with a rigid 5.00 m floor-to-floor, so the
        /// storeys are known numbers rather than something to discover per object.
        /// </summary>
        private static readonly float[] FloorLevels = { 1.60f, 7.70f, 12.70f, 17.70f };

        /// <summary>
        /// How far above its floor a lantern's light is allowed to sit. Five of the six L3 lanterns
        /// model at y ~24.8 over a floor at 17.70 — over 7 m up, with a range of 11, which put the
        /// floor at ~65% of the falloff and made L3 by far the darkest storey. The mesh stays where
        /// the artist put it; only the light is pulled down.
        /// </summary>
        private const float MaxLanternHeight = 3.9f;

        /// <summary>
        /// The six room slots, as world-space XZ rectangles. Taken from the floor slabs, not from
        /// object names: E/W are 17 x 14, and the N/S halves are 24 x 13 split at x 26.43.
        /// </summary>
        private static readonly (string Name, float MinX, float MaxX, float MinZ, float MaxZ)[] Zones =
        {
            ("E",   2.43f, 19.43f, -13.83f,   0.17f),
            ("W",  33.43f, 50.43f, -13.83f,   0.17f),
            ("NR",  2.43f, 26.43f, -26.83f, -13.83f),
            ("NL", 26.43f, 50.43f, -26.83f, -13.83f),
            ("SR",  2.43f, 26.43f,   0.17f,  13.17f),
            ("SL", 26.43f, 50.43f,   0.17f,  13.17f),
        };

        /// <summary>The atrium void: 14 x 14 m, open from the ground floor to the roof.</summary>
        private static readonly Vector2 AtriumCentre = new Vector2(26.43f, -6.83f);

        /// <summary>Clear height of one storey: 5.00 m floor-to-floor, less the 0.4 m slab.</summary>
        private const float StoreyHeight = 4.6f;

        /// <summary>
        /// 128 keeps each captured cubemap around 128 KB compressed. WebGL2 can only have 32
        /// reflection probes visible at once, and this rig makes 25.
        /// </summary>
        private const int ProbeResolution = 128;

        private const string ProbeFolder = "Assets/Scenes/Museum";

        /// <summary>
        /// Zone fill. Soft, wide and warm-neutral — this is what stops the museum reading as pools
        /// under lamps with black between them. It is not a fixture, so it is dimmer and much wider
        /// than a lantern and casts nothing.
        /// </summary>
        private const float FillKelvin = 3600f;
        private const float FillIntensity = 4.5f;
        private const float FillRange = 20f;
        private const float FillHeight = 3.6f;

        /// <summary>
        /// A fixture to light, matched by object name. <see cref="Suffix"/> matches the end of the
        /// name so the eighteen per-room lanterns are caught by one rule; <see cref="Prefix"/> matches
        /// the start, for the numbered sets.
        /// </summary>
        private readonly struct Fixture
        {
            public readonly string Prefix;
            public readonly string Suffix;
            public readonly float Kelvin;
            public readonly float Range;
            public readonly float Intensity;

            /// <summary>How far below the shade's centre the flame sits.</summary>
            public readonly float Drop;

            public Fixture(string prefix, string suffix, float kelvin, float range, float intensity, float drop)
            {
                Prefix = prefix;
                Suffix = suffix;
                Kelvin = kelvin;
                Range = range;
                Intensity = intensity;
                Drop = drop;
            }

            public bool Matches(string name) =>
                (Prefix == null || name.StartsWith(Prefix, System.StringComparison.Ordinal)) &&
                (Suffix == null || name.EndsWith(Suffix, System.StringComparison.Ordinal));
        }

        /// <summary>
        /// The twenty-nine fixtures. The four rules are disjoint — the interior set is named
        /// <c>Int_Lampion_1..6</c>, which does not end in <c>_Lampion</c>, so it cannot be caught by
        /// the per-room suffix rule.
        /// </summary>
        private static readonly Fixture[] Fixtures =
        {
            // The eighteen exhibit rooms, one lantern each, spread over four floors. Range has to
            // clear a 5 m storey from a ceiling mount and still pool on the floor.
            new Fixture(null, "_Lampion", 2200f, 11f, 7f, 0.25f),
            // The ground floor's six, under a taller ceiling than the rooms'.
            new Fixture("Int_Lampion_", null, 2200f, 14f, 9f, 0.3f),
            // Four low posts outside; slightly less orange than the interior lanterns.
            new Fixture("DEC_lampu_", null, 2700f, 6f, 3f, 0.15f),
            // The chandelier hanging 22 m up the atrium void — one light doing a whole storey.
            new Fixture("Lampu_Gantung_Atrium", null, 2400f, 32f, 16f, 0.6f),
        };

        /// <summary>
        /// The one material on all 24 interior lantern shades. Emission above 1 on purpose: that is
        /// what carries it over the bloom threshold and makes the shade glow rather than merely be a
        /// dark lump with light leaking out of it.
        /// </summary>
        private const string ShadeMaterial = "Assets/Material/Material_0.007.mat";
        private static readonly Color ShadeEmission = new Color(2.0f, 1.1f, 0.4f, 1f);

        /// <summary>
        /// The atrium chandelier. Its emission map is a dense field of near-white beads, which is the
        /// brightest surface in the building once the museum is dimmed — untinted it reads as a
        /// cool-white blob against the 2200 K lanterns, so the colour warms it to roughly their
        /// temperature. The map is the art; only the tint is ours.
        /// </summary>
        private const string ChandelierMaterial = "Assets/Material/Material_0.011.mat";

        /// <summary>Metals the recovery left as dielectrics. See ApplyMaterials.</summary>
        private static readonly string[] MetalMaterials =
        {
            "Assets/Material/Brass.mat",
            "Assets/Material/Gold.mat",
            "Assets/Material/MAT_Stair_Steel.mat",
        };
        private static readonly Color ChandelierEmission = new Color(1.6f, 1.0f, 0.45f, 1f);

        // ------------------------------------------------------------------ entry points

        [MenuItem("Museum/Lighting/Build")]
        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = OpenScene();

            Transform root = ResetGeneratedRoot(scene);
            IReadOnlyList<Transform> candidates = FindCandidates(scene);
            int lanterns = BuildLanterns(candidates, root);
            int fills = BuildZoneFills(root);
            int probes = BuildReflectionProbes(root);

            ApplySun(scene);
            ApplyEnvironment();
            ApplyMaterials();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            Debug.Log($"MuseumLightingBuilder: {lanterns} lantern lights, {fills} fill lights, " +
                      $"{probes} reflection probes, sun and environment applied. " +
                      "Run 'Museum/Lighting/Bake Reflection Probes' to capture them.");
        }

        [MenuItem("Museum/Lighting/Clear")]
        public static void Clear()
        {
            UnityEngine.SceneManagement.Scene scene = OpenScene();
            Transform root = FindGeneratedRoot(scene);

            if (root == null)
            {
                Debug.Log($"MuseumLightingBuilder: no '{GeneratedRoot}' to clear.");
                return;
            }

            Undo.DestroyObjectImmediate(root.gameObject);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

            Debug.Log($"MuseumLightingBuilder: '{GeneratedRoot}' removed. The sun and the environment " +
                      "settings are left as they are — re-run Build to restore them.");
        }

        // ------------------------------------------------------------------ the sun

        /// <summary>
        /// The scene's one directional light is <b>adopted</b>, never recreated: it is the only light
        /// that survived the recovery, other scenes' tooling may reference it, and a generator that
        /// emitted a rival would leave the museum with two suns.
        /// </summary>
        private static void ApplySun(UnityEngine.SceneManagement.Scene scene)
        {
            Light sun = FindDirectionalLight(scene);

            if (sun == null)
            {
                Debug.LogWarning("MuseumLightingBuilder: no directional light in the scene; " +
                                 "the museum will have no key light. Add one and re-run.");
                return;
            }

            sun.transform.rotation = Quaternion.Euler(SunEuler);
            sun.useColorTemperature = true;
            sun.colorTemperature = SunKelvin;
            sun.color = Color.white;
            sun.intensity = SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = SunShadowStrength;
            sun.cullingMask = LitLayers;
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            // Without this the procedural skybox does not follow the light and the sky reads as a
            // different time of day from the room.
            RenderSettings.sun = sun;

            EditorUtility.SetDirty(sun);
        }

        private static Light FindDirectionalLight(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional) return light;
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ environment

        private static void ApplyEnvironment()
        {
            // The scene shipped with gradient colours it never used, because the mode was Skybox.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;

            // Default reflections stay on the skybox — the sky IS the atrium's daylight — but at full
            // strength every smooth interior surface mirrors a bright blue sky, which reads as a
            // washed-out room that no amount of pulling the lights down will fix.
            RenderSettings.reflectionIntensity = ReflectionIntensity;
        }

        /// <summary>
        /// Material values that belong to the lighting rather than to the model. Written here rather
        /// than left as hand-edited .mat files for the same reason the lights are generated: a value
        /// someone tweaked by hand is a value the next rebuild loses.
        /// </summary>
        private static void ApplyMaterials()
        {
            SetEmission(ShadeMaterial, ShadeEmission);
            SetEmission(ChandelierMaterial, ChandelierEmission);

            // Every material in this project came out of the recovery at _Metallic 0, metals included.
            // Brass (91 renderers), Gold (48) and the stair steel (42) are already smooth at 0.70-0.78,
            // but a dielectric barely picks the environment up — which is why they read as flat plastic,
            // on exactly the objects sitting under the lanterns.
            foreach (string path in MetalMaterials) SetFloatOn(path, "_Metallic", 0.9f);

            // Cut stone at 0.05 smoothness absorbs everything and stays black under any amount of fill.
            SetFloatOn("Assets/Material/Andesite.mat", "_Smoothness", 0.25f);

            AssetDatabase.SaveAssets();
        }

        private static void SetEmission(string path, Color emission)
        {
            var material = Load(path);

            if (material == null) return;

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission);

            EditorUtility.SetDirty(material);
        }

        private static void SetFloatOn(string path, string property, float value)
        {
            var material = Load(path);

            if (material == null) return;

            material.SetFloat(property, value);
            EditorUtility.SetDirty(material);
        }

        private static Material Load(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Debug.LogWarning($"MuseumLightingBuilder: '{path}' not found; it is unchanged.");
            }

            return material;
        }

        // ------------------------------------------------------------------ reflections

        /// <summary>
        /// One box-projected reflection probe per room slot per storey, plus the atrium.
        ///
        /// This is what puts a real interior in the scene's smooth surfaces. 181 renderers use Brass,
        /// Gold or the stair steel at 0.70-0.78 smoothness and 59 more are glass at ~0.95, and with no
        /// probe in the scene every one of them was mirroring the bare skybox — a blue sky, indoors.
        /// Box projection matters here because the rooms are boxes: it makes the reflection track the
        /// walls instead of sitting at infinity.
        ///
        /// The probes are created empty. `Museum/Lighting/Bake Reflection Probes` captures them, which
        /// is a separate step because it writes .exr files next to the scene.
        /// </summary>
        private static int BuildReflectionProbes(Transform root)
        {
            int built = 0;

            foreach (float floor in FloorLevels)
            {
                foreach (var zone in Zones)
                {
                    var size = new Vector3(zone.MaxX - zone.MinX, StoreyHeight, zone.MaxZ - zone.MinZ);
                    var centre = new Vector3((zone.MinX + zone.MaxX) * 0.5f,
                                             floor + StoreyHeight * 0.5f,
                                             (zone.MinZ + zone.MaxZ) * 0.5f);

                    built += Probe(root, $"{zone.Name}-{floor:0.0}", centre, size);
                }
            }

            // The atrium is a 14 x 14 shaft running the full height, so it gets one tall probe rather
            // than one per storey - a reflection of the void is the same from every level.
            built += Probe(root, "Atrium", new Vector3(AtriumCentre.x, 12f, AtriumCentre.y),
                           new Vector3(14f, 21f, 14f));

            return built;
        }

        private static int Probe(Transform root, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject($"Reflection - {name}");
            Undo.RegisterCreatedObjectUndo(go, MuseumUIStyle.UndoLabel);
            go.transform.SetParent(root, false);
            go.transform.position = centre;

            var probe = Undo.AddComponent<ReflectionProbe>(go);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = size;
            probe.center = Vector3.zero;
            probe.resolution = ProbeResolution;
            probe.cullingMask = LitLayers;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            probe.importance = 1;
            probe.intensity = 1f;

            EditorUtility.SetDirty(probe);
            return 1;
        }

        [MenuItem("Museum/Lighting/Bake Reflection Probes")]
        public static void BakeReflectionProbes()
        {
            UnityEngine.SceneManagement.Scene scene = OpenScene();
            Transform root = FindGeneratedRoot(scene);

            if (root == null)
            {
                Debug.LogWarning("MuseumLightingBuilder: no generated lighting to bake. Run Build first.");
                return;
            }

            ReflectionProbe[] probes = root.GetComponentsInChildren<ReflectionProbe>(true);
            int baked = 0;

            foreach (ReflectionProbe probe in probes)
            {
                string path = $"{ProbeFolder}/{probe.gameObject.name}.exr";

                if (Lightmapping.BakeReflectionProbe(probe, path)) baked++;
                else Debug.LogWarning($"MuseumLightingBuilder: failed to bake '{probe.gameObject.name}'.");
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();

            Debug.Log($"MuseumLightingBuilder: baked {baked} of {probes.Length} reflection probes into {ProbeFolder}.");
        }

        // ------------------------------------------------------------------ fixtures

        private static int BuildLanterns(IReadOnlyList<Transform> candidates, Transform root)
        {
            int built = 0;

            foreach (Fixture fixture in Fixtures)
            {
                int matched = 0;

                foreach (Transform candidate in candidates)
                {
                    if (!fixture.Matches(candidate.name)) continue;
                    if (!TryGetWorldBounds(candidate, out Bounds bounds)) continue;

                    Vector3 position = bounds.center + Vector3.down * fixture.Drop;
                    position.y = ClampToStorey(position.y);

                    Light light = CreateLight($"Lantern - {candidate.name}", root, position);

                    light.type = LightType.Point;
                    light.range = fixture.Range;
                    light.intensity = fixture.Intensity;
                    light.useColorTemperature = true;
                    light.colorTemperature = fixture.Kelvin;
                    light.color = Color.white;
                    // Mobile_RPAsset has additional-light shadows off; asking costs a keyword and
                    // buys nothing.
                    light.shadows = LightShadows.None;
                    light.cullingMask = LitLayers;
                    light.lightmapBakeType = LightmapBakeType.Realtime;

                    EditorUtility.SetDirty(light);
                    matched++;
                }

                if (matched == 0)
                {
                    Debug.LogWarning("MuseumLightingBuilder: no object matched " +
                                     $"'{fixture.Prefix}...{fixture.Suffix}'; those lanterns are unlit.");
                }

                built += matched;
            }

            return built;
        }

        /// <summary>
        /// The fill layer, and the reason the museum stops reading as "bright only under the lamps".
        ///
        /// The lanterns are fixtures: they mark where light *comes from*, but a 2200 K point light
        /// with an 11 m range cannot light a 24 x 13 m room, and on the ground floor all six interior
        /// lanterns sit inside the 14 x 14 m atrium footprint — leaving roughly 1,720 m² of ground
        /// floor with no light in it whatsoever. So each of the six room slots gets a soft, wide,
        /// warm-neutral fill on every storey, plus a column of them up the atrium void.
        ///
        /// These replace the ten podium accents the first pass built. Those aimed at
        /// <c>Podium_T1/T2_*</c>, and every one of those podiums turned out to sit OUTSIDE the
        /// 48 x 40 m plate — they were lighting the approach, not the museum.
        /// </summary>
        private static int BuildZoneFills(Transform root)
        {
            int built = 0;

            foreach (float floor in FloorLevels)
            {
                foreach (var zone in Zones)
                {
                    // A 24 m room cannot be covered from its centre, so the long slots get two.
                    bool longZone = (zone.MaxX - zone.MinX) > 20f;
                    float centreZ = (zone.MinZ + zone.MaxZ) * 0.5f;
                    float centreX = (zone.MinX + zone.MaxX) * 0.5f;

                    if (longZone)
                    {
                        built += Fill(root, $"{zone.Name}-{floor:0.0}-a", new Vector3(centreX - 6f, floor + FillHeight, centreZ));
                        built += Fill(root, $"{zone.Name}-{floor:0.0}-b", new Vector3(centreX + 6f, floor + FillHeight, centreZ));
                    }
                    else
                    {
                        built += Fill(root, $"{zone.Name}-{floor:0.0}", new Vector3(centreX, floor + FillHeight, centreZ));
                    }
                }
            }

            // The atrium shaft runs 21 m with nothing in it between the ground and the chandelier.
            foreach (float y in new[] { 10.5f, 15.5f })
            {
                built += Fill(root, $"Atrium-{y:0.0}", new Vector3(AtriumCentre.x, y, AtriumCentre.y));
            }

            return built;
        }

        private static int Fill(Transform root, string name, Vector3 position)
        {
            Light light = CreateLight($"Fill - {name}", root, position);

            light.type = LightType.Point;
            light.range = FillRange;
            light.intensity = FillIntensity;
            light.useColorTemperature = true;
            light.colorTemperature = FillKelvin;
            light.color = Color.white;
            light.shadows = LightShadows.None;
            light.cullingMask = LitLayers;
            light.lightmapBakeType = LightmapBakeType.Realtime;

            EditorUtility.SetDirty(light);
            return 1;
        }

        /// <summary>
        /// Pulls a light down to a sane height above whichever storey it belongs to. See
        /// <see cref="MaxLanternHeight"/> — the L3 lanterns model 7 m above their floor.
        /// </summary>
        private static float ClampToStorey(float y)
        {
            float floor = FloorLevels[0];

            foreach (float level in FloorLevels)
            {
                if (level <= y + 0.01f) floor = level;
            }

            return Mathf.Min(y, floor + MaxLanternHeight);
        }

        private static Light CreateLight(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, MuseumUIStyle.UndoLabel);

            go.transform.SetParent(parent, false);
            go.transform.position = position;

            Light light = Undo.AddComponent<Light>(go);

            // URP reads its per-light extras off this; without it the light falls back to defaults
            // that ignore the pipeline asset.
            var data = Undo.AddComponent<UniversalAdditionalLightData>(go);
            data.usePipelineSettings = true;

            return light;
        }

        // ------------------------------------------------------------------ lookup

        private static UnityEngine.SceneManagement.Scene OpenScene()
        {
            UnityEngine.SceneManagement.Scene active =
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

            return active.path == ScenePath
                ? active
                : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                      ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        private static Transform FindGeneratedRoot(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GeneratedRoot) return root.transform;
            }

            return null;
        }

        /// <summary>
        /// Clears the generated root and hands back an empty one, so a second run replaces the lights
        /// rather than doubling them.
        /// </summary>
        private static Transform ResetGeneratedRoot(UnityEngine.SceneManagement.Scene scene)
        {
            Transform existing = FindGeneratedRoot(scene);

            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);

            var go = new GameObject(GeneratedRoot);
            Undo.RegisterCreatedObjectUndo(go, MuseumUIStyle.UndoLabel);
            UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(go, scene);

            return go.transform;
        }

        /// <summary>
        /// Every object in the scene that could name a fixture, minus this generator's own. The
        /// museum's 959 meshes are flat direct children of one root, so there is no cheaper subtree
        /// to search.
        /// </summary>
        private static IReadOnlyList<Transform> FindCandidates(UnityEngine.SceneManagement.Scene scene)
        {
            var found = new List<Transform>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GeneratedRoot) continue;

                found.AddRange(root.GetComponentsInChildren<Transform>(true));
            }

            return found;
        }

        /// <summary>
        /// The world-space extent of a named fixture, gathered from itself <i>and its children</i>.
        ///
        /// Matching on the renderer alone is not enough: the four <c>DEC_lampu_*</c> posts are empty
        /// parents whose mesh sits on a child called <c>Mesh_0.017</c> and friends, so a
        /// renderer-name search silently skips them. Encapsulating children also gives the podium
        /// accents a real top edge to hang above.
        /// </summary>
        private static bool TryGetWorldBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                // Particle and UI renderers report bounds that are not the object's shape.
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }
    }
}
