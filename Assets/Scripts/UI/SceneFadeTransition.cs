using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneFadeTransition : MonoBehaviour
{
    private const int FadeSortingOrder = 32767;

    [SerializeField] private float defaultFadeDuration = 0.45f;
    [SerializeField] private Color fadeColor = Color.black;

    private static SceneFadeTransition _instance;

    private Image _fadeImage;
    private bool _isTransitioning;

    public static bool IsTransitioning => _instance != null && _instance._isTransitioning;

    public static void LoadScene(string sceneName, float fadeDuration = -1f)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        Instance.StartCoroutine(Instance.LoadSceneRoutine(sceneName, fadeDuration));
    }

    public static void LoadScene(int sceneBuildIndex, float fadeDuration = -1f)
    {
        if (sceneBuildIndex < 0)
            return;

        Instance.StartCoroutine(Instance.LoadSceneRoutine(sceneBuildIndex, fadeDuration));
    }

    private static SceneFadeTransition Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            GameObject transitionObject = new GameObject(nameof(SceneFadeTransition));
            _instance = transitionObject.AddComponent<SceneFadeTransition>();
            DontDestroyOnLoad(transitionObject);
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureFadeImage();
        SetAlpha(0f);
    }

    private IEnumerator LoadSceneRoutine(string sceneName, float fadeDuration)
    {
        if (_isTransitioning)
            yield break;

        yield return TransitionRoutine(() => SceneManager.LoadSceneAsync(sceneName), fadeDuration);
    }

    private IEnumerator LoadSceneRoutine(int sceneBuildIndex, float fadeDuration)
    {
        if (_isTransitioning)
            yield break;

        yield return TransitionRoutine(() => SceneManager.LoadSceneAsync(sceneBuildIndex), fadeDuration);
    }

    private IEnumerator TransitionRoutine(System.Func<AsyncOperation> loadScene, float fadeDuration)
    {
        _isTransitioning = true;
        EnsureFadeImage();
        _fadeImage.raycastTarget = true;

        float duration = GetDuration(fadeDuration);
        yield return FadeRoutine(0f, 1f, duration);

        Time.timeScale = 1f;
        AsyncOperation operation = loadScene.Invoke();
        while (operation != null && !operation.isDone)
            yield return null;

        yield return null;
        yield return FadeRoutine(1f, 0f, duration);

        _fadeImage.raycastTarget = false;
        _isTransitioning = false;
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlpha(to);
    }

    private float GetDuration(float requestedDuration)
    {
        return requestedDuration >= 0f ? requestedDuration : defaultFadeDuration;
    }

    private void EnsureFadeImage()
    {
        if (_fadeImage != null)
            return;

        GameObject canvasObject = new GameObject("Scene Fade Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = FadeSortingOrder;

        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("Fade Image");
        imageObject.transform.SetParent(canvasObject.transform, false);

        _fadeImage = imageObject.AddComponent<Image>();
        _fadeImage.color = fadeColor;
        _fadeImage.raycastTarget = false;

        RectTransform rectTransform = _fadeImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void SetAlpha(float alpha)
    {
        EnsureFadeImage();

        Color color = fadeColor;
        color.a = Mathf.Clamp01(alpha);
        _fadeImage.color = color;
    }
}
