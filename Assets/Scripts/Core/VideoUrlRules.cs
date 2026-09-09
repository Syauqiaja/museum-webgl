using System;

namespace Museum.Core
{
    /// <summary>What is wrong with a video URL, or <see cref="None"/> if it is usable.</summary>
    public enum VideoUrlProblem
    {
        None,
        MissingUrl,
        NotAbsolute,
        InsecureScheme,
    }

    /// <summary>
    /// Pure rules for the museum's streamed video URLs — no UnityEngine types, so the
    /// decisions are unit-testable and the MonoBehaviour is left with nothing but playback.
    /// See Assets/Docs/boundaries.md.
    /// </summary>
    /// <remarks>
    /// The museum footage is named for humans ("Gobak Sodor.mp4", "Ancak ancak alis.mp4"),
    /// so a URL typed or pasted from a bucket listing routinely contains raw spaces. Some
    /// CDNs answer those with 400, and the browser's video element is what fetches the file
    /// on WebGL, so the URL has to be valid before it reaches <c>VideoPlayer.url</c>.
    /// </remarks>
    public static class VideoUrlRules
    {
        /// <summary>Trims and percent-encodes the spaces a pasted URL tends to carry.</summary>
        /// <remarks>
        /// Deliberately narrow: only spaces are encoded. A URL from a bucket listing is
        /// otherwise already encoded, and re-encoding it wholesale would turn every existing
        /// <c>%20</c> into <c>%2520</c>. Encoding only the space keeps the call idempotent.
        /// </remarks>
        public static string Normalize(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            return url.Trim().Replace(" ", "%20");
        }

        /// <summary>Trims a catalog key; null becomes empty so lookups never throw.</summary>
        public static string SanitizeKey(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }

        /// <summary>True when the URL is an absolute <c>https://</c> URL.</summary>
        public static bool IsSecure(string url)
        {
            return Uri.TryCreate(Normalize(url), UriKind.Absolute, out Uri parsed) &&
                   parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks a URL before it is handed to a video player.
        /// </summary>
        /// <param name="requireSecure">
        /// True for anything that ships: the client is served over <c>https://</c> and browsers
        /// block mixed-content media, often without raising a player error at all. False lets a
        /// developer point at a local <c>http://</c> file server.
        /// </param>
        public static VideoUrlProblem Validate(string url, bool requireSecure)
        {
            string normalized = Normalize(url);
            if (normalized.Length == 0)
            {
                return VideoUrlProblem.MissingUrl;
            }

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri parsed))
            {
                return VideoUrlProblem.NotAbsolute;
            }

            bool isHttp = parsed.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
            bool isHttps = parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            if (!isHttp && !isHttps)
            {
                return VideoUrlProblem.NotAbsolute;
            }

            if (requireSecure && !isHttps)
            {
                return VideoUrlProblem.InsecureScheme;
            }

            return VideoUrlProblem.None;
        }

        /// <summary>Human-readable reason for a failed <see cref="Validate"/>, for logs and build errors.</summary>
        public static string Describe(VideoUrlProblem problem)
        {
            switch (problem)
            {
                case VideoUrlProblem.None:
                    return "ok";
                case VideoUrlProblem.MissingUrl:
                    return "no URL configured";
                case VideoUrlProblem.NotAbsolute:
                    return "not an absolute http:// or https:// URL";
                case VideoUrlProblem.InsecureScheme:
                    return "not https:// — the client is served over https:// and browsers block " +
                           "mixed-content media";
                default:
                    return problem.ToString();
            }
        }
    }
}
