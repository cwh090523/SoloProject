using System.Collections;
using DefaultNamespace;
using Unity.AppUI.MVVM;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public interface IEnemyTargetProvider
{
    bool TryGetTarget(out Transform target, out Health targetHealth);
}

public interface IEnemyMovementBlocker
{
    bool IsMovementBlocked { get; }
}

public interface IEnemySpeedModifier
{
    float SpeedModifier { get; }
}

public interface IEnemyAttackHandler
{
    void Attack(Health targetHealth, float damage);
}

public interface IEnemyAnimationDriver
{
    void PlayIdle();
    void PlayMove();
    void PlayAttack(string stateName);
    void PlayDeath();
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyBTAgent : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private Transform target;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;

    [Header("Target")] [SerializeField] private string playerObjectName = "Player3";
    [SerializeField] private bool requireAliveTarget;
    [SerializeField] private float targetNavMeshSampleRadius = 20f;
    [SerializeField] private bool allowPartialPathToTarget = true;

    [Header("Movement")] [SerializeField] private float detectionRange = 45f;
    [SerializeField] private bool ignoreDetectionRange = true;
    [SerializeField] private float stoppingDistance = 1.25f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float agentSpeed = 8f;
    [SerializeField] private float agentAcceleration = 80f;
    [SerializeField] private float agentAngularSpeed = 720f;
    [SerializeField] private float destinationRefreshInterval = 0.15f;
    [SerializeField] private float destinationMoveThreshold = 0.35f;
    [SerializeField] private bool keepRootUpright = true;

    [Header("Off Mesh Link Jump")] [SerializeField]
    private string jumpStateName = "Jump";

    [SerializeField] private float jumpDuration = 0.65f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private bool faceJumpDirection = true;

    [Header("Dash Approach")] [SerializeField]
    private bool useDashToTarget;

    [SerializeField] private string dashStateName = "Dodge_Combat_F";
    [SerializeField] private float dashStartDistance = 7f;
    [SerializeField] private float dashStopDistance = 2.2f;
    [SerializeField] private float dashSpeedMultiplier = 2.5f;
    [SerializeField] private float dashDuration = 0.55f;
    [SerializeField] private float dashCooldown = 4f;
    [SerializeField] private float dashDestinationRefreshInterval = 0.08f;

