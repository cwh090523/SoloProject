using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerWeapon))]
public class PlayerWeaponHud : MonoBehaviour
{
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private Color hitMarkerColor = new Color(1f, 0.9f, 0.25f);
    [SerializeField] private float crosshairGap = 8f;
    [SerializeField] private float crosshairLength = 10f;
    [SerializeField] private float crosshairThickness = 2f;
    [SerializeField] private float maxSpreadGapBonus = 90f;
    [SerializeField] private float hitMarkerDuration = 0.12f;

    private Canvas _canvas;
    private Image[] _crosshairLines;
    private RectTransform[] _crosshairRects;
    private Text _ammoText;
    private Text _reloadText;
    private float _hitMarkerUntil;

    private void Awake()
    {
        if (weapon == null)
            weapon = GetComponent<PlayerWeapon>();

        BuildHud();
        RefreshAmmo();
        RefreshReload(false);
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
        Color color = Time.time < _hitMarkerUntil ? hitMarkerColor : crosshairColor;
        foreach (Image line in _crosshairLines)
            line.color = color;

        UpdateCrosshairSpread();
    }

    private void BuildHud()
    {
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

        _ammoText = CreateText(root, "Ammo Text", TextAnchor.LowerRight, 28, FontStyle.Bold);
        RectTransform ammoRect = _ammoText.rectTransform;
        ammoRect.anchorMin = new Vector2(1f, 0f);
        ammoRect.anchorMax = new Vector2(1f, 0f);
        ammoRect.pivot = new Vector2(1f, 0f);
        ammoRect.anchoredPosition = new Vector2(-32f, 24f);
        ammoRect.sizeDelta = new Vector2(260f, 72f);

        _reloadText = CreateText(root, "Reload Text", TextAnchor.MiddleCenter, 20, FontStyle.Bold);
        _reloadText.color = new Color(1f, 0.84f, 0.3f);
        RectTransform reloadRect = _reloadText.rectTransform;
        reloadRect.anchorMin = new Vector2(0.5f, 0.34f);
        reloadRect.anchorMax = new Vector2(0.5f, 0.34f);
        reloadRect.pivot = new Vector2(0.5f, 0.5f);
        reloadRect.anchoredPosition = Vector2.zero;
        reloadRect.sizeDelta = new Vector2(180f, 36f);
        _reloadText.text = "RELOADING";
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

    private void UpdateCrosshairSpread()
    {
        if (_crosshairRects == null || weapon == null)
            return;

        float gap = crosshairGap + maxSpreadGapBonus * weapon.SpreadRatio;
        float halfLength = crosshairLength * 0.5f;

        _crosshairRects[0].anchoredPosition = new Vector2(-gap - halfLength, 0f);
        _crosshairRects[1].anchoredPosition = new Vector2(gap + halfLength, 0f);
        _crosshairRects[2].anchoredPosition = new Vector2(0f, gap + halfLength);
        _crosshairRects[3].anchoredPosition = new Vector2(0f, -gap - halfLength);
    }

    private Text CreateText(RectTransform parent, string objectName, TextAnchor alignment, int fontSize, FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.raycastTarget = false;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
    }
}
