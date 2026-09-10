using System.Collections.Generic;
using Museum.Core;
using UnityEngine;

namespace Museum.Games.Egrang
{
    /// <summary>
    /// The scene's race: three lanes, one of which is yours.
    ///
    /// This is the only object that knows which lane is local. It points the camera and the HUD at
    /// that lane, sends your presses to the session, and replays everyone else's. With no session it
    /// runs the scene offline in lane 1, which is how the race is iterated on in the editor.
    ///
    /// Local presses animate before the server has seen them, so the race feels like the
    /// single-player one; the echo either agrees, and is dropped, or disagrees, and the racer is
    /// snapped to the server's count. Remote lanes have no bar and no input.
    /// </summary>
    public sealed class EgrangRace : MonoBehaviour
    {
        [Tooltip("Lane racers, in seat order: Player 1 Point, Player 2 Point, Player 3 Point.")]
        [SerializeField] private EgrangRacer[] racers = new EgrangRacer[3];
        [Tooltip("Camera that follows the local racer.")]
        [SerializeField] private FollowCamera followCamera;
        [Tooltip("HUD strip. It is pointed at the local lane's track.")]
        [SerializeField] private EgrangProgressView progressView;
        [Tooltip("The local player's skill-check bar. Remote lanes have none.")]
        [SerializeField] private SkillCheckBar bar;
        [Tooltip("Stick selection panel, so the chosen stilt reaches the server.")]
        [SerializeField] private EgrangStickSelector stickSelector;
        [Tooltip("Results panel raised when the race ends. Optional — without it the race just stops.")]
        [SerializeField] private EgrangResultsView resultsView;
        [Tooltip("Countdown strip on the selection panel. Optional.")]
        [SerializeField] private EgrangCountdownView countdownView;
        [Tooltip("HUD list of who is racing and how far along they are. Optional.")]
        [SerializeField] private EgrangRosterView rosterView;
        [Tooltip("Name plates above the racers, in lane order. Empty entries are skipped.")]
        [SerializeField] private EgrangNameplate[] nameplates = new EgrangNameplate[0];

        [Header("Start")]
        [Tooltip("Seconds to pick a stilt when there is no server to ask — the offline scene. Online " +
                 "this number is the server's, not this one.")]
        [Min(0f)]
        [SerializeField] private float offlineCountdownSeconds = 15f;

        readonly EgrangSeating _seating = new EgrangSeating();
        IEgrangSession _session;
        int _currentLane = -1;
        bool _seatsResolvedOnce;
        bool _raceOver;

        EgrangStickProfile _stick;
        int _full;
        int _half;
        int _fail;

        // Negative until the run starts / the line is crossed, which is what tells the summary a
        // time is not yet a real time.
        float _startTime = -1f;
        float _finishTime = -1f;
        bool _resultsShown;

        // Unscaled time the race opens, or -1 when no countdown is running. Unscaled because a
        // paused or slowed scene must not stretch a countdown the server is keeping in real time.
        float _startsAt = -1f;
        bool _running;

        /// <summary>Set once something supplied a session — or promised one is coming.</summary>
        bool _bound;

        /// <summary>Set once Start has run, so a late reconnect failure knows who opens the race.</summary>
        bool _started;

        /// <summary>The racer this client drives. Lane 1 while offline.</summary>
        public EgrangRacer LocalRacer { get; private set; }

        /// <summary>Seconds left before steps count, or 0 once the race is open.</summary>
        public float CountdownRemaining => _startsAt < 0f ? 0f : Mathf.Max(0f, _startsAt - Time.unscaledTime);

        /// <summary>True once the countdown has run out and the run has been released.</summary>
        public bool IsRunning => _running;

        /// <summary>
        /// A backgrounded WebGL tab freezes <c>Update</c> and every coroutine with it, but the
        /// socket keeps receiving: each queued <c>step_taken</c> for a lane stops the last stride
        /// before it plays a single frame, so only the final one ever animates. The banked count
        /// still ends up correct — <see cref="OnStepTaken"/> applies every message — so the
        /// mismatch check that guards every other snap never trips and the drift is invisible to it.
        /// Regaining focus re-grounds every racer in the server's truth instead of trusting whatever
        /// the abandoned strides left on screen.
        /// </summary>
        public void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || _session == null || !_seatsResolvedOnce) return;

