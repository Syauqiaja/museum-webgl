#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// A lane's walker dressed in another character must stand where the walker stands. Found
    /// 2026-09-11: every non-Jawa body was posed 8.5 m behind its walker — behind the camera, so
    /// only its shadow showed — because the pose copy carried the walker's offset along its lane.
    /// Built the way the scene is: a lane root, the walker offset back from it and scaled up.
    /// </summary>
    public class EgrangRacerBodyTests
    {
        private const string Folder = "Assets/Models/ASSET_NUSANTARA/1_Karakter/";
        private GameObject _lane;

        [TearDown]
        public void TearDown()
        {
            if (_lane != null) Object.Destroy(_lane);
        }

        [UnityTest]
        public IEnumerator A_worn_body_stands_over_the_walker_wherever_the_walker_is_on_its_lane()
        {
            GameObject jawa = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Char_Jawa_L.fbx");
            GameObject bali = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Char_Bali_P.fbx");
            Assert.IsNotNull(jawa, "Char_Jawa_L.fbx");
            Assert.IsNotNull(bali, "Char_Bali_P.fbx");

            _lane = new GameObject("Player 1 Point");
            _lane.transform.position = new Vector3(98f, 35f, 112f);

            // The authored walker: the Jawa rig (Armature + char1), as the scene's is.
            GameObject walker = Object.Instantiate(jawa, _lane.transform, false);
            walker.name = "Egrang Player";
            walker.transform.localPosition = new Vector3(0f, 0f, -3f);
            walker.transform.localScale = Vector3.one * 2.34f;

            var body = walker.AddComponent<EgrangRacerBody>();
            body.Configure(new EgrangStick[0], new[] { jawa, bali });
            body.Wear("bali");
            yield return null;

            AssertStandsOver(walker.transform, body.WornBody, "at the start");

            // Walked on: the body goes with it.
            walker.transform.localPosition = new Vector3(0f, 0f, 6f);
            yield return null;

            AssertStandsOver(walker.transform, body.WornBody, "after walking");
        }

        private static void AssertStandsOver(Transform walker, Transform worn, string when)
        {
            Assert.IsNotNull(worn, "wearing a body");

            Vector3 authored = Find(walker.Find("Armature"), "Hips").position;
            Vector3 hips = Find(worn.Find("Armature"), "Hips").position;

            Assert.AreEqual(authored.x, hips.x, 0.01f, $"hips x {when}");
            Assert.AreEqual(authored.z, hips.z, 0.01f, $"hips z {when}");
            Assert.AreEqual(authored.y, hips.y, 0.6f, $"hips roughly at the walker's height {when}");
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            foreach (Transform child in root)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
