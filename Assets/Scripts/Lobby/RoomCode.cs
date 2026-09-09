using System;
using System.Text;

namespace Museum.Lobby
{
    /// <summary>
    /// Room-code alphabet and input cleanup. The real codes are generated **by the server** —
    /// this exists so the join field forgives how people actually type (lowercase, spaces,
    /// dashes) and so <see cref="FakeLobbyService"/> can mint codes of the same shape offline.
    ///
    /// The alphabet drops 0/O and 1/I/L because a code is read off a screen and typed by someone
    /// else across the room. Characters outside it cannot occur in a real code, so
    /// <see cref="Sanitize"/> drops them instead of inventing a mapping: there is no honest
    /// answer to "which letter did they mean by O".
    ///
    /// Length is not validated here — the server owns the code scheme
    /// (Assets/Docs/networking.md "Open items"), so the client only checks that something usable
    /// is left after cleanup and lets the server reject the rest.
    /// </summary>
    public static class RoomCode
    {
        public const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

        /// <summary>Length <see cref="Generate"/> mints. Mirrors the server's 5–6 char scheme (CLAUDE.md).</summary>
        public const int GeneratedLength = 5;

        /// <summary>Uppercase, then keep only alphabet characters.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length);

            foreach (char c in raw)
            {
                char upper = char.ToUpperInvariant(c);
                if (Alphabet.IndexOf(upper) >= 0)
                {
                    builder.Append(upper);
                }
            }

            return builder.ToString();
        }

        /// <summary>True when anything survives <see cref="Sanitize"/> — worth sending to the server.</summary>
        public static bool IsPlausible(string raw) => Sanitize(raw).Length > 0;

        /// <summary>Offline stand-in for the server's generator. Only <see cref="FakeLobbyService"/> uses it.</summary>
        public static string Generate(Random random)
        {
            var builder = new StringBuilder(GeneratedLength);

            for (int i = 0; i < GeneratedLength; i++)
            {
                builder.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            return builder.ToString();
        }
    }
}
