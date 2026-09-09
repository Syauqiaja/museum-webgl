using System;
using NUnit.Framework;

namespace Museum.Lobby.Tests
{
    /// <summary>
    /// Codes get read aloud across a museum room and typed on a kiosk, so the alphabet excludes
    /// the characters people confuse (0/O, 1/I/L) and the input field forgives case, spaces and
    /// dashes. Characters outside the alphabet cannot appear in a real code, so they are dropped
    /// rather than guessed at.
    /// </summary>
    public class RoomCodeTests
    {
        [Test]
        public void Alphabet_ExcludesTheConfusableCharacters()
        {
            foreach (char c in "01IOL")
            {
                Assert.That(RoomCode.Alphabet, Does.Not.Contain(c.ToString()), $"'{c}' is confusable");
            }
        }

        [Test]
        public void Sanitize_UppercasesAndStripsSeparators()
        {
            Assert.That(RoomCode.Sanitize(" a b-c d e "), Is.EqualTo("ABCDE"));
        }

        [Test]
        public void Sanitize_DropsCharactersOutsideTheAlphabet()
        {
            Assert.That(RoomCode.Sanitize("AB0OD"), Is.EqualTo("ABD"));
            Assert.That(RoomCode.Sanitize("!!!"), Is.Empty);
        }

        [Test]
        public void Sanitize_NullBecomesEmpty()
        {
            Assert.That(RoomCode.Sanitize(null), Is.EqualTo(string.Empty));
        }

        [Test]
        public void IsPlausible_RequiresANonEmptySanitizedCode()
        {
            Assert.That(RoomCode.IsPlausible("abcde"), Is.True);
            Assert.That(RoomCode.IsPlausible("0OIL"), Is.False, "every character was dropped");
            Assert.That(RoomCode.IsPlausible(""), Is.False);
        }

        [Test]
        public void Generate_ProducesCodesOfTheRightLengthFromTheAlphabet()
        {
            string code = RoomCode.Generate(new Random(1234));

            Assert.That(code.Length, Is.EqualTo(RoomCode.GeneratedLength));
            foreach (char c in code)
            {
                Assert.That(RoomCode.Alphabet, Does.Contain(c.ToString()));
            }
        }
    }
}
