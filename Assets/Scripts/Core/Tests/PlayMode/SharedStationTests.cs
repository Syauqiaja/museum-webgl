using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Museum.Core.Tests
{
    /// <summary>
    /// What another visitor's press does to this visitor's copy of an exhibit. The shared part —
    /// the light, the sound, the swing — plays; the personal part — the engklek run, the lyric
    /// being read — is left alone, and nothing is reported back to the room.
    /// </summary>
    public class SharedStationTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private readonly List<(string, int)> _reported = new List<(string, int)>();

        private void Record(string station, int index) => _reported.Add((station, index));

        [SetUp]
        public void SetUp()
        {
            _reported.Clear();
            MuseumInteractions.LocalInteracted += Record;
        }

        [TearDown]
        public void TearDown()
        {
            MuseumInteractions.LocalInteracted -= Record;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator Another_visitors_step_lights_the_petak_but_leaves_this_visitors_run_alone()
        {
            TMP_Text status = Make("Status").AddComponent<TextMeshPro>();
            HopscotchTile first = Make("Petak 1").AddComponent<HopscotchTile>();
            HopscotchTile second = Make("Petak 2").AddComponent<HopscotchTile>();

            GameObject courseGo = Make("Course", active: false);
            var course = courseGo.AddComponent<HopscotchCourse>();
            Set(course, "tiles", new[] { first, second });
            Set(course, "rows", new[] { 0, 1 });
            Set(course, "source", courseGo.AddComponent<AudioSource>());
            Set(course, "status", status);
            courseGo.SetActive(true);
            string idle = status.text;

            Assert.IsTrue(MuseumInteractions.PlayRemote(MuseumInteractions.Engklek, 1));
            yield return null;

            Assert.Greater(Get<float>(second, "_litUntil"), Time.time, "the petak lights");
            Assert.AreEqual(-1, Get<int>(course, "_row"), "this visitor's run has not started");
            Assert.AreEqual(idle, status.text, "and their status line still says so");
            Assert.IsEmpty(_reported, "a remote step is not sent back");
        }

        [UnityTest]
        public IEnumerator Another_visitors_song_is_heard_but_the_lyric_stays_put()
        {
            TMP_Text lyric = Make("Lyric").AddComponent<TextMeshPro>();
            GameObject stageGo = Make("Tembang", active: false);
            var stage = stageGo.AddComponent<SongStation>();
            AudioSource source = stageGo.AddComponent<AudioSource>();
            Set(stage, "source", source);
            Set(stage, "lyric", lyric);
            stageGo.SetActive(true);
            string idle = lyric.text;

            Assert.IsTrue(MuseumInteractions.PlayRemote(MuseumInteractions.Tembang, 0));
            yield return null;
            yield return null;

            Assert.IsNotNull(source.clip, "the melody plays");
            Assert.AreEqual(idle, lyric.text, "the banner is this visitor's");
            Assert.IsEmpty(_reported);
        }

        [Test]
        public void A_station_reports_the_local_visitors_press_and_not_anothers()
        {
            GameObject gongGo = Make("Gong", active: false);
            var gong = gongGo.AddComponent<GongStation>();
            gongGo.SetActive(true);

            gong.PlayRemote(0);
            Assert.IsEmpty(_reported, "someone else's strike");

            gong.Interact();
            Assert.IsEmpty(_reported, "a press from outside the trigger does nothing");

            Set(gong, "_playerInside", true, typeof(GalleryStation));
            gong.Interact();
            CollectionAssert.AreEqual(new[] { (MuseumInteractions.Gong, 0) }, _reported);
        }

        [Test]
        public void Station_sound_falls_silent_past_its_audible_distance()
        {
            AudioSource source = Make("Source").AddComponent<AudioSource>();

            GalleryStation.TuneSource(source);

            // Logarithmic never reaches zero; a gong struck in the lobby must not reach LT3.
            Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode);
            Assert.AreEqual(GalleryStation.AudibleDistance, source.maxDistance);
            Assert.AreEqual(1f, source.spatialBlend);
        }

        private GameObject Make(string name, bool active = true)
        {
            var go = new GameObject(name);
            // Stations and petak [RequireComponent(typeof(Collider))], and Collider is abstract:
            // Unity cannot add one for them, so AddComponent would return null without this.
            go.AddComponent<BoxCollider>().isTrigger = true;
            go.SetActive(active);
            _made.Add(go);
            return go;
        }

        private static void Set(object target, string field, object value, System.Type owner = null)
        {
            FieldInfo info = (owner ?? target.GetType()).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field}");
            info.SetValue(target, value);
        }

        private static T Get<T>(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field}");
            return (T)info.GetValue(target);
        }
    }
}
