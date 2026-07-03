using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using UIImage = UnityEngine.UI.Image;
using UIDocument = UnityEngine.UIElements.UIDocument;
using UIToggle = UnityEngine.UIElements.Toggle;
using UITextField = UnityEngine.UIElements.TextField;
using UIVisualElement = UnityEngine.UIElements.VisualElement;
using UIChangeEvent = UnityEngine.UIElements.ChangeEvent<string>;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;
using VisualTreeAsset = UnityEngine.UIElements.VisualTreeAsset;
using StyleSheet = UnityEngine.UIElements.StyleSheet;

public class PlayGroundDebugAssist : MonoBehaviour
{
    private const string PlayGroundSceneName = "PLAY_GROUND";
    private const string PlayerName = "Player3";
    private const string DebugPanelResourcePath = "UI/PlayGroundDebugPanel";

    [Header("Toggle")]
    [SerializeField] private bool aimAssistEnabled = true;
    [SerializeField] private bool espEnabled = true;
    [SerializeField] private bool penetrationAssistEnabled;
    [SerializeField] private Key togglePanelKey = Key.F5;
    [SerializeField] private Key toggleAimAssistKey = Key.F6;
    [SerializeField] private Key toggleEspKey = Key.F7;

    [Header("Aim Assist")]
    [SerializeField] private bool requireAimButton = true;
    [SerializeField] private float scanInterval = 0.05f;
    [SerializeField] private float maxAimDistance = 300f;
    [SerializeField, Range(0.01f, 100f)] private float maxScreenCenterDistance = 0.18f;
    [SerializeField] private bool preferHeadHitbox = true;

    [Header("ESP")]
    [SerializeField] private Color espColor = new Color(1f, 0.12f, 0.08f, 0.95f);
    [SerializeField] private Color espTextColor = new Color(1f, 0.88f, 0.35f, 1f);
    [SerializeField] private Vector2 espBoxPadding = new Vector2(10f, 16f);
    [SerializeField] private float espLineThickness = 2f;
    [SerializeField] private float minEspBoxSize = 24f;

    private readonly List<Health> _targets = new List<Health>();
    private readonly Dictionary<Health, EspEntry> _espEntries = new Dictionary<Health, EspEntry>();
    private readonly List<Health> _removeBuffer = new List<Health>();

