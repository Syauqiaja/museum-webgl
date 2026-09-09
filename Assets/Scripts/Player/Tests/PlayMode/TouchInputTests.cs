using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Museum.Core;

namespace Museum.Player.Tests
{
    /// <summary>
    /// Drives the touch widgets through the same uGUI event interfaces a real finger drives them
    /// through, and checks what reaches the controller.
    /// </summary>
    public class TouchInputTests
    {
        private GameObject _bootstrap;
        private GameObject _root;

        private SessionData GivenScheme(ControlScheme scheme)
        {
            _bootstrap = new GameObject("Bootstrap");
            SessionData session = _bootstrap.AddComponent<SessionData>();
            session.Scheme = scheme;
            return session;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_bootstrap != null) Object.DestroyImmediate(_bootstrap);
        }

        private static PointerEventData At(Vector2 position) =>
            new PointerEventData(EventSystem.current) { position = position };

        [Test]
        public void A_joystick_drag_past_the_dead_zone_reports_movement()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Joystick", typeof(RectTransform));
            var joystick = _root.AddComponent<TouchJoystick>();

            joystick.OnPointerDown(At(new Vector2(100f, 100f)));
            joystick.OnDrag(At(new Vector2(300f, 100f)));

            Assert.Greater(joystick.Value.x, 0.5f);
        }

        [Test]
        public void Releasing_the_joystick_zeroes_it()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Joystick", typeof(RectTransform));
            var joystick = _root.AddComponent<TouchJoystick>();

            joystick.OnPointerDown(At(new Vector2(100f, 100f)));
            joystick.OnDrag(At(new Vector2(300f, 100f)));
            joystick.OnPointerUp(At(new Vector2(300f, 100f)));

            Assert.AreEqual(Vector2.zero, joystick.Value);
        }

        [Test]
        public void A_look_drag_is_reported_once_then_gone()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Look", typeof(RectTransform));
            var look = _root.AddComponent<TouchLookArea>();

            look.OnDrag(new PointerEventData(EventSystem.current) { delta = new Vector2(12f, 0f) });

            Assert.Greater(look.ConsumeLook().x, 0f);
            Assert.AreEqual(Vector2.zero, look.ConsumeLook());
        }

        [Test]
        public void TouchOnly_hides_itself_under_the_desktop_scheme()
        {
            GivenScheme(ControlScheme.Desktop);
            _root = new GameObject("Overlay");
            _root.AddComponent<TouchOnly>();

            Assert.IsFalse(_root.activeSelf);
        }

        [Test]
        public void TouchOnly_stays_visible_under_the_touch_scheme()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Overlay");
            _root.AddComponent<TouchOnly>();

            Assert.IsTrue(_root.activeSelf);
        }

        [UnityTest]
        public IEnumerator The_source_feeds_joystick_and_look_into_the_controller()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider));
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_root.transform, false);
            var controller = _root.AddComponent<FPSController>();

            var joystickObject = new GameObject("Joystick", typeof(RectTransform));
            var joystick = joystickObject.AddComponent<TouchJoystick>();
            var lookObject = new GameObject("Look", typeof(RectTransform));
            var look = lookObject.AddComponent<TouchLookArea>();
            joystickObject.transform.SetParent(_root.transform, false);
            lookObject.transform.SetParent(_root.transform, false);

            var source = _root.AddComponent<TouchInputSource>();
            source.Configure(controller, joystick, look);

            joystick.OnPointerDown(At(new Vector2(100f, 100f)));
            joystick.OnDrag(At(new Vector2(100f, 400f)));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Vector3 velocity = controller.GetComponent<Rigidbody>().linearVelocity;
            Assert.Greater(new Vector2(velocity.x, velocity.z).magnitude, 0.1f);
        }

        [UnityTest]
        public IEnumerator ControlSchemeSwitch_enables_exactly_one_source()
        {
            GivenScheme(ControlScheme.Sentuh);
            _root = new GameObject("Player", typeof(Rigidbody), typeof(CapsuleCollider));
            var camera = new GameObject("Camera", typeof(Camera));
            camera.transform.SetParent(_root.transform, false);
            _root.AddComponent<FPSController>();
            var touch = _root.AddComponent<TouchInputSource>();

            // The child Camera stands in for the desktop list: any Behaviour will do, and the real
            // one (FPSInputReader) needs an InputActionAsset this fixture has no reason to build.
            var desktop = camera.GetComponent<Camera>();

            var swap = _root.AddComponent<ControlSchemeSwitch>();
            swap.Configure(new Behaviour[] { desktop }, new Behaviour[] { touch });
            yield return null;

            Assert.IsTrue(touch.enabled);
            Assert.IsFalse(desktop.enabled);
        }
    }
}
