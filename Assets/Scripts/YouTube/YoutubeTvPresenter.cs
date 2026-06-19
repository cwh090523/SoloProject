using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace YouTube
{
    public class YoutubeTvPresenter : MonoBehaviour
    {
        private const string TvUiResourcePath = "UI/YoutubeTvUI";

        [SerializeField] private UIDocument document;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerWeapon playerWeapon;
        [SerializeField] private AimTargetScanner aimTargetScanner;
        [SerializeField] private Rigidbody playerRigidbody;

        private VisualElement _root;
        private TextField _urlInput;
        private Label _messageLabel;
        private Button _playButton;
        private Button _closeButton;
        private YoutubeTvPlayer _currentTv;
        private bool _inputBlockedByTv;
        private bool _wasPlayerControllerEnabled;
        private bool _wasPlayerCameraEnabled;
        private bool _wasPlayerWeaponEnabled;
        private bool _wasAimTargetScannerEnabled;
        private bool _wasCursorVisible;
        private CursorLockMode _previousCursorLockState;

        public bool IsOpen => _root != null && _root.style.display == DisplayStyle.Flex;

        public static YoutubeTvPresenter EnsureForCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded || activeScene.name.ToUpperInvariant().Contains("TITLE"))
                return null;

            YoutubeTvPresenter existingPresenter = FindFirstObjectByType<YoutubeTvPresenter>();
            if (existingPresenter != null)
                return existingPresenter;

            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(TvUiResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"YouTube TV UI resource not found: Resources/{TvUiResourcePath}.uxml");
                return null;
            }

            UIDocument existingDocument = FindFirstObjectByType<UIDocument>();

            GameObject tvUiObject = new GameObject("YouTube TV UI");
            UIDocument uiDocument = tvUiObject.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = visualTree;

            if (existingDocument != null)
                uiDocument.panelSettings = existingDocument.panelSettings;

            YoutubeTvPresenter presenter = tvUiObject.AddComponent<YoutubeTvPresenter>();
            presenter.document = uiDocument;
            return presenter;
        }

        private void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            ResolvePlayerReferences();
            BindElements();
            Hide();
        }

        private void OnEnable()
        {
            if (_playButton != null)
                _playButton.clicked += PlayCurrentUrl;

            if (_closeButton != null)
                _closeButton.clicked += Hide;
        }

        private void OnDisable()
        {
            RestorePlayerInput();

            if (_playButton != null)
                _playButton.clicked -= PlayCurrentUrl;

            if (_closeButton != null)
                _closeButton.clicked -= Hide;
        }

        public void Open(YoutubeTvPlayer tvPlayer)
        {
            _currentTv = tvPlayer;
            ResolvePlayerReferences();

            if (_root != null)
                _root.style.display = DisplayStyle.Flex;

            if (_messageLabel != null)
                _messageLabel.text = "Paste a YouTube URL or video ID.";

            BlockPlayerInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _urlInput?.schedule.Execute(() => _urlInput.Focus());
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;

            _currentTv = null;
            RestorePlayerInput();
        }

        private void BindElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            _root = root.Q<VisualElement>("YoutubeTvRoot");
            _urlInput = root.Q<TextField>("YoutubeUrlInput");
            _messageLabel = root.Q<Label>("YoutubeTvMessageLabel");
            _playButton = root.Q<Button>("YoutubePlayButton");
            _closeButton = root.Q<Button>("YoutubeCloseButton");
        }

        private void PlayCurrentUrl()
        {
            if (_currentTv == null || _urlInput == null)
                return;

            if (!_currentTv.TryPlayUrl(_urlInput.value))
            {
                if (_messageLabel != null)
                    _messageLabel.text = "Invalid YouTube URL or video ID.";
                return;
            }

            if (_messageLabel != null)
                _messageLabel.text = "Now playing on TV.";

            Hide();
        }

        private void ResolvePlayerReferences()
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
                return;

            if (playerController == null)
                playerController = player.GetComponent<PlayerController>();

            if (playerCamera == null)
                playerCamera = player.GetComponent<PlayerCamera>();

            if (playerWeapon == null)
                playerWeapon = player.GetComponent<PlayerWeapon>();

            if (aimTargetScanner == null)
                aimTargetScanner = player.GetComponent<AimTargetScanner>();

            if (playerRigidbody == null)
                playerRigidbody = player.GetComponent<Rigidbody>();
        }

        private void BlockPlayerInput()
        {
            if (_inputBlockedByTv)
                return;

            _previousCursorLockState = Cursor.lockState;
            _wasCursorVisible = Cursor.visible;
            _wasPlayerControllerEnabled = playerController != null && playerController.enabled;
            _wasPlayerCameraEnabled = playerCamera != null && playerCamera.enabled;
            _wasPlayerWeaponEnabled = playerWeapon != null && playerWeapon.enabled;
            _wasAimTargetScannerEnabled = aimTargetScanner != null && aimTargetScanner.enabled;

            if (playerController != null)
                playerController.enabled = false;

            if (playerCamera != null)
                playerCamera.enabled = false;

            if (playerWeapon != null)
                playerWeapon.enabled = false;

            if (aimTargetScanner != null)
                aimTargetScanner.enabled = false;

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            _inputBlockedByTv = true;
        }

        private void RestorePlayerInput()
        {
            if (!_inputBlockedByTv)
                return;

            if (playerController != null)
                playerController.enabled = _wasPlayerControllerEnabled;

            if (playerCamera != null)
                playerCamera.enabled = _wasPlayerCameraEnabled;

            if (playerWeapon != null)
                playerWeapon.enabled = _wasPlayerWeaponEnabled;

            if (aimTargetScanner != null)
                aimTargetScanner.enabled = _wasAimTargetScannerEnabled;

            Cursor.lockState = _previousCursorLockState;
            Cursor.visible = _wasCursorVisible;
            _inputBlockedByTv = false;
        }
    }
}
