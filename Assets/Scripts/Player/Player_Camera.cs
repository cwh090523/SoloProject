using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

public class PlayerCamera : MonoBehaviour
{
    private Rigidbody _rb;
    
    [Header("Rotate")] 
    public float mouseSpeed;
    [SerializeField]private float _yRotate;
    [SerializeField]private float _xRotate;
    [SerializeField]private Camera cam;

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

        _xRotate = Mathf.Clamp(_xRotate, -90f, 90f);

        transform.rotation = Quaternion.Euler(0, _yRotate, 0);
        cam.transform.localRotation = Quaternion.Euler(_xRotate, 0, 0);
    }
}
