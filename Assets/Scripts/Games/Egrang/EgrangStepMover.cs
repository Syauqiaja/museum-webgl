using System.Collections;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// Turns a skill-check result into a step. Put this on the player object, alongside the
    /// <c>Animator</c> that drives the character — and therefore the stilts, which follow the
    /// animated hand and foot bones through <see cref="EgrangStick"/>.
    ///
    /// Wire <see cref="SkillCheckBar.OnStepResult"/> to <see cref="OnStepResult"/>. A half step is
    /// one stride; a full step is that same stride taken twice, settling between the two rather
    /// than sliding one double-length glide — the player visibly plants a foot in the middle. A
    /// fail moves nobody: just a shake in place, never a fall.
    ///
    /// Movement is code-driven: the clips are visual only and <c>applyRootMotion</c> is expected to
    /// be off, so the distance covered is exactly <see cref="stepLength"/> whatever the clips were
    /// authored to do. To hand the distance back to the animation later, delete the lerp in
    /// <see cref="StepRoutine"/> and turn root motion on.
    /// </summary>
    public sealed class EgrangStepMover : MonoBehaviour
    {
        [Header("Animation")]
        [Tooltip("Animator holding the step clips. Leave empty to search this object and its children.")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger fired on a green-zone press.")]
        [SerializeField] private string fullTrigger = "Full";
        [Tooltip("Trigger fired on a yellow-zone press.")]
        [SerializeField] private string halfTrigger = "Half";
        [Tooltip("Trigger fired on a red-zone press.")]
        [SerializeField] private string failTrigger = "Fail";

        [Header("Movement")]
        [Tooltip("Metres covered by one stride. Both step sizes use this; a full step just takes two strides.")]
        [SerializeField] private float stepLength = 0.5f;
        [Tooltip("Seconds to cover one stride. Keep this within the step clip's length so the slide reads as the animation.")]
        [SerializeField] private float moveDuration = 0.5f;
        [Tooltip("Seconds held still between the two strides of a full step. This is the mid-step plant; set it to whatever pause the Full clip has, or 0 for a continuous stride-stride.")]
        [SerializeField] private float interStrideSettle = 0.12f;

        [Header("Stumble")]
        [Tooltip("Seconds the fail shake lasts.")]
        [SerializeField] private float shakeDuration = 0.35f;
        [Tooltip("Metres of sideways wobble at the start of the shake. It decays to nothing.")]
        [SerializeField] private float shakeAmplitude = 0.06f;

        Coroutine _routine;

        /// <summary>
        /// Seconds the longest step — a full one — takes to play out: two strides with the mid-step
        /// plant between them. This is the figure <see cref="SkillCheckBar"/>'s lockout has to cover,
        /// so it is read rather than copied by hand; a lockout shorter than this lets the next press
        /// cut a step off halfway.
        /// </summary>
        public float FullStepSeconds =>
            EgrangStep.StepsFor(EgrangStepResult.Full) * Mathf.Max(0f, moveDuration)
            + Mathf.Max(0f, interStrideSettle);

        /// <summary>Metres covered by one stride. One server-side unit of race progress is exactly this.</summary>
        public float StepLength => stepLength;

        /// <summary>
        /// Puts the racer somewhere without animating, abandoning any step in flight. This is how a
        /// racer arrives at its position on reconnect, and how a mispredicted local step is repaired:
        /// the server's distance is the truth, and sliding to it would read as a step that never
        /// happened.
        /// </summary>
        public void SnapTo(Vector3 position)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            transform.position = position;
        }

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// Plays one step for the given result. Hook this up to the bar's step event.
        ///
        /// A step is assumed to run to completion: the bar's lockout is what keeps a second press
        /// from arriving mid-step, so there is no guard here. If one does arrive anyway, the step in
        /// flight is abandoned where it stands rather than fighting the new one. Size the bar's
        /// lockout against the longest case, a full step —
        /// <c>2 × moveDuration + interStrideSettle</c>.
        /// </summary>
        public void OnStepResult(EgrangStepResult result)
        {
            if (_routine != null) StopCoroutine(_routine);

            Trigger(result);
            _routine = StartCoroutine(result == EgrangStepResult.Fail
                ? ShakeRoutine()
                : StepRoutine(EgrangStep.StepsFor(result)));
        }

        void Trigger(EgrangStepResult result)
        {
            if (animator == null) return;

            string trigger;
            switch (result)
            {
                case EgrangStepResult.Full: trigger = fullTrigger; break;
                case EgrangStepResult.Half: trigger = halfTrigger; break;
                default: trigger = failTrigger; break;
            }

            if (!string.IsNullOrEmpty(trigger)) animator.SetTrigger(trigger);
        }

        /// <summary>
        /// Walks <paramref name="strides"/> strides one after another, pausing between them. Two
        /// separate strides rather than one long slide is what makes a full step read as a full
        /// step: the player covers ground, settles mid-way, then covers it again.
        /// </summary>
        IEnumerator StepRoutine(int strides)
        {
            for (int i = 0; i < strides; i++)
            {
                if (i > 0 && interStrideSettle > 0f) yield return new WaitForSeconds(interStrideSettle);

                yield return StrideRoutine();
            }

            _routine = null;
        }

        IEnumerator StrideRoutine()
        {
            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * stepLength;

            if (moveDuration > 0f)
            {
                for (float elapsed = 0f; elapsed < moveDuration; elapsed += Time.deltaTime)
                {
                    transform.position = Vector3.Lerp(start, end, elapsed / moveDuration);
                    yield return null;
                }
            }

            // Land on the exact target rather than wherever the last frame fell, so repeated
            // strides cannot accumulate drift.
            transform.position = end;
        }

        IEnumerator ShakeRoutine()
        {
            Vector3 start = transform.position;

            for (float elapsed = 0f; elapsed < shakeDuration; elapsed += Time.deltaTime)
            {
                float decay = 1f - elapsed / shakeDuration;
                Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                transform.position = start + offset * (shakeAmplitude * decay);
                yield return null;
            }

            // A stumble costs the player time, not ground: end exactly where the shake began.
            transform.position = start;
            _routine = null;
        }
    }
}
