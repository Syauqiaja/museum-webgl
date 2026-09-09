using NUnit.Framework;
using Museum.Core;

namespace Museum.Core.Tests
{
    /// <summary>
    /// In the Editor the jslib does not exist, so these exercise the fallback path only. What the
    /// real browser reports is verified by hand on a phone — see the plan's manual pass.
    /// </summary>
    public class PlatformDetectTests
    {
        [Test]
        public void LooksLikeTouchDevice_answers_without_throwing_in_the_editor()
        {
            Assert.DoesNotThrow(() => PlatformDetect.LooksLikeTouchDevice());
        }

        [Test]
        public void LooksLikeTouchDevice_is_false_on_a_desktop_editor_with_no_touchscreen()
        {
            // The Editor on macOS has neither Application.isMobilePlatform nor a Touchscreen device.
            Assert.IsFalse(PlatformDetect.LooksLikeTouchDevice());
        }

        [Test]
        public void RequestFullscreen_is_a_no_op_off_the_web()
        {
            Assert.DoesNotThrow(PlatformDetect.RequestFullscreen);
        }
    }
}
