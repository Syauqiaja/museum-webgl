using NUnit.Framework;
using UnityEngine;

namespace Museum.Games.Egrang.Tests.PlayMode
{
    public class EgrangRacerTests
    {
        GameObject _root;

        EgrangRacer Build(out EgrangStepMover mover)
        {
            _root = new GameObject("lane");

            var start = new GameObject("start").transform;
            var finish = new GameObject("finish").transform;
            start.position = new Vector3(0f, 0f, 0f);
            finish.position = new Vector3(0f, 0f, 25f);
            start.SetParent(_root.transform);
            finish.SetParent(_root.transform);

            var racerObject = new GameObject("racer");
            racerObject.transform.SetParent(_root.transform);
            mover = racerObject.AddComponent<EgrangStepMover>();

            var track = _root.AddComponent<EgrangRaceTrack>();
            var racer = _root.AddComponent<EgrangRacer>();
            racer.Bind(lane: 1, track: track, mover: mover, start: start, finish: finish);
            return racer;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SnapToUnits_PlacesTheRacerAStridePerUnitFromTheStart()
        {
            EgrangRacer racer = Build(out EgrangStepMover mover);

            racer.SnapToUnits(4);

            // 4 strides at 0.5 m, along start -> finish.
            Assert.That(mover.transform.position.z, Is.EqualTo(2f).Within(0.001f));
            Assert.That(racer.BankedUnits, Is.EqualTo(4));
        }

        [Test]
        public void SnapToUnits_ClampsToTheLaneEnds()
        {
            EgrangRacer racer = Build(out EgrangStepMover mover);

            racer.SnapToUnits(-5);
            Assert.That(mover.transform.position.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(racer.BankedUnits, Is.EqualTo(0));

            racer.SnapToUnits(999);
            Assert.That(mover.transform.position.z, Is.EqualTo(25f).Within(0.001f));
            Assert.That(racer.BankedUnits, Is.EqualTo(50));
        }

        [Test]
        public void SnapToUnits_OnAZeroLengthLane_StaysAtStartAndBanksZero()
        {
            _root = new GameObject("lane");

            var start = new GameObject("start").transform;
            var finish = new GameObject("finish").transform;
            start.position = new Vector3(1f, 0f, 3f);
            finish.position = new Vector3(1f, 0f, 3f);
            start.SetParent(_root.transform);
            finish.SetParent(_root.transform);

            var racerObject = new GameObject("racer");
            racerObject.transform.SetParent(_root.transform);
            var mover = racerObject.AddComponent<EgrangStepMover>();

            var track = _root.AddComponent<EgrangRaceTrack>();
            var racer = _root.AddComponent<EgrangRacer>();
            racer.Bind(lane: 1, track: track, mover: mover, start: start, finish: finish);

            racer.SnapToUnits(10);

            Assert.That(mover.transform.position.x, Is.EqualTo(start.position.x).Within(0.001f));
            Assert.That(mover.transform.position.y, Is.EqualTo(start.position.y).Within(0.001f));
            Assert.That(mover.transform.position.z, Is.EqualTo(start.position.z).Within(0.001f));
            Assert.That(racer.BankedUnits, Is.EqualTo(0));
        }

        [Test]
        public void ApplyStep_BanksTheStepsWithoutWaitingForTheAnimation()
        {
            EgrangRacer racer = Build(out EgrangStepMover _);

            racer.ApplyStep(EgrangStepResult.Full);
            racer.ApplyStep(EgrangStepResult.Half);
            racer.ApplyStep(EgrangStepResult.Fail);

            Assert.That(racer.BankedUnits, Is.EqualTo(3));
        }
    }
}
