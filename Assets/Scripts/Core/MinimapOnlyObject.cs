using UnityEngine;

/// <summary>
/// Spawns a primitive that only renders on the minimap camera, not the main camera.
/// Requires main camera's culling mask to exclude "MinimapOnly" and the minimap
/// camera's culling mask to include only "MinimapOnly" (set in the Editor).
///
/// The marker is drawn <b>unlit</b>. Every scene light excludes the MinimapOnly layer — a map icon
/// shaded by whichever lantern it happens to be standing under is not a map icon — so a lit
/// material would leave the marker with nothing but the museum's deliberately dark ambient and
/// render a red dot as near-black. The material is loaded from Resources rather than serialised so
/// that it cannot be lost the way this project's inspector values were.
/// </summary>
public class MinimapOnlyObject : MonoBehaviour
{
    private const string LayerName = "MinimapOnly";
    private const string MarkerMaterial = "MinimapMarker";

    [SerializeField] private PrimitiveType shape = PrimitiveType.Sphere;
    [SerializeField] private Vector3 localScale = Vector3.one;
    [SerializeField] private Color color = Color.red;

    private void Awake()
    {
        var obj = GameObject.CreatePrimitive(shape);
        obj.name = $"{name} (Minimap Marker)";
        obj.transform.SetParent(transform, false);
        obj.transform.localScale = localScale;

        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0)
        {
            Debug.LogError($"Layer \"{LayerName}\" not found. Add it in Project Settings > Tags and Layers.");
            return;
        }
        obj.layer = layer;

        var renderer = obj.GetComponent<Renderer>();

        // CreatePrimitive hands back the pipeline's default *lit* material.
        var unlit = Resources.Load<Material>(MarkerMaterial);
        if (unlit != null) renderer.sharedMaterial = unlit;
        else Debug.LogWarning($"Material \"{MarkerMaterial}\" not found in a Resources folder; " +
                              "the minimap marker will be lit and will read as near-black.");

        // Still per-instance: assigning .color goes through .material, which clones.
        renderer.material.color = color;

        // CreatePrimitive also attaches a collider. On WebGL the engine-code stripper drops any
        // collider class no built scene references, so the AddComponent behind CreatePrimitive fails
        // and GetComponent hands back null — hence the null check rather than a bare .enabled = false.
        var primitiveCollider = obj.GetComponent<Collider>();
        if (primitiveCollider != null) Destroy(primitiveCollider); // marker is visual only
    }
}
