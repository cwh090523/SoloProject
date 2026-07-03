using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerWeapon))]
public class PlayerWeaponHud : MonoBehaviour
{
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Color hitMarkerColor = Color.white;
    [SerializeField] private float crosshairGap = 8f;
    [SerializeField] private float crosshairLength = 10f;
    [SerializeField] private float crosshairThickness = 2f;
    [SerializeField] private float maxSpreadGapBonus = 90f;
    [SerializeField] private float hitMarkerDuration = 0.18f;
    [SerializeField] private float hitMarkerGap = 10f;
    [SerializeField] private float hitMarkerLength = 34f;
    [SerializeField] private float hitMarkerThickness = 4f;
    [SerializeField] private float hitMarkerRotation = 45f;
    [Header("Optional Scene HUD")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Image[] serializedCrosshairLines;
    [SerializeField] private Image[] serializedHitMarkerLines;
    [SerializeField] private TMP_Text serializedAmmoText;
    [SerializeField] private TMP_Text serializedReloadText;
    [SerializeField] private Image serializedScopeOverlay;
    [SerializeField] private bool buildHudIfMissing = true;

    private Canvas _canvas;
    private Image[] _crosshairLines;
    private RectTransform[] _crosshairRects;
    private TMP_Text _ammoText;
    private TMP_Text _reloadText;
    private Image _scopeOverlay;
    private Image[] _hitMarkerLines;
    private float _hitMarkerUntil;

    private void Awake()
    {
        if (weapon == null)
            weapon = GetComponent<PlayerWeapon>();

        BuildHud();
        RefreshAmmo();
        RefreshReload(false);
        RefreshScopeOverlay(false);
        UpdateCrosshairSpread();
    }

    private void OnEnable()
    {
        if (weapon == null)
            return;

        weapon.HitConfirmed += ShowHitMarker;
        weapon.AmmoChanged += RefreshAmmo;
        weapon.ReloadStarted += ShowReloading;
        weapon.ReloadFinished += HideReloading;
    }

    private void OnDisable()
    {
        if (weapon == null)
            return;

        weapon.HitConfirmed -= ShowHitMarker;
        weapon.AmmoChanged -= RefreshAmmo;
        weapon.ReloadStarted -= ShowReloading;
        weapon.ReloadFinished -= HideReloading;
    }

    private void Update()
    {
        UpdateHitMarker();

        if (_crosshairLines == null)
            return;

        Color color = crosshairColor;
        foreach (Image line in _crosshairLines)
        {
            if (line == null)
                continue;

            line.color = color;
        }

        UpdateCrosshairSpread();
    }

    private void BuildHud()
    {
        if (TryUseSerializedHud())
            return;

        if (!buildHudIfMissing)
            return;

        _canvas = CreateCanvas();

        RectTransform root = _canvas.GetComponent<RectTransform>();
        _crosshairLines = new Image[4];
        _crosshairRects = new RectTransform[4];
        _crosshairLines[0] = CreateLine(root, "Crosshair Left", new Vector2(-crosshairGap - crosshairLength * 0.5f, 0f), new Vector2(crosshairLength, crosshairThickness));
        _crosshairLines[1] = CreateLine(root, "Crosshair Right", new Vector2(crosshairGap + crosshairLength * 0.5f, 0f), new Vector2(crosshairLength, crosshairThickness));
        _crosshairLines[2] = CreateLine(root, "Crosshair Top", new Vector2(0f, crosshairGap + crosshairLength * 0.5f), new Vector2(crosshairThickness, crosshairLength));
        _crosshairLines[3] = CreateLine(root, "Crosshair Bottom", new Vector2(0f, -crosshairGap - crosshairLength * 0.5f), new Vector2(crosshairThickness, crosshairLength));

        for (int i = 0; i < _crosshairLines.Length; i++)
            _crosshairRects[i] = _crosshairLines[i].rectTransform;

        _ammoText = CreateText(root, "Ammo Text", TextAlignmentOptions.BottomRight, 28, FontStyles.Bold);
        RectTransform ammoRect = _ammoText.rectTransform;
        ammoRect.anchorMin = new Vector2(1f, 0f);
        ammoRect.anchorMax = new Vector2(1f, 0f);
        ammoRect.pivot = new Vector2(1f, 0f);
        ammoRect.anchoredPosition = new Vector2(-32f, 24f);
        ammoRect.sizeDelta = new Vector2(260f, 72f);

        _reloadText = CreateText(root, "Reload Text", TextAlignmentOptions.Center, 20, FontStyles.Bold);
        _reloadText.color = new Color(1f, 0.84f, 0.3f);
        RectTransform reloadRect = _reloadText.rectTransform;
        reloadRect.anchorMin = new Vector2(0.5f, 0.34f);
        reloadRect.anchorMax = new Vector2(0.5f, 0.34f);
        reloadRect.pivot = new Vector2(0.5f, 0.5f);
        reloadRect.anchoredPosition = Vector2.zero;
        reloadRect.sizeDelta = new Vector2(180f, 36f);
        _reloadText.text = "RELOADING";

        _scopeOverlay = CreateScopeOverlay(root);
        _hitMarkerLines = CreateHitMarker(root);
        PrepareHitMarkerLines();
    }

    private bool TryUseSerializedHud()
    {
        if (hudCanvas == null)
            return false;

        if (serializedCrosshairLines == null || serializedCrosshairLines.Length != 4)
            return false;

        if (serializedAmmoText == null || serializedReloadText == null)
            return false;

        _canvas = hudCanvas;
        _crosshairLines = serializedCrosshairLines;
        _crosshairRects = new RectTransform[_crosshairLines.Length];

        for (int i = 0; i < _crosshairLines.Length; i++)
        {
            if (_crosshairLines[i] == null)
                return false;

            _crosshairLines[i].raycastTarget = false;
            _crosshairRects[i] = _crosshairLines[i].rectTransform;
        }

        _ammoText = serializedAmmoText;
        _reloadText = serializedReloadText;
        _scopeOverlay = serializedScopeOverlay;
        if (_scopeOverlay == null)
            _scopeOverlay = CreateScopeOverlay(hudCanvas.GetComponent<RectTransform>());

        _hitMarkerLines = TryUseSerializedHitMarker()
            ? serializedHitMarkerLines
            : CreateHitMarker(hudCanvas.GetComponent<RectTransform>());

        PrepareHitMarkerLines();

        _ammoText.raycastTarget = false;
        _reloadText.raycastTarget = false;
        if (_scopeOverlay != null)
        {
            _scopeOverlay.raycastTarget = false;
            ConfigureScopeOverlay(_scopeOverlay);
            _scopeOverlay.gameObject.SetActive(false);
        }
        return true;
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Player Weapon HUD");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private Image CreateLine(RectTransform parent, string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);

        Image image = lineObject.AddComponent<Image>();
        image.color = crosshairColor;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        return image;
    }

    private Image CreateRotatedLine(RectTransform parent, string objectName, Vector2 anchoredPosition, Vector2 size, float zRotation)
    {
        Image image = CreateLine(parent, objectName, anchoredPosition, size);
        image.rectTransform.localEulerAngles = new Vector3(0f, 0f, zRotation);
        return image;
    }

    private bool TryUseSerializedHitMarker()
    {
        if (serializedHitMarkerLines == null || serializedHitMarkerLines.Length != 4)
            return false;

        for (int i = 0; i < serializedHitMarkerLines.Length; i++)
        {
            if (serializedHitMarkerLines[i] == null)
                return false;
        }

        return true;
    }

    private void UpdateCrosshairSpread()
    {
        bool showScopeOverlay = weapon != null && weapon.IsAiming && weapon.UsesScopeOverlay;
        RefreshScopeOverlay(showScopeOverlay);

        if (weapon != null && weapon.IsAiming && weapon.UsesScopeOverlay)
        {
            foreach (Image image in _crosshairLines)
            {
                if (image == null)
                    continue;

                image.gameObject.SetActive(false);
            }
            return;
        }
        if (_crosshairRects == null || weapon == null)
            return;

        foreach (Image image in _crosshairLines)
        {
            if (image == null)
                continue;

            image.gameObject.SetActive(true);
        }
        
        float gap = crosshairGap + maxSpreadGapBonus * weapon.SpreadRatio;
        float halfLength = crosshairLength * 0.5f;

        _crosshairRects[0].anchoredPosition = new Vector2(-gap - halfLength, 0f);
        _crosshairRects[1].anchoredPosition = new Vector2(gap + halfLength, 0f);
        _crosshairRects[2].anchoredPosition = new Vector2(0f, gap + halfLength);
        _crosshairRects[3].anchoredPosition = new Vector2(0f, -gap - halfLength);
    }

    private Image CreateScopeOverlay(RectTransform parent)
    {
        GameObject overlayObject = new GameObject("Scope Overlay");
        overlayObject.transform.SetParent(parent, false);

        Image image = overlayObject.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        image.gameObject.SetActive(false);
        ConfigureScopeOverlay(image);

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = Vector2.zero;

        return image;
    }

    private void ConfigureScopeOverlay(Image image)
    {
        if (image == null)
            return;

        image.preserveAspect = false;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private Image[] CreateHitMarker(RectTransform parent)
    {
        Image[] lines = new Image[4];
        Vector2 size = new Vector2(hitMarkerLength, hitMarkerThickness);

        lines[0] = CreateRotatedLine(parent, "Hit Marker Top Left", new Vector2(-hitMarkerGap, hitMarkerGap), size, -hitMarkerRotation);
        lines[1] = CreateRotatedLine(parent, "Hit Marker Top Right", new Vector2(hitMarkerGap, hitMarkerGap), size, hitMarkerRotation);
        lines[2] = CreateRotatedLine(parent, "Hit Marker Bottom Left", new Vector2(-hitMarkerGap, -hitMarkerGap), size, hitMarkerRotation);
        lines[3] = CreateRotatedLine(parent, "Hit Marker Bottom Right", new Vector2(hitMarkerGap, -hitMarkerGap), size, -hitMarkerRotation);

        for (int i = 0; i < lines.Length; i++)
            PrepareHitMarkerLine(lines[i]);

        return lines;
    }

    private void PrepareHitMarkerLines()
    {
        if (_hitMarkerLines == null)
            return;

        for (int i = 0; i < _hitMarkerLines.Length; i++)
            PrepareHitMarkerLine(_hitMarkerLines[i]);
    }

    private void PrepareHitMarkerLine(Image line)
    {
        if (line == null)
            return;

        Color visibleHitMarkerColor = hitMarkerColor;
        if (visibleHitMarkerColor.a <= 0f)
            visibleHitMarkerColor.a = 1f;

        line.color = visibleHitMarkerColor;
        line.raycastTarget = false;
        line.transform.SetAsLastSibling();
        line.gameObject.SetActive(false);
    }

    private void UpdateHitMarker()
    {
        if (_hitMarkerLines == null)
            return;

        bool isVisible = Time.time < _hitMarkerUntil;
        float remainRatio = hitMarkerDuration <= 0f ? 0f : Mathf.Clamp01((_hitMarkerUntil - Time.time) / hitMarkerDuration);
        Color markerColor = hitMarkerColor;
        if (markerColor.a <= 0f)
            markerColor.a = 1f;
        markerColor.a *= remainRatio;

        for (int i = 0; i < _hitMarkerLines.Length; i++)
        {
            Image line = _hitMarkerLines[i];
            if (line == null)
                continue;

            line.gameObject.SetActive(isVisible);
            line.color = markerColor;
        }
    }

    private void RefreshScopeOverlay(bool isVisible)
    {
        if (_scopeOverlay == null)
            return;

        Sprite scopeSprite = weapon != null ? weapon.ScopeOverlaySprite : null;
        _scopeOverlay.sprite = scopeSprite;
        UpdateScopeOverlayLayout(scopeSprite);
        _scopeOverlay.gameObject.SetActive(isVisible && scopeSprite != null);
    }

    private void UpdateScopeOverlayLayout(Sprite scopeSprite)
    {
        if (_scopeOverlay == null || scopeSprite == null)
            return;

        RectTransform rect = _scopeOverlay.rectTransform;
        RectTransform parent = rect.parent as RectTransform;

        float parentWidth = parent != null && parent.rect.width > 0f ? parent.rect.width : Screen.width;
        float parentHeight = parent != null && parent.rect.height > 0f ? parent.rect.height : Screen.height;
        if (parentWidth <= 0f || parentHeight <= 0f)
            return;

        float spriteWidth = Mathf.Max(1f, scopeSprite.rect.width);
        float spriteHeight = Mathf.Max(1f, scopeSprite.rect.height);
        float parentAspect = parentWidth / parentHeight;
        float spriteAspect = spriteWidth / spriteHeight;

        Vector2 size;
        if (parentAspect > spriteAspect)
        {
            size = new Vector2(parentWidth, parentWidth / spriteAspect);
        }
        else
        {
            size = new Vector2(parentHeight * spriteAspect, parentHeight);
        }

        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    private TMP_Text CreateText(RectTransform parent, string objectName, TextAlignmentOptions alignment, int fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;

        return text;
    }

    private void RefreshAmmo()
    {
        if (_ammoText == null || weapon == null)
            return;

        _ammoText.text = $"{weapon.CurrentAmmo:00} / {weapon.ReserveAmmo:000}";
    }

    private void ShowReloading()
    {
        RefreshReload(true);
    }

    private void HideReloading()
    {
        RefreshReload(false);
        RefreshAmmo();
    }

    private void RefreshReload(bool isVisible)
    {
        if (_reloadText != null)
            _reloadText.enabled = isVisible;
    }

    private void ShowHitMarker()
    {
        _hitMarkerUntil = Time.time + hitMarkerDuration;
        UpdateHitMarker();
    }
}
