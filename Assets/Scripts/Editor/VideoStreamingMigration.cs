using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Museum.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Museum.Build.Editor
{
    /// <summary>
    /// One-shot migration from embedded <see cref="VideoClip"/> assets to CDN streaming.
    /// </summary>
    /// <remarks>
    /// The museum scene holds eighteen Video Player prefab instances whose clip and render
    /// texture are per-instance prefab overrides. Editing those override blocks by hand is how
    /// a scene ends up quietly repaired into something else, so the rewiring runs through the
    /// editor API instead. Throwaway once the scene is migrated — kept so the report can be
    /// re-run if the mapping is ever questioned.
    /// </remarks>
    public static class VideoStreamingMigration
    {
        private const string MuseumScenePath = "Assets/Scenes/Museum.unity";
        private const string CatalogResource = "VideoCatalog";

        [MenuItem("Museum/Video/Report Video Assignments")]
        public static void Report()
        {
            if (!TryCollect(out List<Assignment> assignments))
            {
                return;
            }

            Debug.Log($"[VideoStreamingMigration] {assignments.Count} screens in {MuseumScenePath}:\n" +
                      Describe(assignments));
        }

        [MenuItem("Museum/Video/Migrate Scene To Streaming")]
        public static void Migrate()
        {
            if (!TryCollect(out List<Assignment> assignments))
            {
                return;
            }

            var catalog = Resources.Load<VideoCatalog>(CatalogResource);
            if (catalog == null)
            {
                Debug.LogError($"[VideoStreamingMigration] No VideoCatalog at Resources/{CatalogResource}. " +
                               "Create it first (Assets → Create → Museum → Video Catalog) so the migration " +
                               "has somewhere to record the keys.");
                return;
            }

            int migrated = 0;
            foreach (Assignment assignment in assignments)
            {
                VideoPlayer player = assignment.Player;

                var screen = player.GetComponent<StreamedVideoScreen>();
                if (screen == null)
                {
                    screen = Undo.AddComponent<StreamedVideoScreen>(player.gameObject);
                }

                var serialized = new SerializedObject(screen);
                serialized.FindProperty("videoKey").stringValue = assignment.Key;

                RawImage raw = player.transform.parent != null
                    ? player.transform.parent.GetComponentInChildren<RawImage>(true)
                    : null;
                if (raw != null)
                {
                    serialized.FindProperty("screen").objectReferenceValue = raw;
                }

                serialized.ApplyModifiedProperties();

                Undo.RecordObject(player, "Migrate video to streaming");
                player.source = VideoSource.Url;
                player.clip = null;
                player.playOnAwake = false;
                player.audioOutputMode = VideoAudioOutputMode.Direct;

                EditorUtility.SetDirty(screen);
                EditorUtility.SetDirty(player);
                if (PrefabUtility.IsPartOfPrefabInstance(player))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
                }

                migrated++;
            }

            WriteKeys(catalog, assignments);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[VideoStreamingMigration] Migrated {migrated} screens and recorded " +
                      $"{catalog.entries.Length} catalog keys. Save the scene, then paste a URL " +
                      $"per key into Resources/{CatalogResource}.\n" + Describe(assignments));
        }

        /// <summary>
        /// Adds keys to the catalog without disturbing URLs someone has already pasted in.
        /// </summary>
        private static void WriteKeys(VideoCatalog catalog, List<Assignment> assignments)
        {
            var byKey = new Dictionary<string, VideoCatalog.Entry>(StringComparer.Ordinal);
            var ordered = new List<VideoCatalog.Entry>();

            foreach (VideoCatalog.Entry existing in catalog.entries ?? Array.Empty<VideoCatalog.Entry>())
            {
                string key = VideoUrlRules.SanitizeKey(existing?.key);
                if (key.Length == 0 || byKey.ContainsKey(key)) continue;
                byKey[key] = existing;
                ordered.Add(existing);
            }

            foreach (string key in assignments.Select(a => a.Key).Where(k => k.Length > 0).Distinct(StringComparer.Ordinal))
            {
                if (byKey.ContainsKey(key)) continue;
                var entry = new VideoCatalog.Entry { key = key, url = string.Empty };
                byKey[key] = entry;
                ordered.Add(entry);
            }

            Undo.RecordObject(catalog, "Record video catalog keys");
            catalog.entries = ordered.ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static bool TryCollect(out List<Assignment> assignments)
        {
            assignments = new List<Assignment>();

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumScenePath)
            {
                Debug.LogError($"[VideoStreamingMigration] Open {MuseumScenePath} first — the active " +
                               $"scene is '{scene.path}'.");
                return false;
            }

            foreach (VideoPlayer player in UnityEngine.Object.FindObjectsByType<VideoPlayer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                assignments.Add(new Assignment(player, KeyFor(player)));
            }

            assignments.Sort((a, b) => string.CompareOrdinal(a.ScreenName, b.ScreenName));
            return true;
        }

        /// <summary>
        /// The catalog key is the clip's <em>asset filename</em>, taken from whatever clip the
        /// instance actually plays — never from the GameObject's name. "Vid Bitingan" plays
        /// "Tok tok pyar.mp4", and three screens fall through to the prefab's default clip.
        /// </summary>
        private static string KeyFor(VideoPlayer player)
        {
            VideoClip clip = player.clip;
            if (clip == null)
            {
                VideoPlayer source = PrefabUtility.GetCorrespondingObjectFromSource(player);
                clip = source != null ? source.clip : null;
            }

            if (clip == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(clip);
            return string.IsNullOrEmpty(path) ? clip.name : Path.GetFileName(path);
        }

        private static string Describe(List<Assignment> assignments)
        {
            var text = new StringBuilder();
            foreach (Assignment assignment in assignments)
            {
                string key = assignment.Key.Length > 0 ? assignment.Key : "(no clip)";
                text.AppendLine($"  {assignment.ScreenName,-20} -> {key}");
            }

            return text.ToString();
        }

        private readonly struct Assignment
        {
            public Assignment(VideoPlayer player, string key)
            {
                Player = player;
                Key = key;
            }

            public VideoPlayer Player { get; }
            public string Key { get; }

            /// <summary>Name of the prefab instance root, which is what the scene hierarchy shows.</summary>
            public string ScreenName =>
                Player.transform.parent != null && Player.transform.parent.parent != null
                    ? Player.transform.parent.parent.name
                    : Player.name;
        }
    }
}
