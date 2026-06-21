using UI.ViewModel;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI.Presenter
{
    public class GameProgressPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private EnemyWaveSpawner waveSpawner;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private GameProgressViewModelSO viewModel;
        [SerializeField] private float announcementDuration = 2.2f;
        [SerializeField] private float announcementFadeDuration = 0.3f;

        private Label _waveLabel;
        private Label _enemyCountLabel;
        private Label _timerLabel;
        private Label _stateLabel;
        private Button _skipRestockButton;
        private VisualElement _announcementRoot;
        private VisualElement _announcementCard;
        private Label _announcementTitleLabel;
        private Label _announcementSubtitleLabel;
        private string _lastAnnouncementKey;
        private float _announcementTimer;

        private void Awake()
        {
            ResolveReferences();
            BindElements();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindElements();
            Refresh();
        }

        private void OnDisable()
        {
            if (_skipRestockButton != null)
                _skipRestockButton.clicked -= HandleSkipRestock;
        }

        private void Update()
        {
            Refresh();
            UpdateAnnouncement();

            if (waveSpawner != null && waveSpawner.IsRestocking && Keyboard.current != null &&
                Keyboard.current.nKey.wasPressedThisFrame)
            {
                waveSpawner.SkipRestock();
            }
        }

        private void ResolveReferences()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (waveSpawner == null)
                waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();

            if (gameStateManager == null)
                gameStateManager = FindFirstObjectByType<GameStateManager>();
        }

        private void BindElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            _waveLabel = document.rootVisualElement.Q<Label>("WaveLabel");
            _enemyCountLabel = document.rootVisualElement.Q<Label>("EnemyCountLabel");
            _timerLabel = document.rootVisualElement.Q<Label>("TimerLabel");
            _stateLabel = document.rootVisualElement.Q<Label>("StateLabel");
            _skipRestockButton = document.rootVisualElement.Q<Button>("SkipRestockButton");
            if (_skipRestockButton != null)
            {
                _skipRestockButton.clicked -= HandleSkipRestock;
                _skipRestockButton.clicked += HandleSkipRestock;
            }
            BindAnnouncementElements(document.rootVisualElement);
        }

        private void Refresh()
        {
            if (waveSpawner == null)
                return;

            if (viewModel != null)
            {
                viewModel.currentWave = waveSpawner.CurrentWave;
                viewModel.maxWave = waveSpawner.MaxWave;
                viewModel.aliveEnemies = waveSpawner.RemainingEnemyCount;
                viewModel.totalEnemies = waveSpawner.TotalEnemyCount;
                viewModel.nextWaveTime = waveSpawner.NextWaveTime;
                viewModel.stateText = waveSpawner.StateText;
            }

            int currentWave = viewModel != null ? viewModel.currentWave : waveSpawner.CurrentWave;
            int maxWave = viewModel != null ? viewModel.maxWave : waveSpawner.MaxWave;
            int remainingEnemies = viewModel != null ? viewModel.aliveEnemies : waveSpawner.RemainingEnemyCount;
            int totalEnemies = viewModel != null ? viewModel.totalEnemies : waveSpawner.TotalEnemyCount;
            float nextWaveTime = viewModel != null ? viewModel.nextWaveTime : waveSpawner.NextWaveTime;
            string stateText = GetStateText();

            if (_waveLabel != null)
                _waveLabel.text = maxWave > 0 ? $"WAVE {currentWave} / {maxWave}" : $"WAVE {currentWave}";

            if (_enemyCountLabel != null)
                _enemyCountLabel.text = $"{remainingEnemies} / {totalEnemies}";

            if (_timerLabel != null)
                _timerLabel.text = FormatTime(nextWaveTime);

            if (_stateLabel != null)
                _stateLabel.text = stateText;

            if (_skipRestockButton != null)
                _skipRestockButton.style.display = waveSpawner.IsRestocking ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshAnnouncementState(currentWave, stateText);
        }

        private void HandleSkipRestock()
        {
            waveSpawner?.SkipRestock();
        }

        private static string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            int minutes = totalSeconds / 60;
            int remainingSeconds = totalSeconds % 60;
            return $"{minutes:00}:{remainingSeconds:00}";
        }

        private string GetStateText()
        {
            if (gameStateManager != null && (gameStateManager.IsGameOver || gameStateManager.IsStageClear))
                return gameStateManager.StateText;

            if (viewModel != null)
                return viewModel.stateText;

            return waveSpawner != null ? waveSpawner.StateText : "READY";
        }

        private void BindAnnouncementElements(VisualElement root)
        {
            if (root == null)
                return;

            _announcementRoot = root.Q<VisualElement>("CenterAnnouncementRoot");
            if (_announcementRoot == null)
            {
                _announcementRoot = new VisualElement { name = "CenterAnnouncementRoot" };
                _announcementRoot.pickingMode = PickingMode.Ignore;
                _announcementRoot.style.position = Position.Absolute;
                _announcementRoot.style.left = 0;
                _announcementRoot.style.right = 0;
                _announcementRoot.style.top = 0;
                _announcementRoot.style.bottom = 0;
                _announcementRoot.style.alignItems = Align.Center;
                _announcementRoot.style.justifyContent = Justify.Center;
                _announcementRoot.style.opacity = 0f;
                root.Add(_announcementRoot);
            }

            _announcementCard = _announcementRoot.Q<VisualElement>("CenterAnnouncementCard");
            if (_announcementCard == null)
            {
                _announcementCard = new VisualElement { name = "CenterAnnouncementCard" };
                _announcementCard.pickingMode = PickingMode.Ignore;
                _announcementCard.style.width = 620;
                _announcementCard.style.minHeight = 148;
                _announcementCard.style.paddingLeft = 28;
                _announcementCard.style.paddingRight = 28;
                _announcementCard.style.paddingTop = 18;
                _announcementCard.style.paddingBottom = 20;
                _announcementCard.style.backgroundColor = new Color(0.02f, 0.025f, 0.03f, 0.84f);
                _announcementCard.style.borderTopColor = new Color(1f, 0.82f, 0.26f, 0.95f);
                _announcementCard.style.borderBottomColor = new Color(1f, 0.82f, 0.26f, 0.95f);
                _announcementCard.style.borderLeftColor = new Color(1f, 0.82f, 0.26f, 0.95f);
                _announcementCard.style.borderRightColor = new Color(1f, 0.82f, 0.26f, 0.95f);
                _announcementCard.style.borderTopWidth = 3;
                _announcementCard.style.borderBottomWidth = 3;
                _announcementCard.style.borderLeftWidth = 1;
                _announcementCard.style.borderRightWidth = 1;
                _announcementCard.style.borderTopLeftRadius = 4;
                _announcementCard.style.borderTopRightRadius = 4;
                _announcementCard.style.borderBottomLeftRadius = 4;
                _announcementCard.style.borderBottomRightRadius = 4;
                _announcementCard.style.alignItems = Align.Center;
                _announcementCard.style.justifyContent = Justify.Center;
                _announcementRoot.Add(_announcementCard);
            }

            _announcementTitleLabel = _announcementCard.Q<Label>("CenterAnnouncementTitle");
            if (_announcementTitleLabel == null)
            {
                _announcementTitleLabel = new Label { name = "CenterAnnouncementTitle" };
                _announcementTitleLabel.style.color = Color.white;
                _announcementTitleLabel.style.fontSize = 58;
                _announcementTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _announcementTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _announcementTitleLabel.style.unityTextOutlineColor = new Color(0f, 0f, 0f, 0.85f);
                _announcementTitleLabel.style.unityTextOutlineWidth = 2;
                _announcementCard.Add(_announcementTitleLabel);
            }

            _announcementSubtitleLabel = _announcementCard.Q<Label>("CenterAnnouncementSubtitle");
            if (_announcementSubtitleLabel == null)
            {
                _announcementSubtitleLabel = new Label { name = "CenterAnnouncementSubtitle" };
                _announcementSubtitleLabel.style.marginTop = 8;
                _announcementSubtitleLabel.style.color = new Color(1f, 0.82f, 0.26f, 1f);
                _announcementSubtitleLabel.style.fontSize = 22;
                _announcementSubtitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                _announcementSubtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                _announcementCard.Add(_announcementSubtitleLabel);
            }
        }

        private void RefreshAnnouncementState(int currentWave, string stateText)
        {
            if (_announcementRoot == null || string.IsNullOrWhiteSpace(stateText))
                return;

            string key = $"{currentWave}:{stateText}";
            if (_lastAnnouncementKey == key)
                return;

            _lastAnnouncementKey = key;

            switch (stateText)
            {
                case "SPAWNING":
                    ShowAnnouncement($"WAVE {currentWave}", "GET READY");
                    break;
                case "NEXT WAVE":
                    ShowAnnouncement("WAVE CLEAR", "RELOAD AND MOVE");
                    break;
                case "RESTOCK":
                    ShowAnnouncement("SHOP OPEN", "RESTOCK TIME");
                    break;
                case "STAGE CLEAR":
                    ShowAnnouncement("STAGE CLEAR", "YOU SURVIVED");
                    break;
                case "GAME OVER":
                    ShowAnnouncement("GAME OVER", "TRY AGAIN");
                    break;
            }
        }

        private void ShowAnnouncement(string title, string subtitle)
        {
            if (_announcementTitleLabel != null)
                _announcementTitleLabel.text = title;

            if (_announcementSubtitleLabel != null)
                _announcementSubtitleLabel.text = subtitle;

            _announcementTimer = Mathf.Max(0.1f, announcementDuration);
        }

        private void UpdateAnnouncement()
        {
            if (_announcementRoot == null)
                return;

            if (_announcementTimer > 0f)
                _announcementTimer = Mathf.Max(0f, _announcementTimer - Time.unscaledDeltaTime);

            float fadeTime = Mathf.Max(0.01f, announcementFadeDuration);
            float fadeIn = Mathf.Clamp01((announcementDuration - _announcementTimer) / fadeTime);
            float fadeOut = Mathf.Clamp01(_announcementTimer / fadeTime);
            float opacity = _announcementTimer > 0f ? Mathf.Min(fadeIn, fadeOut) : 0f;

            _announcementRoot.style.opacity = opacity;
            _announcementCard.style.marginTop = Mathf.Lerp(20f, 0f, opacity);
        }
    }
}
