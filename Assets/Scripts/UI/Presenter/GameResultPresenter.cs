using System;
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
            if(document == null)
                document = GetComponent<UIDocument>();
            if (stateManager == null)
                stateManager = FindFirstObjectByType<GameStateManager>();
            if (waveSpawner == null)
                waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();

            BindElements();
            Hide();
            _startTime = Time.time;
        }

        private void OnEnable()
        {
            if(stateManager != null)
                stateManager.StateChanged += HandleStateChanged;
            if (_restartButton != null)
                _restartButton.clicked += RestartGame;
            if(_titleButton != null)
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

        private void GoTitle()
        {
            Debug.Log("타이틀로 가는 버튼을 눌렀지만 나는 타이틀을 만들지 않았죠");
        }

        private void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }


        private void Hide()
        {
            if(_container != null)
                _container.style.display = DisplayStyle.None;
        }

        private void BindElements()
        {
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
            {
                ShowGameClear();
            }
        }

        private void ShowGameOver()
        {
            _container.style.display = DisplayStyle.Flex;
            _titleLabel.text = "게임 오버!";
            _subtitleLabel.text = "생존에 실패하였습니다.";
            _messageLabel.text = "준비하고, 장전하고, 다시 시도하세요";
            RefreshResult();
            ShowCursor();
        }

        private void ShowGameClear()
        {
            _container.style.display = DisplayStyle.Flex;
            _titleLabel.text = "클리어";
            _subtitleLabel.text = "미션 성공.";
            _messageLabel.text = "당신은 모든 웨이브를 생존하였습니다.";
            RefreshResult();
            ShowCursor();
        }
        
        private void RefreshResult()
        {
            if (_waveLabel != null && waveSpawner != null)
                _waveLabel.text = $"{waveSpawner.CurrentWave}/{waveSpawner.MaxWave}";
            if (_killLabel != null)
                _killLabel.text = "0";
            if (_timeLabel != null)
                _timeLabel.text = FormatTime(Time.time - _startTime);
        }

        private string FormatTime(float time)
        {
            int totalSecons = Mathf.FloorToInt(Mathf.Max(0f, time));
            int minutes = totalSecons / 60;
            int seconds =  totalSecons % 60;
            return $"{minutes:00}:{seconds:00}";
        }

        private void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


    }
}
