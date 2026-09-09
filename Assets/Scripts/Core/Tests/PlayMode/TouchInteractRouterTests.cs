using NUnit.Framework;
using UnityEngine;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The Interaksi and ‹ › buttons have no idea which doorway or plaque the player is standing
    /// at. The router is what closes that gap, and these tests are its contract.
    /// </summary>
    public class TouchInteractRouterTests
    {
        private GameObject _router;
        private GameObject _panelObject;

        [TearDown]
        public void TearDown()
        {
            if (_router != null) Object.DestroyImmediate(_router);
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
        }

        private TouchInteractRouter GivenRouter()
        {
            _router = new GameObject("Router");
            return _router.AddComponent<TouchInteractRouter>();
        }

        [Test]
        public void Paging_with_nothing_in_range_does_nothing_and_does_not_throw()
        {
            TouchInteractRouter router = GivenRouter();
            Assert.DoesNotThrow(router.PageNext);
            Assert.DoesNotThrow(router.PagePrev);
            Assert.DoesNotThrow(router.Interact);
        }

        [Test]
        public void A_registered_reader_receives_paging()
        {
            TouchInteractRouter router = GivenRouter();

            _panelObject = new GameObject("Reader", typeof(BoxCollider));
            var reader = _panelObject.AddComponent<LessonReader>();
            TouchInteractRouter.Register(reader);

            Assert.DoesNotThrow(router.PageNext);

            TouchInteractRouter.Unregister(reader);
            Assert.IsNull(TouchInteractRouter.CurrentReader);
        }

        [Test]
        public void Unregistering_the_reader_that_is_not_current_leaves_the_current_one_alone()
        {
            TouchInteractRouter router = GivenRouter();

            _panelObject = new GameObject("Reader", typeof(BoxCollider));
            var first = _panelObject.AddComponent<LessonReader>();
            var secondObject = new GameObject("Other", typeof(BoxCollider));
            var second = secondObject.AddComponent<LessonReader>();

            TouchInteractRouter.Register(first);
            TouchInteractRouter.Unregister(second);
            Assert.AreSame(first, TouchInteractRouter.CurrentReader);

            TouchInteractRouter.Unregister(first);
            Object.DestroyImmediate(secondObject);
        }
    }
}