    [Header("Attack")] [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackHitDelay = 0.35f;
    [SerializeField] private float attackAnimationLockTime = 0.9f;
    [SerializeField] private bool allowMultipleHitsPerAttack;
    [SerializeField] private float maxAttackHeightDifference = 1.4f;
    [SerializeField] private bool requireAttackLineOfSight = true;
    [SerializeField] private LayerMask attackLineOfSightMask = ~0;
    [SerializeField] private float attackRayStartHeight = 1.2f;
    [SerializeField] private float targetRayHeight = 1f;
    [SerializeField] private string[] attackStateNames = { "Zombie Attack1", "Zombie Attack2", "Zombie Attack3" };

    [Header("Animation")] [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private string moveStateName = "RUNNING";
    [SerializeField] private string deathStateName = "DEATH";
    [SerializeField] private float animationCrossFade = 0.08f;
    [SerializeField] private bool disableAnimatorRootMotion = true;

    [Header("Death")] [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider[] collidersToDisableOnDeath;
    [SerializeField] private GameStateTracker statesTracker;

    [Header("Debug")] [SerializeField] private bool debugMovementState = true;
    [SerializeField] private float debugLogInterval = 1f;

    private BTNode _root;
    private Health _targetHealth;
    private IEnemyTargetProvider[] _targetProviders;
    private IEnemyMovementBlocker[] _movementBlockers;
    private IEnemyAttackHandler[] _attackHandlers;
    private IEnemyAnimationDriver[] _animationDrivers;
    private IEnemySpeedModifier[] _speedModifiers;
    private DamageHitReaction[] _hitReactions;
    private Coroutine _attackRoutine;
    private Coroutine _jumpRoutine;
    private Coroutine _dashRoutine;
    private NavMeshPath _pathProbe;
    private Vector3 _lastDestination;
    private string _currentAnimationState;
    private float _baseAgentSpeed;
    private float _baseAttackDamage;
    private float _baseAttackCooldown;
    private float _baseAttackAnimationLockTime;
    private float _nextAttackTime;
    private float _nextDashTime;
    private float _nextDestinationRefreshTime;
    private float _nextDebugLogTime;
    private bool _hasDestination;
    private bool _isAttacking;
    private bool _attackDamageApplied;
    private bool _isJumping;
    private bool _isDashing;
    private bool _deathAnimationPlayed;
    private bool _wasMovementBlocked;

    private void Awake()
    {
        ResolveReferences();
        ResolveExtensions();
        BuildTree();
    }

    private void OnEnable()
    {
        SubscribeHitReactionEvents();

        if (health != null)
        {
            health.Died += HandleDied;
            health.ResetHealth += HandleResetHealth;
        }

        HandleResetHealth();
    }

    private void OnDisable()
    {
        UnsubscribeHitReactionEvents();

        if (health != null)
        {
            health.Died -= HandleDied;
            health.ResetHealth -= HandleResetHealth;
        }
    }

    private void Update()
    {
        ResolveTarget();
        RefreshAnimationCacheAfterMovementBlock();

        if (_isJumping || _isDashing)
        {
            LogDebugState();
            return;
        }

        _root?.Tick();
        LogDebugState();
    }

    private void LateUpdate()
    {
        if (keepRootUpright)
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    private void ResolveReferences()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (health == null)
            health = GetComponent<Health>();
        if (statesTracker == null)
            statesTracker = FindFirstObjectByType<GameStateTracker>();

        CacheBaseCombatValues();

        CacheDeathColliders();

        if (animator != null && disableAnimatorRootMotion)
            animator.applyRootMotion = false;

        if (agent != null)
        {
            agent.speed = agentSpeed;
            agent.stoppingDistance = stoppingDistance;
            agent.acceleration = agentAcceleration;
            agent.angularSpeed = agentAngularSpeed;
            agent.autoTraverseOffMeshLink = false;
            agent.autoBraking = true;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
        }

        _pathProbe ??= new NavMeshPath();
    }

    private void ResolveExtensions()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        _targetProviders = CollectInterfaces<IEnemyTargetProvider>(behaviours);
        _movementBlockers = CollectInterfaces<IEnemyMovementBlocker>(behaviours);
        _attackHandlers = CollectInterfaces<IEnemyAttackHandler>(behaviours);
        _animationDrivers = CollectInterfaces<IEnemyAnimationDriver>(behaviours);
        _speedModifiers = CollectInterfaces<IEnemySpeedModifier>(behaviours);
        _hitReactions = GetComponents<DamageHitReaction>();
    }

    private void CacheDeathColliders()
    {
        if (!disableCollidersOnDeath)
            return;

        collidersToDisableOnDeath = GetComponentsInChildren<Collider>();
    }

    private T[] CollectInterfaces<T>(MonoBehaviour[] behaviours)
    {
        int count = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this && behaviours[i] is T)
                count++;
        }

        T[] result = new T[count];
        int index = 0;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != this && behaviours[i] is T extension)
                result[index++] = extension;
        }

        return result;
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
                new BTCondition(CanStartDash),
                new BTAction(DoDash)
            ),
            new BTSequence(
                new BTCondition(HasTarget),
                new BTCondition(CanAttackTarget),
                new BTAction(DoAttack)
            ),
            new BTSequence(
                new BTCondition(HasTarget),
                new BTCondition(IsTargetInDetectionRange),
                new BTAction(DoChase)
            ),
            new BTAction(DoIdle)
        );
    }

    private void CacheBaseCombatValues()
    {
        _baseAgentSpeed = agentSpeed;
        _baseAttackDamage = attackDamage;
        _baseAttackCooldown = attackCooldown;
        _baseAttackAnimationLockTime = attackAnimationLockTime;
    }

    private void ResolveTarget()
    {
        for (int i = 0; i < _targetProviders.Length; i++)
        {
            if (_targetProviders[i].TryGetTarget(out Transform providedTarget, out Health providedHealth))
            {
                target = providedTarget;
                _targetHealth = providedHealth;
                return;
            }
        }

        if (target != null && _targetHealth != null)
            return;

        GameObject playerObject = GameObject.Find(playerObjectName);
        if (playerObject != null)
        {
            target = playerObject.transform;
            _targetHealth = playerObject.GetComponent<Health>();
        }

        if (target == null || _targetHealth == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
                return;

            target = player.transform;
            _targetHealth = player.GetComponent<Health>();
        }

        if (_targetHealth == null && target != null)
            _targetHealth = target.GetComponentInParent<Health>();
    }

    private bool IsDead()
    {
        return health == null || health.IsDead;
    }

    private bool HasTarget()
    {
        if (target == null || _targetHealth == null)
            return false;

        return !requireAliveTarget || !_targetHealth.IsDead;
    }

    private bool IsActionLocked()
    {
        return _isAttacking || IsMovementBlocked();
    }

    private bool IsTargetInDetectionRange()
    {
        if (ignoreDetectionRange)
            return true;

        return GetHorizontalSqrDistanceToTarget() <= detectionRange * detectionRange;
    }

    private bool CanAttackTarget()
    {
        if (!IsTargetInAttackRange())
            return false;

        float heightDifference = Mathf.Abs(target.position.y - transform.position.y);
        if (heightDifference > maxAttackHeightDifference)
            return false;

        return !requireAttackLineOfSight || HasClearAttackLine();
    }

    private bool CanStartDash()
    {
        if (!useDashToTarget || _dashRoutine != null || Time.time < _nextDashTime)
            return false;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh || IsDead() || IsMovementBlocked())
            return false;

        float horizontalDistance = Mathf.Sqrt(GetHorizontalSqrDistanceToTarget());
        if (horizontalDistance < dashStartDistance || horizontalDistance <= attackRange)
            return false;

        return TryGetDestinationOnNavMesh(out _, out _);
    }

    private bool IsTargetInAttackRange()
    {
        return GetSqrDistanceToTarget() <= attackRange * attackRange;
    }

    private bool HasClearAttackLine()
    {
        Vector3 start = transform.position + Vector3.up * attackRayStartHeight;
        Vector3 end = target.position + Vector3.up * targetRayHeight;
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(start, direction / distance, distance, attackLineOfSightMask,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(target))
                continue;

            return false;
        }

        return true;
    }

    private BTStatus DoIdle()
    {
        StopAgent();
        PlayIdle();
        return BTStatus.Running;
    }

    private BTStatus DoChase()
    {
        // if (IsMovementBlocked())
        // {
        //     StopAgent();
        //     PlayIdle();
        //     return BTStatus.Running;
        // }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            PlayIdle();
            return BTStatus.Running;
        }

        if (agent.isOnOffMeshLink)
        {
            StartOffMeshLinkJump();
            return BTStatus.Running;
        }

        agent.isStopped = false;
        agent.speed = agentSpeed * GetSpeedMultiplier();
        agent.stoppingDistance = stoppingDistance;

        if (Time.time >= _nextDestinationRefreshTime)
        {
            TrySetDestination();
            _nextDestinationRefreshTime = Time.time + destinationRefreshInterval;
        }

        FaceMoveDirection();
        PlayMove();
        return BTStatus.Running;
    }

    private BTStatus DoDash()
    {
        if (_dashRoutine == null)
            _dashRoutine = StartCoroutine(DashRoutine());

        return BTStatus.Running;
    }

    private BTStatus DoAttack()
    {
        StopAgent();
        FaceTarget();

        if (Time.time >= _nextAttackTime && _attackRoutine == null)
        {
            _nextAttackTime = Time.time + attackCooldown;
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        return BTStatus.Running;
    }

    private BTStatus DoActionLocked()
    {
        StopAgent();
        FaceTarget();
        return BTStatus.Running;
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        _attackDamageApplied = false;

        string stateName = GetAttackStateName();
        PlayAttack(stateName);

        yield return new WaitForSeconds(Mathf.Max(0f, attackHitDelay));

        TryApplyAttackDamage();

        float remainingLockTime = Mathf.Max(0f, attackAnimationLockTime - attackHitDelay);
        if (remainingLockTime > 0f)
            yield return new WaitForSeconds(remainingLockTime);

        _isAttacking = false;
        PlayIdle();
        _attackRoutine = null;
    }

    private IEnumerator DashRoutine()
    {
        _isDashing = true;
        _hasDestination = false;
        _nextDashTime = Time.time + Mathf.Max(0f, dashCooldown);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
            agent.speed = agentSpeed * GetSpeedMultiplier() * Mathf.Max(1f, dashSpeedMultiplier);
            agent.stoppingDistance = Mathf.Max(0.1f, dashStopDistance);
        }

        CrossFade(dashStateName);

        float elapsed = 0f;
        float nextRefreshTime = 0f;
        float duration = Mathf.Max(0.05f, dashDuration);
        while (elapsed < duration && !IsDead())
        {
            elapsed += Time.deltaTime;

            if (agent == null || !agent.enabled || !agent.isOnNavMesh || IsMovementBlocked())
                break;

            if (Time.time >= nextRefreshTime && TryGetDestinationOnNavMesh(out Vector3 destination, out _))
            {
                agent.SetDestination(destination);
                nextRefreshTime = Time.time + Mathf.Max(0.02f, dashDestinationRefreshInterval);
            }

            FaceMoveDirection();

            if (GetHorizontalSqrDistanceToTarget() <= dashStopDistance * dashStopDistance)
                break;

            yield return null;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = agentSpeed * GetSpeedMultiplier();
            agent.stoppingDistance = stoppingDistance;
            agent.velocity = Vector3.zero;
        }

        _isDashing = false;
        _dashRoutine = null;
        _currentAnimationState = string.Empty;
    }

    public void Attack()
    {
        TryApplyAttackDamage();
    }

    public void AttackHit()
    {
        TryApplyAttackDamage();
    }

    public void Hit()
    {
        TryApplyAttackDamage();
    }

    public void DealDamage()
    {
        TryApplyAttackDamage();
    }

    private BTStatus DoDeath()
    {
        StopAgent();

        if (!_deathAnimationPlayed)
        {
            DisableDeathColliders();
            PlayDeath();
            _deathAnimationPlayed = true;
        }

        return BTStatus.Running;
    }

    private void TrySetDestination()
    {
        if (!TryGetDestinationOnNavMesh(out Vector3 destination, out NavMeshPathStatus pathStatus))
            return;


        if (_hasDestination && (destination - _lastDestination).sqrMagnitude <
            destinationMoveThreshold * destinationMoveThreshold)
            return;

        if (agent.SetDestination(destination))
        {
            _lastDestination = destination;
            _hasDestination = true;
        }
    }

    private bool TryGetDestinationOnNavMesh(out Vector3 destination, out NavMeshPathStatus pathStatus)
    {
        destination = target.position;
        pathStatus = NavMeshPathStatus.PathInvalid;

        if (!NavMesh.SamplePosition(target.position, out NavMeshHit hit, targetNavMeshSampleRadius, agent.areaMask))
            return false;

        destination = hit.position;
        _pathProbe ??= new NavMeshPath();
        if (!agent.CalculatePath(destination, _pathProbe))
            return false;

        pathStatus = _pathProbe.status;
        return pathStatus == NavMeshPathStatus.PathComplete ||
               allowPartialPathToTarget && pathStatus == NavMeshPathStatus.PathPartial;
    }

    private void StartOffMeshLinkJump()
    {
        if (_jumpRoutine != null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        _jumpRoutine = StartCoroutine(TraverseOffMeshLinkRoutine());
    }

    private IEnumerator TraverseOffMeshLinkRoutine()
    {
        _isJumping = true;
        _hasDestination = false;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = linkData.endPos;
        endPosition.y += agent.baseOffset;

        NavMeshLinkSplineHeight splineHeight =
            NavMeshLinkSplineHeight.FindBest(startPosition, endPosition, out bool reverseSpline);

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updatePosition = false;
        agent.updateRotation = false;

        if (faceJumpDirection)
            RotateTowardsFlatDirection(endPosition - startPosition);

        PlayJump();

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, jumpDuration);
        while (elapsed < duration && !IsDead())
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 nextPosition = Vector3.Lerp(startPosition, endPosition, t);
            float linearY = nextPosition.y;

            if (splineHeight != null && splineHeight.TrySampleY(t, reverseSpline, linearY, out float splineY))
            {
                nextPosition.y = splineY;
            }
            else
            {
                float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                nextPosition.y = linearY + arc;
            }

            transform.position = nextPosition;
            agent.nextPosition = nextPosition;
            yield return null;
        }

        if (!IsDead())
        {
            agent.Warp(endPosition);
            transform.position = endPosition;
            agent.CompleteOffMeshLink();
        }

        agent.updatePosition = true;
        agent.updateRotation = false;
        agent.isStopped = false;
        agent.velocity = Vector3.zero;

        _isJumping = false;
        _jumpRoutine = null;
        _currentAnimationState = string.Empty;
    }

    private void ExecuteAttack()
    {
        if (_attackHandlers.Length > 0)
        {
            for (int i = 0; i < _attackHandlers.Length; i++)
                _attackHandlers[i].Attack(_targetHealth, attackDamage);

            return;
        }

        _targetHealth.TakeDamage(attackDamage);
    }

    private void TryApplyAttackDamage()
    {
        if (!allowMultipleHitsPerAttack && _attackDamageApplied)
            return;

        if (!_isAttacking || IsDead() || !HasTarget() || !CanAttackTarget())
            return;

        _attackDamageApplied = true;
        ExecuteAttack();
    }

    private bool IsMovementBlocked()
    {
        for (int i = 0; i < _movementBlockers.Length; i++)
        {
            if (_movementBlockers[i].IsMovementBlocked)
                return true;
        }

        return false;
    }

    private float GetSpeedMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < _speedModifiers.Length; i++)
        {
            multiplier *= Mathf.Clamp01(_speedModifiers[i].SpeedModifier);
        }

        return multiplier;
    }

    private void RefreshAnimationCacheAfterMovementBlock()
    {
        bool isMovementBlocked = IsMovementBlocked();
        if (_wasMovementBlocked && !isMovementBlocked)
            _currentAnimationState = string.Empty;

        _wasMovementBlocked = isMovementBlocked;
    }

    private float GetSqrDistanceToTarget()
    {
        if (target == null)
            return float.MaxValue;

        return (target.position - transform.position).sqrMagnitude;
    }

    private float GetHorizontalSqrDistanceToTarget()
    {
        if (target == null)
            return float.MaxValue;

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        RotateTowardsFlatDirection(target.position - transform.position);
    }

    private void FaceMoveDirection()
    {
        if (agent == null)
            return;

        Vector3 direction = agent.desiredVelocity;
        if (direction.sqrMagnitude <= 0.01f && agent.hasPath)
            direction = agent.steeringTarget - transform.position;

        RotateTowardsFlatDirection(direction);
    }

    private void RotateTowardsFlatDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation =
            Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        _hasDestination = false;
    }

    public void SetDashEnabled(bool isEnabled)
    {
        useDashToTarget = isEnabled;
    }

    public void SetCombatPhaseModifiers(float speedMultiplier, float damageMultiplier, float attackCooldownMultiplier)
    {
        agentSpeed = _baseAgentSpeed * Mathf.Max(0.1f, speedMultiplier);
        attackDamage = _baseAttackDamage * Mathf.Max(0f, damageMultiplier);
        attackCooldown = _baseAttackCooldown * Mathf.Max(0.05f, attackCooldownMultiplier);
        attackAnimationLockTime = _baseAttackAnimationLockTime * Mathf.Max(0.05f, attackCooldownMultiplier);

        if (agent != null)
            agent.speed = agentSpeed * GetSpeedMultiplier();
    }

    private string GetAttackStateName()
    {
        if (attackStateNames == null || attackStateNames.Length == 0)
            return string.Empty;

        return attackStateNames[Random.Range(0, attackStateNames.Length)];
    }

    private void PlayIdle()
    {
        if (IsHitReacting())
            return;

        if (TryPlayExtensionAnimation(driver => driver.PlayIdle()))
            return;

        CrossFade(idleStateName);
    }

    private void PlayMove()
    {
        if (IsHitReacting())
            return;

        if (TryPlayExtensionAnimation(driver => driver.PlayMove()))
            return;

        CrossFade(moveStateName);
    }

    private void PlayAttack(string stateName)
    {
        if (TryPlayExtensionAnimation(driver => driver.PlayAttack(stateName)))
            return;

        CrossFade(stateName);
    }

    private void PlayDeath()
    {
        if (TryPlayExtensionAnimation(driver => driver.PlayDeath()))
            return;

        CrossFade(deathStateName);
    }

    private void PlayJump()
    {
        CrossFade(jumpStateName);
    }

    private bool TryPlayExtensionAnimation(System.Action<IEnemyAnimationDriver> play)
    {
        if (_animationDrivers.Length == 0)
            return false;

        for (int i = 0; i < _animationDrivers.Length; i++)
            play(_animationDrivers[i]);

        return true;
    }

    private void CrossFade(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        if (_currentAnimationState == stateName)
            return;

        _currentAnimationState = stateName;
        animator.CrossFadeInFixedTime(stateName, animationCrossFade, 0, 0f);
    }

    private bool IsHitReacting()
    {
        if (_hitReactions == null)
            return false;

        for (int i = 0; i < _hitReactions.Length; i++)
        {
            if (_hitReactions[i] != null && _hitReactions[i].IsReacting)
                return true;
        }

        return false;
    }

    private void SubscribeHitReactionEvents()
    {
        if (_hitReactions == null || _hitReactions.Length == 0)
            _hitReactions = GetComponents<DamageHitReaction>();

        if (_hitReactions == null)
            return;

        for (int i = 0; i < _hitReactions.Length; i++)
        {
            if (_hitReactions[i] == null)
                continue;

            _hitReactions[i].ReactionFinished -= HandleHitReactionFinished;
            _hitReactions[i].ReactionFinished += HandleHitReactionFinished;
        }
    }

    private void UnsubscribeHitReactionEvents()
    {
        if (_hitReactions == null)
            return;

        for (int i = 0; i < _hitReactions.Length; i++)
        {
            if (_hitReactions[i] != null)
                _hitReactions[i].ReactionFinished -= HandleHitReactionFinished;
        }
    }

    private void HandleHitReactionFinished()
    {
        _currentAnimationState = string.Empty;
    }

    private void HandleDied()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }

        if (_dashRoutine != null)
        {
            StopCoroutine(_dashRoutine);
            _dashRoutine = null;
        }

        if(statesTracker != null)
            statesTracker.AddKill();

        DamageHitReaction[] hitReactions = GetComponents<DamageHitReaction>();
        for (int i = 0; i < hitReactions.Length; i++)
            hitReactions[i].CancelReaction();

        _isJumping = false;
        _isDashing = false;
        _isAttacking = false;
        _attackDamageApplied = false;
        StopAgent();
        DisableDeathColliders();
        _deathAnimationPlayed = false;
        _currentAnimationState = string.Empty;
    }

    private void HandleResetHealth()
    {
        _deathAnimationPlayed = false;
        _isJumping = false;
        _isDashing = false;
        _isAttacking = false;
        _attackDamageApplied = false;
        _currentAnimationState = string.Empty;
        EnableDeathColliders();
    }

    private void DisableDeathColliders()
    {
        if (!disableCollidersOnDeath || collidersToDisableOnDeath == null)
            return;

        for (int i = 0; i < collidersToDisableOnDeath.Length; i++)
        {
            if (collidersToDisableOnDeath[i] != null)
                collidersToDisableOnDeath[i].enabled = false;
        }
    }

    private void EnableDeathColliders()
    {
        if (!disableCollidersOnDeath || collidersToDisableOnDeath == null)
            return;

        for (int i = 0; i < collidersToDisableOnDeath.Length; i++)
        {
            if (collidersToDisableOnDeath[i] != null)
                collidersToDisableOnDeath[i].enabled = true;
        }
    }

    private void LogDebugState()
    {
        if (!debugMovementState || Time.time < _nextDebugLogTime)
            return;

        _nextDebugLogTime = Time.time + Mathf.Max(0.1f, debugLogInterval);

        string targetText = target != null ? $"{target.name} {target.position}" : "null";
        string agentText = agent != null
            ? $"onNavMesh={agent.isOnNavMesh}, stopped={agent.isStopped}, hasPath={agent.hasPath}, status={agent.pathStatus}, velocity={agent.velocity.magnitude:0.00}"
            : "null";

        Debug.Log($"{name} BT | target={targetText} | agent={agentText}", this);
    }
}
