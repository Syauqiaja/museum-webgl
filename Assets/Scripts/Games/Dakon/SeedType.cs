using UnityEngine;

namespace Museum.Games.Dakon
{
    /// <summary>
    /// Authored definition of a seed species: display name, card art, and category. One asset
    /// per species. This is the engine-side catalog; the pure model never references it — a
    /// model <see cref="Seed"/> only carries this type's <see cref="TypeId"/>, and the view
    /// resolves TypeId -> SeedType -> sprite/name for rendering.
    /// </summary>
    [CreateAssetMenu(menuName = "Dakon/Seed Type", fileName = "SeedType")]
    public sealed class SeedType : ScriptableObject
    {
        [Tooltip("Stable id used by the model. Falls back to the asset name if left blank.")]
        [SerializeField] private string typeId;

        [SerializeField] private string displayName;
        [SerializeField] private Sprite cardSprite;
        [Tooltip("3D mesh dropped into the hole when this species is played. Null = fall back to the flat card-shrink sweep.")]
        [SerializeField] private GameObject seedPrefab;
        [SerializeField] private SeedCategory category;

        public string TypeId => string.IsNullOrEmpty(typeId) ? name : typeId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? TypeId : displayName;
        public Sprite CardSprite => cardSprite;
        public GameObject SeedPrefab => seedPrefab;
        public SeedCategory Category => category;
    }
}
