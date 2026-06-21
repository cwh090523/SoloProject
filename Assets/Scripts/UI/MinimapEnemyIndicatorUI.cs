using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapEnemyIndicatorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private RectTransform minimapRect;
    [SerializeField] private Camera minimapCamera;

    [Header("Indicator")]
    [SerializeField] private Color indicatorColor = new Color(0.9f, 0.08f, 0.08f, 1f);
    [SerializeField] private Vector2 indicatorSize = new Vector2(22f, 26f);
    [SerializeField] private float edgePadding = 18f;

    private readonly Dictionary<Health, RectTransform> _indicators = new Dictionary<Health, RectTransform>();
    private readonly List<Health> _removeBuffer = new List<Health>();
    private Sprite _triangleSprite;
    private RectTransform _indicatorRoot;
    private Transform _player;

    public void Initialize(EnemyWaveSpawner spawner)
    {
        waveSpawner = spawner;
        ResolveReferences();
        Subscribe();
        RegisterExistingEnemies();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        ResolveReferences();
        UpdateIndicators();
    }

    private void ResolveReferences()
    {
        if (waveSpawner == null)
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();

        if (_player == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
                _player = playerController.transform;
        }

        if (minimapCamera == null && _player != null)
        {
            Camera[] cameras = _player.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].name.IndexOf("MiniMap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    minimapCamera = cameras[i];
                    break;
                }
            }
        }

        if (minimapRect == null)
        {
            RawImage[] rawImages = FindObjectsByType<RawImage>(FindObjectsSortMode.None);
            for (int i = 0; i < rawImages.Length; i++)
            {
                if (rawImages[i].name.IndexOf("MiniMap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    minimapRect = rawImages[i].rectTransform;
                    break;
                }
            }
        }

        EnsureIndicatorRoot();
    }

    private void EnsureIndicatorRoot()
    {
        if (_indicatorRoot != null || minimapRect == null)
            return;

        GameObject rootObject = new GameObject("Offscreen Enemy Indicators", typeof(RectTransform));
        _indicatorRoot = rootObject.GetComponent<RectTransform>();
        _indicatorRoot.SetParent(minimapRect, false);
        _indicatorRoot.anchorMin = Vector2.zero;
        _indicatorRoot.anchorMax = Vector2.one;
        _indicatorRoot.offsetMin = Vector2.zero;
        _indicatorRoot.offsetMax = Vector2.zero;
        _indicatorRoot.SetAsLastSibling();
    }

    private void Subscribe()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.EnemyRegistered -= RegisterEnemy;
        waveSpawner.EnemyRegistered += RegisterEnemy;
        waveSpawner.EnemyRemoved -= RemoveEnemy;
        waveSpawner.EnemyRemoved += RemoveEnemy;
    }

    private void Unsubscribe()
    {
        if (waveSpawner == null)
            return;

        waveSpawner.EnemyRegistered -= RegisterEnemy;
        waveSpawner.EnemyRemoved -= RemoveEnemy;
    }

    private void RegisterExistingEnemies()
    {
        if (waveSpawner == null)
            return;

        IReadOnlyList<Health> enemies = waveSpawner.AliveEnemies;
        for (int i = 0; i < enemies.Count; i++)
            RegisterEnemy(enemies[i]);
    }

    private void RegisterEnemy(Health enemyHealth)
    {
        if (enemyHealth == null || _indicators.ContainsKey(enemyHealth))
            return;

        EnsureIndicatorRoot();
        if (_indicatorRoot == null)
            return;

        GameObject indicatorObject = new GameObject($"{enemyHealth.name} Direction", typeof(RectTransform), typeof(Image));
        RectTransform indicator = indicatorObject.GetComponent<RectTransform>();
        indicator.SetParent(_indicatorRoot, false);
        indicator.anchorMin = new Vector2(0.5f, 0.5f);
        indicator.anchorMax = new Vector2(0.5f, 0.5f);
        indicator.pivot = new Vector2(0.5f, 0.5f);
        indicator.sizeDelta = indicatorSize;

        Image image = indicatorObject.GetComponent<Image>();
        image.sprite = GetTriangleSprite();
        image.color = indicatorColor;
        image.raycastTarget = false;

        _indicators.Add(enemyHealth, indicator);
    }

    private void RemoveEnemy(Health enemyHealth)
    {
        if (ReferenceEquals(enemyHealth, null) || !_indicators.TryGetValue(enemyHealth, out RectTransform indicator))
            return;

        if (indicator != null)
            Destroy(indicator.gameObject);

        _indicators.Remove(enemyHealth);
    }

    private void UpdateIndicators()
    {
        if (minimapRect == null || minimapCamera == null)
            return;

        Rect mapBounds = minimapRect.rect;
        float halfWidth = Mathf.Max(1f, mapBounds.width * 0.5f - edgePadding);
        float halfHeight = Mathf.Max(1f, mapBounds.height * 0.5f - edgePadding);
        _removeBuffer.Clear();

        foreach (KeyValuePair<Health, RectTransform> pair in _indicators)
        {
            Health enemy = pair.Key;
            RectTransform indicator = pair.Value;
            if (enemy == null || indicator == null || enemy.IsDead)
            {
                _removeBuffer.Add(enemy);
                continue;
            }

            Vector3 viewport = minimapCamera.WorldToViewportPoint(enemy.transform.position);
            bool isInsideMap = viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
            indicator.gameObject.SetActive(!isInsideMap);
            if (isInsideMap)
                continue;

            Vector3 worldOffset = enemy.transform.position - minimapCamera.transform.position;
            Vector2 direction = new Vector2(
                Vector3.Dot(worldOffset, minimapCamera.transform.right),
                Vector3.Dot(worldOffset, minimapCamera.transform.up));

            if (direction.sqrMagnitude <= 0.001f)
                direction = Vector2.up;

            float edgeScale = Mathf.Min(
                halfWidth / Mathf.Max(0.001f, Mathf.Abs(direction.x)),
                halfHeight / Mathf.Max(0.001f, Mathf.Abs(direction.y)));
            Vector2 edgePosition = direction * edgeScale;
            indicator.anchoredPosition = edgePosition;
            indicator.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
            RemoveEnemy(_removeBuffer[i]);
    }

    private Sprite GetTriangleSprite()
    {
        if (_triangleSprite != null)
            return _triangleSprite;

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Minimap Triangle";
        texture.filterMode = FilterMode.Bilinear;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < size; y++)
        {
            float halfTriangleWidth = (size - 1 - y) * 0.5f;
            for (int x = 0; x < size; x++)
            {
                bool isInside = Mathf.Abs(x - (size - 1) * 0.5f) <= halfTriangleWidth;
                texture.SetPixel(x, y, isInside ? Color.white : clear);
            }
        }

        texture.Apply();
        _triangleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        _triangleSprite.name = "Runtime Minimap Triangle";
        return _triangleSprite;
    }
}
