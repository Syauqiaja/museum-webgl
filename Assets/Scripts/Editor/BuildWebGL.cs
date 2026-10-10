using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Museum.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Museum.Build.Editor
{
    /// <summary>
    /// Repeatable WebGL builds for the VPS deployment, so the shipped settings
    /// don't depend on whatever the Build Settings window happened to be left on.
    /// </summary>
    /// <remarks>
    /// Production applies the settings from
    /// <c>Assets/Docs/dev-plan.md</c> P5 — Brotli compression, explicitly-thrown
    /// exceptions only, data caching — and points <see cref="ServerConfig"/> at
    /// the production endpoint for the duration of the build. Development keeps
    /// the dev endpoint and full exception support for local debugging.
    ///
    /// Headless:
    /// <code>
    /// Unity -quit -batchmode -projectPath . \
    ///       -executeMethod Museum.Build.Editor.BuildWebGL.Production
    /// </code>
    /// Deploy the result with the runbook in the server repo's
    /// <c>deploy/README.md</c>.
    /// </remarks>
    public static class BuildWebGL
    {
        private const string OutputPath = "Builds/WebGL";
        private const string ServerConfigResource = "ServerConfig";
        private const string VideoCatalogResource = "VideoCatalog";

        [MenuItem("Museum/Build/WebGL (Production)")]
        public static void Production() => Run(development: false);

        [MenuItem("Museum/Build/WebGL (Development)")]
        public static void Development() => Run(development: true);

        private static void Run(bool development)
        {
            var scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                Fail("No enabled scenes in Build Settings — nothing to build.");
                return;
            }

            var config = Resources.Load<ServerConfig>(ServerConfigResource);
            if (config == null)
            {
                Fail($"No ServerConfig at Resources/{ServerConfigResource}. " +
                     "The build would ship with no server endpoint.");
                return;
            }

            // A production build must not carry the dev endpoint, but the asset
            // has to go back to whatever the developer had it on afterwards —
            // otherwise every build silently breaks Play mode.
            var previousUseDev = config.useDevEndpoint;
            var endpoint = development ? config.devEndpoint : config.prodEndpoint;

            if (!development && (string.IsNullOrWhiteSpace(endpoint) ||
                                 !endpoint.StartsWith("wss://", StringComparison.Ordinal)))
            {
                Fail($"ServerConfig.prodEndpoint is '{endpoint}'. A production build " +
                     "needs a wss:// URL — the client is served over https:// and " +
                     "browsers block mixed-content WebSockets.");
                return;
            }

            if (!ValidateVideoCatalog(development))
            {
                return;
            }

            ApplyPlayerSettings(development);
            config.useDevEndpoint = development;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputPath,
                    target = BuildTarget.WebGL,
                    options = development ? BuildOptions.Development : BuildOptions.None,
                });
            }
            finally
            {
                // BuildPlayer unloads unused assets, which destroys the managed
                // wrapper we loaded above — touching it here throws
                // MissingReferenceException and leaves the asset stuck on the
                // build's endpoint. Load it again instead of reusing `config`.
                var reloaded = Resources.Load<ServerConfig>(ServerConfigResource);
                if (reloaded != null)
                {
                    reloaded.useDevEndpoint = previousUseDev;
                    EditorUtility.SetDirty(reloaded);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    Debug.LogWarning(
                        $"[BuildWebGL] Could not reload Resources/{ServerConfigResource} to restore " +
                        $"useDevEndpoint={previousUseDev}. Check the asset before entering Play mode.");
                }
            }

            Report(report, endpoint);
        }

        /// <summary>
        /// The museum videos stream from a CDN instead of shipping in the build, so a bad
        /// entry is invisible until a visitor walks up to that one screen. A production build
        /// refuses to go out with an unusable URL; a development build only warns, because
        /// videos are usually the last thing uploaded.
        /// </summary>
        private static bool ValidateVideoCatalog(bool development)
        {
            var catalog = Resources.Load<VideoCatalog>(VideoCatalogResource);
            if (catalog == null)
            {
                Fail($"No VideoCatalog at Resources/{VideoCatalogResource}. Every museum screen " +
                     "would fail to load its video.");
                return false;
            }

            var problems = new List<string>();
            foreach (var entry in catalog.entries)
            {
                if (entry == null) continue;

                var problem = VideoUrlRules.Validate(entry.url, requireSecure: !development);
                if (problem != VideoUrlProblem.None)
                {
                    problems.Add($"  {entry.key}: {VideoUrlRules.Describe(problem)}");
                }
            }

            if (problems.Count == 0)
            {
                Debug.Log($"[BuildWebGL] Video catalog: {catalog.entries.Length} streamable entries.");
                return true;
            }

            var detail = $"{problems.Count} of {catalog.entries.Length} video catalog entries are " +
                         $"not playable:\n{string.Join("\n", problems)}";

            if (development)
            {
                Debug.LogWarning($"[BuildWebGL] {detail}");
                return true;
            }

            Fail(detail);
            return false;
        }

        private static void ApplyPlayerSettings(bool development)
        {
            // The stock template lets the page scroll and pinch-zoom, which eats the gestures the
            // touch scheme needs. Set here rather than left in ProjectSettings for the same reason
            // ServerConfig is an asset: a lost project setting should not change what ships.
            PlayerSettings.WebGL.template = "PROJECT:Wiraga";

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.dataCaching = true;

            // Brotli is decompressed by the browser via Content-Encoding, so the
            // JS fallback decompressor is dead weight. This is why the Nginx
            // config MUST send `Content-Encoding: br` — without it the loader
            // has no way to inflate the payload and the build fails to start.
            PlayerSettings.WebGL.decompressionFallback = false;

            PlayerSettings.WebGL.exceptionSupport = development
                ? WebGLExceptionSupport.FullWithStacktrace
                : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            PlayerSettings.SetIl2CppCompilerConfiguration(
                UnityEditor.Build.NamedBuildTarget.WebGL,
                development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Master);
        }

        private static string[] EnabledScenes()
        {
            return EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        private static void Report(BuildReport report, string endpoint)
        {
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"WebGL build {summary.result} with {summary.totalErrors} error(s).");
                return;
            }

            var stamp = StampCacheBuster();

            Debug.Log(
                $"WebGL build succeeded in {summary.totalTime:hh\\:mm\\:ss}.\n" +
                $"  Output:   {Path.GetFullPath(OutputPath)}\n" +
                $"  Endpoint: {endpoint}\n" +
                $"  Cache:    ?v={stamp}\n" +
                $"  Payload:  {DescribePayload()}");
        }

        /// <summary>
        /// Rewrites the <c>BUILDSTAMP</c> placeholder the Wiraga template puts in its
        /// <c>?v=</c> query onto the four <c>Build/</c> URLs.
        ///
        /// nginx serves <c>Build/</c> as <c>immutable, max-age=31536000</c> and every build
        /// emits the same four filenames, so without this a returning visitor pairs a cached
        /// <c>framework.js</c> with a freshly downloaded <c>.wasm</c>. Add or remove a
        /// <c>.jslib</c> function and that mismatch is a hard <c>LinkError</c> at instantiate
        /// — <c>"function import requires a callable"</c> — which no reload clears, because
        /// the stale response is the one the cache was told to keep. <c>index.html</c> is
        /// served <c>no-cache</c>, so changing the query there invalidates all four.
        /// </summary>
        private static string StampCacheBuster()
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var indexPath = Path.Combine(OutputPath, "index.html");

            if (!File.Exists(indexPath))
            {
                Debug.LogWarning($"[BuildWebGL] No index.html at {indexPath}; skipped cache buster.");
                return stamp;
            }

            var html = File.ReadAllText(indexPath);
            if (!html.Contains("BUILDSTAMP"))
            {
                Debug.LogWarning(
                    "[BuildWebGL] index.html has no BUILDSTAMP placeholder. The Wiraga template " +
                    "must keep it, or returning visitors will load a stale framework.js against " +
                    "a new .wasm. See Assets/Docs/build-and-deploy.md.");
                return stamp;
            }

            File.WriteAllText(indexPath, html.Replace("BUILDSTAMP", stamp));
            return stamp;
        }

        /// <summary>
        /// Transfer size of the files a first-time visitor downloads. Worth
        /// watching: this project's asset folders are large, and the WebGL data
        /// file is the single biggest factor in whether the site is usable on a
        /// museum or mobile connection.
        /// </summary>
        private static string DescribePayload()
        {
            var buildDir = new DirectoryInfo(Path.Combine(OutputPath, "Build"));
            if (!buildDir.Exists)
            {
                return "unknown (no Build/ directory)";
            }

            var parts = new List<string>();
            long total = 0;
            foreach (var file in buildDir.GetFiles().OrderByDescending(f => f.Length))
            {
                total += file.Length;
                parts.Add($"{file.Name} {Mb(file.Length)}");
            }

            return $"{Mb(total)} total — {string.Join(", ", parts)}";
        }

        private static string Mb(long bytes) => $"{bytes / 1024f / 1024f:0.#} MB";

        private static void Fail(string message)
        {
            Debug.LogError($"[BuildWebGL] {message}");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
