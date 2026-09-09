using NUnit.Framework;
using UnityEngine;

namespace Museum.Player.Tests
{
    /// <summary>
    /// Guards the assembly split itself: the player scripts must stay somewhere a test can
    /// reference, and must keep seeing Museum.Core.
    /// </summary>
    public class FPSControllerAssemblyTests
    {
        [Test]
        public void The_controller_lives_in_the_player_assembly()
        {
            Assert.AreEqual("Museum.Player", typeof(FPSController).Assembly.GetName().Name);
        }

        [Test]
        public void The_player_assembly_can_see_the_core_assembly()
        {
            Assert.AreEqual(Museum.Core.ControlScheme.Unknown, default(Museum.Core.ControlScheme));
        }
    }
}
