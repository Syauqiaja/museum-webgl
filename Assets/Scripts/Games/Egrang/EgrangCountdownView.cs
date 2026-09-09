using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The clock on the stilt-selection screen: how long is left to choose, and who else is in the
    /// room. Put this on the selection panel, or on a strip inside it.
    ///
    /// Pure view, like <see cref="EgrangProgressView"/> and <see cref="EgrangResultsView"/>: it
    /// draws what <see cref="EgrangRace"/> hands it and never decides that time is up. The race
    /// owns the clock because the race is what has to start when it runs out.
    /// </summary>
    public sealed class EgrangCountdownView : MonoBehaviour
    {
        [Header("Readout")]
        [Tooltip("The countdown line, e.g. \"MULAI DALAM 12\".")]
        [SerializeField] private TMP_Text countdownText;
        [Tooltip("Root hidden once the countdown reaches zero. Leave empty to use this object.")]
        [SerializeField] private GameObject root;

        [Header("Roster")]
        [Tooltip("One row per lane, filled with the racers' names. Spare rows are hidden.")]
        [SerializeField] private TMP_Text[] rosterRows = new TMP_Text[0];
        [Tooltip("Holder switched off when nobody else is in the room. Optional.")]
        [SerializeField] private GameObject rosterRoot;

        [Header("Copy")]
        [Tooltip("{0} is whole seconds remaining.")]
        [SerializeField] private string countdownFormat = "MULAI DALAM {0}";
        [Tooltip("Shown for the last moment before the race opens.")]
        [SerializeField] private string goText = "MULAI!";
        [Tooltip("Appended to this client's own roster row.")]
        [SerializeField] private string localSuffix = " (Kamu)";

        /// <summary>Seconds last drawn. Read by the tests.</summary>
        public float Seconds { get; private set; }

        /// <summary>True while the countdown strip is up.</summary>
        public bool IsShowing { get; private set; }

        void Awake()
        {
            if (root == null) root = gameObject;
        }

        /// <summary>
        /// Draws the time left. Seconds are rounded up, so "1" covers the last whole second and the
        /// readout never shows a 0 that lingers.
        /// </summary>
        public void Show(float seconds)
        {
            Seconds = Mathf.Max(0f, seconds);
            IsShowing = true;

            if (root != null) root.SetActive(true);

            if (countdownText != null)
            {
                int whole = Mathf.CeilToInt(Seconds);
                countdownText.text = whole > 0 ? string.Format(countdownFormat, whole) : goText;
            }
        }

        /// <summary>Fills the roster in. Names come from the race's seating; empty lanes are dropped.</summary>
        public void SetRoster(IReadOnlyList<EgrangStanding> racers)
        {
            int count = racers?.Count ?? 0;

            if (rosterRoot != null) rosterRoot.SetActive(count > 0);

            for (int i = 0; i < rosterRows.Length; i++)
            {
                TMP_Text row = rosterRows[i];
                if (row == null) continue;

                bool used = i < count;
                row.gameObject.SetActive(used);
                if (!used) continue;

                EgrangStanding racer = racers[i];
                row.text = racer.Label + (racer.IsLocal ? localSuffix : string.Empty);
            }
        }

        /// <summary>Takes the strip down — the countdown is over and the race is running.</summary>
        public void Hide()
        {
            IsShowing = false;
            if (root != null) root.SetActive(false);
        }
    }
}
