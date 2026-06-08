using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");

    [SerializeField] private Animator animator;
    [SerializeField] private float dampTime = 0.1f;
    [SerializeField] private string idleStateName = "IDLE";
    [SerializeField] private string walkStateName = "WALK";
    [SerializeField] private string runStateName = "RUN";
    [SerializeField] private string jumpStateName = "";
    [SerializeField] private string aimStateName = "AIMMING";
    [SerializeField] private bool keepAimStateWhileAiming = true;
    [SerializeField] private float crossFadeDuration = 0.12f;

    private PlayerController _playerController;
    private int _currentStateHash;
    private float _actionLockedUntil;
    private bool _isAiming;

    private void Awake()
    {
        _playerController = GetComponentInParent<PlayerController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
    }

    private void Update()
    {
        if (animator == null || _playerController == null)
            return;

        Vector2 moveInput = _playerController.MoveInput;
        float deltaTime = Time.deltaTime;

        animator.SetFloat(MoveXHash, moveInput.x, dampTime, deltaTime);
        animator.SetFloat(MoveYHash, moveInput.y, dampTime, deltaTime);
        animator.SetFloat(SpeedHash, _playerController.CurrentMoveSpeed, dampTime, deltaTime);
        animator.SetBool(IsMovingHash, _playerController.IsMoving);
        animator.SetBool(IsSprintingHash, _playerController.IsSprinting);
        animator.SetBool(IsGroundedHash, _playerController.IsGrounded);
        animator.SetFloat(VerticalVelocityHash, _playerController.VerticalVelocity, dampTime, deltaTime);

        if (Time.time < _actionLockedUntil)
            return;

        UpdateState();
    }

    public void PlayActionState(string stateName, float duration)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        CrossFadeTo(stateName);
        _actionLockedUntil = Time.time + duration;
    }

    public void SetAiming(bool isAiming)
    {
        if (_isAiming == isAiming)
            return;

        _isAiming = isAiming;
        _currentStateHash = 0;

        if (_isAiming && keepAimStateWhileAiming)
            CrossFadeTo(aimStateName);
    }

    private void UpdateState()
    {
        if (_isAiming && keepAimStateWhileAiming && HasState(aimStateName))
        {
            CrossFadeTo(aimStateName);
            return;
        }

        if (!_playerController.IsGrounded && !string.IsNullOrWhiteSpace(jumpStateName))
        {
            CrossFadeTo(jumpStateName);
            return;
        }

        CrossFadeTo(_playerController.IsMoving ? GetMoveStateName() : idleStateName);
    }

    private void CrossFadeTo(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (_currentStateHash == stateHash)
            return;

        if (!HasState(stateName))
            return;

        animator.CrossFadeInFixedTime(stateName, crossFadeDuration);
        _currentStateHash = stateHash;
    }

    private bool HasState(string stateName)
    {
        return !string.IsNullOrWhiteSpace(stateName) && animator.HasState(0, Animator.StringToHash(stateName));
    }

    private string GetMoveStateName()
    {
        return _playerController.IsSprinting ? runStateName : walkStateName;
    }
}
