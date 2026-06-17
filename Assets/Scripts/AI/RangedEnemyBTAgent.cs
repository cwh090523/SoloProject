using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class RangedEnemyBTAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private Health targetHealth;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Target")]
    [SerializeField] private string playerObjectName = "Player3";
    [SerializeField] private bool requireAliveTarget = true;

    [Header("Movement")]
    [SerializeField] private float detectionRange = 35f;
    [SerializeField] private float attackRange = 18f;
    [SerializeField] private float preferredDistance = 11f;
    [SerializeField] private float retreatDistance = 6f;
    [SerializeField] private float destinationRefreshInterval = 0.2f;
    [SerializeField] private float destinationSampleRadius = 4f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float agentSpeed = 4.5f;
    [SerializeField] private float agentAcceleration = 35f;
    [SerializeField] private float moveDestinationStopDistance = 0.25f;
    [SerializeField] private bool ignoreDetectionRange = true;

    [Header("Attack")]
    [SerializeField] private float damage = 12f;
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float readyAttackDuration = 0.35f;
    [SerializeField] private float releaseDelay = 0.45f;
    [SerializeField] private float projectileSpeed = 24f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private float aimHeight = 1.1f;
    [SerializeField] private LayerMask lineOfSightMask = ~0;
    [SerializeField] private LayerMask projectileHitLayers = ~0;
    [SerializeField] private bool useHitscanWhenProjectileMissing = true;
    [SerializeField] private bool attackAtPreferredDistanceOnly = true;
    [SerializeField] private float attackDistanceTolerance = 1.5f;

    [Header("Animation")]
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private string moveStateName = "RUN";
    [SerializeField] private string aimStateName = "READATTACK";
    [SerializeField] private string attackStateName = "ATTACK";
    [SerializeField] private string bodyHitStateName = "SMOTHHIT";
    [SerializeField] private string headHitStateName = "HEADHIT";
    [SerializeField] private string deathStateName = "DEATH";
    [SerializeField] private float hitLockDuration = 0.55f;
    [SerializeField] private float crossFadeDuration = 0.08f;
    [SerializeField] private bool replayNonLoopingMoveState = true;
    [SerializeField] private bool disableRootMotion = true;

    [Header("Visual Aim Correction")]
    [SerializeField] private bool useVisualAimCorrection = true;
    [SerializeField] private bool autoUseFirstChildAsVisualRoot = true;
    [SerializeField] private Transform[] visualAimRoots;
    [SerializeField] private float visualYawOffset;

    [Header("Death")]
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider[] collidersToDisableOnDeath;

    [Header("Debug")]
    [SerializeField] private bool drawDebugAttackRay;

    private BTNode _root;
    private Coroutine _attackRoutine;
    private Coroutine _hitRoutine;
    private Quaternion[] _visualBaseLocalRotations;
    private string _currentAnimationState;
    private float _nextAttackTime;
    private float _nextDestinationRefreshTime;
    private bool _deathPlayed;
    private bool _shouldApplyVisualAimCorrection;

    private void Awake()
    {
        ResolveReferences();
        BuildTree();
    }

    private void OnEnable()
    {
        if (health != null)
            health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDied;

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (_hitRoutine != null)
        {
            StopCoroutine(_hitRoutine);
            _hitRoutine = null;
        }
    }

    private void Update()
    {
        ResolveTarget();
        _root?.Tick();
    }

    private void LateUpdate()
    {
        ApplyVisualAimCorrection();
    }

    private void ResolveReferences()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (health == null)
            health = GetComponent<Health>();

        if (firePoint == null)
            firePoint = transform;

        if (animator != null && disableRootMotion)
            animator.applyRootMotion = false;

        if (agent != null)
        {
            agent.speed = agentSpeed;
            agent.acceleration = agentAcceleration;
            agent.stoppingDistance = moveDestinationStopDistance;
            agent.autoBraking = true;
            agent.updateRotation = false;
        }

        if (disableCollidersOnDeath && (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0))
            collidersToDisableOnDeath = GetComponentsInChildren<Collider>();

        CacheVisualAimRoots();
    }

    private void CacheVisualAimRoots()
    {
        if ((visualAimRoots == null || visualAimRoots.Length == 0) && autoUseFirstChildAsVisualRoot && transform.childCount > 0)
            visualAimRoots = new[] { transform.GetChild(0) };

        if (visualAimRoots == null || visualAimRoots.Length == 0)
            return;

        _visualBaseLocalRotations = new Quaternion[visualAimRoots.Length];
        for (int i = 0; i < visualAimRoots.Length; i++)
            _visualBaseLocalRotations[i] = visualAimRoots[i] != null ? visualAimRoots[i].localRotation : Quaternion.identity;
    }

    private void BuildTree()
    {
        _root = new BTSelector(
            new BTSequence(
                new BTCondition(IsDead),
                new BTAction(DoDeath)
            ),
            new BTSequence(
                new BTCondition(IsActionLocked),
                new BTAction(DoActionLocked)
            ),
            new BTSequence(
                new BTCondition(HasTarget),
                new BTCondition(IsTooClose),
                new BTAction(DoRetreat)
            ),
            new BTSequence(
                new BTCondition(HasTarget),
                new BTCondition(CanAttack),
                new BTAction(DoAttack)
            ),
            new BTSequence(
                new BTCondition(HasTarget),
                new BTCondition(IsTargetInDetectionRange),
                new BTAction(DoKeepDistance)
            ),
            new BTAction(DoIdle)
        );
    }

    private void ResolveTarget()
    {
        if (target != null && targetHealth != null)
            return;

        GameObject playerObject = GameObject.Find(playerObjectName);
        if (playerObject != null)
        {
            target = playerObject.transform;
            targetHealth = playerObject.GetComponent<Health>();
        }

        if (target == null)
        {
            PlayerController controller = FindFirstObjectByType<PlayerController>();
            if (controller != null)
            {
                target = controller.transform;
                targetHealth = controller.GetComponent<Health>();
            }
        }

        if (targetHealth == null && target != null)
            targetHealth = target.GetComponentInParent<Health>();
    }

    private bool HasTarget()
    {
        if (target == null || targetHealth == null)
            return false;

        return !requireAliveTarget || !targetHealth.IsDead;
    }

    private bool IsDead()
    {
        return health == null || health.IsDead;
    }

    private bool IsTargetInDetectionRange()
    {
        if (ignoreDetectionRange)
            return true;

        return GetHorizontalDistanceToTarget() <= detectionRange;
    }

    private bool IsTooClose()
    {
        return GetHorizontalDistanceToTarget() < retreatDistance;
    }

    private bool CanAttack()
    {
        float distance = GetHorizontalDistanceToTarget();
        if (attackAtPreferredDistanceOnly && distance > preferredDistance + attackDistanceTolerance)
            return false;

        return Time.time >= _nextAttackTime
               && _attackRoutine == null
               && distance <= attackRange
               && HasLineOfSight();
    }

    private bool IsActionLocked()
    {
        return _attackRoutine != null || _hitRoutine != null;
    }

    private BTStatus DoIdle()
    {
        StopAgent();
        _shouldApplyVisualAimCorrection = false;
        PlayState(idleStateName);
        return BTStatus.Running;
    }

    private BTStatus DoKeepDistance()
    {
        float distance = GetHorizontalDistanceToTarget();
        if (distance <= preferredDistance && HasLineOfSight())
        {
            StopAgent();
            FaceTarget();
            _shouldApplyVisualAimCorrection = true;
            PlayState(aimStateName);
            return BTStatus.Running;
        }

        SetAgentStoppingDistance(preferredDistance);
        SetDestination(target.position);
        FaceMoveDirection();
        _shouldApplyVisualAimCorrection = false;
        PlayState(moveStateName, replayNonLoopingMoveState);
        return BTStatus.Running;
    }

    private BTStatus DoRetreat()
    {
        SetAgentStoppingDistance(moveDestinationStopDistance);
        SetDestination(GetPositionAwayFromTarget(preferredDistance));
        FaceMoveDirection();
        _shouldApplyVisualAimCorrection = false;
        PlayState(moveStateName, replayNonLoopingMoveState);
        return BTStatus.Running;
    }

    private BTStatus DoAttack()
    {
        StopAgent();
        FaceTarget();
        _shouldApplyVisualAimCorrection = true;

        if (_attackRoutine == null)
            _attackRoutine = StartCoroutine(AttackRoutine());

        return BTStatus.Running;
    }

    private BTStatus DoActionLocked()
    {
        StopAgent();
        FaceTarget();
        _shouldApplyVisualAimCorrection = _attackRoutine != null;
        return BTStatus.Running;
    }

    private BTStatus DoDeath()
    {
        StopAgent();
        _shouldApplyVisualAimCorrection = false;

        if (!_deathPlayed)
        {
            _deathPlayed = true;
            PlayState(deathStateName);
            DisableColliders();
        }

        return BTStatus.Running;
    }

    private IEnumerator AttackRoutine()
    {
        _nextAttackTime = Time.time + attackCooldown;
        PlayState(aimStateName);

        yield return new WaitForSeconds(readyAttackDuration);

        if (IsDead())
        {
            _attackRoutine = null;
            yield break;
        }

        PlayState(attackStateName);

        yield return new WaitForSeconds(releaseDelay);

        if (!IsDead() && HasTarget() && HasLineOfSight())
            Fire();

        _attackRoutine = null;
    }

    public void PlayHitReaction(bool isHeadshot)
    {
        if (IsDead())
            return;

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (_hitRoutine != null)
            StopCoroutine(_hitRoutine);

        _hitRoutine = StartCoroutine(HitReactionRoutine(isHeadshot));
    }

    private IEnumerator HitReactionRoutine(bool isHeadshot)
    {
        StopAgent();
        _currentAnimationState = null;
        _shouldApplyVisualAimCorrection = false;
        PlayState(isHeadshot ? headHitStateName : bodyHitStateName);

        yield return new WaitForSeconds(hitLockDuration);

        _currentAnimationState = null;
        _hitRoutine = null;
    }

    private void Fire()
    {
        Vector3 origin = firePoint.position;
        Vector3 aimPoint = target.position + Vector3.up * aimHeight;
        Vector3 direction = (aimPoint - origin).normalized;

        if (projectilePrefab != null)
        {
            GameObject projectileObject = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
            EnemyProjectile projectile = projectileObject.GetComponent<EnemyProjectile>();
            if (projectile == null)
                projectile = projectileObject.AddComponent<EnemyProjectile>();

            projectile.Launch(transform, direction * projectileSpeed, damage, projectileLifetime, projectileHitLayers);
            return;
        }

        if (!useHitscanWhenProjectileMissing)
            return;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, attackRange, projectileHitLayers,
                QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
        }

        if (drawDebugAttackRay)
            Debug.DrawRay(origin, direction * attackRange, Color.red, 0.4f);
    }

    private bool HasLineOfSight()
    {
        if (target == null)
            return false;

        Vector3 origin = firePoint.position;
        Vector3 aimPoint = target.position + Vector3.up * aimHeight;
        Vector3 direction = aimPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction / distance, distance, lineOfSightMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(target))
                continue;

            return false;
        }

        return true;
    }

    private void SetDestination(Vector3 desiredPosition)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (Time.time < _nextDestinationRefreshTime)
            return;

        _nextDestinationRefreshTime = Time.time + destinationRefreshInterval;
        agent.isStopped = false;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, destinationSampleRadius, agent.areaMask))
            agent.SetDestination(hit.position);
    }

    private Vector3 GetPositionAwayFromTarget(float desiredDistance)
    {
        Vector3 away = transform.position - target.position;
        away.y = 0f;

        if (away.sqrMagnitude < 0.001f)
            away = -transform.forward;

        return target.position + away.normalized * desiredDistance;
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private void SetAgentStoppingDistance(float distance)
    {
        if (agent == null)
            return;

        agent.stoppingDistance = Mathf.Max(0f, distance);
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction),
            rotationSpeed * Time.deltaTime);
    }

    private void FaceMoveDirection()
    {
        if (agent == null)
        {
            FaceTarget();
            return;
        }

        Vector3 direction = agent.desiredVelocity;
        if (direction.sqrMagnitude <= 0.01f && agent.hasPath)
            direction = agent.steeringTarget - transform.position;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            FaceTarget();
            return;
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction),
            rotationSpeed * Time.deltaTime);
    }

    private void ApplyVisualAimCorrection()
    {
        if (!useVisualAimCorrection || !_shouldApplyVisualAimCorrection || target == null || _visualBaseLocalRotations == null || IsDead())
        {
            RestoreVisualAimRoots();
            return;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion worldLookRotation = Quaternion.LookRotation(direction);
        Quaternion localLookCorrection = Quaternion.Inverse(transform.rotation) * worldLookRotation;
        Quaternion localYawOffset = Quaternion.Euler(0f, visualYawOffset, 0f);
        Quaternion correction = localLookCorrection * localYawOffset;

        for (int i = 0; i < visualAimRoots.Length; i++)
        {
            if (visualAimRoots[i] == null)
                continue;

            visualAimRoots[i].localRotation = _visualBaseLocalRotations[i] * correction;
        }
    }

    private void RestoreVisualAimRoots()
    {
        if (_visualBaseLocalRotations == null || visualAimRoots == null)
            return;

        for (int i = 0; i < visualAimRoots.Length; i++)
        {
            if (visualAimRoots[i] != null)
                visualAimRoots[i].localRotation = _visualBaseLocalRotations[i];
        }
    }

    private float GetHorizontalDistanceToTarget()
    {
        if (target == null)
            return float.MaxValue;

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    private void PlayState(string stateName, bool restartCompletedNonLoopingState = false)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (!animator.HasState(0, stateHash))
            return;

        if (_currentAnimationState == stateName)
        {
            if (!ShouldRestartCurrentState(stateHash, restartCompletedNonLoopingState))
                return;
        }

        animator.CrossFadeInFixedTime(stateHash, crossFadeDuration);
        _currentAnimationState = stateName;
    }

    private bool ShouldRestartCurrentState(int stateHash, bool restartCompletedNonLoopingState)
    {
        if (!restartCompletedNonLoopingState)
            return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (stateInfo.shortNameHash != stateHash)
            return false;

        return !stateInfo.loop && stateInfo.normalizedTime >= 0.95f;
    }

    private void DisableColliders()
    {
        if (!disableCollidersOnDeath || collidersToDisableOnDeath == null)
            return;

        for (int i = 0; i < collidersToDisableOnDeath.Length; i++)
        {
            if (collidersToDisableOnDeath[i] != null)
                collidersToDisableOnDeath[i].enabled = false;
        }
    }

    private void HandleDied()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (_hitRoutine != null)
        {
            StopCoroutine(_hitRoutine);
            _hitRoutine = null;
        }

        _deathPlayed = false;
        _shouldApplyVisualAimCorrection = false;
    }
}
