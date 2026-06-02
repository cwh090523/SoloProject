using UnityEngine;

public class FirstPersonWeaponView : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Renderer[] bodyRenderers;
    [SerializeField] private Vector3 localPosition = new Vector3(0.32f, -0.32f, 0.62f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool hideBodyForLocalView = true;

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = GetComponentInChildren<Camera>();

        AttachWeaponToCamera();
        SetBodyVisible(!hideBodyForLocalView);
    }

    private void AttachWeaponToCamera()
    {
        if (viewCamera == null || weaponSocket == null)
            return;

        weaponSocket.SetParent(viewCamera.transform, false);
        weaponSocket.localPosition = localPosition;
        weaponSocket.localRotation = Quaternion.Euler(localEulerAngles);
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
