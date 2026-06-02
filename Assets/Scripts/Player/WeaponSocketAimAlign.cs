using UnityEngine;

public class WeaponSocketAimAlign : MonoBehaviour
{
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Vector3 localEulerOffset = new Vector3(0f, 0f, 90f);
    [SerializeField] private float alignSpeed = 25f;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>();
    }

    private void LateUpdate()
    {
        if (aimCamera == null || weaponSocket == null)
            return;

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Quaternion targetRotation = Quaternion.LookRotation(aimRay.direction, transform.up) * Quaternion.Euler(localEulerOffset);

        weaponSocket.rotation = Quaternion.Slerp(
            weaponSocket.rotation,
            targetRotation,
            alignSpeed * Time.deltaTime);
    }
}
