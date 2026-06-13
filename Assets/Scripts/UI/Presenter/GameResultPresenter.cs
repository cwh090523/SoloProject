using DefaultNamespace;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace UI.Presenter
{
    public class GameResultPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private EnemyWaveSpawner waveSpawner;
        [SerializeField] private GameStateTracker statesTracker;

        private float _startTime;

        private VisualElement _container;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _waveLabel;
        private Label _killLabel;
        private Label _timeLabel;
        private Label _messageLabel;
        private Button _restartButton;
        private Button _titleButton;

        private void Awake()
        {
            ResolveReferences();
            BindElements();
            Hide();
            _startTime = Time.time;
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (stateManager != null)
                stateManager.StateChanged += HandleStateChanged;

            if (_restartButton != null)
                _restartButton.clicked += RestartGame;

            if (_titleButton != null)
                _titleButton.clicked += GoTitle;
        }

        private void OnDisable()
        {
            if (stateManager != null)
                stateManager.StateChanged -= HandleStateChanged;

            if (_restartButton != null)
                _restartButton.clicked -= RestartGame;

            if (_titleButton != null)
                _titleButton.clicked -= GoTitle;
        }

        private void ResolveReferences()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (stateManager == null)
                stateManager = FindFirstObjectByType<GameStateManager>();

            if (waveSpawner == null)
                waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();

            if (statesTracker == null)
                statesTracker = FindFirstObjectByType<GameStateTracker>();
        }

        private void BindElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            _container = root.Q<VisualElement>("GameResultContainer");
            _titleLabel = root.Q<Label>("ResultTitleLabel");
            _subtitleLabel = root.Q<Label>("ResultSubtitleLabel");
            _waveLabel = root.Q<Label>("ResultWaveLabel");
            _killLabel = root.Q<Label>("ResultKillLabel");
            _timeLabel = root.Q<Label>("ResultTimeLabel");
            _messageLabel = root.Q<Label>("ResultMessageLabel");
            _restartButton = root.Q<Button>("RestartButton");
            _titleButton = root.Q<Button>("TitleButton");
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                ShowGameOver();
                return;
            }

            if (state == GameState.StageClear)
                ShowGameClear();
        }

        private void ShowGameOver()
        {
            ShowContainer();
            SetResultText("GAME OVER", "SURVIVAL FAILED", "Prepare, reload, and try again.");
            RefreshResult();
            ShowCursor();
        }

        private void ShowGameClear()
        {
            ShowContainer();
            SetResultText("GAME CLEAR", "MISSION COMPLETE", "You survived every wave.");
            RefreshResult();
            ShowCursor();
        }

        private void ShowContainer()
        {
            if (_container != null)
                _container.style.display = DisplayStyle.Flex;
        }

        private void Hide()
        {
            if (_container != null)
                _container.style.display = DisplayStyle.None;
        }

        private void SetResultText(string title, string subtitle, string message)
        {
            if (_titleLabel != null)
                _titleLabel.text = title;

            if (_subtitleLabel != null)
                _subtitleLabel.text = subtitle;

            if (_messageLabel != null)
                _messageLabel.text = message;
        }

        private void RefreshResult()
        {
            ResolveReferences();

            if (_waveLabel != null && waveSpawner != null)
                _waveLabel.text = $"{waveSpawner.CurrentWave} / {waveSpawner.MaxWave}";

            if (_killLabel != null)
                _killLabel.text = statesTracker != null ? statesTracker.KillCount.ToString() : "0";

            if (_timeLabel != null)
                _timeLabel.text = FormatTime(Time.time - _startTime);
        }

        private string FormatTime(float time)
        {
            int totalSeconds = Mathf.FloorToInt(Mathf.Max(0f, time));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        private void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void GoTitle()
        {
            Debug.Log("Title scene is not connected yet.", this);
        }
    }
}
