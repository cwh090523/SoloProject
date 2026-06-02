using UnityEngine;

public class PlayerVisualYawOffset : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform[] staticVisualRoots;
    [SerializeField] private Transform[] animatedVisualRoots;
    [SerializeField] private float yawOffset = 90f;
    [SerializeField] private string[] actionLookStateNames = { "Firing Rifle", "Reloading" };
    [SerializeField] private float actionLookBlendSpeed = 12f;

    private Quaternion[] _staticBaseRotations;
    private float _actionLookWeight;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>();

        CacheStaticBaseRotations();
    }

    private void LateUpdate()
    {
        Quaternion offset = Quaternion.Euler(0f, GetCurrentYawOffset(), 0f);

        ApplyStaticVisualOffset(offset);
        ApplyAnimatedVisualOffset(offset);
    }

    private float GetCurrentYawOffset()
    {
        float targetWeight = IsActionLookState() ? 1f : 0f;
        _actionLookWeight = Mathf.MoveTowards(_actionLookWeight, targetWeight, actionLookBlendSpeed * Time.deltaTime);

        if (_actionLookWeight <= 0f || aimCamera == null)
            return yawOffset;

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 localAimDirection = transform.InverseTransformDirection(aimRay.direction);
        localAimDirection.y = 0f;

        if (localAimDirection.sqrMagnitude < 0.0001f)
            return yawOffset;

        float rayYaw = Mathf.Atan2(localAimDirection.x, localAimDirection.z) * Mathf.Rad2Deg;
        float targetYaw = yawOffset + rayYaw;
        return Mathf.LerpAngle(yawOffset, targetYaw, _actionLookWeight);
    }

    private void CacheStaticBaseRotations()
    {
        if (staticVisualRoots == null)
            return;

        _staticBaseRotations = new Quaternion[staticVisualRoots.Length];
        for (int i = 0; i < staticVisualRoots.Length; i++)
        {
            _staticBaseRotations[i] = staticVisualRoots[i] != null
                ? staticVisualRoots[i].localRotation
                : Quaternion.identity;
        }
    }

    private void ApplyStaticVisualOffset(Quaternion offset)
    {
        if (staticVisualRoots == null || _staticBaseRotations == null)
            return;

        for (int i = 0; i < staticVisualRoots.Length; i++)
        {
            if (staticVisualRoots[i] == null)
                continue;

            staticVisualRoots[i].localRotation = _staticBaseRotations[i] * offset;
        }
    }

    private void ApplyAnimatedVisualOffset(Quaternion offset)
    {
        if (animatedVisualRoots == null)
            return;

        for (int i = 0; i < animatedVisualRoots.Length; i++)
        {
            if (animatedVisualRoots[i] == null)
                continue;

            animatedVisualRoots[i].localRotation *= offset;
        }
    }

    private bool IsActionLookState()
    {
        if (animator == null || actionLookStateNames == null || aimCamera == null)
            return false;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (MatchesAnyState(currentState))
            return true;

        if (!animator.IsInTransition(0))
            return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        return MatchesAnyState(nextState);
    }

    private bool MatchesAnyState(AnimatorStateInfo stateInfo)
    {
        for (int i = 0; i < actionLookStateNames.Length; i++)
        {
            string stateName = actionLookStateNames[i];
            if (!string.IsNullOrWhiteSpace(stateName) && stateInfo.IsName(stateName))
                return true;
        }

        return false;
    }
}
