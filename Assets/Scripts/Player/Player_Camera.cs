using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;

public class PlayerCamera : MonoBehaviour
{
    private Rigidbody _rb;
    
    [Header("Rotate")] 
    public float mouseSpeed;
    [SerializeField]private float _yRotate;
    [SerializeField]private float _xRotate;
    [SerializeField]private Camera cam;
    [SerializeField] private float recoilReturnSpeed = 12f;
    [SerializeField] private float recoilSnappiness = 20f;

    private Vector2 _targetRecoil;
    private Vector2 _currentRecoil;

    public Camera Camera => cam;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
    }

    private void Update()
    {
        Rotate();
    }

    private void Rotate()
    {
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

        transform.rotation = Quaternion.Euler(0, _yRotate, 0);
        cam.transform.localRotation = Quaternion.Euler(_xRotate - _currentRecoil.x, _currentRecoil.y, 0);
    }

    public void AddRecoil(float vertical, float horizontal)
    {
        _targetRecoil += new Vector2(vertical, horizontal);
    }
}
