using NUnit.Framework;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The avatar ids are held twice — here and in the server's <c>AVATAR_IDS</c>
    /// (<c>src/rooms/avatars.ts</c>). A drift is silent: the server rewrites an id it does not know
    /// to Jawa, so everyone else sees the wrong character and nobody sees an error.
    /// </summary>
    public class PlayerAvatarsTests
    {
        [Test]
        public void The_ids_are_the_servers_list_in_the_servers_order()
        {
            CollectionAssert.AreEqual(new[] { "jawa", "bali", "bugis", "minang" }, PlayerAvatars.Ids);
            Assert.AreEqual("jawa", PlayerAvatars.Default);
        }

        [Test]
        public void Sanitize_keeps_known_ids_and_defaults_the_rest()
        {
            Assert.AreEqual("bali", PlayerAvatars.Sanitize("bali"));
            Assert.AreEqual("minang", PlayerAvatars.Sanitize("  MINANG "));
            Assert.AreEqual("jawa", PlayerAvatars.Sanitize(null));
            Assert.AreEqual("jawa", PlayerAvatars.Sanitize(""));
            Assert.AreEqual("jawa", PlayerAvatars.Sanitize("naga"));
        }

        [Test]
        public void Asset_names_resolve_to_their_id()
        {
            Assert.AreEqual("jawa", PlayerAvatars.IdInName("Visitor Jawa L"));
            Assert.AreEqual("bali", PlayerAvatars.IdInName("Char_Bali_P"));
            Assert.AreEqual("bugis", PlayerAvatars.IdInName("Bugis"));
            Assert.IsNull(PlayerAvatars.IdInName("Egrang Player"));
            Assert.IsNull(PlayerAvatars.IdInName(null));
        }

        [Test]
        public void Display_names_are_capitalised_ids()
        {
            Assert.AreEqual("Jawa", PlayerAvatars.DisplayName("jawa"));
            Assert.AreEqual("Minang", PlayerAvatars.DisplayName("minang"));
            Assert.AreEqual("Jawa", PlayerAvatars.DisplayName("unknown"));
        }
    }
}
