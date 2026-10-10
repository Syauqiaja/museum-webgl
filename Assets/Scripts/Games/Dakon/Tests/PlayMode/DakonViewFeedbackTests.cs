using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Museum.Games.Dakon.Tests.PlayMode
{
    /// <summary>
    /// The view's half of the drop feedback: which outcome a drop reads as. The vignette itself is
    /// covered by <see cref="DakonVignettePulseTests"/>; what matters here is that a seed landing
    /// in a hole of its own kind tints green and a mismatch tints red — the one piece of the
    /// feature that can be silently inverted.
    ///
    /// The board is a local session because that is the cheapest one that answers a drop
    /// immediately; online the same <c>DropApplied</c> carries the same fields.
    /// </summary>
    public class DakonViewFeedbackTests
    {
        GameObject _root;
        DakonView _view;
        DakonVignette _vignette;
        Image _image;
        LocalDakonSession _session;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("dakon", typeof(RectTransform));

            var overlay = new GameObject("vignette", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(_root.transform, false);
            _image = overlay.GetComponent<Image>();
            _vignette = overlay.AddComponent<DakonVignette>();

            _view = _root.AddComponent<DakonView>();

            _session = new LocalDakonSession(DakonConfig.Default, rngSeed: 12345);
            _view.Bind(_session);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        /// <summary>A seed in hand whose category matches (or does not) the hole the next drop is forced into.</summary>
        string SeedFor(bool match)
        {
            SeedCategory holeType = _session.HoleTypeAt(_session.NextHoleIndex);
            foreach (var seed in _session.Hand)
                if ((seed.Category == holeType) == match)
                    return seed.Id;

            Assert.Fail($"the opening hand has no seed with match={match}");
            return null;
        }

        [UnityTest]
        public IEnumerator AMatchingDropTintsTheScreenGreen()
        {
            _session.RequestDrop(SeedFor(match: true));

            yield return null;
            yield return null;

            Assert.That(_image.color.a, Is.GreaterThan(0f), "no feedback showed at all");
            Assert.That(_image.color.g, Is.GreaterThan(_image.color.r), "a matching drop did not read as correct");
        }

        [UnityTest]
        public IEnumerator AMismatchedDropTintsTheScreenRed()
        {
            _session.RequestDrop(SeedFor(match: false));

            yield return null;
            yield return null;

            Assert.That(_image.color.a, Is.GreaterThan(0f), "no feedback showed at all");
            Assert.That(_image.color.r, Is.GreaterThan(_image.color.g), "a mismatched drop did not read as wrong");
        }
    }
}
