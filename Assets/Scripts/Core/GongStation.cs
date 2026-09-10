using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The lobby's gong ageng, struck with Enter: a synthesised gong (<see cref="ProceduralAudio"/>)
    /// and a tabuh (mallet) that swings in to meet it. The gong itself is one of the building's
    /// merged meshes, stand and all, so it is the mallet that moves, not the gong.
    /// </summary>
    public class GongStation : GalleryStation
    {
        [Tooltip("Hinge the mallet hangs from; the shaft and head are its children.")]
        [SerializeField] private Transform mallet;

        [Tooltip("World axis the mallet swings about. Cross(gong face direction, up) swings the head into the gong.")]
        [SerializeField] private Vector3 swingAxis = Vector3.right;

        [SerializeField] private AudioSource source;
        [SerializeField] private float swingDegrees = 22f;

        protected override string StationId => MuseumInteractions.Gong;

        private Quaternion _rest;
        private float _struck = -10f;
        private bool _sounded = true;

        protected override void Awake()
        {
            base.Awake();
            if (mallet != null) _rest = mallet.rotation;
            if (source != null) TuneSource(source);
        }

        protected override void OnInteract()
        {
            _struck = Time.time;
            _sounded = false;
        }

        protected override void Update()
        {
            base.Update();

            float t = Time.time - _struck;
            if (t > 1.2f) return;

            // Swing in over 0.3 s, ring at the top of the swing, fall back with a little wobble.
            float angle = t < 0.3f
                ? swingDegrees * Mathf.Sin(Mathf.PI * t / 0.3f)
                : swingDegrees * 0.12f * Mathf.Exp(-4f * (t - 0.3f)) * Mathf.Sin(2f * Mathf.PI * 3f * (t - 0.3f));

            if (!_sounded && t >= 0.15f)
            {
                _sounded = true;
                ProceduralAudio.Play(source, ProceduralAudio.Gong());
            }

            if (mallet != null) mallet.rotation = Quaternion.AngleAxis(angle, swingAxis.normalized) * _rest;
        }
    }
}
