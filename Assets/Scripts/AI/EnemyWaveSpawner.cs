using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;

public class EnemyWaveSpawner : MonoBehaviour
{
    private enum WaveState
    {
        Ready,
        Spawning,
        InProgress,
        NextWave,
        Clear
    }

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
    [SerializeField] private bool snapSpawnToNavMesh = true;
    [SerializeField] private float spawnNavMeshSampleRadius = 8f;
    [SerializeField] private bool logFailedNavMeshSpawn = true;
    [SerializeField] private bool logSuccessfulNavMeshSpawn = true;

    private readonly List<Health> _aliveEnemies = new List<Health>();
    private Coroutine _waveRoutine;
    private int _waveIndex;
    private int _currentWaveTotalEnemies;
    private int _remainingEnemiesToSpawn;
    private float _nextWaveTime;
    private WaveState _state = WaveState.Ready;

    public int CurrentWave => MaxWave <= 0 ? 0 : Mathf.Clamp(_waveIndex + 1, 1, MaxWave);
    public int MaxWave => waves == null ? 0 : waves.Length;
    public int AliveEnemyCount => _aliveEnemies.Count;
    public int RemainingEnemyCount => _aliveEnemies.Count + _remainingEnemiesToSpawn;
    public int TotalEnemyCount => _currentWaveTotalEnemies;
    public float NextWaveTime => _nextWaveTime;
    public bool IsStageClear => _state == WaveState.Clear;
    public event Action StageCleared;

    public string StateText => _state switch
    {
        WaveState.Ready => "READY",
        WaveState.Spawning => "SPAWNING",
        WaveState.InProgress => "IN PROGRESS",
        WaveState.NextWave => "NEXT WAVE",
        WaveState.Clear => "CLEAR",
        _ => "READY"
    };

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

    public void StopWaves()
    {
        if (_waveRoutine != null)
        {
            StopCoroutine(_waveRoutine);
            _waveRoutine = null;
        }
    }

    private IEnumerator WaveRoutine()
    {
        if (waves == null || waves.Length == 0)
        {
            _state = WaveState.Clear;
            yield break;
        }

        for (_waveIndex = 0; _waveIndex < waves.Length; _waveIndex++)
        {
            Wave wave = waves[_waveIndex];
            _currentWaveTotalEnemies = Mathf.Max(0, wave.enemyCount);
            _remainingEnemiesToSpawn = _currentWaveTotalEnemies;
            _nextWaveTime = 0f;
            _state = WaveState.Spawning;

            yield return SpawnWaveRoutine(wave);

            _state = WaveState.InProgress;
            while (_aliveEnemies.Count > 0)
                yield return null;

            if (_waveIndex < waves.Length - 1)
                yield return DelayNextWaveRoutine();
        }

        _nextWaveTime = 0f;
        _state = WaveState.Clear;
        _waveRoutine = null;
        StageCleared?.Invoke();
    }

    private IEnumerator SpawnWaveRoutine(Wave wave)
    {
        if (enemyPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
            yield break;

        int count = Mathf.Max(0, wave.enemyCount);
        for (int i = 0; i < count; i++)
        {
            SpawnEnemy();
            _remainingEnemiesToSpawn = Mathf.Max(0, _remainingEnemiesToSpawn - 1);
            yield return new WaitForSeconds(Mathf.Max(0f, wave.spawnInterval));
        }
    }

    private IEnumerator DelayNextWaveRoutine()
    {
        _state = WaveState.NextWave;
        _nextWaveTime = Mathf.Max(0f, delayBetweenWaves);

        while (_nextWaveTime > 0f)
        {
            yield return null;
            _nextWaveTime = Mathf.Max(0f, _nextWaveTime - Time.deltaTime);
        }
    }

    private void SpawnEnemy()
    {
        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
        SnapEnemyToNavMesh(enemy, spawnPoint.position);

        Health enemyHealth = enemy.GetComponent<Health>();
        if (enemyHealth == null)
            enemyHealth = enemy.GetComponentInChildren<Health>();

        if (enemyHealth == null)
            return;

        enemyHealth.RestoreFullHealth();
        enemyHealth.Died += () => HandleEnemyDied(enemyHealth);
        _aliveEnemies.Add(enemyHealth);
    }

    private void SnapEnemyToNavMesh(GameObject enemy, Vector3 spawnPosition)
    {
        if (!snapSpawnToNavMesh || enemy == null)
            return;

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = enemy.GetComponentInChildren<NavMeshAgent>();

        int areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas;
        float sampleRadius = Mathf.Max(0.1f, spawnNavMeshSampleRadius);
        if (!NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, sampleRadius, areaMask))
        {
            if (logFailedNavMeshSpawn)
                Debug.LogWarning($"Enemy spawn point is not near a NavMesh. Position: {spawnPosition}", this);

            return;
        }

        enemy.transform.position = hit.position;

        if (agent != null && agent.enabled)
        {
            agent.Warp(hit.position);
            agent.ResetPath();
        }

        if (logSuccessfulNavMeshSpawn)
        {
            float snapDistance = Vector3.Distance(spawnPosition, hit.position);
            Debug.Log($"Enemy spawned on NavMesh. Spawn: {spawnPosition}, NavMesh: {hit.position}, Distance: {snapDistance:0.00}", enemy);
        }
    }

    private void HandleEnemyDied(Health enemyHealth)
    {
        _aliveEnemies.Remove(enemyHealth);
    }
}
