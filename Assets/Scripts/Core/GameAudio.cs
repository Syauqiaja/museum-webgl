using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Museum.Core
{
    /// <summary>
    /// The project's music and its non-diegetic sounds: each scene's track, the button click and
    /// hover, and the win jingle. One instance for the whole visit, made before the first scene
    /// loads, so no scene has to carry it and none can lose it.
    /// </summary>
    /// <remarks>
    /// <b>Music</b> follows the scene (<see cref="SceneMusic"/>) and crossfades on a change; a scene
    /// that keeps the track (the Lobby into its game) does not restart it. It dips under anything
    /// that talks — a museum video (<see cref="SetDucked"/>) or the win jingle — and comes back.
    ///
    /// <b>Buttons</b> are found, not wired: every active <see cref="Button"/> and <see cref="Toggle"/>
    /// gets a <see cref="UiSelectableSound"/> the first time it is seen, so the generated scenes
    /// need no rebuild and a panel built at runtime is covered too.
    ///
    /// The 3D sounds of the museum's exhibits (<see cref="GalleryStation"/>) are not here: they
    /// belong to the thing in the room that makes them.
    /// </remarks>
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>Resources path of the <see cref="GameAudioLibrary"/>.</summary>
        public const string LibraryResource = "GameAudio";

        /// <summary>Seconds a track takes to fade in or out on a scene change.</summary>
        public const float FadeSeconds = 1.2f;

        /// <summary>Fraction of the music level kept while something talks over it.</summary>
        public const float DuckedLevel = 0.2f;

        private const float DuckSeconds = 0.4f;
        private const float ScanInterval = 0.1f;
        private const float MinHoverGap = 0.06f;

        public static GameAudio Instance { get; private set; }

        private GameAudioLibrary _library;
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly float[] _weight = new float[2];
        private int _current;
        private AudioSource _effects;

        private readonly HashSet<Object> _duckers = new HashSet<Object>();
        private float _duck = 1f;
        private float _winDuckUntil = -1f;
        private float _lastHover = -1f;

        private Selectable[] _selectables = new Selectable[64];
        private float _nextScan;

        /// <summary>The track playing, or fading in.</summary>
        public MusicTrack Track { get; private set; } = MusicTrack.None;

        /// <summary>True while the music is held down under a video or the win jingle.</summary>
        public bool Ducked => _duckers.Count > 0 || Time.unscaledTime < _winDuckUntil;

        /// <summary>The music source's current volume — for tests and for a future mixer screen.</summary>
        public float MusicLevel => _music[_current] != null ? _music[_current].volume : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;

            var library = Resources.Load<GameAudioLibrary>(LibraryResource);
            if (library == null)
            {
                Debug.LogWarning($"[GameAudio] no Resources/{LibraryResource} — the game runs silent.");
                return;
            }

            Create(library);
        }

        /// <summary>Makes the instance with <paramref name="library"/>. Boot does this; tests do it with their own.</summary>
        public static GameAudio Create(GameAudioLibrary library)
        {
            var go = new GameObject("Game Audio");
            DontDestroyOnLoad(go);
            var audio = go.AddComponent<GameAudio>();
            audio._library = library;
            return audio;
        }

        /// <summary>Plays the button click. Safe to call with no instance.</summary>
        public static void PlayClick()
        {
            if (Instance != null && Instance._library != null) Instance.PlayEffect(Instance._library.buttonClick, Instance._library.uiVolume);
        }

        /// <summary>Plays the hover tick, at most once every few frames.</summary>
        public static void PlayHover()
        {
            if (Instance == null || Instance._library == null) return;
            if (Time.unscaledTime - Instance._lastHover < MinHoverGap) return;

            Instance._lastHover = Time.unscaledTime;
            Instance.PlayEffect(Instance._library.buttonHover, Instance._library.uiVolume);
        }

        /// <summary>The local player won. The music dips for the length of the jingle.</summary>
        public static void PlayWin()
        {
            if (Instance == null || Instance._library == null || Instance._library.win == null) return;

            Instance.PlayEffect(Instance._library.win, Instance._library.winVolume);
            Instance._winDuckUntil = Time.unscaledTime + Instance._library.win.length;
        }

        /// <summary>
        /// Holds the music down while <paramref name="owner"/> talks (a playing museum video), and
        /// lets it back up when the last one stops. Each owner counts once.
        /// </summary>
        public static void SetDucked(Object owner, bool ducked)
        {
            if (Instance == null || owner == null) return;

            if (ducked) Instance._duckers.Add(owner);
            else Instance._duckers.Remove(owner);
        }

        /// <summary>Switches to <paramref name="track"/>, crossfading; the same track keeps playing where it is.</summary>
        public void Play(MusicTrack track)
        {
            if (track == Track) return;
            Track = track;

            AudioClip clip = _library != null ? _library.ClipFor(track) : null;

            _current = 1 - _current;
            AudioSource next = _music[_current];
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            _weight[_current] = 0f;
            if (clip != null) next.Play();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            for (int i = 0; i < _music.Length; i++)
            {
                _music[i] = gameObject.AddComponent<AudioSource>();
                _music[i].loop = true;
                _music[i].playOnAwake = false;
                _music[i].spatialBlend = 0f;
                _music[i].priority = 0;
            }

            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.spatialBlend = 0f;
            _effects.priority = 16;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            // Made by a test (or after the first scene), there is no load event for the scene
            // already open.
            if (Track == MusicTrack.None) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            Play(SceneMusic.For(scene.name, LobbyRequest.LastGameScene));
            _nextScan = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Crossfade: the current source rises to full, the other falls and stops.
            for (int i = 0; i < _music.Length; i++)
            {
                float target = i == _current && _music[i].clip != null ? 1f : 0f;
                _weight[i] = Mathf.MoveTowards(_weight[i], target, dt / FadeSeconds);
                if (_weight[i] <= 0f && i != _current && _music[i].isPlaying) _music[i].Stop();
            }

            _duckers.RemoveWhere(owner => owner == null);   // a screen destroyed mid-video
            _duck = Mathf.MoveTowards(_duck, Ducked ? DuckedLevel : 1f, dt / DuckSeconds);

            float level = _library != null ? _library.musicVolume : 0f;
            for (int i = 0; i < _music.Length; i++) _music[i].volume = _weight[i] * level * _duck;

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + ScanInterval;
                AttachButtonSounds();
            }
        }

        /// <summary>Gives every active button and toggle its click/hover sound, once.</summary>
        private void AttachButtonSounds()
        {
            int count = Selectable.allSelectableCount;
            if (_selectables.Length < count) _selectables = new Selectable[count * 2];

            int found = Selectable.AllSelectablesNoAlloc(_selectables);
            for (int i = 0; i < found; i++)
            {
                Selectable selectable = _selectables[i];
                if (selectable == null || !(selectable is Button || selectable is Toggle)) continue;
                if (!selectable.TryGetComponent(out UiSelectableSound _)) selectable.gameObject.AddComponent<UiSelectableSound>();
            }

            System.Array.Clear(_selectables, 0, found);
        }

        private void PlayEffect(AudioClip clip, float volume)
        {
            if (clip != null && _effects != null) _effects.PlayOneShot(clip, volume);
        }
    }
}
