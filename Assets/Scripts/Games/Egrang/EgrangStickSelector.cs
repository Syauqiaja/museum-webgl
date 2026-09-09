using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Museum.Games.Egrang
{
    /// <summary>Raised once the player has committed to a stick.</summary>
    [Serializable]
    public sealed class EgrangStickSelectedEvent : UnityEvent<EgrangStickProfile> { }

    /// <summary>
    /// The screen the Egrang scene opens on: three stilts to look over, one to walk on. The pick
    /// sets the timing bar's difficulty — a square pole's wide green and slow sweep, a triangle's
    /// thin green and fast one — then the panel steps aside and the run begins.
    ///
    /// Put this on the selection panel's root. It owns the choice; the cards are views and the bar
    /// knows nothing about selection, so this is the only place the flow lives.
    ///
    /// The bar and the player belong under <see cref="runRoot"/>, left <b>inactive</b> in the scene.
    /// That is what keeps the player from pressing Step through the panel, without the bar needing a
    /// "not started yet" state of its own.
    /// </summary>
    public sealed class EgrangStickSelector : MonoBehaviour
    {
        [Header("Sticks")]
        [Tooltip("The selectable stilts, easiest first. One profile asset per cross-section.")]
        [SerializeField] private EgrangStickProfile[] profiles = Array.Empty<EgrangStickProfile>();
        [Tooltip("Cards to fill in, in the same order as the profiles above. Spare cards are hidden; spare profiles go unshown.")]
        [SerializeField] private EgrangStickCard[] cards = Array.Empty<EgrangStickCard>();

        [Header("Scene wiring")]
        [Tooltip("The skill-check bar to configure with the chosen stick.")]
        [SerializeField] private SkillCheckBar bar;
        [Tooltip("Panel hidden once the run starts. Leave empty to hide this object.")]
        [SerializeField] private GameObject panel;
        [Tooltip("Everything the run needs — bar UI, player rig — left inactive in the scene and switched on once a stick is chosen.")]
        [SerializeField] private GameObject runRoot;

        [Header("Start")]
        [Tooltip("Wait for EgrangRace to release the run instead of starting it on commit. On, the " +
                 "run root only switches on when the server's countdown reaches zero — which is " +
                 "what stops an early picker from stepping before the server accepts steps.")]
        [SerializeField] private bool holdForCountdown = true;

        [Header("Confirmation")]
        [Tooltip("Optional, and normally empty: the countdown commits the highlighted stick when it reaches zero. Assign a button only for a flow with no clock of its own, where the player must confirm before the run begins.")]
        [SerializeField] private Button startButton;

        [Header("Output")]
        [Tooltip("Fires once, with the committed stick. Later work — race timer, networking — listens here instead of editing this class.")]
        [SerializeField] private EgrangStickSelectedEvent onStickSelected = new EgrangStickSelectedEvent();

        /// <summary>Fires once the player commits to a stick. Also exposed in the inspector.</summary>
        public EgrangStickSelectedEvent OnStickSelected => onStickSelected;

        /// <summary>The highlighted stick, which is not yet the committed one when a start button is in use.</summary>
        public EgrangStickProfile Highlighted { get; private set; }

        /// <summary>The stick the run is being played with, or null before the player commits.</summary>
        public EgrangStickProfile Chosen { get; private set; }

        /// <summary>True once the run root is live and presses count.</summary>
        public bool Running { get; private set; }

        void Awake()
        {
            if (bar == null)
            {
                Debug.LogError($"{nameof(EgrangStickSelector)} on '{name}' has no {nameof(SkillCheckBar)} assigned, " +
                               "so a pick would change nothing.", this);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                cards[i].Bind(i < profiles.Length ? profiles[i] : null, Highlight);
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(CommitHighlighted);
                // Nothing to start until something is picked.
                startButton.interactable = false;
            }

            if (runRoot != null) runRoot.SetActive(false);

            // Pre-highlight the easiest stick so the panel is never showing a blank right-hand side,
            // and so a player who only presses Start gets the beginner's pole rather than nothing.
            if (profiles.Length > 0) Highlight(profiles[0]);
        }

        void OnDestroy()
        {
            if (startButton != null) startButton.onClick.RemoveListener(CommitHighlighted);
        }

        /// <summary>
        /// Wiring from code — used by the tests, the same way <see cref="EgrangRace.Configure"/> is.
        /// Call it before <c>Awake</c> runs, i.e. on a deactivated object, if the pre-highlight
        /// matters.
        /// </summary>
        public void Configure(SkillCheckBar bar, GameObject panel, GameObject runRoot,
                              Button startButton = null, bool holdForCountdown = true,
                              EgrangStickProfile[] profiles = null)
        {
            this.bar = bar;
            this.panel = panel;
            this.runRoot = runRoot;
            this.holdForCountdown = holdForCountdown;
            this.profiles = profiles ?? Array.Empty<EgrangStickProfile>();

            if (this.startButton != null) this.startButton.onClick.RemoveListener(CommitHighlighted);
            this.startButton = startButton;
            if (this.startButton != null) this.startButton.onClick.AddListener(CommitHighlighted);
        }

        /// <summary>
        /// Makes a stick the current pick.
        ///
        /// Whether that pick is also the decision depends on what is starting the run. With a start
        /// button assigned, the button decides. Holding for the countdown, the *countdown* decides —
        /// the clock is already on screen and it commits whatever is highlighted when it reaches
        /// zero, so a card click here only moves the highlight and the player may keep changing
        /// their mind until then. With neither, the card click is the decision and the run begins.
        /// </summary>
        public void Highlight(EgrangStickProfile profile)
        {
            if (profile == null) return;

            Highlighted = profile;

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null) cards[i].SetSelected(cards[i].Profile == profile);
            }

            if (startButton != null)
            {
                startButton.interactable = true;
                return;
            }

            // Committing here would put the panel away the instant a card is touched, leaving the
            // player watching an empty screen for the rest of the countdown — and unable to change
            // a pick they made in the first second.
            if (holdForCountdown) return;

            Commit(profile);
        }

        /// <summary>Commits whatever is currently highlighted. The countdown calls this at zero.</summary>
        public void CommitHighlighted() => Commit(Highlighted);

        /// <summary>
        /// Locks the stick in, configures the bar and puts the panel away. Ignored after the first
        /// commit — the panel is gone by then, and reconfiguring the bar mid-run would move the
        /// goalposts on a player already walking.
        ///
        /// Whether the run actually begins here depends on <see cref="holdForCountdown"/>: held,
        /// the bar and player rig stay switched off until <see cref="ReleaseRun"/>, so a player who
        /// picks early waits with everyone else instead of pressing Step into a server that is not
        /// accepting steps yet.
        /// </summary>
        public void Commit(EgrangStickProfile profile)
        {
            if (profile == null || Chosen != null) return;

            Chosen = profile;

            if (bar != null) bar.Configure(profile);

            GameObject toHide = panel != null ? panel : gameObject;
            toHide.SetActive(false);

            onStickSelected.Invoke(profile);

            if (!holdForCountdown) ReleaseRun();
        }

        /// <summary>
        /// Starts the run: switches the bar and player rig on. Called by <see cref="EgrangRace"/>
        /// when the countdown reaches zero, or straight from <see cref="Commit"/> when the panel is
        /// not holding for one. Safe to call twice.
        /// </summary>
        public void ReleaseRun()
        {
            if (Running) return;

            Running = true;
            if (runRoot != null) runRoot.SetActive(true);
        }
    }
}
