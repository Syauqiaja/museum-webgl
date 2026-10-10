using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Museum.Core.Tests
{
    /// <summary>
    /// Every screen a finger touches — Dakon's cards, the lobby, the menus, Egrang's JALAN button —
    /// reaches uGUI through the EventSystem's input module. Only InputSystemUIInputModule routes
    /// touch under the Input System backend, so a scene that loses it loses touch silently.
    /// </summary>
    /// <remarks>
    /// Opens each build scene in the Editor rather than at play time: this is a wiring fact about
    /// the scene asset, and an EditMode check reports the scene by name when it breaks.
    /// </remarks>
    public class EventSystemModuleTests
    {
        private static IEnumerable<string> BuildScenePaths()
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled) yield return scene.path;
            }
        }

        [Test]
        public void Every_build_scene_routes_uGUI_through_the_input_system_module(
            [ValueSource(nameof(BuildScenePaths))] string scenePath)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                EventSystem eventSystem = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    eventSystem = root.GetComponentInChildren<EventSystem>(true);
                    if (eventSystem != null) break;
                }

                Assert.IsNotNull(eventSystem, $"{scenePath} has no EventSystem — no uGUI input at all.");
                Assert.IsNotNull(eventSystem.GetComponent<InputSystemUIInputModule>(),
                    $"{scenePath}'s EventSystem has no InputSystemUIInputModule. A StandaloneInputModule " +
                    "does not route touch under the Input System backend, so every button in this scene " +
                    "would stop responding on a phone.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
