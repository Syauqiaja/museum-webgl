using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Museum.Core.Tests
{
    /// <summary>
    /// The music and the button sounds, with a library of silent clips so the tests hear nothing
    /// and depend on no asset.
    /// </summary>
    public class GameAudioTests
    {
        private GameAudioLibrary _library;
        private GameAudio _audio;
        private GameObject _button;

        [SetUp]
        public void SetUp()
        {
            // The real one, booted from Resources before the test scene loaded.
            if (GameAudio.Instance != null) Object.DestroyImmediate(GameAudio.Instance.gameObject);

            _library = ScriptableObject.CreateInstance<GameAudioLibrary>();
            _library.museumMusic = Silence("museum", 2f);
            _library.dakonMusic = Silence("dakon", 2f);
            _library.egrangMusic = Silence("egrang", 2f);
            _library.buttonClick = Silence("click", 0.1f);
            _library.buttonHover = Silence("hover", 0.05f);
            _library.win = Silence("win", 0.5f);

            _audio = GameAudio.Create(_library);
        }

        [TearDown]
        public void TearDown()
        {
            if (_audio != null) Object.DestroyImmediate(_audio.gameObject);
            if (_button != null) Object.DestroyImmediate(_button);
            Object.DestroyImmediate(_library);
        }

        [UnityTest]
        public IEnumerator Every_button_gets_its_sounds_without_being_wired()
        {
            _button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            yield return null;
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.IsNotNull(_button.GetComponent<UiSelectableSound>());
        }

        [UnityTest]
        public IEnumerator A_scene_change_switches_the_track_and_the_same_track_carries_on()
        {
            yield return null;

            _audio.Play(MusicTrack.Dakon);
            Assert.AreEqual(MusicTrack.Dakon, _audio.Track);
            AudioSource playing = PlayingMusic();
            Assert.AreSame(_library.dakonMusic, playing.clip);

            // The lobby into its game: same track, no restart.
            _audio.Play(MusicTrack.Dakon);
            Assert.AreSame(playing, PlayingMusic(), "the source playing is not swapped");
        }

        [UnityTest]
        public IEnumerator The_music_dips_under_a_video_and_comes_back()
        {
            _audio.Play(MusicTrack.Museum);
            yield return new WaitForSecondsRealtime(GameAudio.FadeSeconds + 0.1f);
            float full = _audio.MusicLevel;
            Assert.Greater(full, 0f);

            var video = new GameObject("Video");
            GameAudio.SetDucked(video, true);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(full * GameAudio.DuckedLevel, _audio.MusicLevel, 0.01f);

            GameAudio.SetDucked(video, false);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(full, _audio.MusicLevel, 0.01f);

            Object.DestroyImmediate(video);
        }

        [UnityTest]
        public IEnumerator A_screen_destroyed_mid_video_does_not_hold_the_music_down()
        {
            yield return null;

            var video = new GameObject("Video");
            GameAudio.SetDucked(video, true);
            Object.DestroyImmediate(video);
            yield return null;

            Assert.IsFalse(_audio.Ducked);
        }

        [UnityTest]
        public IEnumerator A_silent_button_clicks_quietly_and_an_ordinary_one_does_not()
        {
            _button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            var quiet = new GameObject("Step Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(SilentButton));
            yield return new WaitForSecondsRealtime(0.15f);

            AudioSource effects = Effects();
            quiet.GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(effects.isPlaying, "JALAN makes no click");

            _button.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(effects.isPlaying, "every other button still does");

            Object.DestroyImmediate(quiet);
        }

        [Test]
        public void A_win_holds_the_music_down_for_the_jingle()
        {
            GameAudio.PlayWin();

            Assert.IsTrue(_audio.Ducked);
        }

        private AudioSource PlayingMusic()
        {
            foreach (AudioSource source in _audio.GetComponents<AudioSource>())
            {
                if (source.loop && source.isPlaying) return source;
            }

            return null;
        }

        private AudioSource Effects()
        {
            foreach (AudioSource source in _audio.GetComponents<AudioSource>())
            {
                if (!source.loop) return source;
            }

            return null;
        }

        private static AudioClip Silence(string name, float seconds)
        {
            return AudioClip.Create(name, Mathf.CeilToInt(44100 * seconds), 1, 44100, false);
        }
    }
}
