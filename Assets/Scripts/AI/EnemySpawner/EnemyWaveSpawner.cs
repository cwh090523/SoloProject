using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace AI.EnemySpawner
{
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
        private readonly Dictionary<Health, Action> _enemyDeathHandlers = new();
    
        public int CurrentWave => MaxWave <= 0 ? 0 : Mathf.Clamp(_waveIndex + 1, 1, MaxWave);
        public int MaxWave => waves == null ? 0 : waves.Length;
        public int AliveEnemyCount => _aliveEnemies.Count;
        public int RemainingEnemyCount => _aliveEnemies.Count + _remainingEnemiesToSpawn;
        public int TotalEnemyCount => _currentWaveTotalEnemies;
        public float NextWaveTime => _nextWaveTime;
        public bool IsRestocking => _state == WaveState.Restock;
        public IReadOnlyList<Health> AliveEnemies => _aliveEnemies;
        public bool IsStageClear => _state == WaveState.Clear;
        public event Action StageCleared;
        public event Action<float> RestockStarted;
        public event Action RestockEnded;
        public event Action<Health> EnemyRegistered;
        public event Action<Health> EnemyRemoved;

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
            EnsureMinimapEnemyIndicators();

            if (startOnAwake)
                StartWaves();
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<Health, Action> pair in _enemyDeathHandlers)
            {
                if(pair.Key != null)
                    pair.Key.Died -= pair.Value;
            }

            _enemyDeathHandlers.Clear();
            _aliveEnemies.Clear();
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

        public void SkipRestock()
        {
            if (_state == WaveState.Restock)
                _nextWaveTime = 0f;
        }

        private void RegisterEnemy(Health enemyHealth)
        {
            // 기존에는 적이 스폰될 때마다 해당 적의 Health 인스턴스를 기억해서
            // 사망 처리 함수에 넘겨야 했기 때문에 람다식을 사용했다.
            // 일반 메서드만 구독하면 어떤 적이 죽었는지 구분하기 어렵지만,
            // 람다를 사용하면 현재 등록 중인 enemyHealth를 캡처해서
            // HandleEnemyDied(enemyHealth)로 정확히 전달할 수 있다.
            //
            // 다만 익명 람다를 바로 이벤트에 구독하면,
            // 나중에 같은 형태의 람다를 다시 작성해도 같은 참조가 아니기 때문에
            // -= 연산자로 구독 해제하기 어렵다는 문제가 있다.
            //
            // 그래서 람다식을 deathHandler 변수에 저장하고,
            // Dictionary<Health, Action>에 Health별 핸들러를 보관하도록 변경했다.
            // 이렇게 하면 적이 죽거나 스포너가 정리될 때,
            // 처음 구독했던 것과 같은 핸들러 참조를 찾아 안전하게 구독 해제할 수 있다.
            
            // 익명 람다를 사용한 이유 : 매개변수 없는 Died 이벤트에 연결하면서
            // 현재 등록 중인 enemyHealth를 사망 처리 함수에 넘기기 위해서 
        
            if (enemyHealth == null)
                return;
            Action deathHandler = () => HandleEnemyDied(enemyHealth);
            _enemyDeathHandlers[enemyHealth] = deathHandler;
            enemyHealth.Died += deathHandler;
        
            _aliveEnemies.Add(enemyHealth);
            EnemyRegistered?.Invoke(enemyHealth);
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

            EnsureBossHealthTarget(enemy, enemyHealth, prefab);
            EnsureKnightBossPhaseController(enemy, enemyHealth, prefab);
            EnsureEnemyHealthBar(enemyHealth);
            enemyHealth.RestoreFullHealth();
            RegisterEnemy(enemyHealth);
        }

        public void RegisterSummonedEnemy(GameObject enemy)
        {
            if (enemy == null)
                return;

            Health enemyHealth = enemy.GetComponent<Health>();
            if (enemyHealth == null)
                enemyHealth = enemy.GetComponentInChildren<Health>();

            if (enemyHealth == null || _aliveEnemies.Contains(enemyHealth))
                return;

            EnsureEnemyHealthBar(enemyHealth);
            enemyHealth.RestoreFullHealth();
            RegisterEnemy(enemyHealth);
            _currentWaveTotalEnemies++;
        }

        private void EnsureEnemyHealthBar(Health enemyHealth)
        {
            if (!addHealthBarOnSpawn || enemyHealth == null)
                return;

            if (IsBossHealth(enemyHealth))
                return;

            if (enemyHealth.GetComponent<EnemyHealthBarUI>() != null)
                return;

            enemyHealth.gameObject.AddComponent<EnemyHealthBarUI>();
        }

        private void EnsureBossHealthTarget(GameObject enemy, Health enemyHealth, GameObject sourcePrefab)
        {
            if (enemy == null || enemyHealth == null)
                return;

            if (!IsBossEnemy(enemy, enemyHealth, sourcePrefab))
                return;

            BossHealthTarget bossTarget = enemyHealth.GetComponent<BossHealthTarget>();
            if (bossTarget == null)
                bossTarget = enemyHealth.GetComponentInParent<BossHealthTarget>();
            if (bossTarget == null)
                bossTarget = enemyHealth.gameObject.AddComponent<BossHealthTarget>();

            bossTarget.Initialize(enemyHealth, GetBossDisplayName(enemy, sourcePrefab));
            DisableRegularHealthBar(enemyHealth);
        }

        private void EnsureKnightBossPhaseController(GameObject enemy, Health enemyHealth, GameObject sourcePrefab)
        {
            if (enemy == null || enemyHealth == null || !IsKnightBossEnemy(enemy, enemyHealth, sourcePrefab))
                return;

            KnightBossPhaseController phaseController = enemyHealth.GetComponent<KnightBossPhaseController>();
            if (phaseController == null)
                phaseController = enemyHealth.GetComponentInParent<KnightBossPhaseController>();
            if (phaseController == null)
                phaseController = enemyHealth.gameObject.AddComponent<KnightBossPhaseController>();

            EnemyBTAgent btAgent = enemy.GetComponent<EnemyBTAgent>();
            if (btAgent == null)
                btAgent = enemy.GetComponentInChildren<EnemyBTAgent>();

            phaseController.InitializeRuntime(enemyHealth, btAgent, this, enemyPrefab);
        }

        private void DisableRegularHealthBar(Health enemyHealth)
        {
            EnemyHealthBarUI healthBar = enemyHealth.GetComponent<EnemyHealthBarUI>();
            if (healthBar != null)
                healthBar.enabled = false;
        }

        private bool IsBossHealth(Health enemyHealth)
        {
            if (enemyHealth == null)
                return false;

            return enemyHealth.GetComponent<BossHealthTarget>() != null ||
                   enemyHealth.GetComponentInParent<BossHealthTarget>() != null ||
                   IsBossName(enemyHealth.gameObject.name);
        }

        private bool IsBossEnemy(GameObject enemy, Health enemyHealth, GameObject sourcePrefab)
        {
            if (enemyHealth != null &&
                (enemyHealth.GetComponent<BossHealthTarget>() != null || enemyHealth.GetComponentInParent<BossHealthTarget>() != null))
                return true;

            return IsBossName(enemy == null ? null : enemy.name) ||
                   IsBossName(enemyHealth == null ? null : enemyHealth.gameObject.name) ||
                   IsBossName(sourcePrefab == null ? null : sourcePrefab.name);
        }

        private bool IsKnightBossEnemy(GameObject enemy, Health enemyHealth, GameObject sourcePrefab)
        {
            return IsBossEnemy(enemy, enemyHealth, sourcePrefab) &&
                   (IsKnightName(enemy == null ? null : enemy.name) ||
                    IsKnightName(enemyHealth == null ? null : enemyHealth.gameObject.name) ||
                    IsKnightName(sourcePrefab == null ? null : sourcePrefab.name));
        }

        private bool IsBossName(string objectName)
        {
            return !string.IsNullOrWhiteSpace(objectName) &&
                   objectName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsKnightName(string objectName)
        {
            return !string.IsNullOrWhiteSpace(objectName) &&
                   objectName.IndexOf("Knight", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetBossDisplayName(GameObject enemy, GameObject sourcePrefab)
        {
            string rawName = sourcePrefab != null ? sourcePrefab.name : enemy.name;
            return rawName.Replace("(Clone)", string.Empty).Replace("_", " ").Trim();
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
            if (enemyHealth == null)
                return;

            if (_enemyDeathHandlers.TryGetValue(enemyHealth, out Action deathHandler))
            {
                // TryGetValue를 사용하는 이유
                // Dictionary에 해당하는 적의 핸들러가 있을 때만 안전하게 꺼내기 위해
                // 없는데 접근하면 에러가 날 수 있음
                enemyHealth.Died -= deathHandler;
                _enemyDeathHandlers.Remove(enemyHealth);
            }
        
            _aliveEnemies.Remove(enemyHealth);
        
            EnemyRemoved?.Invoke(enemyHealth);

            if (playerWallet != null && killReward > 0)
                playerWallet.AddMoney(killReward);

        }

        private void EnsureMinimapEnemyIndicators()
        {
            MinimapEnemyIndicatorUI indicatorUI = FindFirstObjectByType<MinimapEnemyIndicatorUI>();
            if (indicatorUI == null)
                indicatorUI = gameObject.AddComponent<MinimapEnemyIndicatorUI>();

            indicatorUI.Initialize(this);
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
}
