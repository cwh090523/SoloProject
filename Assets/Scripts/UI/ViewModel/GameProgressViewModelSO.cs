using UnityEngine;
using UnityEngine.UIElements;

namespace UI.ViewModel
{
    [CreateAssetMenu(fileName = "game progress view model", menuName = "Game/UI/Game Progress View Model", order = 0)]
    public class GameProgressViewModelSO : ScriptableObject
    {
        public int currentWave;
        public int maxWave;

        public int aliveEnemies;
        public int totalEnemies;

        public float nextWaveTime;

        public string stateText;

        public string WaveText => maxWave > 0 ? $"WAVE {currentWave} / {maxWave}" : $"WAVE {currentWave}";

        public string EnemyCountText => $"{aliveEnemies} / {totalEnemies}";

        public string TimerText
        {
            get
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, nextWaveTime));
                int minute = seconds / 60;
                int second = seconds % 60;
                return $"{minute:00}:{second:00}";
            }
        }
    }
}