            SnapToSyncedProgress();

            // The countdown ran on unscaled time while the tab was frozen, which is not the same as
            // the server's clock ticking on regardless. Ask again rather than trust the local one.
            if (!_running) _session.RequestCountdown();
        }

        void Awake()
        {
            if (bar != null) bar.OnStepResult.AddListener(ReportLocalStep);
            if (stickSelector != null) stickSelector.OnStickSelected.AddListener(OnStickSelected);
        }

        void Start()
        {
            _started = true;

            // The net bootstrap binds (or claims) in its own Awake, before this runs. Nothing by
            // now means nobody is driving this scene from a room, so it is an offline race.
            //
            // Without this line the scene simply never starts: every field that opens a race —
            // the local lane, the picking window, the roster — is set inside Bind, and until
            // recently nothing ever called Bind(null). The stilt panel came up, the presses went
            // nowhere and the run was never released.
            if (!_bound)
            {
                Bind(null);
            }
        }

        /// <summary>
        /// Claims this race for a session that is still connecting, so <see cref="Start"/> does
        /// not open an offline race underneath it. Pair it with
        /// <see cref="ReleaseNetworkReservation"/> on every failure path, or the scene is left
        /// with no race at all.
        /// </summary>
        public void ReserveForNetwork()
        {
            _bound = true;
        }

        /// <summary>
        /// Gives up a claim whose session never arrived and opens the offline race
        /// <see cref="Start"/> would have opened. Does nothing once a session has bound.
        /// </summary>
        public void ReleaseNetworkReservation()
        {
            if (_session != null) return;

            _bound = false;

            // A reconnect can fail before Start has run. Leaving it to Start keeps this to one
            // offline race rather than one here and a second a frame later.
            if (_started) Bind(null);
        }

        void OnDestroy()
        {
            if (bar != null) bar.OnStepResult.RemoveListener(ReportLocalStep);
            if (stickSelector != null) stickSelector.OnStickSelected.RemoveListener(OnStickSelected);

            Unsubscribe();
        }

        /// <summary>Wiring from code — used by the tests.</summary>
        public void Configure(EgrangRacer[] racers, FollowCamera followCamera, EgrangProgressView progressView,
                              SkillCheckBar bar, EgrangResultsView resultsView = null,
                              EgrangStickSelector stickSelector = null, EgrangCountdownView countdownView = null,
                              EgrangRosterView rosterView = null, EgrangNameplate[] nameplates = null)
        {
            this.racers = racers;
            this.followCamera = followCamera;
            this.progressView = progressView;
            this.bar = bar;
            this.resultsView = resultsView;

            // Awake has already subscribed to whatever selector was wired in the scene — none, for a
            // race built in code — so swapping one in here has to move the listener with it.
            if (this.stickSelector != null) this.stickSelector.OnStickSelected.RemoveListener(OnStickSelected);
            this.stickSelector = stickSelector;
            if (this.stickSelector != null) this.stickSelector.OnStickSelected.AddListener(OnStickSelected);

            this.countdownView = countdownView;
            this.rosterView = rosterView;
            this.nameplates = nameplates ?? new EgrangNameplate[0];
        }

        /// <summary>Length of the offline picking window, for the tests and for a kiosk retune.</summary>
        public void SetOfflineCountdown(float seconds) => offlineCountdownSeconds = Mathf.Max(0f, seconds);

        /// <summary>
        /// Leaves the race mid-run for the museum. The results panel has its own exit for a
        /// finished race; this one is for the player who wants out before the line, and until it
        /// existed the only way off the track was the browser's back button.
        ///
        /// The name is frozen: the HUD's exit button stores it as a persistent listener in
        /// Egrang.unity, and CLAUDE.md lists it among the handler names a generated scene wires
        /// by string.
        ///
        /// Online the leave is consented, so the server withdraws this lane at once and the other
        /// two keep racing (see the server's <c>EgrangRoom.onLeave</c>); a socket merely dropped
        /// by the scene unloading would hold the seat open through the reconnection window
        /// instead. The session also clears the held room, so the next visit does not try to
        /// reconnect into a race that ended without us.
        /// </summary>
        public void BackToMuseum()
        {
            _session?.Leave();
            Unsubscribe();

            if (SessionData.Instance != null) SessionData.Instance.ClearRoomSession();

            // Playing the Egrang scene on its own has no loader — same fallback as the results
            // panel's exit, for the same reason.
            if (SceneLoader.Instance != null) SceneLoader.Instance.LoadScene(SceneReference.Museum);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneReference.Museum);
        }

        /// <summary>
        /// Attaches the race to a session, or to nothing. A null session is the offline scene: lane 1
        /// is yours and the other two stand still.
        /// </summary>
        public void Bind(IEgrangSession session)
        {
            Unsubscribe();
            _session = session;
            _bound = true;
            _currentLane = -1;
            _seatsResolvedOnce = false;
            _raceOver = false;
            _resultsShown = false;
            _finishTime = -1f;
            _startTime = -1f;
            _running = false;

            if (_session == null)
            {
                TakeLane(0);
                // Nobody to ask, so the offline scene keeps its own picking window — same shape as
                // a real race, and the only way this path is testable at all.
                BeginCountdown(offlineCountdownSeconds);
                DrawRoster();
                return;
            }

            _session.SeatsChanged += OnSeatsChanged;
            _session.StepTaken += OnStepTaken;
            _session.RaceOver += OnRaceOver;
            _session.CountdownChanged += BeginCountdown;

            _seating.SetLocal(_session.LocalSessionId);
            OnSeatsChanged();

            // The server broadcasts the countdown when the race starts, which is normally while this
            // client is still loading the scene. This is the request that actually gets it.
            _session.RequestCountdown();
        }

        /// <summary>
        /// Arms the picking window. Zero or less means the race is already open — a client that
        /// joined late, or reconnected mid-race — so the run is released at once.
        /// </summary>
        void BeginCountdown(float seconds)
        {
            if (_running) return;

            if (seconds <= 0f)
            {
                ReleaseRun();
                return;
            }

            _startsAt = Time.unscaledTime + seconds;
            if (countdownView != null) countdownView.Show(seconds);
        }

        /// <summary>
        /// Opens the race: takes the highlighted stilt for anyone who never chose one, switches the
        /// run root on, and starts the clock the results panel reports. Called when the countdown
        /// runs out, and immediately for a client that arrives after the race has already begun.
        /// </summary>
        void ReleaseRun()
        {
            if (_running) return;

            _running = true;
            _startsAt = -1f;

            if (countdownView != null) countdownView.Hide();

            if (stickSelector != null)
            {
                // Never picked, so the pole under the cursor is the pole — the selector pre-highlights
                // the easiest one, so this is always a real choice rather than nothing.
                if (stickSelector.Chosen == null) stickSelector.CommitHighlighted();
                stickSelector.ReleaseRun();
            }

            _startTime = Time.time;
        }

        /// <summary>
        /// A graded local press: animate it now, tell the server, let the echo confirm it.
        ///
        /// Once the race is over, or once this racer's own lane has reached the finish line, there
        /// is nothing left to confirm: the server rejects any further step with no echo, so without
        /// this guard the racer would keep animating past the line for as long as presses kept
        /// arriving.
        /// </summary>
        public void ReportLocalStep(EgrangStepResult result)
        {
            if (_raceOver) return;
            if (LocalRacer != null && LocalRacer.HasFinishedLane) return;

            // Normally the clock starts when the stick is committed. A scene with no selector wired
            // to this race — and the tests — would otherwise never start it at all, and a race with
            // no clock cannot report a time or notice the finish line.
            if (_startTime < 0f) _startTime = Time.time;

            Tally(result);

            if (LocalRacer != null) LocalRacer.ApplyStep(result);

            _session?.SendStep(result);
        }

        /// <summary>
        /// Counts a press that actually became a step. Counted here rather than off the bar's event
        /// so the tally cannot include presses the guards above threw away — a run that reads "24
        /// langkah" when the racer only ever took 20 is worse than no tally at all.
        /// </summary>
        void Tally(EgrangStepResult result)
        {
            switch (result)
            {
                case EgrangStepResult.Full: _full++; break;
                case EgrangStepResult.Half: _half++; break;
                default: _fail++; break;
            }
        }

        void OnStickSelected(EgrangStickProfile profile)
        {
            if (profile == null) return;

            _stick = profile;

            // The clock is not started here any more: with a picking window in front of the race,
            // committing early would otherwise count the waiting as part of your time. ReleaseRun
            // starts it, for everyone at once.
            _session?.SendStick(profile.Shape);
        }

        /// <summary>
        /// Ticks the countdown, keeps the HUD list current, stamps the crossing, and offline also
        /// ends the race: with no server there is no `game_over` to wait for, and the local line is
        /// the only finish there is.
        /// </summary>
        void Update()
        {
            if (!_running && _startsAt >= 0f)
            {
                float remaining = CountdownRemaining;

                if (countdownView != null) countdownView.Show(remaining);
                if (remaining <= 0f) ReleaseRun();
            }

            DrawRosterProgress();

            if (_finishTime >= 0f || _startTime < 0f) return;
            if (LocalRacer == null || !LocalRacer.HasFinishedLane) return;

            _finishTime = Time.time;

            if (_session != null) return;

            _raceOver = true;
            ShowResults(1, 1, null);
        }

        void OnSeatsChanged()
        {
            if (_session == null) return;

            foreach ((string sessionId, int seat) in _session.Seats)
            {
                _seating.Assign(sessionId, seat);
                _seating.SetName(sessionId, _session.DisplayNameOf(sessionId));
            }

            int lane = _seating.LocalLane;

            TakeLane(lane < 0 ? 0 : lane);

            DrawRoster();

            // Only the first time seats resolve after binding: a fresh race has every banked count
            // at 0, so this is a no-op, but a reconnect lands here with racers already under way and
            // needs placing without animating through everyone else's steps to get there.
            if (!_seatsResolvedOnce)
            {
                _seatsResolvedOnce = true;
                SnapToSyncedProgress();
            }
        }

        /// <summary>
        /// What to call the racer in a lane: their nickname, or "Pemain 2" when there is none —
        /// offline, or a lobby entry that never carried one.
        /// </summary>
        public string NameOfLane(int lane)
        {
            string name = _seating.NameOf(lane);
            return string.IsNullOrEmpty(name) ? $"Pemain {lane + 1}" : name;
        }

        /// <summary>True when this lane is the one this client races in.</summary>
        public bool IsLocalLane(int lane) => LocalRacer != null && RacerAt(lane) == LocalRacer;

        /// <summary>
        /// Writes the names onto everything that shows them: the plates over the racers, the HUD
        /// list, and the countdown panel's roster. Called whenever seating changes, which is the
        /// only time a name can appear or move.
        /// </summary>
        void DrawRoster()
        {
            var roster = new List<EgrangStanding>();

            for (int lane = 0; lane < LaneCount; lane++)
            {
                // Offline there is one racer and no seating at all, so lane 1 is the player and the
                // other two lanes are scenery rather than empty seats.
                bool occupied = _session == null ? lane == 0 : !string.IsNullOrEmpty(_seating.SessionAt(lane));
                bool isLocal = IsLocalLane(lane);
                string label = occupied ? NameOfLane(lane) : string.Empty;

                if (nameplates != null && lane < nameplates.Length && nameplates[lane] != null)
                {
                    nameplates[lane].SetName(label);
                }

                if (rosterView != null) rosterView.SetName(lane, label, isLocal);

                if (occupied) roster.Add(new EgrangStanding(lane + 1, 0, label, isLocal));
            }

            if (countdownView != null) countdownView.SetRoster(roster);
        }

        void DrawRosterProgress()
        {
            if (rosterView == null) return;

            for (int lane = 0; lane < LaneCount; lane++)
            {
                EgrangRacer racer = RacerAt(lane);
                if (racer != null) rosterView.SetProgress(lane, racer.Progress.Normalized);
            }
        }

        int LaneCount => racers != null ? racers.Length : 0;

        void SnapToSyncedProgress()
        {
            foreach ((string sessionId, int seat) in _session.Seats)
            {
                int lane = _seating.LaneOf(sessionId);
                EgrangRacer racer = RacerAt(lane);
                if (racer == null) continue;

                racer.SnapToUnits(_session.StepUnitsOf(sessionId));
            }
        }

        void OnStepTaken(string sessionId, EgrangStepResult result, int stepUnits)
        {
            int lane = _seating.LaneOf(sessionId);
            EgrangRacer racer = RacerAt(lane);
            if (racer == null) return;

            // Your own step already played when you pressed. Take the server's count as the truth and
            // only intervene when it disagrees — replaying it would double the stride.
            if (racer == LocalRacer)
            {
                if (racer.BankedUnits != stepUnits) racer.SnapToUnits(stepUnits);
                return;
            }

            racer.ApplyStep(result);

            if (racer.BankedUnits != stepUnits) racer.SnapToUnits(stepUnits);
        }

        void OnRaceOver(IReadOnlyDictionary<string, int> places)
        {
            // Stops feeding ReportLocalStep: past this point the server sends no more echoes for
            // anyone, so there's nothing left for a local press to confirm against.
            _raceOver = true;

            var standings = new List<EgrangStanding>();
            int localPlace = 0;

            foreach (KeyValuePair<string, int> entry in places)
            {
                int lane = _seating.LaneOf(entry.Key);
                string name = _seating.NameOfSession(entry.Key);
                Debug.Log($"Egrang: lane {lane + 1} ({name}) finished {entry.Value}");

                // A place for a session that was never seated has no lane to put it against; it is
                // still logged above, but the table only lists racers the scene can name.
                if (lane >= 0)
                {
                    standings.Add(StandingFor(entry.Key, lane, entry.Value, name));
                }

                if (entry.Key == _session?.LocalSessionId) localPlace = entry.Value;
            }

            ShowResults(localPlace, standings.Count, EgrangRunSummary.Sort(standings));
        }

        /// <summary>
        /// One results row for a racer, filled in from the synced state: where they came, how far
        /// they got, and which stilt they walked on. Every racer's stilt is in `state.racers`, so
        /// the panel can name all three rather than only this client's — that is the whole reason
        /// <see cref="IEgrangSession.StickOf"/> exists.
        /// </summary>
        EgrangStanding StandingFor(string sessionId, int lane, int place, string name)
        {
            EgrangStickPreset stick = EgrangStickPresets.For(_session.StickOf(sessionId));

            return new EgrangStanding(
                lane + 1, place, name, _seating.IsLocal(sessionId),
                _session.StepUnitsOf(sessionId), _session.FinishUnits,
                stick.DisplayName, stick.SizeText);
        }

        /// <summary>
        /// Renders the run onto the results panel, once. `game_over` can land after the offline
        /// fallback has already shown the panel — a race bound to a session that ends locally first
        /// is the reconnect case — and a second Show would restate the same run with a place the
        /// player has already read.
        /// </summary>
        void ShowResults(int place, int racers, IReadOnlyList<EgrangStanding> standings)
        {
            if (_resultsShown || resultsView == null) return;

            _resultsShown = true;

            float seconds = _startTime >= 0f && _finishTime >= _startTime ? _finishTime - _startTime : -1f;

            resultsView.Show(new EgrangRunSummary(
                place, racers, seconds,
                _full, _half, _fail,
                _stick != null ? _stick.DisplayName : null,
                _stick != null ? _stick.SizeText : null,
                standings));
        }

        void TakeLane(int lane)
        {
            if (lane == _currentLane) return;

            EgrangRacer racer = RacerAt(lane);
            if (racer == null) return;

            _currentLane = lane;
            LocalRacer = racer;

            if (followCamera != null)
            {
                // The racer's body, not the lane root: the root is a fixed mark on the ground, so
                // following it would park the camera and never move it again.
                followCamera.Target = racer.Body;
                followCamera.SnapToTarget();
            }

            if (progressView != null) progressView.SetTrack(racer.Track);
        }

        EgrangRacer RacerAt(int lane) =>
            racers != null && lane >= 0 && lane < racers.Length ? racers[lane] : null;

        void Unsubscribe()
        {
            if (_session == null) return;

            _session.SeatsChanged -= OnSeatsChanged;
            _session.StepTaken -= OnStepTaken;
            _session.RaceOver -= OnRaceOver;
            _session.CountdownChanged -= BeginCountdown;
            _session = null;
        }
    }
}
