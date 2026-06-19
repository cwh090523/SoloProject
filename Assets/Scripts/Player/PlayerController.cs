using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = ScriptableObjectScripts.PlayerInput;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")] [SerializeField] private PlayerInput playerInput;

    [Header("Movement")] [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float airControl = 0.45f;
    [SerializeField] private PlayerStamina stamina;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private float fireMoveSlowDuration = 0.25f;

    [Header("Jump")] [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float groundCheckRadius = 0.24f;
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Crouch")] [SerializeField] private Transform cameraRoot;
    [SerializeField] private float standingHeight = 1.8f;
    [SerializeField] private float crouchingHeight = 1.1f;
    [SerializeField] private float standingCameraHeight = 1.6f;
    [SerializeField] private float crouchingCameraHeight = 1.0f;
    [SerializeField] private float crouchSmoothSpeed = 10f;

    private Rigidbody _rigidbody;
    private CapsuleCollider _capsuleCollider;
    private Vector2 _moveInput;
    private bool _wantsToSprint;
    private bool _isSprinting;
    private bool _isGrounded;
    private bool _wantsToCrouch;
    private bool _isCrouching;
    private Vector3 _cameraRootBaseLocalPosition;
    private float _fireMoveSlowUntilTime;

    public Vector2 MoveInput => _moveInput;
    public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;
    public bool IsSprinting => _isSprinting;
    public bool IsCrouching => _isCrouching;
    public bool IsGrounded => _isGrounded;
    public float CurrentMoveSpeed => new Vector2(_rigidbody.linearVelocity.x, _rigidbody.linearVelocity.z).magnitude;
    public float VerticalVelocity => _rigidbody.linearVelocity.y;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _rigidbody.freezeRotation = true;

        if (cameraRoot == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();
            if (childCamera != null)
                cameraRoot = childCamera.transform;
        }

        if (cameraRoot != null)
            _cameraRootBaseLocalPosition = cameraRoot.localPosition;

        if (stamina == null)
            stamina = GetComponent<PlayerStamina>();

        if (playerWeapon == null)
            playerWeapon = GetComponent<PlayerWeapon>();
        if (playerWeapon == null)
            playerWeapon = GetComponentInChildren<PlayerWeapon>();
    }

    private void OnEnable()
    {
        if (playerWeapon != null)
            playerWeapon.Fired += HandleWeaponFired;

        if (playerInput == null)
            return;

        playerInput.OnMovementChange += HandleMovementChange;
        playerInput.OnJumpKeyPressed += HandleJump;
        playerInput.OnSprintKeyPressed += HandleSprint;
        playerInput.OnCrouchKeyPressed += HandleCrouch;
    }

    private void OnDisable()
    {
        if (playerWeapon != null)
            playerWeapon.Fired -= HandleWeaponFired;

        if (playerInput == null)
            return;

        playerInput.OnMovementChange -= HandleMovementChange;
        playerInput.OnJumpKeyPressed -= HandleJump;
        playerInput.OnSprintKeyPressed -= HandleSprint;
        playerInput.OnCrouchKeyPressed -= HandleCrouch;
    }

    private void Update()
    {
        UpdateCrouchState();
        UpdateSprintState();
        stamina?.Tick(_isSprinting, Time.deltaTime);
#if UNITY_EDITOR
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            stamina.RestoreFullStamina();
        }
#endif
    }

    private void FixedUpdate()
    {
        CheckGround();
        Move();
    }

    private void HandleMovementChange(Vector2 movement)
    {
        _moveInput = Vector2.ClampMagnitude(movement, 1f);
    }

    private void HandleSprint(bool isPressed)
    {
        _wantsToSprint = isPressed;
    }

    private void HandleCrouch(bool isPressed)
    {
        _wantsToCrouch = isPressed;
    }

    private void HandleWeaponFired()
    {
        _fireMoveSlowUntilTime = Time.time + Mathf.Max(0f, fireMoveSlowDuration);
    }

    private void HandleJump()
    {
        if (!_isGrounded)
            return;

        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        _rigidbody.linearVelocity = velocity;
        _rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        _isGrounded = false;
    }

    private void Move()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = forward * _moveInput.y + right * _moveInput.x;
        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        float targetSpeed = ShouldUseSlowMoveSpeed() ? crouchSpeed : (_isSprinting ? sprintSpeed : moveSpeed);
        Vector3 targetVelocity = moveDirection * targetSpeed;
        Vector3 currentVelocity = _rigidbody.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float control = _isGrounded ? acceleration : acceleration * airControl;
        Vector3 nextHorizontalVelocity =
            Vector3.MoveTowards(horizontalVelocity, targetVelocity, control * Time.fixedDeltaTime);

        _rigidbody.linearVelocity = new Vector3(nextHorizontalVelocity.x, currentVelocity.y, nextHorizontalVelocity.z);
    }

    private void CheckGround()
    {
        Vector3 center = transform.TransformPoint(_capsuleCollider.center);
        float castDistance = Mathf.Max((_capsuleCollider.height * 0.5f) - _capsuleCollider.radius, 0f);
        Vector3 sphereCenter = center + Vector3.down * castDistance;
        float checkDistance = groundCheckDistance + 0.05f;

        _isGrounded = Physics.SphereCast(
            sphereCenter + Vector3.up * 0.05f,
            groundCheckRadius,
            Vector3.down,
            out _,
            checkDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void UpdateCrouchState()
    {
        bool shouldCrouch = _wantsToCrouch;
        _isCrouching = shouldCrouch;

        float targetHeight = shouldCrouch ? crouchingHeight : standingHeight;
        float targetCameraHeight = shouldCrouch ? crouchingCameraHeight : standingCameraHeight;

        _capsuleCollider.height = Mathf.Lerp(_capsuleCollider.height, targetHeight, crouchSmoothSpeed * Time.deltaTime);
        _capsuleCollider.center = new Vector3(0f, _capsuleCollider.height * 0.5f, 0f);

        if (cameraRoot == null)
            return;

        Vector3 targetCameraPosition = _cameraRootBaseLocalPosition;
        targetCameraPosition.y = targetCameraHeight;
        cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, targetCameraPosition,
            crouchSmoothSpeed * Time.deltaTime);
    }

    private void UpdateSprintState()
    {
        bool hasMoveInput = _moveInput.sqrMagnitude > 0.01f;
        bool canUseStamina = stamina == null || stamina.CanSprint;
        _isSprinting = _wantsToSprint && hasMoveInput && !ShouldUseSlowMoveSpeed() && canUseStamina;
    }

    private bool ShouldUseSlowMoveSpeed()
    {
        if (_isCrouching)
            return true;

        if (playerWeapon != null && playerWeapon.IsAiming)
            return true;

        return Time.time < _fireMoveSlowUntilTime;
    }
}
