using System;
using System.Collections.Generic;

namespace Museum.Core
{
    /// <summary>
    /// The characters a player can wear: chosen on the welcome screen, sent to the server as the
    /// <c>avatar</c> join option, and rendered wherever that player appears — as a museum visitor
    /// and as an Egrang racer.
    /// </summary>
    /// <remarks>
    /// The ids are the server's <c>AVATAR_IDS</c> (<c>src/rooms/avatars.ts</c>) and must stay the
    /// same list in the same spelling: the server sanitises anything it does not know to
    /// <see cref="Default"/>, so a client-only id would silently render as Jawa for everyone else.
    /// Each id names one ASSET_NUSANTARA model (<c>Char_Jawa_L</c>, <c>Char_Bali_P</c>,
    /// <c>Char_Bugis_P</c>, <c>Char_Minang_L</c>) and one portrait in
    /// <c>Assets/Sprites/Char Avatars</c>, matched by name.
    /// </remarks>
    public static class PlayerAvatars
    {
        public const string Jawa = "jawa";
        public const string Bali = "bali";
        public const string Bugis = "bugis";
        public const string Minang = "minang";

        /// <summary>What a player wears until they choose — and what an unknown id falls back to.</summary>
        public const string Default = Jawa;

        /// <summary>Every id, in the order the welcome screen shows them.</summary>
        public static readonly IReadOnlyList<string> Ids = new[] { Jawa, Bali, Bugis, Minang };

        /// <summary>An id reduced to a known one: trimmed, lower-cased, and <see cref="Default"/> if unknown.</summary>
        public static string Sanitize(string id)
        {
            string clean = (id ?? string.Empty).Trim().ToLowerInvariant();

            foreach (string known in Ids)
            {
                if (known == clean) return known;
            }

            return Default;
        }

        /// <summary>The label under a portrait: the id capitalised ("Jawa").</summary>
        public static string DisplayName(string id)
        {
            string clean = Sanitize(id);
            return char.ToUpperInvariant(clean[0]) + clean.Substring(1);
        }

        /// <summary>
        /// The id an asset is named after — <c>"Visitor Jawa L"</c>, <c>"Char_Bali_P"</c> and
        /// <c>"Minang"</c> all resolve — or null when the name contains none.
        /// </summary>
        public static string IdInName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            foreach (string id in Ids)
            {
                if (assetName.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0) return id;
            }

            return null;
        }
    }
}
