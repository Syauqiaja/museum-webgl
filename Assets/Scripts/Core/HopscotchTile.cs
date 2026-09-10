using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// One petak of the lobby's walk-on engklek court: a floor trigger that reports the step to
    /// its <see cref="HopscotchCourse"/>, and a renderer whose emission is raised while lit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HopscotchTile : MonoBehaviour
    {
        [SerializeField] private string playerTag = "Player";

        [Tooltip("The tile face whose material lights up. Instanced at Awake so tiles light independently.")]
        [SerializeField] private Renderer face;

        [SerializeField] private Color litColor = new Color(0.757f, 0.624f, 0.380f, 1f);

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private HopscotchCourse _course;
        private int _index;
        private Material _material;
        private Color _baseColor;
        private float _litUntil = -1f;

        internal void Bind(HopscotchCourse course, int index)
        {
            _course = course;
            _index = index;
        }

        private void Awake()
        {
            if (face == null) return;
            _material = face.material;
            _baseColor = _material.HasProperty(BaseColor) ? _material.GetColor(BaseColor) : Color.white;
            _material.EnableKeyword("_EMISSION");
            _material.SetColor(EmissionColor, Color.black);
        }

        /// <summary>Raise the glow for a while.</summary>
        public void Light(float seconds)
        {
            _litUntil = Time.time + seconds;
        }

        private void Update()
        {
            if (_material == null) return;

            float remaining = _litUntil - Time.time;
            float k = remaining <= 0f ? 0f : Mathf.Clamp01(remaining / 1.5f);
            _material.SetColor(EmissionColor, litColor * (k * 1.6f));
            _material.SetColor(BaseColor, Color.Lerp(_baseColor, litColor, k * 0.6f));
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag) || _course == null) return;
            _course.Stepped(_index);
        }
    }
}
