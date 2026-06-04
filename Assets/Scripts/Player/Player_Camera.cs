using UnityEngine;
using UnityEngine.InputSystem;
using ScriptableObjectScripts;
using Cursor = UnityEngine.Cursor;
using PlayerInput = ScriptableObjectScripts.PlayerInput;

public class PlayerCamera : MonoBehaviour
{
    private Rigidbody _rb;

    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;
    
    [Header("Rotate")] 
    public float mouseSpeed;
    [SerializeField]private float _yRotate;
    [SerializeField]private float _xRotate;
    [SerializeField] private float cameraYawOffset;
    [SerializeField]private Camera cam;
    [SerializeField] private float recoilReturnSpeed = 12f;
    [SerializeField] private float recoilSnappiness = 20f;

    [Header("Lean")]
    [SerializeField] private float leanOffset = 0.25f;
    [SerializeField] private float leanAngle = 12f;
    [SerializeField] private float leanSmoothSpeed = 10f;

    private Vector2 _targetRecoil;
    private Vector2 _currentRecoil;
    private float _targetLean;
    private float _currentLean;
    private Vector3 _cameraBaseLocalPosition;

    public Camera Camera => cam;

    private void Awake()
    {
        if (playerInput == null)
        {
            PlayerInput[] inputs = Resources.FindObjectsOfTypeAll<PlayerInput>();
            playerInput = inputs.Length > 0 ? inputs[0] : null;
        }

        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        if (cam != null)
            _cameraBaseLocalPosition = cam.transform.localPosition;
    }

    private void OnEnable()
    {
        if (playerInput != null)
            playerInput.OnLeanChanged += HandleLeanChanged;
    }

    private void OnDisable()
    {
        if (playerInput != null)
            playerInput.OnLeanChanged -= HandleLeanChanged;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
            _rb.freezeRotation = true;
    }

    private void Update()
    {
        Rotate();
    }

    private void Rotate()
    {
        if (cam == null || Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        mouseDelta.x = Mathf.Clamp(mouseDelta.x, -20f, 20f);
        mouseDelta.y = Mathf.Clamp(mouseDelta.y, -20f, 20f);

        float mouseX = mouseDelta.x * mouseSpeed;
        float mouseY = mouseDelta.y * mouseSpeed;

        _yRotate += mouseX;
        _xRotate -= mouseY;

        _xRotate = Mathf.Clamp(_xRotate, -90f, 75f);

        _targetRecoil = Vector2.Lerp(_targetRecoil, Vector2.zero, recoilReturnSpeed * Time.deltaTime);
        _currentRecoil = Vector2.Lerp(_currentRecoil, _targetRecoil, recoilSnappiness * Time.deltaTime);
        _currentLean = Mathf.Lerp(_currentLean, _targetLean, leanSmoothSpeed * Time.deltaTime);

        transform.rotation = Quaternion.Euler(0, _yRotate, 0);
        cam.transform.localRotation = Quaternion.Euler(
            _xRotate - _currentRecoil.x,
            cameraYawOffset + _currentRecoil.y,
            -_currentLean * leanAngle);

        UpdateLeanPosition();
    }

    public void AddRecoil(float vertical, float horizontal)
    {
        _targetRecoil += new Vector2(vertical, horizontal);
    }

    private void HandleLeanChanged(float lean)
    {
        _targetLean = Mathf.Clamp(lean, -1f, 1f);
    }

    private void UpdateLeanPosition()
    {
        Vector3 localPosition = cam.transform.localPosition;
        localPosition.x = _cameraBaseLocalPosition.x + _currentLean * leanOffset;
        localPosition.z = _cameraBaseLocalPosition.z;
        cam.transform.localPosition = localPosition;
    }
}
