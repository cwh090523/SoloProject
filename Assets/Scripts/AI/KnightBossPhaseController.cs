using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
public class KnightBossPhaseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private EnemyBTAgent btAgent;
    [SerializeField] private EnemyWaveSpawner waveSpawner;

    [Header("Summon")]
    [SerializeField] private GameObject minionPrefab;
    [SerializeField] private Transform[] summonPoints;
    [SerializeField] private int minionsPerThreshold = 3;
    [SerializeField] private float summonRadius = 4f;
    [SerializeField] private float summonNavMeshSampleRadius = 8f;
    [SerializeField] private float[] summonHealthPercents = { 0.7f, 0.5f, 0.3f };

    [Header("Phase 2")]
    [SerializeField] private float phase2HealthPercent = 0.5f;
    [SerializeField] private Transform phase2EffectSpawnPoint;
    [SerializeField] private GameObject phase2EnterEffectPrefab;
    [SerializeField] private ParticleSystem[] phase2EnterParticles;
    [SerializeField] private Vector3 phase2EnterEffectOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float phase2EnterEffectLifetime = 3f;
    [SerializeField] private float phase2EnterPauseDuration = 0.35f;
    [SerializeField] private ParticleSystem[] silentParticles;
    [SerializeField] private GameObject[] silentParticleObjects;
    [SerializeField] private ParticleSystem[] phase2Particles;
    [SerializeField] private GameObject[] phase2ParticleObjects;
    [SerializeField] private float phase2SpeedMultiplier = 1.35f;
    [SerializeField] private float phase2DamageMultiplier = 1.5f;
    [SerializeField] private float phase2AttackCooldownMultiplier = 0.7f;

    [Header("Dash")]
    [SerializeField] private bool enableDashApproach = true;

    private readonly HashSet<int> _triggeredThresholdIndexes = new HashSet<int>();
    private Coroutine _phase2EnterPauseRoutine;
    private bool _enteredPhase2;
    private bool _warnedMissingMinionPrefab;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
            health.HealthChanged += HandleHealthChanged;
            health.Died -= HandleDied;
            health.Died += HandleDied;
        }

        if (btAgent != null)
            btAgent.SetDashEnabled(enableDashApproach);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDied;
        }
    }

    public void InitializeRuntime(Health runtimeHealth, EnemyBTAgent runtimeBtAgent, EnemyWaveSpawner runtimeSpawner,
        GameObject fallbackMinionPrefab)
    {
        if (runtimeHealth != null)
            health = runtimeHealth;

        if (runtimeBtAgent != null)
            btAgent = runtimeBtAgent;

        if (runtimeSpawner != null)
            waveSpawner = runtimeSpawner;

        if (minionPrefab == null)
            minionPrefab = fallbackMinionPrefab;

        if (btAgent != null)
            btAgent.SetDashEnabled(enableDashApproach);
    }

    private void ResolveReferences()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (btAgent == null)
            btAgent = GetComponent<EnemyBTAgent>();

        if (waveSpawner == null)
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0f || health == null || health.IsDead)
            return;

        float normalizedHealth = Mathf.Clamp01(currentHealth / maxHealth);
        TriggerSummons(normalizedHealth);

        if (!_enteredPhase2 && normalizedHealth <= phase2HealthPercent)
            EnterPhase2();
    }

    private void TriggerSummons(float normalizedHealth)
    {
        if (summonHealthPercents == null)
            return;

        for (int i = 0; i < summonHealthPercents.Length; i++)
        {
            if (_triggeredThresholdIndexes.Contains(i))
                continue;

            float threshold = Mathf.Clamp01(summonHealthPercents[i]);
            if (normalizedHealth > threshold)
                continue;

            _triggeredThresholdIndexes.Add(i);
            SummonMinions();
        }
    }

    private void SummonMinions()
    {
        if (minionPrefab == null)
        {
            if (!_warnedMissingMinionPrefab)
            {
                Debug.LogWarning($"{name} Knight boss cannot summon because Minion Prefab is empty.", this);
                _warnedMissingMinionPrefab = true;
            }

            return;
        }

        int count = Mathf.Max(0, minionsPerThreshold);
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = GetSummonPosition(i);
            GameObject minion = Instantiate(minionPrefab, spawnPosition, transform.rotation);
            SnapToNavMesh(minion, spawnPosition);

            if (waveSpawner != null)
                waveSpawner.RegisterSummonedEnemy(minion);
        }
    }

    private Vector3 GetSummonPosition(int index)
    {
        if (summonPoints != null && summonPoints.Length > 0)
        {
            Transform point = summonPoints[index % summonPoints.Length];
            if (point != null)
                return point.position;
        }

        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(1.5f, Mathf.Max(1.5f, summonRadius));
        return transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }

    private void SnapToNavMesh(GameObject spawnedObject, Vector3 spawnPosition)
    {
        if (spawnedObject == null)
            return;

        NavMeshAgent spawnedAgent = spawnedObject.GetComponent<NavMeshAgent>();
        if (spawnedAgent == null)
            spawnedAgent = spawnedObject.GetComponentInChildren<NavMeshAgent>();

        int areaMask = spawnedAgent != null ? spawnedAgent.areaMask : NavMesh.AllAreas;
        if (!NavMesh.SamplePosition(spawnPosition, out NavMeshHit hit, Mathf.Max(0.1f, summonNavMeshSampleRadius), areaMask))
            return;

        spawnedObject.transform.position = hit.position;
        if (spawnedAgent != null && spawnedAgent.enabled)
            spawnedAgent.Warp(hit.position);
    }

    private void EnterPhase2()
    {
        _enteredPhase2 = true;

        SetParticleGroup(silentParticles, false);
        SetObjectGroup(silentParticleObjects, false);
        PlayPhase2EnterEffect();
        SetObjectGroup(phase2ParticleObjects, true);
        SetParticleGroup(phase2Particles, true);

        if (btAgent != null)
            btAgent.SetCombatPhaseModifiers(phase2SpeedMultiplier, phase2DamageMultiplier, phase2AttackCooldownMultiplier);
    }

    private void PlayPhase2EnterEffect()
    {
        Vector3 effectPosition = GetPhase2EffectPosition();

        if (phase2EnterEffectPrefab != null)
        {
            GameObject effect = Instantiate(phase2EnterEffectPrefab, effectPosition, Quaternion.identity);
            Destroy(effect, Mathf.Max(0.1f, phase2EnterEffectLifetime));
        }

        SetParticleGroup(phase2EnterParticles, true);

        if (_phase2EnterPauseRoutine != null)
            StopCoroutine(_phase2EnterPauseRoutine);
        _phase2EnterPauseRoutine = StartCoroutine(Phase2EnterPauseRoutine());
    }

    private Vector3 GetPhase2EffectPosition()
    {
        Transform origin = phase2EffectSpawnPoint != null ? phase2EffectSpawnPoint : transform;
        return origin.position + phase2EnterEffectOffset;
    }

    private IEnumerator Phase2EnterPauseRoutine()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = GetComponentInChildren<NavMeshAgent>();

        bool wasStopped = agent != null && agent.enabled && agent.isOnNavMesh && agent.isStopped;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        yield return new WaitForSeconds(Mathf.Max(0f, phase2EnterPauseDuration));

        if (agent != null && agent.enabled && agent.isOnNavMesh && !health.IsDead)
            agent.isStopped = wasStopped;

        _phase2EnterPauseRoutine = null;
    }

    private void SetParticleGroup(ParticleSystem[] particles, bool isPlaying)
    {
        if (particles == null)
            return;

        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;

            if (isPlaying)
            {
                particles[i].gameObject.SetActive(true);
                particles[i].Play(true);
            }
            else
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetObjectGroup(GameObject[] objects, bool isActive)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(isActive);
        }
    }

    private void HandleDied()
    {
        if (_phase2EnterPauseRoutine != null)
        {
            StopCoroutine(_phase2EnterPauseRoutine);
            _phase2EnterPauseRoutine = null;
        }

        SetParticleGroup(phase2Particles, false);
        SetObjectGroup(phase2ParticleObjects, false);
    }
}
