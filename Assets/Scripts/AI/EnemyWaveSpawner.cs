using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

public class EnemyWaveSpawner : MonoBehaviour
{
    private enum WaveState
    {
        Ready,
        Spawning,
        InProgress,
        NextWave,
        Restock,
        Clear
    }

    [System.Serializable]
    public class Wave
    {
        public int enemyCount = 3;
        public float spawnInterval = 0.5f;
        public GameObject[] enemyPrefabs;
        public FixedEnemySpawn[] fixedSpawns;
    }

    [System.Serializable]
    public class FixedEnemySpawn
    {
        public GameObject enemyPrefab;
        public int count = 1;
    }

    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Wave[] waves =
    {
        new Wave { enemyCount = 3, spawnInterval = 0.5f },
        new Wave { enemyCount = 5, spawnInterval = 0.45f },
        new Wave { enemyCount = 8, spawnInterval = 0.4f }
    };
    [SerializeField] private float normalWaveDelay = 5f;
    [FormerlySerializedAs("delayBetweenWaves")]
    [SerializeField] private float restockDuration = 180f;
    [SerializeField] private int restockEveryWaves = 5;
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private bool snapSpawnToNavMesh = true;
    [SerializeField] private float spawnNavMeshSampleRadius = 8f;
    [SerializeField] private bool logFailedNavMeshSpawn = true;
    [SerializeField] private bool logSuccessfulNavMeshSpawn = true;

    [Header("Enemy UI")]
    [SerializeField] private bool addHealthBarOnSpawn = true;

    [Header("Reward")]
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private int killReward = 20;

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
    public event Action<float> RestockStarted;
    public event Action RestockEnded;

    public string StateText => _state switch
    {
        WaveState.Ready => "READY",
        WaveState.Spawning => "SPAWNING",
        WaveState.InProgress => "IN PROGRESS",
        WaveState.NextWave => "NEXT WAVE",
        WaveState.Restock => "RESTOCK",
        WaveState.Clear => "CLEAR",
        _ => "READY"
    };

    private void Start()
    {
        ResolveRewardTarget();

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
            _currentWaveTotalEnemies = GetWaveEnemyCount(wave);
            _remainingEnemiesToSpawn = _currentWaveTotalEnemies;
            _nextWaveTime = 0f;
            _state = WaveState.Spawning;

            yield return SpawnWaveRoutine(wave);

            _state = WaveState.InProgress;
            while (_aliveEnemies.Count > 0)
                yield return null;

            if (_waveIndex < waves.Length - 1)
            {
                if (ShouldRestockAfterCurrentWave())
                    yield return RestockNextWaveRoutine();
                else
                    yield return DelayNextWaveRoutine();
            }
        }

