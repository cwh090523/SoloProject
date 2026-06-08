using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWaveSpawner : MonoBehaviour
{
    [System.Serializable]
    public class Wave
    {
        public int enemyCount = 3;
        public float spawnInterval = 0.5f;
    }

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Wave[] waves =
    {
        new Wave { enemyCount = 3, spawnInterval = 0.5f },
        new Wave { enemyCount = 5, spawnInterval = 0.45f },
        new Wave { enemyCount = 8, spawnInterval = 0.4f }
    };
    [SerializeField] private float delayBetweenWaves = 3f;
    [SerializeField] private bool startOnAwake = true;

    private readonly List<Health> _aliveEnemies = new List<Health>();
    private Coroutine _waveRoutine;
    private int _waveIndex;

    public int CurrentWave => _waveIndex + 1;
    public int AliveEnemyCount => _aliveEnemies.Count;

    private void Start()
    {
        if (startOnAwake)
            StartWaves();
    }

    public void StartWaves()
    {
        if (_waveRoutine != null)
            return;

        _waveRoutine = StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        for (_waveIndex = 0; _waveIndex < waves.Length; _waveIndex++)
        {
            Wave wave = waves[_waveIndex];
            yield return SpawnWaveRoutine(wave);

            while (_aliveEnemies.Count > 0)
                yield return null;

            yield return new WaitForSeconds(delayBetweenWaves);
        }

        _waveRoutine = null;
    }

    private IEnumerator SpawnWaveRoutine(Wave wave)
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            yield break;

        int count = Mathf.Max(0, wave.enemyCount);
        for (int i = 0; i < count; i++)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(Mathf.Max(0f, wave.spawnInterval));
        }
    }

    private void SpawnEnemy()
    {
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);

        Health enemyHealth = enemy.GetComponent<Health>();
        if (enemyHealth == null)
            enemyHealth = enemy.GetComponentInChildren<Health>();

        if (enemyHealth == null)
            return;

        enemyHealth.RestoreFullHealth();
        enemyHealth.Died += () => HandleEnemyDied(enemyHealth);
        _aliveEnemies.Add(enemyHealth);
    }

    private void HandleEnemyDied(Health enemyHealth)
    {
        _aliveEnemies.Remove(enemyHealth);
    }
}
