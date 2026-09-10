using UnityEditor;
using UnityEngine;

namespace Museum.Core.EditorTools
{
    /// <summary>
    /// Import settings for the Blender-built props under <see cref="Folder"/>. The FBXs
    /// arrive with materials named after the <see cref="MuseumDecorMaterials"/> palette
    /// (<c>Teak_Light</c>, <c>Cloth_Red</c>, …); this swaps each for the shared URP Lit asset
    /// so a prop never carries its own copy, and a hand-tweak to the palette recolours every
    /// prop at once.
    /// </summary>
    /// <remarks>
    /// The models are static decor: no rig, no animation, no blend shapes, and read/write is
    /// off so the mesh lives on the GPU only (WebGL memory is the budget that matters — see
    /// <c>Assets/Docs/asset-budget.md</c>). Colliders are decided by the decor builder, not
    /// here, because most props are too small to be worth one.
    /// </remarks>
    public sealed class GeneratedModelPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/Models/Generated/";

        private bool Applies => assetPath.StartsWith(Folder, System.StringComparison.Ordinal);

        private void OnPreprocessModel()
        {
            if (!Applies) return;
            var importer = (ModelImporter)assetImporter;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.addCollider = false;
            importer.generateSecondaryUV = false;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        }

        // Assets cannot be created during an import, so this only *loads* the palette; run
        // Museum/Decor/Reimport Generated Models after adding a palette entry so the
        // materials exist before the FBXs are (re)imported.
        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            if (!Applies) return null;
            Material shared = MuseumDecorMaterials.Load(material.name);
            if (shared == null)
            {
                Debug.LogWarning($"GeneratedModelPostprocessor: '{assetPath}' uses material '{material.name}', which is not in the decor palette on disk; keeping the imported copy. Run Museum/Decor/Reimport Generated Models.");
                return null;
            }
            return shared;
        }

        [MenuItem("Museum/Decor/Reimport Generated Models")]
        public static void ReimportAll()
        {
            MuseumDecorMaterials.EnsurePalette();
            AssetDatabase.SaveAssets();
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') });
            foreach (string guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            Debug.Log($"GeneratedModelPostprocessor: reimported {guids.Length} model(s) under {Folder}.");
        }
    }
}
