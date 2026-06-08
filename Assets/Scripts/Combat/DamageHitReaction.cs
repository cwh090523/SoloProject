using System.Collections;
using System;
using UnityEngine;
using UnityEngine.AI;

public class DamageHitReaction : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private string bodyHitStateName = "Stomach Hit";
    [SerializeField] private string headHitStateName = "Head Hit";
    [SerializeField] private float crossFadeDuration = 0.05f;
    [SerializeField] private float fallbackReturnToIdleDelay = 0.55f;
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private bool lockAnimatorLocalTransform = true;
    [SerializeField] private bool lockOwnerLocalTransform;
    [SerializeField] private bool playIdleOnEnable;

    private Coroutine _returnRoutine;
    private Transform _animatorTransform;
    private Vector3 _baseAnimatorLocalPosition;
    private Quaternion _baseAnimatorLocalRotation;
    private Vector3 _baseOwnerLocalPosition;
    private Quaternion _baseOwnerLocalRotation;
    private NavMeshAgent _ownerAgent;

    public event Action ReactionStarted;
    public event Action ReactionFinished;

    public bool IsReacting => _returnRoutine != null;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheOwnerTransform();
        CacheAnimatorTransform();
    }

    private void OnEnable()
    {
        CacheOwnerTransform();
        CacheAnimatorTransform();

        if (playIdleOnEnable)
            PlayIdle();
    }

    private void LateUpdate()
    {
        RestoreAnimatorTransform();
        RestoreOwnerTransform();
    }

    public void PlayHitReaction(bool isHeadshot)
    {
        if (animator == null)
            return;

        string stateName = isHeadshot ? headHitStateName : bodyHitStateName;
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        ReactionStarted?.Invoke();
        animator.CrossFadeInFixedTime(stateName, crossFadeDuration, 0, 0f);
        _returnRoutine = StartCoroutine(ReturnToIdleRoutine(stateName));
    }

    public void CancelReaction()
    {
        if (_returnRoutine != null)
        {
            StopCoroutine(_returnRoutine);
            _returnRoutine = null;
            ReactionFinished?.Invoke();
        }
    }

    private IEnumerator ReturnToIdleRoutine(string hitStateName)
    {
        yield return null;

        float waitTime = GetCurrentStateRemainingTime(hitStateName);
        yield return new WaitForSeconds(waitTime);

        PlayIdle();
        _returnRoutine = null;
        ReactionFinished?.Invoke();
    }

    private float GetCurrentStateRemainingTime(string hitStateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(hitStateName))
            return fallbackReturnToIdleDelay;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(hitStateName))
            return fallbackReturnToIdleDelay;

        float normalizedTime = stateInfo.normalizedTime % 1f;
        float remainingRatio = Mathf.Clamp01(1f - normalizedTime);
        float remainingTime = stateInfo.length * remainingRatio;

        return Mathf.Max(remainingTime, fallbackReturnToIdleDelay);
    }

    private void PlayIdle()
    {
        if (animator == null || string.IsNullOrWhiteSpace(idleStateName))
            return;

        animator.CrossFadeInFixedTime(idleStateName, crossFadeDuration);
    }

    private void CacheAnimatorTransform()
    {
        if (animator == null)
            return;

        if (disableRootMotion)
            animator.applyRootMotion = false;

        _animatorTransform = animator.transform;
        _baseAnimatorLocalPosition = _animatorTransform.localPosition;
        _baseAnimatorLocalRotation = _animatorTransform.localRotation;
    }

    private void CacheOwnerTransform()
    {
        if (_ownerAgent == null)
            _ownerAgent = GetComponent<NavMeshAgent>();

        _baseOwnerLocalPosition = transform.localPosition;
        _baseOwnerLocalRotation = transform.localRotation;
    }

    private void RestoreAnimatorTransform()
    {
        if (!lockAnimatorLocalTransform || _animatorTransform == null)
            return;

        _animatorTransform.localPosition = _baseAnimatorLocalPosition;
        _animatorTransform.localRotation = _baseAnimatorLocalRotation;
    }

    private void RestoreOwnerTransform()
    {
        if (!lockOwnerLocalTransform)
            return;

        if (_ownerAgent != null && _ownerAgent.enabled)
            return;

        transform.localPosition = _baseOwnerLocalPosition;
        transform.localRotation = _baseOwnerLocalRotation;
    }
}
