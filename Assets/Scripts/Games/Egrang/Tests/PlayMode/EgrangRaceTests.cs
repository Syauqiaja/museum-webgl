using System;
using System.Collections;
using System.Collections.Generic;
using Museum.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    public class EgrangRaceTests
    {
        sealed class FakeSession : IEgrangSession
        {
            public string LocalSessionId { get; set; } = "me";
            public List<(string sessionId, int seat)> SeatList = new List<(string, int)>();
            public IEnumerable<(string sessionId, int seat)> Seats => SeatList;
            public List<EgrangStepResult> Sent = new List<EgrangStepResult>();
            public List<EgrangStickShape> SentSticks = new List<EgrangStickShape>();
            public Dictionary<string, int> StepUnits = new Dictionary<string, int>();
            public Dictionary<string, string> Names = new Dictionary<string, string>();
            public Dictionary<string, EgrangStickShape> Sticks = new Dictionary<string, EgrangStickShape>();
            public int FinishUnits { get; set; }
            public int CountdownRequests;

            public event Action<string, EgrangStepResult, int> StepTaken;
            public event Action SeatsChanged;
            public event Action<IReadOnlyDictionary<string, int>> RaceOver;
            public event Action<float> CountdownChanged;

            public void SendStep(EgrangStepResult result) => Sent.Add(result);
            public void SendStick(EgrangStickShape shape) => SentSticks.Add(shape);
            public int StepUnitsOf(string sessionId) => StepUnits.TryGetValue(sessionId, out int units) ? units : 0;
            public EgrangStickShape StickOf(string sessionId) =>
                sessionId != null && Sticks.TryGetValue(sessionId, out EgrangStickShape shape)
                    ? shape
                    : EgrangStickShape.Persegi;
            public string DisplayNameOf(string sessionId) =>
                sessionId != null && Names.TryGetValue(sessionId, out string name) ? name : string.Empty;
            public Dictionary<string, string> Avatars = new Dictionary<string, string>();
            public string AvatarOf(string sessionId) =>
                sessionId != null && Avatars.TryGetValue(sessionId, out string avatar) ? avatar : "jawa";
            public void RequestCountdown() => CountdownRequests++;

            public void RaiseCountdown(float seconds) => CountdownChanged?.Invoke(seconds);
            public void RaiseSeats() => SeatsChanged?.Invoke();
            public void RaiseStep(string sessionId, EgrangStepResult result, int units) => StepTaken?.Invoke(sessionId, result, units);
            public void RaiseOver(IReadOnlyDictionary<string, int> places) => RaceOver?.Invoke(places);
        }

        GameObject _root;
        EgrangRace _race;
        EgrangRacer[] _racers;
        FollowCamera _camera;
        EgrangResultsView _results;
        EgrangCountdownView _countdown;
        EgrangStickSelector _selector;
        EgrangStickProfile _profile;
        GameObject _selectorRoot;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("race");
            _racers = new EgrangRacer[3];

            for (int lane = 0; lane < 3; lane++)
            {
                var laneRoot = new GameObject($"lane{lane}");
                laneRoot.transform.SetParent(_root.transform);

                var start = new GameObject("start").transform;
                var finish = new GameObject("finish").transform;
                start.SetParent(laneRoot.transform);
                finish.SetParent(laneRoot.transform);
                start.position = new Vector3(lane * 3f, 0f, 0f);
                finish.position = new Vector3(lane * 3f, 0f, 25f);

                var moverObject = new GameObject("racer");
                moverObject.transform.SetParent(laneRoot.transform);
                var mover = moverObject.AddComponent<EgrangStepMover>();

                var track = laneRoot.AddComponent<EgrangRaceTrack>();
                var racer = laneRoot.AddComponent<EgrangRacer>();
                racer.Bind(lane, track, mover, start, finish);
                _racers[lane] = racer;
            }

            _camera = new GameObject("camera").AddComponent<FollowCamera>();
            // Its own object, not the race's: the view switches its panel off on Awake, and with the
            // panel defaulting to the object it lives on that would deactivate the race with it.
            _results = new GameObject("results").AddComponent<EgrangResultsView>();
            _countdown = new GameObject("countdown").AddComponent<EgrangCountdownView>();

            // The selection panel as the scene ships it: a start button, so a card click only
            // highlights, and a run root that begins switched off.
            _selectorRoot = new GameObject("selector");
            // Built inactive so Configure lands before Awake — the selector checks its wiring there
            // and pre-highlights the first stilt.
            _selectorRoot.SetActive(false);
            var runRoot = new GameObject("run root");
            runRoot.transform.SetParent(_selectorRoot.transform);
            var startButton = new GameObject("start", typeof(RectTransform), typeof(Button))
                .GetComponent<Button>();
            startButton.transform.SetParent(_selectorRoot.transform);
            var bar = new GameObject("bar").AddComponent<SkillCheckBar>();
            bar.transform.SetParent(_selectorRoot.transform);

            _selector = _selectorRoot.AddComponent<EgrangStickSelector>();
            _selector.Configure(bar, panel: null, runRoot: runRoot, startButton: startButton);
            _selectorRoot.SetActive(true);

            _profile = ScriptableObject.CreateInstance<EgrangStickProfile>();

            _race = _root.AddComponent<EgrangRace>();
            _race.Configure(_racers, _camera, progressView: null, bar: null, resultsView: _results,
                            stickSelector: _selector, countdownView: _countdown);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_camera != null) UnityEngine.Object.DestroyImmediate(_camera.gameObject);
            if (_results != null) UnityEngine.Object.DestroyImmediate(_results.gameObject);
            if (_countdown != null) UnityEngine.Object.DestroyImmediate(_countdown.gameObject);
            if (_selectorRoot != null) UnityEngine.Object.DestroyImmediate(_selectorRoot);
            if (_profile != null) UnityEngine.Object.DestroyImmediate(_profile);
        }

        [Test]
        public void TheCameraFollowsWhicheverLaneIsLocal()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("them", 0));
            session.SeatList.Add(("me", 2));

            _race.Bind(session);
            session.RaiseSeats();

            Assert.That(_race.LocalRacer, Is.SameAs(_racers[2]));
            Assert.That(_camera.Target, Is.SameAs(_racers[2].Body));
        }

        [Test]
        public void EachSeatedLaneWearsItsPlayersAvatarAndAnEmptyLaneWearsJawa()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 2));
            session.Avatars["me"] = "bali";
            session.Avatars["them"] = "minang";

            _race.Bind(session);
            session.RaiseSeats();

            Assert.That(_race.AvatarOfLane(0), Is.EqualTo("bali"));
            Assert.That(_race.AvatarOfLane(1), Is.EqualTo("jawa"), "nobody sat in lane 2");
            Assert.That(_race.AvatarOfLane(2), Is.EqualTo("minang"));
        }

        [Test]
        public void ARemoteStepAnimatesThatLaneOnly()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseStep("them", EgrangStepResult.Full, 2);

            Assert.That(_racers[1].BankedUnits, Is.EqualTo(2));
            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void ALaneNobodySatInIsHiddenOnline()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));

            _race.Bind(session);

            Assert.That(_racers[0].Body.gameObject.activeSelf, Is.True);
            Assert.That(_racers[1].Body.gameObject.activeSelf, Is.True);
            Assert.That(_racers[2].Body.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void AFullRoomShowsEveryLane()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            session.SeatList.Add(("other", 2));

            _race.Bind(session);

            foreach (EgrangRacer racer in _racers) Assert.That(racer.Body.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void NoLaneIsHiddenBeforeAnySeatArrives()
        {
            _race.Bind(new FakeSession { LocalSessionId = "me" });

            foreach (EgrangRacer racer in _racers) Assert.That(racer.Body.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void OfflineEveryLaneStaysVisible()
        {
            _race.Bind(null);

            foreach (EgrangRacer racer in _racers) Assert.That(racer.Body.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void APlateUnderALanesRacerNamesThatLaneWhateverTheArrayOrder()
        {
            var plates = new EgrangNameplate[3];
            for (int lane = 0; lane < 3; lane++)
            {
                var plateObject = new GameObject("plate");
                plateObject.transform.SetParent(_racers[lane].Body);
                plates[lane] = plateObject.AddComponent<EgrangNameplate>();
            }

            // The order the recovered scene shipped: lanes 3, 1, 2.
            _race.Configure(_racers, _camera, progressView: null, bar: null, resultsView: _results,
                            stickSelector: _selector, countdownView: _countdown,
                            nameplates: new[] { plates[2], plates[0], plates[1] });

            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            session.Names["me"] = "Budi";
            session.Names["them"] = "Sari";

            _race.Bind(session);

            Assert.That(plates[0].Text, Is.EqualTo("Budi"));
            Assert.That(plates[1].Text, Is.EqualTo("Sari"));
            Assert.That(plates[2].Text, Is.Empty);
        }

        [Test]
        public void AnEchoOfTheLocalStepIsIgnoredWhenItAgrees()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            _race.ReportLocalStep(EgrangStepResult.Full);   // predicted: 2
            session.RaiseStep("me", EgrangStepResult.Full, 2);

            Assert.That(session.Sent, Is.EqualTo(new[] { EgrangStepResult.Full }));
            Assert.That(_racers[0].BankedUnits, Is.EqualTo(2));
        }

        [Test]
        public void AnEchoThatDisagreesSnapsTheLocalRacerBack()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            _race.ReportLocalStep(EgrangStepResult.Full);   // predicted 2, server rejected it
            session.RaiseStep("me", EgrangStepResult.Fail, 0);

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void WithNoSessionTheLocalLaneIsLaneOne()
        {
            _race.Bind(null);

            Assert.That(_race.LocalRacer, Is.SameAs(_racers[0]));
            Assert.That(_camera.Target, Is.SameAs(_racers[0].Body));
        }

        [Test]
        public void RepeatedSeatsChangedWithAnUnchangedLaneDoesNotResnapTheCamera()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            Vector3 originalCameraPosition = _camera.transform.position;

            // Simulate the camera having eased partway toward its mark, lagging behind — exactly
            // what SmoothDamp leaves it doing between patches.
            _camera.transform.position += new Vector3(1f, 2f, 3f);

            // Same seat, same lane: a patch that changes nothing about who is local.
            session.RaiseSeats();

            // If TakeLane had re-assigned Target, FollowCamera would have recaptured its offset from
            // the camera's current (moved) position, and SnapToTarget would land back on the moved
            // spot rather than the original mark.
            _camera.SnapToTarget();

            Assert.That(Vector3.Distance(_camera.transform.position, originalCameraPosition), Is.LessThan(0.001f));
        }

        [Test]
        public void ReportLocalStepStopsAnimatingAndSendingOnceTheRaceIsOver()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseOver(new Dictionary<string, int> { { "me", 1 } });

            _race.ReportLocalStep(EgrangStepResult.Full);

            Assert.That(session.Sent, Is.Empty);
            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void ReportLocalStepStopsOnceTheLocalRacerReachesTheFinishLine()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            // Lane 0 is 25 m at 0.5 m/stride: 50 strides reaches the finish exactly.
            _racers[0].SnapToUnits(50);

            _race.ReportLocalStep(EgrangStepResult.Full);

            Assert.That(session.Sent, Is.Empty);
            Assert.That(_racers[0].BankedUnits, Is.EqualTo(50));
        }

        [Test]
        public void FirstSeatsResolveAfterBindSnapsEveryRacerToItsSyncedStepUnits()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            session.StepUnits["me"] = 10;
            session.StepUnits["them"] = 30;

            _race.Bind(session);
            session.RaiseSeats();

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(10));
            Assert.That(_racers[1].BankedUnits, Is.EqualTo(30));
        }

        [Test]
        public void ASecondSeatsResolveDoesNotReSnapAlreadyMovedRacers()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.StepUnits["me"] = 10;

            _race.Bind(session);
            session.RaiseSeats();

            // The racer moves on from the reconnect snap via ordinary steps...
            session.RaiseStep("me", EgrangStepResult.Full, 12);

            // ...and a later, unrelated seats patch must not snap it back to the stale synced value.
            session.RaiseSeats();

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(12));
        }

        [Test]
        public void OnAFreshRaceEveryStepUnitsIsZeroSoTheReconnectSnapIsANoOp()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));

            _race.Bind(session);
            session.RaiseSeats();

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void RegainingFocusResnapsEveryRacerToItsSyncedStepUnits()
        {
            // A backgrounded tab freezes coroutines while step_taken messages keep landing: each one
            // stops the last, so only the final queued stride ever plays. Banked counts stay correct
            // (ApplyStep runs regardless), so the mismatch check that guards every other snap never
            // trips — regaining focus is the only thing left that can catch the drift.
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            _race.Bind(session);
            session.RaiseSeats();

            session.StepUnits["me"] = 10;
            session.StepUnits["them"] = 30;
            _racers[0].ApplyStep(EgrangStepResult.Full); // banked units matches the echo, so it never re-snaps
            _racers[1].ApplyStep(EgrangStepResult.Full); // stand-in for a stride the backgrounded coroutine ate

            _race.OnApplicationFocus(true);

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(10));
            Assert.That(_racers[1].BankedUnits, Is.EqualTo(30));
        }

        [Test]
        public void LosingFocusDoesNotResnapAnything()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            session.StepUnits["me"] = 10;

            _race.OnApplicationFocus(false);

            Assert.That(_racers[0].BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void RegainingFocusWithNoSessionDoesNothing()
        {
            _race.Bind(null);

            Assert.DoesNotThrow(() => _race.OnApplicationFocus(true));
        }

        [Test]
        public void TheRaceOverPlacesReachTheResultsPanel()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 1));
            session.SeatList.Add(("them", 0));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseOver(new Dictionary<string, int> { { "them", 1 }, { "me", 2 } });

            Assert.That(_results.IsShowing, Is.True);
            Assert.That(_results.Summary.Place, Is.EqualTo(2));
            Assert.That(_results.Summary.Racers, Is.EqualTo(2));
            // Lanes are 1-based on the panel, best first: "them" is seated in lane 1 and came first.
            Assert.That(_results.Summary.Standings[0].Lane, Is.EqualTo(1));
            Assert.That(_results.Summary.Standings[1].Lane, Is.EqualTo(2));
        }

        [Test]
        public void EveryStandingCarriesThatRacersStiltAndDistance()
        {
            var session = new FakeSession { LocalSessionId = "me", FinishUnits = 50 };
            session.SeatList.Add(("me", 0));
            session.SeatList.Add(("them", 1));
            session.StepUnits["me"] = 50;
            session.StepUnits["them"] = 31;
            session.Sticks["me"] = EgrangStickShape.Segitiga;
            session.Sticks["them"] = EgrangStickShape.Lingkaran;
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseOver(new Dictionary<string, int> { { "me", 1 }, { "them", 0 } });

            EgrangStanding mine = _results.Summary.Standings[0];
            EgrangStanding theirs = _results.Summary.Standings[1];

            Assert.That(mine.StickName, Is.EqualTo(EgrangStickPresets.Segitiga.DisplayName));
            Assert.That(mine.StickSize, Is.EqualTo(EgrangStickPresets.Segitiga.SizeText));
            Assert.That(mine.Units, Is.EqualTo(50));
            Assert.That(mine.FinishUnits, Is.EqualTo(50));
            Assert.That(mine.HasDistance, Is.True);

            // The racer who never crossed still gets a row: the panel is a summary of the whole
            // race, not only of whoever finished it.
            Assert.That(theirs.Place, Is.EqualTo(0));
            Assert.That(theirs.StickName, Is.EqualTo(EgrangStickPresets.Lingkaran.DisplayName));
            Assert.That(theirs.Units, Is.EqualTo(31));
        }

        [Test]
        public void TheStepTallyCountsOnlyPressesThatBecameSteps()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            _race.ReportLocalStep(EgrangStepResult.Full);
            _race.ReportLocalStep(EgrangStepResult.Full);
            _race.ReportLocalStep(EgrangStepResult.Half);
            _race.ReportLocalStep(EgrangStepResult.Fail);

            session.RaiseOver(new Dictionary<string, int> { { "me", 1 } });

            // Presses after the race ends are rejected by the server with no echo, so they are not
            // steps and must not appear in the tally the panel reports.
            _race.ReportLocalStep(EgrangStepResult.Full);

            Assert.That(_results.Summary.FullSteps, Is.EqualTo(2));
            Assert.That(_results.Summary.HalfSteps, Is.EqualTo(1));
            Assert.That(_results.Summary.FailSteps, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WithNoSessionCrossingTheLineShowsTheResults()
        {
            _race.Bind(null);

            // Starts the clock the way the first press does in the scene, then puts the racer on the
            // line: offline there is no game_over, so the line itself has to end the race.
            _race.ReportLocalStep(EgrangStepResult.Full);
            _racers[0].SnapToUnits(50);

            yield return null;

            Assert.That(_results.IsShowing, Is.True);
            Assert.That(_results.Summary.Place, Is.EqualTo(1));
            Assert.That(_results.Summary.Racers, Is.EqualTo(1));
            Assert.That(_results.Summary.Standings, Is.Empty);
            Assert.That(_results.Summary.Seconds, Is.GreaterThanOrEqualTo(0f));
        }

        [UnityTest]
        public IEnumerator AnUnstartedRaceNeverShowsTheResults()
        {
            _race.Bind(null);

            // Nobody has pressed anything, so the racer standing at the line — a scene whose finish
            // marker sits on the start, say — is not a finished run.
            _racers[0].SnapToUnits(50);

            yield return null;

            Assert.That(_results.IsShowing, Is.False);
        }

        [Test]
        public void TheStandingsAreNamedAfterThePlayers()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 1));
            session.SeatList.Add(("them", 0));
            session.Names["me"] = "Budi";
            session.Names["them"] = "Sari";
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseOver(new Dictionary<string, int> { { "them", 1 }, { "me", 2 } });

            Assert.That(_results.Summary.Standings[0].Name, Is.EqualTo("Sari"));
            Assert.That(_results.Summary.Standings[0].IsLocal, Is.False);
            Assert.That(_results.Summary.Standings[1].Name, Is.EqualTo("Budi"));
            Assert.That(_results.Summary.Standings[1].IsLocal, Is.True);
        }

        [Test]
        public void AnUnnamedRacerFallsBackToItsLaneNumber()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            Assert.That(_race.NameOfLane(0), Is.EqualTo("Pemain 1"));
        }

        [Test]
        public void BindingAsksTheServerHowLongIsLeft()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));

            _race.Bind(session);

            Assert.That(session.CountdownRequests, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheRunIsHeldUntilTheCountdownRunsOut()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseCountdown(0.25f);

            yield return null;

            Assert.That(_race.IsRunning, Is.False, "the run must not start while the countdown is up");
            Assert.That(_countdown.IsShowing, Is.True);
            Assert.That(_selector.Running, Is.False);

            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(_race.IsRunning, Is.True);
            Assert.That(_countdown.IsShowing, Is.False);
            Assert.That(_selector.Running, Is.True);
        }

        [UnityTest]
        public IEnumerator AtZeroTheHighlightedStickIsTakenForAPlayerWhoNeverChose()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            _selector.Highlight(_profile);
            session.RaiseCountdown(0.2f);

            yield return null;

            // Highlighting is not choosing while a start button is in play — that is the kiosk
            // behaviour the selection panel ships with.
            Assert.That(_selector.Chosen, Is.Null);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(_selector.Chosen, Is.SameAs(_profile));
            Assert.That(session.SentSticks, Is.EqualTo(new[] { _profile.Shape }));
        }

        [UnityTest]
        public IEnumerator ChoosingEarlyStillWaitsForTheCountdown()
        {
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseCountdown(0.3f);
            _selector.Commit(_profile);

            yield return null;

            // The stilt is locked in and the panel is gone, but the bar stays off: the server
            // rejects a step before its start instant, so an early presser would only collect
            // "not_started" errors.
            Assert.That(_selector.Chosen, Is.SameAs(_profile));
            Assert.That(_selector.Running, Is.False);
            Assert.That(_race.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator ACountdownThatHasAlreadyExpiredStartsTheRunAtOnce()
        {
            // What a client that joined late — or reconnected mid-race — is told.
            var session = new FakeSession { LocalSessionId = "me" };
            session.SeatList.Add(("me", 0));
            _race.Bind(session);
            session.RaiseSeats();

            session.RaiseCountdown(0f);

            yield return null;

            Assert.That(_race.IsRunning, Is.True);
            Assert.That(_selector.Running, Is.True);
        }

        [UnityTest]
        public IEnumerator OfflineTheRaceRunsItsOwnPickingWindow()
        {
            _race.SetOfflineCountdown(0.25f);

            _race.Bind(null);

            yield return null;

            Assert.That(_race.IsRunning, Is.False);
            Assert.That(_countdown.IsShowing, Is.True);

            yield return new WaitForSecondsRealtime(0.35f);

            Assert.That(_race.IsRunning, Is.True);
        }
    }
}