    private Health _playerHealth;
    private PlayerStamina _playerStamina;
    private PlayerCamera _playerCamera;
    private PlayerWeapon _playerWeapon;
    private Camera _camera;
    private Transform _playerRoot;
    private RectTransform _espRoot;
    private UIDocument _panelDocument;
    private UIVisualElement _panelRoot;
    private UIToggle _aimAssistToggle;
    private UIToggle _espToggle;
    private UIToggle _penetrationAssistToggle;
    private UITextField _aimAssistRangeInput;
    private UITextField _healthInput;
    private UITextField _maxHealthInput;
    private UITextField _staminaInput;
    private UITextField _maxStaminaInput;
    private UITextField _staminaRecoveryInput;
    private UITextField _damageInput;
    private UITextField _fireRateInput;
    private Sprite _lineSprite;
    private float _nextScanTime;
    private bool _isPanelOpen;
    private bool _wasPlayerCameraEnabled;
    private bool _wasPlayerWeaponEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryCreateForCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForCurrentScene();
    }

    private static void TryCreateForCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!string.Equals(scene.name, PlayGroundSceneName, System.StringComparison.OrdinalIgnoreCase))
            return;

        if (FindFirstObjectByType<PlayGroundDebugAssist>() != null)
            return;

        GameObject assistObject = new GameObject("PLAY_GROUND Debug Assist");
        assistObject.AddComponent<PlayGroundDebugAssist>();
    }

    private void Awake()
    {
        ResolveReferences();
        CreateEspCanvas();
        CreateDebugPanel();
        RefreshTargets();
    }

    private void Update()
    {
        HandleToggleInput();

        if (Time.time >= _nextScanTime)
        {
            _nextScanTime = Time.time + scanInterval;
            RefreshTargets();
        }

        UpdateAimAssist();
        UpdateEsp();
    }

    private void OnDestroy()
    {
        foreach (EspEntry entry in _espEntries.Values)
            entry.Destroy();

        if (_playerWeapon != null)
            _playerWeapon.PenetrationAssistEnabled = false;

        _espEntries.Clear();
    }

    private void ResolveReferences()
    {
        if (_playerCamera == null)
        {
            GameObject player = GameObject.Find(PlayerName);
            if (player != null)
            {
                _playerRoot = player.transform;
                _playerHealth = player.GetComponent<Health>();
                _playerStamina = player.GetComponent<PlayerStamina>();
                _playerCamera = player.GetComponent<PlayerCamera>();
                _playerWeapon = player.GetComponent<PlayerWeapon>();
            }
        }

        if (_playerHealth == null && _playerRoot != null)
            _playerHealth = _playerRoot.GetComponent<Health>();

        if (_playerStamina == null && _playerRoot != null)
            _playerStamina = _playerRoot.GetComponent<PlayerStamina>();

        if (_camera == null)
            _camera = _playerCamera != null ? _playerCamera.Camera : Camera.main;

        if (_playerWeapon == null && _playerRoot != null)
            _playerWeapon = _playerRoot.GetComponent<PlayerWeapon>();

        if (_playerWeapon != null)
            _playerWeapon.PenetrationAssistEnabled = penetrationAssistEnabled;
    }

    private void HandleToggleInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[togglePanelKey].wasPressedThisFrame)
            SetPanelOpen(!_isPanelOpen);

        if (_isPanelOpen)
            return;

        if (keyboard[toggleAimAssistKey].wasPressedThisFrame)
            SetAimAssistEnabled(!aimAssistEnabled);

        if (keyboard[toggleEspKey].wasPressedThisFrame)
            SetEspEnabled(!espEnabled);
    }

    private void RefreshTargets()
    {
        ResolveReferences();
        _targets.Clear();

        Health[] healths = FindObjectsByType<Health>(FindObjectsSortMode.None);
        for (int i = 0; i < healths.Length; i++)
        {
            Health health = healths[i];
            if (!IsValidTarget(health))
                continue;

            _targets.Add(health);
        }
    }

    private bool IsValidTarget(Health health)
    {
        if (health == null || health.IsDead || !health.gameObject.activeInHierarchy)
            return false;

        if (_playerRoot != null && health.transform.root == _playerRoot.root)
            return false;

        if (string.Equals(health.gameObject.name, PlayerName, System.StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void UpdateAimAssist()
    {
        if (!aimAssistEnabled || _playerCamera == null || _camera == null)
            return;

        if (requireAimButton && (Mouse.current == null || !Mouse.current.rightButton.isPressed))
            return;

        Health target = FindBestAimTarget();
        if (target == null)
            return;

        _playerCamera.AimAtWorldPoint(GetAimPoint(target));
    }

    private Health FindBestAimTarget()
    {
        Health bestTarget = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < _targets.Count; i++)
        {
            Health target = _targets[i];
            if (!IsValidTarget(target))
                continue;

            Vector3 aimPoint = GetAimPoint(target);
            float distance = Vector3.Distance(_camera.transform.position, aimPoint);
            if (distance > maxAimDistance)
                continue;

            Vector3 viewportPoint = _camera.WorldToViewportPoint(aimPoint);
            if (viewportPoint.z <= 0f)
                continue;

            Vector2 screenOffset = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
            float centerDistance = screenOffset.magnitude;
            if (centerDistance > maxScreenCenterDistance)
                continue;

            float score = centerDistance + distance * 0.0005f;
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestTarget = target;
        }

        return bestTarget;
    }

    private Vector3 GetAimPoint(Health target)
    {
        if (target == null)
            return Vector3.zero;

        if (preferHeadHitbox)
        {
            Hitbox[] hitboxes = target.GetComponentsInChildren<Hitbox>(true);
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].IsHeadshot)
                    return GetRendererCenter(hitboxes[i].transform);
            }
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        if (TryGetRendererBounds(renderers, out Bounds bounds))
            return bounds.center + Vector3.up * bounds.extents.y * 0.35f;

        return target.transform.position + Vector3.up * 1.2f;
    }

    private Vector3 GetRendererCenter(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
        if (TryGetRendererBounds(renderers, out Bounds bounds))
            return bounds.center;

        return root.position;
    }

    private void CreateEspCanvas()
    {
        GameObject canvasObject = new GameObject("PLAY_GROUND ESP Canvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _espRoot = canvas.GetComponent<RectTransform>();
        _lineSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
    }

    private void CreateDebugPanel()
    {
        VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(DebugPanelResourcePath);
        if (visualTree == null)
        {
            Debug.LogWarning($"PlayGround debug panel resource not found: Resources/{DebugPanelResourcePath}.uxml");
            return;
        }

        GameObject panelObject = new GameObject("PLAY_GROUND Assist Panel");
        panelObject.transform.SetParent(transform, false);

        _panelDocument = panelObject.AddComponent<UIDocument>();
        _panelDocument.visualTreeAsset = visualTree;

        UIDocument existingDocument = FindFirstObjectByType<UIDocument>();
        if (existingDocument != null && existingDocument != _panelDocument)
            _panelDocument.panelSettings = existingDocument.panelSettings;

        StyleSheet styleSheet = Resources.Load<StyleSheet>(DebugPanelResourcePath);
        if (styleSheet != null)
            _panelDocument.rootVisualElement.styleSheets.Add(styleSheet);

        BindDebugPanelElements();
        SetPanelVisible(false);
    }

    private void BindDebugPanelElements()
    {
        if (_panelDocument == null || _panelDocument.rootVisualElement == null)
            return;

        _panelRoot = _panelDocument.rootVisualElement.Q<UIVisualElement>("AssistPanelRoot");
        _aimAssistToggle = _panelDocument.rootVisualElement.Q<UIToggle>("AimAssistToggle");
        _espToggle = _panelDocument.rootVisualElement.Q<UIToggle>("EspToggle");
        _penetrationAssistToggle = _panelDocument.rootVisualElement.Q<UIToggle>("PenetrationAssistToggle");

        _aimAssistRangeInput = BindDelayedField("AimAssistRangeInput", ApplyAimAssistDebugStats);
        _healthInput = BindDelayedField("HealthInput", ApplyPlayerDebugStats);
        _maxHealthInput = BindDelayedField("MaxHealthInput", ApplyPlayerDebugStats);
        _staminaInput = BindDelayedField("StaminaInput", ApplyPlayerDebugStats);
        _maxStaminaInput = BindDelayedField("MaxStaminaInput", ApplyPlayerDebugStats);
        _staminaRecoveryInput = BindDelayedField("StaminaRecoveryInput", ApplyPlayerDebugStats);
        _damageInput = BindDelayedField("DamageInput", ApplyWeaponDebugStats);
        _fireRateInput = BindDelayedField("FireRateInput", ApplyWeaponDebugStats);

        if (_aimAssistToggle != null)
            _aimAssistToggle.RegisterValueChangedCallback(evt => SetAimAssistEnabled(evt.newValue));

        if (_espToggle != null)
            _espToggle.RegisterValueChangedCallback(evt => SetEspEnabled(evt.newValue));

        if (_penetrationAssistToggle != null)
            _penetrationAssistToggle.RegisterValueChangedCallback(evt => SetPenetrationAssistEnabled(evt.newValue));
    }

    private UITextField BindDelayedField(string elementName, System.Action<UIChangeEvent> onChanged)
    {
        UITextField field = _panelDocument.rootVisualElement.Q<UITextField>(elementName);
        if (field == null)
        {
            Debug.LogWarning($"PlayGround debug panel field not found: {elementName}");
            return null;
        }

        field.isDelayed = true;
        field.RegisterValueChangedCallback(evt => onChanged(evt));
        return field;
    }

    private void SetPanelVisible(bool isVisible)
    {
        if (_panelRoot != null)
            _panelRoot.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetPanelOpen(bool isOpen)
    {
        if (_isPanelOpen == isOpen)
            return;

        _isPanelOpen = isOpen;

        SetPanelVisible(_isPanelOpen);

        if (_isPanelOpen)
        {
            ResolveReferences();
            _wasPlayerCameraEnabled = _playerCamera != null && _playerCamera.enabled;
            _wasPlayerWeaponEnabled = _playerWeapon != null && _playerWeapon.enabled;

            if (_playerCamera != null)
                _playerCamera.enabled = false;

            if (_playerWeapon != null)
                _playerWeapon.enabled = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SyncPanelToggles();
            RefreshDebugStatFields();
            return;
        }

        if (_playerCamera != null)
            _playerCamera.enabled = _wasPlayerCameraEnabled;

        if (_playerWeapon != null)
            _playerWeapon.enabled = _wasPlayerWeaponEnabled;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SyncPanelToggles()
    {
        if (_aimAssistToggle != null)
            _aimAssistToggle.SetValueWithoutNotify(aimAssistEnabled);

        if (_espToggle != null)
            _espToggle.SetValueWithoutNotify(espEnabled);

        if (_penetrationAssistToggle != null)
            _penetrationAssistToggle.SetValueWithoutNotify(penetrationAssistEnabled);
    }

    private void RefreshDebugStatFields()
    {
        ResolveReferences();

        SetInputText(_healthInput, _playerHealth != null ? _playerHealth.CurrentHealth : 0f);
        SetInputText(_maxHealthInput, _playerHealth != null ? _playerHealth.MaxHealth : 0f);
        SetInputText(_staminaInput, _playerStamina != null ? _playerStamina.CurrentStamina : 0f);
        SetInputText(_maxStaminaInput, _playerStamina != null ? _playerStamina.MaxStamina : 0f);
        SetInputText(_staminaRecoveryInput, _playerStamina != null ? _playerStamina.RecoveryPerSecond : 0f);
        SetInputText(_damageInput, _playerWeapon != null ? _playerWeapon.Damage : 0f);
        SetInputText(_fireRateInput, _playerWeapon != null ? _playerWeapon.FireRate : 0f);
        SetInputText(_aimAssistRangeInput, maxScreenCenterDistance);
    }

    private void SetInputText(UITextField input, float value)
    {
        if (input != null)
            input.SetValueWithoutNotify(value.ToString("0.##"));
    }

    private void ApplyPlayerDebugStats(UIChangeEvent _)
    {
        ResolveReferences();

        if (_playerHealth != null)
        {
            float currentHealth = ReadInputFloat(_healthInput, _playerHealth.CurrentHealth);
            float maxHealth = ReadInputFloat(_maxHealthInput, _playerHealth.MaxHealth);
            _playerHealth.SetDebugHealth(currentHealth, maxHealth);
        }

        if (_playerStamina != null)
        {
            float currentStamina = ReadInputFloat(_staminaInput, _playerStamina.CurrentStamina);
            float maxStamina = ReadInputFloat(_maxStaminaInput, _playerStamina.MaxStamina);
            float recovery = ReadInputFloat(_staminaRecoveryInput, _playerStamina.RecoveryPerSecond);
            _playerStamina.SetDebugStamina(currentStamina, maxStamina, recovery);
        }

        RefreshDebugStatFields();
    }

    private void ApplyAimAssistDebugStats(UIChangeEvent _)
    {
        maxScreenCenterDistance = Mathf.Clamp(ReadInputFloat(_aimAssistRangeInput, maxScreenCenterDistance), 0.01f, 1000f);
        RefreshDebugStatFields();
    }

    private void ApplyWeaponDebugStats(UIChangeEvent _)
    {
        ResolveReferences();
        if (_playerWeapon == null)
            return;

        _playerWeapon.SetDebugDamage(ReadInputFloat(_damageInput, _playerWeapon.Damage));
        _playerWeapon.SetDebugFireRate(ReadInputFloat(_fireRateInput, _playerWeapon.FireRate));
        RefreshDebugStatFields();
    }

    private float ReadInputFloat(UITextField input, float fallback)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.value))
            return fallback;

        return float.TryParse(input.value, out float value) ? value : fallback;
    }

    private void SetAimAssistEnabled(bool isEnabled)
    {
        aimAssistEnabled = isEnabled;
        SyncPanelToggles();
    }

    private void SetPenetrationAssistEnabled(bool isEnabled)
    {
        penetrationAssistEnabled = isEnabled;

        if (_playerWeapon != null)
            _playerWeapon.PenetrationAssistEnabled = penetrationAssistEnabled;

        SyncPanelToggles();
    }

    private void SetEspEnabled(bool isEnabled)
    {
        espEnabled = isEnabled;
        if (!espEnabled)
            SetAllEspVisible(false);

        SyncPanelToggles();
    }

    private void UpdateEsp()
    {
        if (_espRoot == null || _camera == null)
            return;

        if (!espEnabled)
        {
            SetAllEspVisible(false);
            return;
        }

        _removeBuffer.Clear();
        foreach (Health health in _espEntries.Keys)
        {
            if (!_targets.Contains(health) || !IsValidTarget(health))
                _removeBuffer.Add(health);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            Health health = _removeBuffer[i];
            _espEntries[health].Destroy();
            _espEntries.Remove(health);
        }

        for (int i = 0; i < _targets.Count; i++)
        {
            Health target = _targets[i];
            if (!IsValidTarget(target))
                continue;

            EspEntry entry = GetOrCreateEspEntry(target);
            UpdateEspEntry(target, entry);
        }
    }

    private EspEntry GetOrCreateEspEntry(Health target)
    {
        if (_espEntries.TryGetValue(target, out EspEntry entry))
            return entry;

        entry = new EspEntry(_espRoot, _lineSprite, espColor, espTextColor);
        _espEntries.Add(target, entry);
        return entry;
    }

    private void UpdateEspEntry(Health target, EspEntry entry)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
        if (!TryGetScreenBounds(renderers, out Rect screenRect))
        {
            entry.SetVisible(false);
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_espRoot, screenRect.center, null, out Vector2 localCenter);
        Vector2 size = new Vector2(
            Mathf.Max(minEspBoxSize, screenRect.width + espBoxPadding.x),
            Mathf.Max(minEspBoxSize, screenRect.height + espBoxPadding.y));

        float distance = Vector3.Distance(_camera.transform.position, GetAimPoint(target));
        entry.SetVisible(true);
        entry.SetBox(localCenter, size, espLineThickness);
        entry.SetText($"{target.name}\nHP {target.CurrentHealth:0}/{target.MaxHealth:0}\n{distance:0}m");
    }

    private bool TryGetScreenBounds(Renderer[] renderers, out Rect screenRect)
    {
        screenRect = default;

        if (!TryGetRendererBounds(renderers, out Bounds bounds))
            return false;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        bool hasPoint = false;
        Vector2 screenMin = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 screenMax = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 screenPoint = _camera.WorldToScreenPoint(corners[i]);
            if (screenPoint.z <= 0f)
                continue;

            hasPoint = true;
            screenMin = Vector2.Min(screenMin, screenPoint);
            screenMax = Vector2.Max(screenMax, screenPoint);
        }

        if (!hasPoint)
            return false;

        screenRect = Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);
        return true;
    }

    private static bool TryGetRendererBounds(Renderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void SetAllEspVisible(bool isVisible)
    {
        foreach (EspEntry entry in _espEntries.Values)
            entry.SetVisible(isVisible);
    }

    private sealed class EspEntry
    {
        private readonly GameObject _rootObject;
        private readonly RectTransform _root;
        private readonly RectTransform _top;
        private readonly RectTransform _bottom;
        private readonly RectTransform _left;
        private readonly RectTransform _right;
        private readonly TMP_Text _label;

        public EspEntry(RectTransform parent, Sprite lineSprite, Color lineColor, Color textColor)
        {
            _rootObject = new GameObject("ESP Target", typeof(RectTransform));
            _rootObject.transform.SetParent(parent, false);
            _root = _rootObject.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);

            _top = CreateLine("Top", _root, lineSprite, lineColor);
            _bottom = CreateLine("Bottom", _root, lineSprite, lineColor);
            _left = CreateLine("Left", _root, lineSprite, lineColor);
            _right = CreateLine("Right", _root, lineSprite, lineColor);

            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(_root, false);
            _label = labelObject.AddComponent<TextMeshProUGUI>();
            _label.raycastTarget = false;
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 16f;
            _label.fontStyle = FontStyles.Bold;
            _label.color = textColor;
        }

        public void SetVisible(bool isVisible)
        {
            _rootObject.SetActive(isVisible);
        }

        public void SetBox(Vector2 center, Vector2 size, float thickness)
        {
            _root.anchoredPosition = center;
            _root.sizeDelta = size;

            SetLine(_top, new Vector2(0f, size.y * 0.5f), new Vector2(size.x, thickness));
            SetLine(_bottom, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, thickness));
            SetLine(_left, new Vector2(-size.x * 0.5f, 0f), new Vector2(thickness, size.y));
            SetLine(_right, new Vector2(size.x * 0.5f, 0f), new Vector2(thickness, size.y));

            RectTransform labelRect = _label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 8f);
            labelRect.sizeDelta = new Vector2(Mathf.Max(140f, size.x + 60f), 64f);
        }

        public void SetText(string text)
        {
            _label.text = text;
        }

        public void Destroy()
        {
            if (_rootObject != null)
                Object.Destroy(_rootObject);
        }

        private static RectTransform CreateLine(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject lineObject = new GameObject(name, typeof(RectTransform));
            lineObject.transform.SetParent(parent, false);

            UIImage image = lineObject.AddComponent<UIImage>();
            image.raycastTarget = false;
            image.sprite = sprite;
            image.color = color;

            return lineObject.GetComponent<RectTransform>();
        }

        private static void SetLine(RectTransform line, Vector2 position, Vector2 size)
        {
            line.anchorMin = new Vector2(0.5f, 0.5f);
            line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = position;
            line.sizeDelta = size;
        }
    }
}
