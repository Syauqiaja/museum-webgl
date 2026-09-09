using NUnit.Framework;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The museum's videos stream from a CDN, and a bad URL is invisible until a visitor walks
    /// up to that one screen. These rules are the only place a URL can be rejected early — the
    /// production build refuses to ship a catalog entry that fails <see cref="VideoUrlRules.Validate"/>.
    /// Spaces matter because most of the footage is named for humans ("Gobak Sodor.mp4").
    /// </summary>
    public class VideoUrlRulesTests
    {
        [Test]
        public void Normalize_EncodesTheSpacesInAFilename()
        {
            Assert.That(VideoUrlRules.Normalize("https://cdn.example/v/Gobak Sodor.mp4"),
                Is.EqualTo("https://cdn.example/v/Gobak%20Sodor.mp4"));
        }

        [Test]
        public void Normalize_DoesNotDoubleEncodeAnAlreadyEncodedUrl()
        {
            const string encoded = "https://cdn.example/v/Gobak%20Sodor.mp4";

            Assert.That(VideoUrlRules.Normalize(encoded), Is.EqualTo(encoded));
            Assert.That(VideoUrlRules.Normalize(VideoUrlRules.Normalize(encoded)), Is.EqualTo(encoded),
                "normalizing twice must be the same as normalizing once");
        }

        [Test]
        public void Normalize_TrimsSurroundingWhitespace()
        {
            Assert.That(VideoUrlRules.Normalize("  https://cdn.example/v/DAKON.mp4 "),
                Is.EqualTo("https://cdn.example/v/DAKON.mp4"));
        }

        [Test]
        public void Normalize_NullBecomesEmpty()
        {
            Assert.That(VideoUrlRules.Normalize(null), Is.Empty);
        }

        [Test]
        public void SanitizeKey_TrimsAndTurnsNullIntoEmpty()
        {
            Assert.That(VideoUrlRules.SanitizeKey("  DAKON.mp4 "), Is.EqualTo("DAKON.mp4"));
            Assert.That(VideoUrlRules.SanitizeKey(null), Is.Empty);
            Assert.That(VideoUrlRules.SanitizeKey("   "), Is.Empty);
        }

        [Test]
        public void IsSecure_AcceptsOnlyHttps()
        {
            Assert.That(VideoUrlRules.IsSecure("https://cdn.example/v/DAKON.mp4"), Is.True);
            Assert.That(VideoUrlRules.IsSecure("HTTPS://cdn.example/v/DAKON.mp4"), Is.True, "scheme is case-insensitive");
            Assert.That(VideoUrlRules.IsSecure("http://cdn.example/v/DAKON.mp4"), Is.False);
            Assert.That(VideoUrlRules.IsSecure(""), Is.False);
        }

        [Test]
        public void Validate_AcceptsAnHttpsUrlWithSpaces()
        {
            Assert.That(VideoUrlRules.Validate("https://cdn.example/v/Ancak ancak alis.mp4", requireSecure: true),
                Is.EqualTo(VideoUrlProblem.None));
        }

        [Test]
        public void Validate_RejectsHttpWhenSecureIsRequired()
        {
            Assert.That(VideoUrlRules.Validate("http://cdn.example/v/DAKON.mp4", requireSecure: true),
                Is.EqualTo(VideoUrlProblem.InsecureScheme));
        }

        [Test]
        public void Validate_AcceptsHttpForLocalDevelopment()
        {
            Assert.That(VideoUrlRules.Validate("http://localhost:8080/DAKON.mp4", requireSecure: false),
                Is.EqualTo(VideoUrlProblem.None));
        }

        [Test]
        public void Validate_RejectsAnEmptyUrl()
        {
            Assert.That(VideoUrlRules.Validate(null, requireSecure: true), Is.EqualTo(VideoUrlProblem.MissingUrl));
            Assert.That(VideoUrlRules.Validate("   ", requireSecure: true), Is.EqualTo(VideoUrlProblem.MissingUrl));
        }

        [Test]
        public void Validate_RejectsARelativePathOrAForeignScheme()
        {
            Assert.That(VideoUrlRules.Validate("videos/DAKON.mp4", requireSecure: false),
                Is.EqualTo(VideoUrlProblem.NotAbsolute));
            Assert.That(VideoUrlRules.Validate("file:///Users/mac/DAKON.mp4", requireSecure: false),
                Is.EqualTo(VideoUrlProblem.NotAbsolute), "the browser cannot fetch file:// from a hosted page");
        }

        [Test]
        public void Describe_ExplainsWhyHttpIsRefused()
        {
            Assert.That(VideoUrlRules.Describe(VideoUrlProblem.InsecureScheme), Does.Contain("https://"));
        }
    }
}
