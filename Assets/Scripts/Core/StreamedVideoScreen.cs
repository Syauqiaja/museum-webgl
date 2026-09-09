using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Museum.Core
{
    /// <summary>
    /// Plays one museum screen's video by streaming it from the CDN named in
    /// <see cref="VideoCatalog"/>. The screen carries a key, never a URL.
    /// </summary>
    /// <remarks>
    /// Split from <c>VideoTriggerPlayer</c> on purpose: triggering is a collider concern,
    /// while loading a remote file is a small state machine with failures, retries and a
    /// placeholder. On WebGL the browser fetches the file, so nothing here can assume the
    /// video is ready — every path goes through <see cref="VideoPlayer.Prepare"/> first.
    /// </remarks>
    [RequireComponent(typeof(VideoPlayer))]
    public class StreamedVideoScreen : MonoBehaviour
    {
        /// <summary>Where this screen is in the load-and-play cycle.</summary>
        public enum ScreenState
        {
            Idle,
            Preparing,
            Playing,
            Failed,
        }

        [Tooltip("Leave empty to load Resources/VideoCatalog.")]
        [SerializeField] private VideoCatalog catalog;

        [Tooltip("Key into the catalog — the original asset filename, e.g. \"DAKON.mp4\".")]
        [SerializeField] private string videoKey;

        [Tooltip("The RawImage this screen renders through. Falls back to a child RawImage.")]
        [SerializeField] private RawImage screen;

        [Tooltip("Shown while loading, and left up when loading fails.")]
        [SerializeField] private Texture placeholder;

        [Tooltip("Automatic retries after a failure before the screen gives up until the visitor walks away.")]
        [SerializeField] private int maxAutoRetries = 2;

        [SerializeField] private float retryDelaySeconds = 2f;

        [Tooltip("A stalled fetch can leave the player preparing forever without ever raising an error.")]
        [SerializeField] private float prepareTimeoutSeconds = 12f;

        private VideoPlayer _player;
        private Coroutine _retryRoutine;
        private Coroutine _timeoutRoutine;
        private int _retries;

        /// <summary>Current state, for tests and for anything that wants to show load progress.</summary>
        public ScreenState State { get; private set; } = ScreenState.Idle;

        /// <summary>Catalog key this screen plays.</summary>
        public string VideoKey => VideoUrlRules.SanitizeKey(videoKey);

        private void Awake()
        {
            _player = GetComponent<VideoPlayer>();

            // Set in code as well as on the prefab: these four are what make URL streaming work
            // on WebGL, and a stale prefab override would otherwise silently re-embed a clip.
            _player.source = VideoSource.Url;
            _player.clip = null;
            _player.playOnAwake = false;
            _player.audioOutputMode = VideoAudioOutputMode.Direct;

            if (screen == null && transform.parent != null)
            {
                // The RawImage is a sibling under the same world-space Canvas, not a child of
                // the VideoPlayer object — same shape as VideoTriggerPlayer's lookup.
                screen = transform.parent.GetComponentInChildren<RawImage>(true);
            }

            if (catalog == null)
            {
                catalog = Resources.Load<VideoCatalog>("VideoCatalog");
            }

            if (catalog == null)
            {
                // Deliberately not the exception ColyseusNetManager throws when its config is
                // missing: the museum has to stay walkable, and eighteen screens throwing in
                // Awake would bury every other console message.
                Debug.LogError($"[StreamedVideoScreen] {name}: no VideoCatalog assigned and none at " +
                               "Resources/VideoCatalog — this screen cannot stream anything.", this);
                State = ScreenState.Failed;
            }

            _player.errorReceived += OnVideoError;
            _player.prepareCompleted += OnPrepared;

            ShowPlaceholder();
        }

        private void OnDestroy()
        {
            if (_player == null) return;
            _player.errorReceived -= OnVideoError;
            _player.prepareCompleted -= OnPrepared;
        }

        /// <summary>Starts loading and then playing this screen's video. Safe to call repeatedly.</summary>
        public void RequestPlay()
        {
            if (State == ScreenState.Playing || State == ScreenState.Preparing)
            {
                return;
            }

            _retries = 0;
            BeginPrepare();
        }

        /// <summary>Stops playback and releases the browser's video element.</summary>
        public void RequestStop()
        {
            CancelPending();
            _retries = 0;

            if (_player != null)
            {
                _player.Stop();
            }

            // Walking away and back is the retry the museum actually offers: the canvas is
            // world-space with no event camera, so a Retry button would not reliably take clicks.
            State = ScreenState.Idle;
            ShowPlaceholder();
        }

        /// <summary>Tries the load again from scratch.</summary>
        public void Retry()
        {
            CancelPending();
            BeginPrepare();
        }

        private void BeginPrepare()
        {
            if (catalog == null)
            {
                State = ScreenState.Failed;
                return;
            }

            string key = VideoKey;
            if (key.Length == 0)
            {
                Debug.LogError($"[StreamedVideoScreen] {name}: no video key assigned.", this);
                State = ScreenState.Failed;
                ShowPlaceholder();
                return;
            }

            string url = catalog.UrlFor(key);
            VideoUrlProblem problem = VideoUrlRules.Validate(url, requireSecure: false);
            if (problem != VideoUrlProblem.None)
            {
                // Expected while a video is still being uploaded — one line, no retry storm.
                Debug.LogWarning($"[StreamedVideoScreen] {name}: '{key}' is not playable " +
                                 $"({VideoUrlRules.Describe(problem)}).", this);
                State = ScreenState.Failed;
                ShowPlaceholder();
                return;
            }

            ShowPlaceholder();
            State = ScreenState.Preparing;
            _player.url = url;
            _player.Prepare();

            _timeoutRoutine = StartCoroutine(FailIfStillPreparing(url));
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (State != ScreenState.Preparing)
            {
                return;
            }

            StopTimeout();
            _retries = 0;
            State = ScreenState.Playing;
            ShowVideo();
            _player.Play();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            HandleFailure($"{message} (url: {_player.url})");
        }

        /// <summary>
        /// A CORS-tainted or stalled fetch on WebGL can leave the player preparing forever
        /// without ever raising <see cref="VideoPlayer.errorReceived"/>. Without this guard the
        /// screen holds its placeholder and logs nothing at all.
        /// </summary>
        private IEnumerator FailIfStillPreparing(string url)
        {
            yield return new WaitForSeconds(prepareTimeoutSeconds);
            _timeoutRoutine = null;

            if (State == ScreenState.Preparing)
            {
                HandleFailure($"timed out after {prepareTimeoutSeconds:0}s (url: {url})");
            }
        }

        private void HandleFailure(string reason)
        {
            StopTimeout();
            State = ScreenState.Failed;
            ShowPlaceholder();
            Debug.LogError($"[StreamedVideoScreen] {name} failed to load '{VideoKey}': {reason}", this);

            if (_retries < maxAutoRetries && _retryRoutine == null)
            {
                _retries++;
                _retryRoutine = StartCoroutine(RetryAfterDelay());
            }
        }

        private IEnumerator RetryAfterDelay()
        {
            yield return new WaitForSeconds(retryDelaySeconds);
            _retryRoutine = null;
            BeginPrepare();
        }

        private void CancelPending()
        {
            StopTimeout();

            if (_retryRoutine != null)
            {
                StopCoroutine(_retryRoutine);
                _retryRoutine = null;
            }
        }

        private void StopTimeout()
        {
            if (_timeoutRoutine != null)
            {
                StopCoroutine(_timeoutRoutine);
                _timeoutRoutine = null;
            }
        }

        // The RenderTexture is written by the browser asynchronously, so the texture pointer is
        // swapped rather than blitted over — a Blit would race with the video element.
        private void ShowPlaceholder()
        {
            if (screen != null && placeholder != null)
            {
                screen.texture = placeholder;
            }
        }

        private void ShowVideo()
        {
            if (screen != null && _player.targetTexture != null)
            {
                screen.texture = _player.targetTexture;
            }
        }
    }
}
