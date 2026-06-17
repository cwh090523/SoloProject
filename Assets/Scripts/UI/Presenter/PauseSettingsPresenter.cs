using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace UI.Presenter
{
    public class PauseSettingsPresenter : MonoBehaviour
    {
        private const string PauseUiResourcePath = "UI/PauseSettingsUI";

        [SerializeField] private UIDocument document;
        [SerializeField] private GameStateManager stateManager;

        private readonly List<Resolution> _resolutionOptions = new();
        private readonly List<string> _resolutionLabels = new();

        private VisualElement _root;
        private Button _resumeButton;
        private Button _titleButton;
        private Slider _mouseSensitivitySlider;
        private Slider _masterVolumeSlider;
        private Slider _bgmVolumeSlider;
        private Slider _sfxVolumeSlider;
        private DropdownField _resolutionDropdown;
        private Toggle _fullscreenToggle;
        private VisualElement _mouseSensitivityValueContainer;
        private Label _mouseSensitivityValueLabel;
        private TextField _mouseSensitivityInput;
        private Label _masterVolumeValueLabel;
        private Label _bgmVolumeValueLabel;
        private Label _sfxVolumeValueLabel;

        public bool IsOpen => _root != null && _root.style.display == DisplayStyle.Flex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            CreateForCurrentScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateForCurrentScene();
        }

        private static void CreateForCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded || activeScene.name.ToUpperInvariant().Contains("TITLE"))
                return;

            if (FindFirstObjectByType<PauseSettingsPresenter>() != null)
                return;

            GameStateManager manager = FindFirstObjectByType<GameStateManager>();
            if (manager == null)
                return;

            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(PauseUiResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"Pause UI resource not found: Resources/{PauseUiResourcePath}.uxml");
                return;
            }

            UIDocument existingDocument = FindFirstObjectByType<UIDocument>();

            GameObject pauseObject = new GameObject("Pause Settings UI");
            UIDocument uiDocument = pauseObject.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = visualTree;

            if (existingDocument != null)
                uiDocument.panelSettings = existingDocument.panelSettings;

            PauseSettingsPresenter presenter = pauseObject.AddComponent<PauseSettingsPresenter>();
            presenter.document = uiDocument;
            presenter.stateManager = manager;
        }

        private void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (stateManager == null)
                stateManager = FindFirstObjectByType<GameStateManager>();

            BindElements();
            ConfigureTextInputStyle(_mouseSensitivityInput, new Color(1f, 0.84f, 0.28f, 1f), TextAnchor.MiddleRight);
            BuildResolutionOptions();
            LoadSettingsToUI();
            HideWithoutResuming();
        }

        private void OnEnable()
        {
            if (_resumeButton != null)
                _resumeButton.clicked += ClosePause;

            if (_titleButton != null)
                _titleButton.clicked += GoTitle;

            RegisterSettingsCallbacks();
        }

        private void OnDisable()
        {
            if (_resumeButton != null)
                _resumeButton.clicked -= ClosePause;

            if (_titleButton != null)
                _titleButton.clicked -= GoTitle;

            UnregisterSettingsCallbacks();

            if (stateManager != null && stateManager.IsPaused)
                stateManager.ResumeGame();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (IsOpen)
                ClosePause();
            else
                OpenPause();
        }

        private void BindElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            _root = root.Q<VisualElement>("PauseRoot");
            _resumeButton = root.Q<Button>("ResumeButton");
            _titleButton = root.Q<Button>("TitleButton");
            _mouseSensitivitySlider = root.Q<Slider>("MouseSensitivitySlider");
            _masterVolumeSlider = root.Q<Slider>("MasterVolumeSlider");
            _bgmVolumeSlider = root.Q<Slider>("BgmVolumeSlider");
            _sfxVolumeSlider = root.Q<Slider>("SfxVolumeSlider");
            _resolutionDropdown = root.Q<DropdownField>("ResolutionDropdown");
            _fullscreenToggle = root.Q<Toggle>("FullscreenToggle");
            _mouseSensitivityValueContainer = root.Q<VisualElement>("MouseSensitivityValueContainer");
            _mouseSensitivityValueLabel = root.Q<Label>("MouseSensitivityValueLabel");
            _mouseSensitivityInput = root.Q<TextField>("MouseSensitivityInput");
            _masterVolumeValueLabel = root.Q<Label>("MasterVolumeValueLabel");
            _bgmVolumeValueLabel = root.Q<Label>("BgmVolumeValueLabel");
            _sfxVolumeValueLabel = root.Q<Label>("SfxVolumeValueLabel");
        }

        private void ConfigureTextInputStyle(TextField textField, Color textColor, TextAnchor textAlign)
        {
            if (textField == null)
                return;

            VisualElement textInput = textField.Q<VisualElement>(className: "unity-text-input");
            if (textInput == null)
                return;

            textInput.style.color = textColor;
            textInput.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            textInput.style.unityTextAlign = textAlign;
        }

        private void RegisterSettingsCallbacks()
        {
            _mouseSensitivitySlider?.RegisterValueChangedCallback(HandleMouseSensitivityChanged);
            _mouseSensitivityValueContainer?.RegisterCallback<MouseDownEvent>(HandleMouseSensitivityValueClicked);
            _mouseSensitivityInput?.RegisterValueChangedCallback(HandleMouseSensitivityInputChanged);
            _mouseSensitivityInput?.RegisterCallback<KeyDownEvent>(HandleMouseSensitivityInputKeyDown);
            _mouseSensitivityInput?.RegisterCallback<FocusOutEvent>(HandleMouseSensitivityInputFocusOut);
            _masterVolumeSlider?.RegisterValueChangedCallback(HandleMasterVolumeChanged);
            _bgmVolumeSlider?.RegisterValueChangedCallback(HandleBgmVolumeChanged);
            _sfxVolumeSlider?.RegisterValueChangedCallback(HandleSfxVolumeChanged);
            _resolutionDropdown?.RegisterValueChangedCallback(HandleResolutionChanged);
            _fullscreenToggle?.RegisterValueChangedCallback(HandleFullscreenChanged);
        }

        private void UnregisterSettingsCallbacks()
        {
            _mouseSensitivitySlider?.UnregisterValueChangedCallback(HandleMouseSensitivityChanged);
            _mouseSensitivityValueContainer?.UnregisterCallback<MouseDownEvent>(HandleMouseSensitivityValueClicked);
            _mouseSensitivityInput?.UnregisterValueChangedCallback(HandleMouseSensitivityInputChanged);
            _mouseSensitivityInput?.UnregisterCallback<KeyDownEvent>(HandleMouseSensitivityInputKeyDown);
            _mouseSensitivityInput?.UnregisterCallback<FocusOutEvent>(HandleMouseSensitivityInputFocusOut);
            _masterVolumeSlider?.UnregisterValueChangedCallback(HandleMasterVolumeChanged);
            _bgmVolumeSlider?.UnregisterValueChangedCallback(HandleBgmVolumeChanged);
            _sfxVolumeSlider?.UnregisterValueChangedCallback(HandleSfxVolumeChanged);
            _resolutionDropdown?.UnregisterValueChangedCallback(HandleResolutionChanged);
            _fullscreenToggle?.UnregisterValueChangedCallback(HandleFullscreenChanged);
        }

        private void OpenPause()
        {
            if (stateManager == null || !stateManager.CanPause())
                return;

            stateManager.PauseGame();
            LoadSettingsToUI();

            if (_root != null)
                _root.style.display = DisplayStyle.Flex;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ClosePause()
        {
            HideWithoutResuming();

            if (stateManager != null && stateManager.IsPaused)
                stateManager.ResumeGame();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void GoTitle()
        {
            HideWithoutResuming();

            if (stateManager != null && stateManager.IsPaused)
                stateManager.ResumeGame();

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneFadeTransition.LoadScene("TEST_TITLE");
        }

        private void HideWithoutResuming()
        {
            HideMouseSensitivityInput();

            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        private void BuildResolutionOptions()
        {
            _resolutionOptions.Clear();
            _resolutionLabels.Clear();

            Resolution[] resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                Resolution resolution = resolutions[i];
                if (resolution.width < 800 || resolution.height < 600)
                    continue;

                string label = GetResolutionLabel(resolution);
                if (_resolutionLabels.Contains(label))
                    continue;

                _resolutionOptions.Add(resolution);
                _resolutionLabels.Add(label);
            }

            if (_resolutionOptions.Count == 0)
            {
                Resolution current = Screen.currentResolution;
                _resolutionOptions.Add(current);
                _resolutionLabels.Add(GetResolutionLabel(current));
            }

            if (_resolutionDropdown != null)
                _resolutionDropdown.choices = _resolutionLabels;
        }

        private void LoadSettingsToUI()
        {
            GameSettings.ApplyAudio();

            SetSliderValue(_mouseSensitivitySlider, GameSettings.MouseSensitivity);
            SetSliderValue(_masterVolumeSlider, GameSettings.MasterVolume);
            SetSliderValue(_bgmVolumeSlider, GameSettings.BgmVolume);
            SetSliderValue(_sfxVolumeSlider, GameSettings.SfxVolume);

            if (_fullscreenToggle != null)
                _fullscreenToggle.SetValueWithoutNotify(GameSettings.Fullscreen);

            int resolutionIndex = FindResolutionIndex(GameSettings.ResolutionWidth, GameSettings.ResolutionHeight);
            if (_resolutionDropdown != null && _resolutionLabels.Count > 0)
            {
                resolutionIndex = Mathf.Clamp(resolutionIndex, 0, _resolutionLabels.Count - 1);
                _resolutionDropdown.index = resolutionIndex;
                _resolutionDropdown.SetValueWithoutNotify(_resolutionLabels[resolutionIndex]);
            }

            RefreshSettingLabels();
        }

        private void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private void HandleMouseSensitivityChanged(ChangeEvent<float> evt)
        {
            GameSettings.MouseSensitivity = evt.newValue;
            ApplyMouseSensitivityToActiveCameras();
            GameSettings.Save();
            RefreshSettingLabels();
        }

        private void HandleMouseSensitivityValueClicked(MouseDownEvent evt)
        {
            ShowMouseSensitivityInput();
            evt.StopPropagation();
        }

        private void HandleMouseSensitivityInputChanged(ChangeEvent<string> evt)
        {
            if (!TryParseMouseSensitivity(evt.newValue, out float sensitivity))
                return;

            GameSettings.MouseSensitivity = sensitivity;
            ApplyMouseSensitivityToActiveCameras();
            GameSettings.Save();

            if (_mouseSensitivitySlider != null)
                _mouseSensitivitySlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        }

        private void HandleMouseSensitivityInputFocusOut(FocusOutEvent evt)
        {
            HideMouseSensitivityInput();
            RefreshSettingLabels();
        }

        private void HandleMouseSensitivityInputKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;

            HideMouseSensitivityInput();
            RefreshSettingLabels();
            evt.StopPropagation();
        }

        private void HandleMasterVolumeChanged(ChangeEvent<float> evt)
        {
            GameSettings.MasterVolume = evt.newValue;
            GameSettings.ApplyAudio();
            GameSettings.Save();
            RefreshSettingLabels();
        }

        private void HandleBgmVolumeChanged(ChangeEvent<float> evt)
        {
            GameSettings.BgmVolume = evt.newValue;
            GameSettings.Save();
            RefreshSettingLabels();
        }

        private void HandleSfxVolumeChanged(ChangeEvent<float> evt)
        {
            GameSettings.SfxVolume = evt.newValue;
            GameSettings.Save();
            RefreshSettingLabels();
        }

        private void HandleResolutionChanged(ChangeEvent<string> evt)
        {
            int index = _resolutionLabels.IndexOf(evt.newValue);
            if (index < 0 || index >= _resolutionOptions.Count)
                return;

            Resolution resolution = _resolutionOptions[index];
            GameSettings.ResolutionWidth = resolution.width;
            GameSettings.ResolutionHeight = resolution.height;
            GameSettings.ResolutionRefreshRate = Mathf.RoundToInt((float)resolution.refreshRateRatio.value);
            GameSettings.ApplyDisplay();
            GameSettings.Save();
        }

        private void HandleFullscreenChanged(ChangeEvent<bool> evt)
        {
            GameSettings.Fullscreen = evt.newValue;
            GameSettings.ApplyDisplay();
            GameSettings.Save();
        }

        private void RefreshSettingLabels()
        {
            string mouseSensitivityText = FormatMouseSensitivity(GameSettings.MouseSensitivity);

            if (_mouseSensitivityValueLabel != null)
                _mouseSensitivityValueLabel.text = mouseSensitivityText;

            if (_mouseSensitivityInput != null)
                _mouseSensitivityInput.SetValueWithoutNotify(mouseSensitivityText);

            if (_masterVolumeValueLabel != null)
                _masterVolumeValueLabel.text = FormatPercent(GameSettings.MasterVolume);

            if (_bgmVolumeValueLabel != null)
                _bgmVolumeValueLabel.text = FormatPercent(GameSettings.BgmVolume);

            if (_sfxVolumeValueLabel != null)
                _sfxVolumeValueLabel.text = FormatPercent(GameSettings.SfxVolume);
        }

        private void ShowMouseSensitivityInput()
        {
            RefreshSettingLabels();

            if (_mouseSensitivityValueLabel != null)
                _mouseSensitivityValueLabel.style.display = DisplayStyle.None;

            if (_mouseSensitivityInput == null)
                return;

            _mouseSensitivityInput.style.display = DisplayStyle.Flex;
            _mouseSensitivityInput.schedule.Execute(() =>
            {
                _mouseSensitivityInput.Focus();
                _mouseSensitivityInput.SelectAll();
            });
        }

        private void HideMouseSensitivityInput()
        {
            if (_mouseSensitivityInput != null)
                _mouseSensitivityInput.style.display = DisplayStyle.None;

            if (_mouseSensitivityValueLabel != null)
                _mouseSensitivityValueLabel.style.display = DisplayStyle.Flex;
        }

        private void ApplyMouseSensitivityToActiveCameras()
        {
            PlayerCamera[] cameras = FindObjectsByType<PlayerCamera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
                cameras[i].mouseSpeed = GameSettings.MouseSensitivity;
        }

        private string FormatPercent(float value)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)}%";
        }

        private string FormatMouseSensitivity(float value)
        {
            return Mathf.Clamp(value, GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity)
                .ToString("0.000", CultureInfo.InvariantCulture);
        }

        private bool TryParseMouseSensitivity(string text, out float sensitivity)
        {
            bool parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out sensitivity) ||
                          float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out sensitivity);

            if (!parsed)
                return false;

            sensitivity = Mathf.Clamp(sensitivity, GameSettings.MinMouseSensitivity, GameSettings.MaxMouseSensitivity);
            return true;
        }

        private int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < _resolutionOptions.Count; i++)
            {
                if (_resolutionOptions[i].width == width && _resolutionOptions[i].height == height)
                    return i;
            }

            return Mathf.Max(0, _resolutionOptions.Count - 1);
        }

        private string GetResolutionLabel(Resolution resolution)
        {
            int refreshRate = Mathf.RoundToInt((float)resolution.refreshRateRatio.value);
            return $"{resolution.width} x {resolution.height} @ {refreshRate}Hz";
        }
    }
}
