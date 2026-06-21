using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Health))]
public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Health health;
    [SerializeField] private Camera targetCamera;

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.25f, 0f);
    [SerializeField] private Vector2 barSize = new Vector2(140f, 16f);
    [SerializeField] private float canvasScale = 0.01f;

    [Header("Visual")]
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.02f, 0.02f, 0.75f);
    [SerializeField] private Color fillColor = new Color(0.9f, 0.08f, 0.08f, 0.95f);
    [SerializeField] private Color borderColor = new Color(0f, 0f, 0f, 0.75f);

    [Header("Visibility")]
    [SerializeField] private bool hideWhenFullHealth = true;
    [SerializeField] private bool hideOnDeath = true;
    [SerializeField] private bool logHealthBarState;

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private Image _fillImage;
    private RectTransform _fillRect;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();

        CreateHealthBar();
        Refresh(health.CurrentHealth, health.MaxHealth);
    }

    private void OnEnable()
    {
        if (health == null)
            return;

        health.HealthChanged += HandleHealthChanged;
        health.Died += HandleDied;
        health.ResetHealth += HandleResetHealth;
    }

    private void OnDisable()
    {
        if (health == null)
            return;

        health.HealthChanged -= HandleHealthChanged;
        health.Died -= HandleDied;
        health.ResetHealth -= HandleResetHealth;
    }

    private void LateUpdate()
    {
        if (_canvas == null || !_canvas.gameObject.activeSelf)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        _canvas.transform.position = transform.position + worldOffset;

        if (targetCamera != null)
            _canvas.transform.rotation = targetCamera.transform.rotation;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        Refresh(currentHealth, maxHealth);
    }

    private void HandleDied()
    {
        if (hideOnDeath && _canvas != null)
            _canvas.gameObject.SetActive(false);
    }

    private void HandleResetHealth()
    {
        Refresh(health.CurrentHealth, health.MaxHealth);
    }

    private void Refresh(float currentHealth, float maxHealth)
    {
        if (_canvas == null || _fillImage == null || _fillRect == null)
            return;

        float normalized = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        _fillRect.localScale = new Vector3(normalized, 1f, 1f);

        bool shouldShow = currentHealth > 0f && (!hideWhenFullHealth || currentHealth < maxHealth);
        _canvas.gameObject.SetActive(shouldShow);

        if (logHealthBarState)
            Debug.Log($"{name} HP Bar | Current: {currentHealth:0.##}/{maxHealth:0.##}, Show: {shouldShow}", this);
    }

    private void CreateHealthBar()
    {
        if (_canvas != null)
            return;

        GameObject canvasObject = new GameObject("Enemy HP Bar");
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = worldOffset;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one * canvasScale;

        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;

        _canvasRect = canvasObject.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = barSize;

        CreateImage("Border", _canvasRect, borderColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform background = CreateImage(
            "Background",
            _canvasRect,
            backgroundColor,
            new Vector2(0.04f, 0.18f),
            new Vector2(0.96f, 0.82f),
            Vector2.zero,
            Vector2.zero);

        RectTransform fillRoot = CreateEmptyRect("Fill Root", background);
        fillRoot.anchorMin = Vector2.zero;
        fillRoot.anchorMax = Vector2.one;
        fillRoot.offsetMin = Vector2.zero;
        fillRoot.offsetMax = Vector2.zero;

        RectTransform fillRect = CreateImage("Fill", fillRoot, fillColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _fillRect = fillRect;
        _fillRect.pivot = new Vector2(1f, 0.5f);
        _fillImage = fillRect.GetComponent<Image>();
        _fillImage.type = Image.Type.Simple;
    }

    private static RectTransform CreateImage(
        string objectName,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;

        return rectTransform;
    }

    private static RectTransform CreateEmptyRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName);
        rectObject.transform.SetParent(parent, false);
        return rectObject.AddComponent<RectTransform>();
    }
}
