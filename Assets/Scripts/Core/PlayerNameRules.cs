using System.Text;

namespace Museum.Core
{
    /// <summary>
    /// The one place a typed nickname is cleaned and checked. Pure C# and free of
    /// UnityEngine so both the MainMenu field and the lobby's fallback prompt can share it
    /// and it can be unit-tested without entering Play mode.
    ///
    /// Validity is judged on the *sanitized* string: an overlong name is not an error, it is
    /// truncated. Only a name that is still too short after trimming is rejected.
    /// </summary>
    public static class PlayerNameRules
    {
        public const int MinLength = 2;
        public const int MaxLength = 16;

        /// <summary>Trim, collapse runs of whitespace to single spaces, truncate to <see cref="MaxLength"/>.</summary>
        public static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(raw.Length);
            bool pendingSpace = false;

            foreach (char c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(c);

                if (builder.Length == MaxLength)
                {
                    break;
                }
            }

            return builder.ToString();
        }

        /// <summary>True when <see cref="Sanitize"/> leaves at least <see cref="MinLength"/> characters.</summary>
        public static bool IsValid(string raw) => Sanitize(raw).Length >= MinLength;
    }
}
