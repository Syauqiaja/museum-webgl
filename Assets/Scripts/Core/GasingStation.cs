using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The atrium's gasing sculpture, spun with Enter. Each press adds spin; it winds down on
    /// its own, with a looping hum whose pitch follows the speed. The sculpture is a set of
    /// sibling meshes (body, shoulder, stem) sharing one axis, not a hierarchy, so each part
    /// is turned about that shared axis rather than reparented under a pivot.
    /// </summary>
    public class GasingStation : GalleryStation
    {
        [Tooltip("The top's meshes, all centred on the same vertical axis. The pedestal is not one of them.")]
        [SerializeField] private Transform[] parts;

        [Tooltip("A point on the spin axis, in world space.")]
        [SerializeField] private Vector3 axisPoint;

        [SerializeField] private AudioSource source;

        [Tooltip("Degrees per second added by one press.")]
        [SerializeField] private float kick = 360f;

        [Tooltip("Top speed the sculpture will reach with repeated presses.")]
        [SerializeField] private float maxSpeed = 1080f;

        [Tooltip("Fraction of the speed lost per second.")]
        [SerializeField] private float drag = 0.35f;

        protected override string StationId => MuseumInteractions.Gasing;

        private float _speed;

        protected override void Awake()
        {
            base.Awake();
            if (source != null)
            {
                TuneSource(source);
                source.loop = true;
                source.clip = ProceduralAudio.Whir();
                source.volume = 0f;
            }
        }

        protected override void OnInteract()
        {
            _speed = Mathf.Min(maxSpeed, _speed + kick);
            if (source != null && !source.isPlaying) source.Play();
        }

        protected override void Update()
        {
            base.Update();
            if (_speed <= 0.01f) return;

            _speed *= Mathf.Max(0f, 1f - drag * Time.deltaTime);
            if (_speed < 2f) _speed = 0f;

            float step = _speed * Time.deltaTime;
            foreach (Transform part in parts)
            {
                if (part != null) part.RotateAround(axisPoint, Vector3.up, step);
            }

            if (source != null)
            {
                float k = _speed / maxSpeed;
                source.volume = Mathf.Clamp01(k * 1.6f) * 0.8f;
                source.pitch = 0.6f + k * 1.2f;
                if (_speed == 0f) source.Stop();
            }
        }
    }
}
