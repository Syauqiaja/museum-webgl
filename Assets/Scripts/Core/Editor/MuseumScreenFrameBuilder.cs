using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Hangs a gallery bezel around every exhibit screen in the Museum: a dark teak outer bar,
    /// a thin gold inner lip, and a near-black backboard behind the canvas so the video reads
    /// as set into the wall rather than floating in front of it.
    /// </summary>
    /// <remarks>
    /// Menu: <c>Museum/Decor/Build Screen Frames</c> (and <c>Clear</c>). Runs
    /// <see cref="MuseumScreenAligner"/> first so the screens are flat on their walls. Idempotent — everything
    /// goes under one scene-root container, <c>Screen Frames (Generated)</c>, which is rebuilt
    /// from scratch each run. Nothing is parented under the <c>Vid *</c> groups: those carry the
    /// video wiring that survived the recovery, and a generator should never own a hand-placed
    /// object's children.
    ///
    /// The bezel sits on the room side of the canvas (<see cref="MuseumScreenGeometry.ScreenFrame.Out"/>)
    /// by a couple of centimetres, and the backboard on the wall side. Cubes without colliders:
    /// they are 6 cm deep and 2.3 m up, nothing walks into them, and a collider there would only
    /// stop the trigger raycasts the screens rely on.
    /// </remarks>
    public static class MuseumScreenFrameBuilder
    {
        public const string RootName = "Screen Frames (Generated)";

        // Outer bar cross-section.
        private const float BarThickness = 0.09f;
        private const float BarDepth = 0.06f;

        // Gold lip just inside the bar, flush with the picture.
        private const float LipThickness = 0.018f;
        private const float LipDepth = 0.035f;

        // Backboard overhang past the picture, all sides.
        private const float BackboardMargin = 0.02f;
        private const float BackboardDepth = 0.02f;

        // Bezel front sits this far in front of the canvas plane.
        private const float FrontOffset = 0.015f;

        /// <summary>How far the bezel reaches past the picture edge, each side — what a neighbour on the wall has to clear.</summary>
        public const float Overhang = LipThickness + BarThickness;

        [MenuItem("Museum/Decor/Build Screen Frames")]
        public static void Build()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            Material wood = MuseumDecorMaterials.FrameWood();
            Material gold = MuseumDecorMaterials.FrameGold();
            Material back = MuseumDecorMaterials.Backboard();
            if (wood == null || gold == null || back == null) return;

            // Flat on the wall first, or the bezel would inherit the 15° tilt.
            MuseumScreenAligner.AlignAll(scene);

            MuseumScreenGeometry.ClearRoot(scene, RootName);
            Transform root = MuseumScreenGeometry.EnsureRoot(scene, RootName);

            List<MuseumScreenGeometry.ScreenFrame> screens = MuseumScreenGeometry.All(scene);
            foreach (MuseumScreenGeometry.ScreenFrame screen in screens)
            {
                BuildOne(screen, root, wood, gold, back);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"MuseumScreenFrameBuilder: framed {screens.Count} screens under '{RootName}'.");
        }

        [MenuItem("Museum/Decor/Clear Screen Frames")]
        public static void Clear()
        {
            Scene scene = MuseumScreenGeometry.OpenMuseum();
            if (!scene.IsValid()) return;

            if (!MuseumScreenGeometry.ClearRoot(scene, RootName))
            {
                Debug.Log("MuseumScreenFrameBuilder: nothing to clear.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildOne(MuseumScreenGeometry.ScreenFrame s, Transform root, Material wood, Material gold, Material back)
        {
            var holder = new GameObject($"Frame_{Key(s.GroupName)}");
            Undo.RegisterCreatedObjectUndo(holder, MuseumUIStyle.UndoLabel);
            holder.transform.SetParent(root, false);
            holder.transform.SetPositionAndRotation(s.Centre, Quaternion.LookRotation(s.Into, s.Up));

            float w = s.Width;
            float h = s.Height;

            // Backboard, on the wall side, slightly larger than the picture. Fits inside the
            // gap MuseumScreenAligner leaves between canvas and plaster.
            Bar(holder.transform, "Backboard", back,
                new Vector3(0f, 0f, BackboardDepth * 0.5f + 0.005f),
                new Vector3(w + 2f * BackboardMargin, h + 2f * BackboardMargin, BackboardDepth));

            // Gold lip: four thin bars hugging the picture edge.
            float lipZ = -(FrontOffset + LipDepth * 0.5f);
            float lipW = w + 2f * LipThickness;
            float lipH = h + 2f * LipThickness;
            Ring(holder.transform, "Lip", gold, lipW, lipH, LipThickness, LipDepth, lipZ);

            // Outer teak bar around the lip.
            float barZ = -(FrontOffset + BarDepth * 0.5f);
            float barW = lipW + 2f * BarThickness;
            float barH = lipH + 2f * BarThickness;
            Ring(holder.transform, "Bar", wood, barW, barH, BarThickness, BarDepth, barZ);
        }

        /// <summary>Four bars forming a rectangle of outer size (w × h), each <paramref name="t"/> thick.</summary>
        /// <remarks>Shared with <see cref="MuseumSignageBuilder"/>, so a signboard's lip is cut from the same stock as the bezel's.</remarks>
        internal static void Ring(Transform parent, string name, Material material, float w, float h, float t, float depth, float z)
        {
            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            Bar(parent, name + " Top", material, new Vector3(0f, halfH - t * 0.5f, z), new Vector3(w, t, depth));
            Bar(parent, name + " Bottom", material, new Vector3(0f, -halfH + t * 0.5f, z), new Vector3(w, t, depth));
            Bar(parent, name + " Left", material, new Vector3(-halfW + t * 0.5f, 0f, z), new Vector3(t, h - 2f * t, depth));
            Bar(parent, name + " Right", material, new Vector3(halfW - t * 0.5f, 0f, z), new Vector3(t, h - 2f * t, depth));
        }

        internal static void Bar(Transform parent, string name, Material material, Vector3 localPos, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, MuseumUIStyle.UndoLabel);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = size;

            Object.DestroyImmediate(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        /// <summary>"Vid Sluku-sluku Bathok" → "Sluku-sluku_Bathok".</summary>
        public static string Key(string groupName)
        {
            string name = groupName.StartsWith("Vid ") ? groupName.Substring(4) : groupName;
            return name.Trim().Replace(' ', '_');
        }
    }
}
