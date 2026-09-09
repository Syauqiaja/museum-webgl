using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Museum.Core
{
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Loading Screen Prefab")]
        [Tooltip("Prefab with Canvas, Image panel, and LoadingText. Instantiated on load.")]
        [SerializeField] private GameObject loadingScreenPrefab;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        [SerializeField] private float minDisplayTime = 0.5f;

        private GameObject _loadingScreenInstance;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _loadingText;
        private bool _isLoading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Only this component goes, not the GameObject: a scene's bootstrap object
                // carries SessionData and ColyseusNetManager too, and destroying it would take
                // the player's nickname and seat down with the duplicate loader.
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoadScene(string sceneName)
        {
            if (_isLoading) return;
            StartCoroutine(LoadSceneAsync(sceneName));
        }

        public void LoadScene(int buildIndex)
        {
            if (_isLoading) return;
            StartCoroutine(LoadSceneAsync(buildIndex));
        }

        private IEnumerator LoadSceneAsync(string sceneName)
        {
            _isLoading = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return ShowLoadingScreen();

            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            float startTime = Time.unscaledTime;

            while (op.progress < 0.9f)
                yield return null;

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < minDisplayTime)
                yield return new WaitForSecondsRealtime(minDisplayTime - elapsed);

            op.allowSceneActivation = true;
            yield return op;

            yield return HideLoadingScreen();
            _isLoading = false;
        }

        private IEnumerator LoadSceneAsync(int buildIndex)
        {
            _isLoading = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return ShowLoadingScreen();

            var op = SceneManager.LoadSceneAsync(buildIndex);
            op.allowSceneActivation = false;

            float startTime = Time.unscaledTime;

            while (op.progress < 0.9f)
                yield return null;

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < minDisplayTime)
                yield return new WaitForSecondsRealtime(minDisplayTime - elapsed);

            op.allowSceneActivation = true;
            yield return op;

            yield return HideLoadingScreen();
            _isLoading = false;
        }

        private IEnumerator ShowLoadingScreen()
        {
            if (loadingScreenPrefab == null)
            {
                Debug.LogError("SceneLoader: loadingScreenPrefab is not assigned.");
                yield break;
            }

            _loadingScreenInstance = Instantiate(loadingScreenPrefab);
            DontDestroyOnLoad(_loadingScreenInstance);

            _canvasGroup = _loadingScreenInstance.GetComponentInChildren<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _loadingScreenInstance.AddComponent<CanvasGroup>();

            _loadingText = _loadingScreenInstance.GetComponentInChildren<TextMeshProUGUI>();

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
        }

        private IEnumerator HideLoadingScreen()
        {
            if (_canvasGroup == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;

            if (_loadingScreenInstance != null)
                Destroy(_loadingScreenInstance);
        }
    }
}
