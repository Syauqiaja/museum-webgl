using System;
using NUnit.Framework;

namespace Museum.Games.Dakon.Tests
{
    public class DakonErrorTextTests
    {
        [Test]
        public void Every_error_has_its_own_wording()
        {
            var errors = (DakonError[])Enum.GetValues(typeof(DakonError));

            foreach (DakonError error in errors)
            {
                string message = DakonErrorText.MessageFor(error);

                Assert.IsNotEmpty(message, $"{error} has no wording");
                Assert.AreNotEqual(error.ToString(), message,
                                   $"{error} still shows the enum name to the player");
                Assert.AreNotEqual(DakonErrorText.Unknown, message,
                                   $"{error} falls through to the catch-all");
            }

            CollectionAssert.AllItemsAreUnique(Array.ConvertAll(errors, DakonErrorText.MessageFor),
                                               "two errors read the same to the player");
        }

        [Test]
        public void An_unmapped_error_falls_back_rather_than_throwing()
        {
            Assert.AreEqual(DakonErrorText.Unknown, DakonErrorText.MessageFor((DakonError)999));
        }
    }
}
