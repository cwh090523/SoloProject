using UnityEngine;

public class FirstPersonWeaponView : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private Vector3 localPosition = new Vector3(0.32f, -0.32f, 0.62f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool hideBodyForLocalView = true;

    [Header("Aim")]
    [SerializeField] private Vector3 aimLocalPosition = new Vector3(0f, -0.21f, 0.46f);
    [SerializeField] private Vector3 aimLocalEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] private float hipFov = 60f;
    [SerializeField] private float aimFov = 42f;
    [SerializeField] private float aimSmoothSpeed = 14f;

    [Header("Recoil")]
    [SerializeField] private Vector3 recoilPosition = new Vector3(0f, -0.025f, -0.12f);
    [SerializeField] private Vector3 recoilEulerAngles = new Vector3(-8f, 2f, 0f);
    [SerializeField] private float recoilSnappiness = 22f;
    [SerializeField] private float recoilReturnSpeed = 14f;

    private Vector3 _currentPositionOffset;
    private Vector3 _targetPositionOffset;
    private Vector3 _currentRotationOffset;
    private Vector3 _targetRotationOffset;
    private float _aimWeight;

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>();

        if (weapon == null)
            weapon = GetComponent<PlayerWeapon>();

        AttachWeaponToCamera();
        SetBodyVisible(!hideBodyForLocalView);
    }

    private void OnEnable()
    {
        if (weapon != null)
            weapon.Fired += AddRecoil;
    }

    private void OnDisable()
    {
        if (weapon != null)
            weapon.Fired -= AddRecoil;
    }

    private void LateUpdate()
    {
        UpdateAim();
        UpdateRecoil();
    }

    private void AttachWeaponToCamera()
    {
        if (viewCamera == null || weaponSocket == null)
            return;

        weaponSocket.SetParent(viewCamera.transform, false);
        weaponSocket.localPosition = localPosition;
        weaponSocket.localRotation = Quaternion.Euler(localEulerAngles);

        if (viewCamera != null)
            hipFov = viewCamera.fieldOfView;
    }

    private void AddRecoil()
    {
        _targetPositionOffset += recoilPosition;
        _targetRotationOffset += recoilEulerAngles;
    }

    private void UpdateRecoil()
    {
        if (weaponSocket == null)
            return;

        _targetPositionOffset = Vector3.Lerp(_targetPositionOffset, Vector3.zero, recoilReturnSpeed * Time.deltaTime);
        _targetRotationOffset = Vector3.Lerp(_targetRotationOffset, Vector3.zero, recoilReturnSpeed * Time.deltaTime);

        _currentPositionOffset = Vector3.Lerp(_currentPositionOffset, _targetPositionOffset, recoilSnappiness * Time.deltaTime);
        _currentRotationOffset = Vector3.Lerp(_currentRotationOffset, _targetRotationOffset, recoilSnappiness * Time.deltaTime);

        Vector3 basePosition = GetBaseWeaponPosition();
        Vector3 baseEulerAngles = GetBaseWeaponEulerAngles();
        weaponSocket.localPosition = basePosition + _currentPositionOffset;
        weaponSocket.localRotation = Quaternion.Euler(baseEulerAngles + _currentRotationOffset);
    }

    private void UpdateAim()
    {
        if (viewCamera == null)
            return;

        float targetAimWeight = weapon != null && weapon.IsAiming ? 1f : 0f;
        _aimWeight = Mathf.MoveTowards(_aimWeight, targetAimWeight, aimSmoothSpeed * Time.deltaTime);

        float targetFov = Mathf.Lerp(hipFov, aimFov, _aimWeight);
        viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, targetFov, aimSmoothSpeed * Time.deltaTime);
    }

    private Vector3 GetBaseWeaponPosition()
    {
        return Vector3.Lerp(localPosition, aimLocalPosition, _aimWeight);
    }

    private Vector3 GetBaseWeaponEulerAngles()
    {
        return Vector3.Lerp(localEulerAngles, aimLocalEulerAngles, _aimWeight);
    }

    private void SetBodyVisible(bool isVisible)
    {
        if (bodyRenderers == null)
            return;

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].enabled = isVisible;
        }
    }
}
