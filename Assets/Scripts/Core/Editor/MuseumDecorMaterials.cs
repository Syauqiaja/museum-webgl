using System.IO;
using UnityEditor;
using UnityEngine;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// The small palette of URP Lit materials the generated decor (screen frames, signage,
    /// room dressing, the lobby gallery) is built from. Created on demand under
    /// <c>Assets/Material/Decor/</c> and reused by asset path, so a re-run never spawns a
    /// second copy and a hand-tweak to one of them sticks.
    /// </summary>
    /// <remarks>
    /// Kept deliberately narrow: three woods, two metals, a matte for painted boards, a
    /// paper-ish cream, and a couple of saturated cloth colours. Anything richer than this
    /// comes in as a textured model from the Blender pipeline, not as another flat material.
    /// </remarks>
    public static class MuseumDecorMaterials
    {
        public const string Folder = "Assets/Material/Decor";

        private const string UrpLitShader = "Universal Render Pipeline/Lit";

        // Dark stained teak: bezels, plinth caps, sign frames.
        public static Material FrameWood() => Get("Frame_Wood", Hex("3A2414"), metallic: 0f, smoothness: 0.45f);

        // Warm brass-gold: the inner lip of a bezel, sign lettering plates.
        public static Material FrameGold() => Get("Frame_Gold", Hex("B8892E"), metallic: 0.85f, smoothness: 0.7f);

        // Near-black matte behind a screen, so the bezel reads as depth and not as a floating rectangle.
        public static Material Backboard() => Get("Backboard", Hex("141010"), metallic: 0f, smoothness: 0.15f);

        // Light teak for plinths, benches and prop bodies.
        public static Material Teak() => Get("Teak_Light", Hex("A27B54"), metallic: 0f, smoothness: 0.4f);

        // Bamboo: egrang stilts, bitingan sticks, blarak ribs.
        public static Material Bamboo() => Get("Bamboo", Hex("C9B36A"), metallic: 0f, smoothness: 0.35f);

        // Painted museum board: the info panels' body.
        public static Material BoardCream() => Get("Board_Cream", Hex("EFE4CC"), metallic: 0f, smoothness: 0.2f);

        // Pewter/iron: bekel balls, gatheng stones' plinth rings.
        public static Material Iron() => Get("Iron", Hex("5A5D62"), metallic: 0.9f, smoothness: 0.55f);

        // River stone: gatheng, cirak pebbles.
        public static Material Stone() => Get("Stone", Hex("8C8578"), metallic: 0f, smoothness: 0.25f);

        // Batik-red cloth accent.
        public static Material ClothRed() => Get("Cloth_Red", Hex("8E2A24"), metallic: 0f, smoothness: 0.2f);

        // Indigo cloth accent.
        public static Material ClothIndigo() => Get("Cloth_Indigo", Hex("2B3A6B"), metallic: 0f, smoothness: 0.2f);

        // Leaf green: jamuran/cublak foliage, blarak fronds.
        public static Material Leaf() => Get("Leaf", Hex("4F7A3A"), metallic: 0f, smoothness: 0.3f);

        // Terracotta: ancak trays, pots.
        public static Material Terracotta() => Get("Terracotta", Hex("B5623A"), metallic: 0f, smoothness: 0.3f);

        // Dry coconut shell: bathok halves, benthik's pit.
        public static Material CoconutHusk() => Get("Coconut_Husk", Hex("5C3B21"), metallic: 0f, smoothness: 0.2f);

        // Chalk lines drawn on the ground: engklek courts, gobak sodor pitches.
        public static Material ChalkWhite() => Get("Chalk_White", Hex("F2F0E6"), metallic: 0f, smoothness: 0.05f);

        // Cowrie/tamarind-seed ivory: dakon and bekel seeds.
        public static Material ShellIvory() => Get("Shell_Ivory", Hex("E8DBC2"), metallic: 0f, smoothness: 0.6f);

        // Black rubber: bekel ball, skipping rope.
        public static Material Rubber() => Get("Rubber", Hex("292929"), metallic: 0f, smoothness: 0.5f);

        /// <summary>Every palette entry, creating any that is missing on disk.</summary>
        public static Material[] EnsurePalette()
        {
            return new[]
            {
                FrameWood(), FrameGold(), Backboard(), Teak(), Bamboo(), BoardCream(), Iron(), Stone(),
                ClothRed(), ClothIndigo(), Leaf(), Terracotta(), CoconutHusk(), ChalkWhite(), ShellIvory(), Rubber(),
            };
        }

        /// <summary>
        /// A palette material by name, or null if it is not on disk yet. Read-only, so it is
        /// safe from inside an asset import (where <c>AssetDatabase.CreateAsset</c> is
        /// forbidden) — <c>GeneratedModelPostprocessor</c> relies on that.
        /// </summary>
        public static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");

        /// <summary>Loads or creates one material under <see cref="Folder"/>.</summary>
        public static Material Get(string name, Color color, float metallic, float smoothness)
        {
            string path = $"{Folder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError($"MuseumDecorMaterials: shader '{UrpLitShader}' not found; is URP installed?");
                return null;
            }

            EnsureFolder();

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString("#" + rgb, out Color c) ? c : Color.magenta;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;

            string parent = Path.GetDirectoryName(Folder)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", "Material");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(Folder));
        }
    }
}
