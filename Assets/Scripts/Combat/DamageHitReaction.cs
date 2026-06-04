using System.Collections;
using UnityEngine;

public class DamageHitReaction : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private string bodyHitStateName = "Stomach Hit";
    [SerializeField] private string headHitStateName = "Head Hit";
    [SerializeField] private float crossFadeDuration = 0.05f;
    [SerializeField] private float fallbackReturnToIdleDelay = 0.55f;

    private Coroutine _returnRoutine;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        PlayIdle();
    }

    public void PlayHitReaction(bool isHeadshot)
    {
        if (animator == null)
            return;

        string stateName = isHeadshot ? headHitStateName : bodyHitStateName;
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        animator.CrossFadeInFixedTime(stateName, crossFadeDuration, 0, 0f);

        if (_returnRoutine != null)
            StopCoroutine(_returnRoutine);

        _returnRoutine = StartCoroutine(ReturnToIdleRoutine(stateName));
    }

    private IEnumerator ReturnToIdleRoutine(string hitStateName)
    {
        yield return null;

        float waitTime = GetCurrentStateRemainingTime(hitStateName);
        yield return new WaitForSeconds(waitTime);

        PlayIdle();
        _returnRoutine = null;
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
}