        _nextWaveTime = 0f;
        _state = WaveState.Clear;
        _waveRoutine = null;
        StageCleared?.Invoke();
    }

    private IEnumerator SpawnWaveRoutine(Wave wave)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            yield break;

        List<GameObject> spawnQueue = BuildSpawnQueue(wave);
        for (int i = 0; i < spawnQueue.Count; i++)
        {
            SpawnEnemy(spawnQueue[i]);
            _remainingEnemiesToSpawn = Mathf.Max(0, _remainingEnemiesToSpawn - 1);
            yield return new WaitForSeconds(Mathf.Max(0f, wave.spawnInterval));
        }
    }

    private IEnumerator DelayNextWaveRoutine()
    {
        _state = WaveState.NextWave;
        yield return CountdownRoutine(normalWaveDelay);
    }

    private IEnumerator RestockNextWaveRoutine()
    {
        _state = WaveState.Restock;
        _nextWaveTime = Mathf.Max(0f, restockDuration);
        RestockStarted?.Invoke(_nextWaveTime);

        while (_nextWaveTime > 0f)
        {
            yield return null;
            _nextWaveTime = Mathf.Max(0f, _nextWaveTime - Time.deltaTime);
        }

        RestockEnded?.Invoke();
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        _nextWaveTime = Mathf.Max(0f, duration);

        while (_nextWaveTime > 0f)
        {
            yield return null;
            _nextWaveTime = Mathf.Max(0f, _nextWaveTime - Time.deltaTime);
        }
    }

    private bool ShouldRestockAfterCurrentWave()
    {
        if (restockEveryWaves <= 0)
            return false;

        int completedWaveNumber = _waveIndex + 1;
        return completedWaveNumber % restockEveryWaves == 0;
    }

    private List<GameObject> BuildSpawnQueue(Wave wave)
    {
        List<GameObject> spawnQueue = new List<GameObject>();

        if (wave == null)
            return spawnQueue;

        if (wave.fixedSpawns != null && wave.fixedSpawns.Length > 0)
        {
            for (int i = 0; i < wave.fixedSpawns.Length; i++)
            {
                FixedEnemySpawn fixedSpawn = wave.fixedSpawns[i];
                if (fixedSpawn == null || fixedSpawn.enemyPrefab == null)
                    continue;

                int count = Mathf.Max(0, fixedSpawn.count);
                for (int j = 0; j < count; j++)
                    spawnQueue.Add(fixedSpawn.enemyPrefab);
            }

            Shuffle(spawnQueue);
            return spawnQueue;
        }

        int enemyCount = Mathf.Max(0, wave.enemyCount);
        for (int i = 0; i < enemyCount; i++)
        {
            GameObject selectedPrefab = GetRandomPrefabForWave(wave);
            if (selectedPrefab != null)
                spawnQueue.Add(selectedPrefab);
        }

        return spawnQueue;
    }

    private int GetWaveEnemyCount(Wave wave)
    {
        if (wave == null)
            return 0;

        if (wave.fixedSpawns == null || wave.fixedSpawns.Length == 0)
            return Mathf.Max(0, wave.enemyCount);

        int count = 0;
        for (int i = 0; i < wave.fixedSpawns.Length; i++)
        {
            FixedEnemySpawn fixedSpawn = wave.fixedSpawns[i];
            if (fixedSpawn != null && fixedSpawn.enemyPrefab != null)
                count += Mathf.Max(0, fixedSpawn.count);
        }

        return count;
    }

    private GameObject GetRandomPrefabForWave(Wave wave)
    {
        if (wave != null && wave.enemyPrefabs != null && wave.enemyPrefabs.Length > 0)
        {
            List<GameObject> availablePrefabs = new List<GameObject>();
            for (int i = 0; i < wave.enemyPrefabs.Length; i++)
            {
                if (wave.enemyPrefabs[i] != null)
                    availablePrefabs.Add(wave.enemyPrefabs[i]);
            }

            if (availablePrefabs.Count > 0)
                return availablePrefabs[UnityEngine.Random.Range(0, availablePrefabs.Count)];
        }

        return enemyPrefab;
    }

    private void Shuffle(List<GameObject> spawnQueue)
    {
        for (int i = 0; i < spawnQueue.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, spawnQueue.Count);
            (spawnQueue[i], spawnQueue[randomIndex]) = (spawnQueue[randomIndex], spawnQueue[i]);
        }
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null)
            return;

        Transform spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        SnapEnemyToNavMesh(enemy, spawnPoint.position);

        Health enemyHealth = enemy.GetComponent<Health>();
        if (enemyHealth == null)
            enemyHealth = enemy.GetComponentInChildren<Health>();

        if (enemyHealth == null)
            return;

        EnsureEnemyHealthBar(enemyHealth);
        enemyHealth.RestoreFullHealth();
        enemyHealth.Died += () => HandleEnemyDied(enemyHealth);
        _aliveEnemies.Add(enemyHealth);
    }

    private void EnsureEnemyHealthBar(Health enemyHealth)
    {
        if (!addHealthBarOnSpawn || enemyHealth == null)
            return;

        if (enemyHealth.GetComponent<EnemyHealthBarUI>() != null)
            return;

        enemyHealth.gameObject.AddComponent<EnemyHealthBarUI>();
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
                // Debug.LogWarning($"Enemy spawn point is not near a NavMesh. Position: {spawnPosition}", this);

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
            // Debug.Log($"Enemy spawned on NavMesh. Spawn: {spawnPosition}, NavMesh: {hit.position}, Distance: {snapDistance:0.00}", enemy);
        }
    }

    private void HandleEnemyDied(Health enemyHealth)
    {
        _aliveEnemies.Remove(enemyHealth);

        if (playerWallet != null && killReward > 0)
            playerWallet.AddMoney(killReward);
    }

    private void ResolveRewardTarget()
    {
        if (playerWallet != null)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null)
            return;

        playerWallet = player.GetComponent<PlayerWallet>();
        if (playerWallet == null)
            playerWallet = player.gameObject.AddComponent<PlayerWallet>();
    }
}
