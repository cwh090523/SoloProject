using UI.ViewModel;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Presenter
{
    public class GameProgressPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private EnemyWaveSpawner waveSpawner;
        [SerializeField] private GameStateManager gameStateManager;
        [SerializeField] private GameProgressViewModelSO viewModel;

        private Label _waveLabel;
        private Label _enemyCountLabel;
        private Label _timerLabel;
        private Label _stateLabel;

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

        private void Update()
        {
            Refresh();
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
    }
}
