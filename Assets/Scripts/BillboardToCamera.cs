using UnityEngine;

public class BillboardToCamera : MonoBehaviour
{
        [SerializeField] private Camera cam;

        private void LateUpdate()
        {
                if (cam == null)
                        cam = Camera.main;
                if (cam == null)
                        return;

                Vector3 directionToCamera = cam.transform.position - transform.position;
                directionToCamera.y = 0f;

                if (directionToCamera.sqrMagnitude <= 0.001f)
                        return;

                transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
}
