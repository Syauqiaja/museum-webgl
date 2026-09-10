using System.Collections.Generic;
using Museum.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The screen the race ends on: where you came, how long you took, how your presses were graded
    /// and which stilt you walked on. Put this on the results panel's root.
    ///
    /// Pure view, the same deal as <see cref="EgrangProgressView"/>: it renders an
    /// <see cref="EgrangRunSummary"/> handed to it and works nothing out. <see cref="EgrangRace"/>
    /// decides when the race is over and what the numbers are — the panel cannot end a race, and
    /// cannot disagree with the server about who won.
    /// </summary>
    public sealed class EgrangResultsView : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root switched on when the results are shown. Leave empty to use this object.")]
        [SerializeField] private GameObject panel;

        [Header("Readout")]
        [Tooltip("Headline, e.g. \"SELESAI!\". Optional — it is fixed copy unless a place is known.")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("Placing line, e.g. \"Juara 2 dari 3\".")]
        [SerializeField] private TMP_Text placeText;
        [Tooltip("Run time, e.g. \"01:23.45\".")]
        [SerializeField] private TMP_Text timeText;
        [Tooltip("Step tally, e.g. \"18 penuh / 6 setengah\".")]
        [SerializeField] private TMP_Text stepsText;
        [Tooltip("Stumbles, e.g. \"4\".")]
        [SerializeField] private TMP_Text failsText;
        [Tooltip("Stick walked on, e.g. \"Egrang Persegi (Sisi 8 cm)\".")]
        [SerializeField] private TMP_Text stickText;

        [Header("Standings")]
        [Tooltip("Rows for the other lanes, one per lane. Spare rows are hidden; offline they all are.")]
        [SerializeField] private TMP_Text[] standingRows = new TMP_Text[0];
        [Tooltip("Holder switched off when there are no standings to show — offline there is one racer.")]
        [SerializeField] private GameObject standingsRoot;

        [Header("Copy")]
        [SerializeField] private string titleFormat = "SELESAI!";
        [Tooltip("{0} is the place, {1} how many raced.")]
        [SerializeField] private string placeFormat = "Juara {0} dari {1}";
        [Tooltip("Shown instead of a place when the race ended without this player finishing.")]
        [SerializeField] private string unfinishedText = "Belum sampai garis finis";
        [Tooltip("{0} is full steps, {1} half steps.")]
        [SerializeField] private string stepsFormat = "{0} penuh / {1} setengah";
        [Tooltip("{0} is the stick name, {1} its size.")]
        [SerializeField] private string stickFormat = "{0} ({1})";
        [Tooltip("{0} is the racer's name, {1} their place.")]
        [SerializeField] private string standingFormat = "{0} — juara {1}";
        [Tooltip("{0} is the racer's name, for someone with no place.")]
        [SerializeField] private string standingUnfinishedFormat = "{0} — tidak selesai";
        [Tooltip("Appended to this client's own row, e.g. \" (Kamu)\".")]
        [SerializeField] private string localSuffix = " (Kamu)";
        [Tooltip("Second line of a standings row. {0} is the stilt, {1} the distance. Either may be empty.")]
        [SerializeField] private string standingDetailFormat = "{0} · {1}";
        [Tooltip("Distance covered on a standings row. {0} is strides taken, {1} the lane's length.")]
        [SerializeField] private string standingDistanceFormat = "{0}/{1} langkah";

        [Header("Exit")]
        [Tooltip("Button back to the museum. Optional.")]
        [SerializeField] private Button exitButton;
        [Tooltip("Scene the exit button loads.")]
        [SerializeField] private string exitScene = SceneReference.Museum;

        /// <summary>True once <see cref="Show"/> has put the panel up. Read by the tests.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>What is currently on screen, for the tests to assert against.</summary>
        public EgrangRunSummary Summary { get; private set; }

        /// <summary>
        /// The root this view raises and lowers, resolved lazily.
        ///
        /// Lazily because the panel is authored **inactive** in the scene, so `Awake` does not run
        /// until something turns it on — and the thing that turns it on is <see cref="Show"/>.
        /// Reading the field through here means `Show` works before `Awake` has ever run.
        /// </summary>
        GameObject Panel => panel != null ? panel : (panel = gameObject);

        void Awake()
        {
            if (exitButton != null) exitButton.onClick.AddListener(Exit);

            // Hidden until the race ends — a panel left visible in the scene would cover the run.
            //
            // Except when Show is what woke us. A panel that starts inactive in the scene runs
            // Awake inside `SetActive(true)`, i.e. in the middle of Show, so an unconditional hide
            // here shuts the results down in the same frame the race opened them — the panel is
            // filled in, IsShowing is true, and nothing is ever on screen.
            if (!IsShowing) Panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
        }

        /// <summary>Fills the panel in and puts it up. Showing twice re-renders rather than stacking.</summary>
        public void Show(EgrangRunSummary summary)
        {
            Summary = summary;
            IsShowing = true;

            Set(titleText, titleFormat);
            Set(placeText, summary.HasPlace
                ? string.Format(placeFormat, summary.Place, Mathf.Max(summary.Racers, summary.Place))
                : unfinishedText);
            Set(timeText, summary.TimeText);
            Set(stepsText, string.Format(stepsFormat, summary.FullSteps, summary.HalfSteps));
            Set(failsText, summary.FailSteps.ToString());
            Set(stickText, string.IsNullOrEmpty(summary.StickName)
                ? string.Empty
                : string.Format(stickFormat, summary.StickName, summary.StickSize));

            DrawStandings(summary.Standings);

            Panel.SetActive(true);
        }

        /// <summary>Takes the panel back down. Only the tests and a replay need this.</summary>
        public void Hide()
        {
            IsShowing = false;
            Panel.SetActive(false);
        }

        void DrawStandings(IReadOnlyList<EgrangStanding> standings)
        {
            int count = standings?.Count ?? 0;

            // One racer is the offline scene: a standings table listing only yourself says nothing
            // the placing line above it has not already said.
            if (standingsRoot != null) standingsRoot.SetActive(count > 1);

            for (int i = 0; i < standingRows.Length; i++)
            {
                TMP_Text row = standingRows[i];
                if (row == null) continue;

                bool used = count > 1 && i < count;
                row.gameObject.SetActive(used);
                if (!used) continue;

                EgrangStanding standing = standings[i];
                string who = standing.Label + (standing.IsLocal ? localSuffix : string.Empty);
                string placing = standing.Place > 0
                    ? string.Format(standingFormat, who, standing.Place)
                    : string.Format(standingUnfinishedFormat, who);

                string detail = DetailOf(standing);
                row.text = string.IsNullOrEmpty(detail) ? placing : placing + "\n" + detail;
            }
        }

        /// <summary>
        /// The second line of a standings row: the stilt that racer walked on and how far they got.
        /// Both halves are optional — an offline run knows no lane length, and a racer whose stilt
        /// never reached the state has no name for it — so the row degrades to whichever half it
        /// has rather than printing an empty separator.
        /// </summary>
        string DetailOf(EgrangStanding standing)
        {
            string distance = standing.HasDistance
                ? string.Format(standingDistanceFormat, standing.Units, standing.FinishUnits)
                : string.Empty;

            if (string.IsNullOrEmpty(standing.StickName)) return distance;
            if (string.IsNullOrEmpty(distance)) return standing.StickName;

            return string.Format(standingDetailFormat, standing.StickName, distance);
        }

        void Exit()
        {
            if (string.IsNullOrEmpty(exitScene)) return;

            // Same reason DakonView.BackToMainMenu clears it: the race is over and its room is
            // closing, so a kept reconnection token would send the next visit to this scene
            // reconnecting into a room that no longer exists.
            if (SessionData.Instance != null) SessionData.Instance.ClearRoomSession();

            // The loader lives on the bootstrap object the lobby carries between scenes. Playing the
            // Egrang scene on its own — in the editor, or straight off a build — has no loader, and
            // "the exit button does nothing" would be a worse answer than a cut to the museum.
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(exitScene);
            else SceneManager.LoadScene(exitScene);
        }

        static void Set(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
