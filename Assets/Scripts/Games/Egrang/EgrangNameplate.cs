using TMPro;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The name floating over one racer. Put this on a small world-space canvas parented above a
    /// lane's stilt-walker.
    ///
    /// It turns to face the camera every late update rather than being parented to it: the racers
    /// are metres apart across three lanes, and a plate that only matched the camera's rotation
    /// would still read edge-on for the outside lanes.
    ///
    /// Pure view. <see cref="EgrangRace"/> sets the text when seats resolve.
    /// </summary>
    public sealed class EgrangNameplate : MonoBehaviour
    {
        [Tooltip("The label. Leave empty to find one on this object or its children.")]
        [SerializeField] private TMP_Text label;
        [Tooltip("Camera to face. Leave empty to use the main camera.")]
        [SerializeField] private Camera facing;
        [Tooltip("Hide the plate when it has no name to show, rather than leaving a blank card.")]
        [SerializeField] private bool hideWhenUnnamed = true;

        /// <summary>What the plate currently reads. Empty when nobody is in this lane.</summary>
        public string Text { get; private set; } = string.Empty;

        void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            if (facing == null) facing = Camera.main;

            SetName(Text);
        }

        /// <summary>Names the racer under this plate. An empty name hides it.</summary>
        public void SetName(string value)
        {
            Text = value ?? string.Empty;

            if (label != null) label.text = Text;
            if (hideWhenUnnamed && label != null) label.gameObject.SetActive(Text.Length > 0);
        }

        void LateUpdate()
        {
            // Main camera is looked up lazily: the Egrang scene's camera rig is built by the race,
            // which runs after this component's Awake in a scene loaded straight into play.
            if (facing == null) facing = Camera.main;
            if (facing == null) return;

            // Facing away from the camera rather than towards it, because a canvas draws on its
            // local +Z: LookAt(camera) would show the player the back of the text.
            transform.rotation = Quaternion.LookRotation(transform.position - facing.transform.position,
                                                         Vector3.up);
        }
    }
}
