using UnityEngine;

namespace DefaultNamespace
{
    public class GameStateTracker : MonoBehaviour
    {
        public int KillCount { get; private set; }

        public void AddKill()
        {
            KillCount++;
            Debug.Log($"적이 죽음 현재 킬 수 {KillCount}");
        }
    }
}