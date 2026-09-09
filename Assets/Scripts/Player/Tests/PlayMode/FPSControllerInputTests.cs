using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Museum.Core;

namespace Museum.Player.Tests
{
    /// <summary>
    /// The controller is the seam both schemes push through. These tests drive it by script only —
    /// no Input System devices — which is exactly how the touch source drives it in the build.
    /// </summary>
    public class FPSControllerInputTests
    {
        private GameObject _bootstrap;
        private GameObject _player;

        private SessionData GivenScheme(ControlScheme scheme)
        {
            _bootstrap = new GameObject("Bootstrap");
            SessionData session = _bootstrap.AddComponent<SessionData>();
            session.Scheme = scheme;
            return session;
        }

        private FPSController GivenPlayer()
        {
            _player = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider));
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_player.transform, false);
            return _player.AddComponent<FPSController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player);
            if (_bootstrap != null) Object.DestroyImmediate(_bootstrap);
            Cursor.lockState = CursorLockMode.None;
        }

        [UnityTest]
        public IEnumerator Look_delta_from_script_yaws_the_body()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();
            float before = controller.transform.eulerAngles.y;

            controller.AddLookDelta(new Vector2(10f, 0f));
            yield return null;

            Assert.AreNotEqual(before, controller.transform.eulerAngles.y);
        }

        [UnityTest]
        public IEnumerator Look_delta_is_consumed_so_the_camera_stops_when_the_thumb_does()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();

            controller.AddLookDelta(new Vector2(10f, 0f));
            yield return null;
            float afterDrag = controller.transform.eulerAngles.y;

            yield return null;
            Assert.AreEqual(afterDrag, controller.transform.eulerAngles.y, 0.001f);
        }

        [UnityTest]
        public IEnumerator Touch_scheme_never_locks_the_cursor()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();
            yield return null;

            Assert.IsFalse(controller.UsesCursorLock);
            Assert.AreNotEqual(CursorLockMode.Locked, Cursor.lockState);
        }

        [UnityTest]
        public IEnumerator Desktop_scheme_locks_the_cursor()
        {
            GivenScheme(ControlScheme.Desktop);
            FPSController controller = GivenPlayer();
            yield return null;

            Assert.IsTrue(controller.UsesCursorLock);
        }

        [UnityTest]
        public IEnumerator An_unchosen_scheme_behaves_as_desktop()
        {
            // Opening Museum.unity directly in the Editor never runs the picker.
            GivenScheme(ControlScheme.Unknown);
            FPSController controller = GivenPlayer();
            yield return null;

            Assert.IsTrue(controller.UsesCursorLock);
        }

        [UnityTest]
        public IEnumerator Move_input_from_script_reaches_the_rigidbody()
        {
            GivenScheme(ControlScheme.Sentuh);
            FPSController controller = GivenPlayer();

            controller.SetMoveInput(new Vector2(0f, 1f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Vector3 velocity = controller.GetComponent<Rigidbody>().linearVelocity;
            Assert.Greater(new Vector2(velocity.x, velocity.z).magnitude, 0.1f);
        }
    }
}
