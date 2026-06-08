using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyBTAgent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [SerializeField] private DamageHitReaction hitReaction;

    [Header("Detection")]
    [SerializeField] private float detectionRange = 45f;
    [SerializeField] private bool ignoreDetectionRange = true;
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float stoppingDistance = 1.25f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private bool requireAliveTarget = false;

    [Header("NavMesh Tuning")]
    [SerializeField] private float agentAcceleration = 80f;
    [SerializeField] private float agentAngularSpeed = 720f;
    [SerializeField] private float destinationRefreshInterval = 0.1f;
    [SerializeField] private bool clearVelocityOnStop = true;

    [Header("Off Mesh Link Jump")]
    [SerializeField] private string jumpStateName = "Jump";
    [SerializeField] private float jumpDuration = 0.65f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private bool faceJumpDirection = true;

    [Header("Attack")]
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float attackHitDelay = 0.35f;
    [SerializeField] private float attackAnimationLockTime = 0.9f;
    [SerializeField] private float maxAttackHeightDifference = 1.4f;
    [SerializeField] private bool requireAttackLineOfSight = true;
    [SerializeField] private LayerMask attackLineOfSightMask = ~0;
    [SerializeField] private float attackRayStartHeight = 1.2f;
    [SerializeField] private float targetRayHeight = 1f;
    [SerializeField] private bool lockPositionDuringAttack = true;
    [SerializeField] private string[] attackStateNames = { "Zombie Attack1", "Zombie Attack2", "Zombie Attack3" };

    [Header("Animation")]
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private string moveStateName = "RUNNING";
    [SerializeField] private string deathStateName = "DEATH";
    [SerializeField] private float animationCrossFade = 0.08f;
    [SerializeField] private bool disableAnimatorRootMotion = true;

    [Header("Death")]
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private Collider[] collidersToDisableOnDeath;

    private BTNode _root;
    private Health _targetHealth;
    private Coroutine _attackRoutine;
    private Coroutine _jumpRoutine;
    private float _nextAttackTime;
    private float _nextDestinationRefreshTime;
    private float _actionLockedUntil;
    private string _currentStateName;
    private Vector3 _attackLockPosition;
    private bool _isDead;
    private bool _isHitReacting;
    private bool _isAttackPositionLocked;
    private bool _isTraversingLink;
    private bool _deathAnimationPlayed;

    private void Awake()
    {
        ResolveReferences();
        BuildTree();
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDied;
            health.ResetHealth += HandleResetHealth;

            if (!health.IsDead)
                HandleResetHealth();
        }

        if (hitReaction != null)
        {
            hitReaction.ReactionStarted += HandleHitReactionStarted;
            hitReaction.ReactionFinished += HandleHitReactionFinished;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
            health.ResetHealth -= HandleResetHealth;
        }

        if (hitReaction != null)
        {
            hitReaction.ReactionStarted -= HandleHitReactionStarted;
            hitReaction.ReactionFinished -= HandleHitReactionFinished;
        }
    }

    private void Update()
    {
        ResolveTarget();

        if (_isHitReacting)
        {
            StopAgent();
            return;
        }

        if (_isTraversingLink)
            return;

        _root?.Tick();
    }

    private void LateUpdate()
    {
        if (_isAttackPositionLocked)
            LockAttackPosition();
    }

    private void ResolveReferences()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (health == null)
            health = GetComponent<Health>();

        if (hitReaction == null)
            hitReaction = GetComponent<DamageHitReaction>();

        if (collidersToDisableOnDeath == null || collidersToDisableOnDeath.Length == 0)
            collidersToDisableOnDeath = GetComponentsInChildren<Collider>();

        if (animator != null && disableAnimatorRootMotion)
            animator.applyRootMotion = false;

        if (agent != null)
        {
            agent.stoppingDistance = stoppingDistance;
            agent.acceleration = agentAcceleration;
            agent.angularSpeed = agentAngularSpeed;
            agent.autoBraking = true;
            agent.autoTraverseOffMeshLink = false;
        }
    }

    private void BuildTree()
    {
        _root = new BTSelector(
            new BTSequence(
                new BTCondition(IsDead),
                new BTAction(DoDeath)
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

    private void ResolveTarget()
    {
        if (target != null && _targetHealth != null)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null)
            return;

        target = player.transform;
        _targetHealth = player.GetComponent<Health>();
        if (_targetHealth == null)
            _targetHealth = player.GetComponentInParent<Health>();
    }

    private bool IsDead()
    {
        return _isDead || health == null || health.IsDead;
    }

    private bool HasTarget()
    {
        if (target == null || _targetHealth == null)
            return false;

        return !requireAliveTarget || !_targetHealth.IsDead;
    }

    private bool IsTargetInDetectionRange()
    {
        if (ignoreDetectionRange)
            return true;

        return GetSqrDistanceToTarget() <= detectionRange * detectionRange;
    }

    private bool IsTargetInAttackRange()
    {
        return GetSqrDistanceToTarget() <= attackRange * attackRange;
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

    private bool HasClearAttackLine()
    {
        Vector3 start = transform.position + Vector3.up * attackRayStartHeight;
        Vector3 end = target.position + Vector3.up * targetRayHeight;
        Vector3 direction = end - start;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(start, direction / distance, distance, attackLineOfSightMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            if (hitTransform.IsChildOf(transform) || hitTransform.IsChildOf(target))
                continue;

            return false;
        }

        return true;
    }

    private float GetSqrDistanceToTarget()
    {
        if (target == null)
            return float.MaxValue;

        Vector3 offset = target.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    private BTStatus DoIdle()
    {
        StopAgent();
        PlayState(idleStateName);
        return BTStatus.Running;
    }

    private BTStatus DoChase()
    {
        if (agent == null || target == null)
            return BTStatus.Failure;

        if (agent.enabled && agent.isOnNavMesh)
        {
            if (agent.isOnOffMeshLink)
            {
                StartOffMeshLinkJump();
                return BTStatus.Running;
            }

            agent.isStopped = false;
            agent.stoppingDistance = stoppingDistance;

            if (Time.time >= _nextDestinationRefreshTime)
            {
                agent.SetDestination(target.position);
                _nextDestinationRefreshTime = Time.time + destinationRefreshInterval;
            }
        }

        FaceTarget();
        PlayState(moveStateName);
        return BTStatus.Running;
    }

    private void StartOffMeshLinkJump()
    {
        if (_jumpRoutine != null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        _jumpRoutine = StartCoroutine(TraverseOffMeshLinkRoutine());
    }

    private IEnumerator TraverseOffMeshLinkRoutine()
    {
        _isTraversingLink = true;
        _currentStateName = string.Empty;

        OffMeshLinkData linkData = agent.currentOffMeshLinkData;
        Vector3 startPosition = transform.position;
        Vector3 endPosition = linkData.endPos;
        endPosition.y += agent.baseOffset;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.updatePosition = false;
        agent.updateRotation = false;

        if (faceJumpDirection)
            FaceDirection(endPosition - startPosition, 1f);

        PlayState(jumpStateName, true);

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, jumpDuration);
        while (elapsed < duration && !IsDead() && !_isHitReacting)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;
            Vector3 nextPosition = Vector3.Lerp(startPosition, endPosition, t) + Vector3.up * arc;

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
        agent.updateRotation = true;
        agent.isStopped = false;

        _isTraversingLink = false;
        _jumpRoutine = null;
        _currentStateName = string.Empty;
    }

    private void CancelOffMeshLinkJump()
    {
        if (_jumpRoutine != null)
        {
            StopCoroutine(_jumpRoutine);
            _jumpRoutine = null;
        }

        _isTraversingLink = false;

        if (agent == null || !agent.enabled)
            return;

        agent.updatePosition = true;
        agent.updateRotation = true;

        if (agent.isOnNavMesh)
        {
            agent.Warp(transform.position);
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private BTStatus DoAttack()
    {
        StopAgent();
        FaceTarget();

        if (Time.time < _actionLockedUntil)
            return BTStatus.Running;

        if (Time.time >= _nextAttackTime && _attackRoutine == null)
        {
            _nextAttackTime = Time.time + attackCooldown;
            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        return BTStatus.Running;
    }

    private BTStatus DoDeath()
    {
        StopAgent();

        if (!_deathAnimationPlayed && !string.IsNullOrWhiteSpace(deathStateName))
        {
            PlayState(deathStateName, true, true);
            _deathAnimationPlayed = true;
        }

        return BTStatus.Running;
    }

    private IEnumerator AttackRoutine()
    {
        BeginAttackPositionLock();

        string attackStateName = GetAttackStateName();
        _actionLockedUntil = Time.time + attackAnimationLockTime;
        PlayState(attackStateName, true);

        yield return new WaitForSeconds(attackHitDelay);

        if (!IsDead() && HasTarget() && CanAttackTarget())
            _targetHealth.TakeDamage(attackDamage);

        float remainingLockTime = Mathf.Max(0f, _actionLockedUntil - Time.time);
        if (remainingLockTime > 0f)
            yield return new WaitForSeconds(remainingLockTime);

        EndAttackPositionLock();
        _attackRoutine = null;
    }

    private void BeginAttackPositionLock()
    {
        if (!lockPositionDuringAttack)
            return;

        _attackLockPosition = transform.position;
        _isAttackPositionLocked = true;
        LockAttackPosition();
    }

    private void EndAttackPositionLock()
    {
        if (!_isAttackPositionLocked)
            return;

        LockAttackPosition();
        _isAttackPositionLocked = false;
    }

    private void LockAttackPosition()
    {
        transform.position = _attackLockPosition;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.nextPosition = _attackLockPosition;
            agent.velocity = Vector3.zero;
        }
    }

    private string GetAttackStateName()
    {
        if (attackStateNames == null || attackStateNames.Length == 0)
            return string.Empty;

        return attackStateNames[Random.Range(0, attackStateNames.Length)];
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void FaceDirection(Vector3 direction, float lerp)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Mathf.Clamp01(lerp));
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();

        if (clearVelocityOnStop)
            agent.velocity = Vector3.zero;
    }

    private void PlayState(string stateName, bool force = false, bool ignoreHitReaction = false)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        if (!ignoreHitReaction && (_isHitReacting || (hitReaction != null && hitReaction.IsReacting)))
            return;

        if (!force && Time.time < _actionLockedUntil)
            return;

        if (!force && _currentStateName == stateName)
            return;

        _currentStateName = stateName;
        animator.CrossFadeInFixedTime(stateName, animationCrossFade, 0, 0f);
    }

    private void HandleDied()
    {
        _isDead = true;
        _isHitReacting = false;
        _actionLockedUntil = 0f;
        _currentStateName = string.Empty;
        _deathAnimationPlayed = false;

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        if (hitReaction != null)
            hitReaction.CancelReaction();

        CancelOffMeshLinkJump();

        EndAttackPositionLock();
        StopAgent();
        DisableDeathColliders();

        if (!string.IsNullOrWhiteSpace(deathStateName))
        {
            PlayState(deathStateName, true, true);
            _deathAnimationPlayed = true;
        }
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

    private void HandleResetHealth()
    {
        _isDead = false;
        _isHitReacting = false;
        _isAttackPositionLocked = false;
        _isTraversingLink = false;
        _deathAnimationPlayed = false;
        _currentStateName = string.Empty;
        EnableDeathColliders();
    }

    private void HandleHitReactionStarted()
    {
        _isHitReacting = true;
        _actionLockedUntil = 0f;
        _currentStateName = string.Empty;

        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        CancelOffMeshLinkJump();
        EndAttackPositionLock();
        StopAgent();
    }

    private void HandleHitReactionFinished()
    {
        _isHitReacting = false;
        _currentStateName = string.Empty;
    }
}
