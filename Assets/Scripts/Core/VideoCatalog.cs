using System;
using UnityEngine;

namespace Museum.Core
{
    /// <summary>
    /// The one place a museum video URL is written down. Scene objects carry a key
    /// ("DAKON.mp4"); the URL behind it lives here, so no absolute URL is ever serialized
    /// into a scene. Mirrors <see cref="ServerConfig"/>. Create via:
    /// Assets → Create → Museum → Video Catalog.
    /// </summary>
    /// <remarks>
    /// The footage used to ship inside the build — 1.9 GB of source video that Unity
    /// transcoded into the WebGL data file. It now streams from a CDN instead, one URL per
    /// video, so the build stays small and a clip is only fetched when a visitor walks up to
    /// that screen. URLs must be <c>https://</c> in anything that ships; see
    /// Assets/Docs/architecture.md.
    /// </remarks>
    [CreateAssetMenu(menuName = "Museum/Video Catalog", fileName = "VideoCatalog")]
    public class VideoCatalog : ScriptableObject
    {
        /// <summary>One museum video: the key a screen asks for, and where it streams from.</summary>
        [Serializable]
        public class Entry
        {
            [Tooltip("Key a screen asks for — the original asset filename, e.g. \"DAKON.mp4\".")]
            public string key;

            [Tooltip("Full URL of the video on the CDN. Empty means \"not uploaded yet\" — " +
                     "the screen shows its placeholder instead of failing.")]
            public string url;
        }

        [Tooltip("Every video the museum can play. Keys come from the migration tool; URLs are pasted in.")]
        public Entry[] entries = Array.Empty<Entry>();

        /// <summary>URL for a key, or empty when the key is unknown or has no URL yet.</summary>
        public string UrlFor(string key)
        {
            string wanted = VideoUrlRules.SanitizeKey(key);
            if (wanted.Length == 0 || entries == null)
            {
                return string.Empty;
            }

            foreach (Entry entry in entries)
            {
                if (entry != null && VideoUrlRules.SanitizeKey(entry.key) == wanted)
                {
                    return VideoUrlRules.Normalize(entry.url);
                }
            }

            return string.Empty;
        }

        /// <summary>True when the key resolves to a URL that is worth handing to a player.</summary>
        public bool HasUrlFor(string key)
        {
            return UrlFor(key).Length > 0;
        }
    }
}
