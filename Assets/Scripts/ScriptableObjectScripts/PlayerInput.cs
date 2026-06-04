using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScriptableObjectScripts
{
    [CreateAssetMenu(fileName = "Player Input", menuName = "SO/PlayerInput", order = 0)]
    public class PlayerInput : ScriptableObject, Controls.IPlayerActions
    {
        public event Action<Vector2> OnMovementChange;
        public event Action OnAttackKeyPressed;
        public event Action<bool> OnAimKeyPressed;
        public event Action<bool> OnCrouchKeyPressed;
        public event Action<float> OnLeanChanged;
        public event Action OnJumpKeyPressed;
        public event Action<bool> OnSprintKeyPressed;
        
        public delegate void SkillKeyPress(int keyCode, bool isPressed);
        public event SkillKeyPress OnSkillKeyPressed;

        [SerializeField] private LayerMask whatIsGround;
        private Controls _controls;
        private bool _isLeaningRight;
        private bool _isLeaningLeft;
        

        private Camera _mainCam;

        public Camera MainCam
        {
            get
            {
                if(_mainCam == null)
                    _mainCam = Camera.main;
                return _mainCam;
            }
        }

        private void OnEnable()
        {
            if (_controls == null)
            {
                _controls = new Controls();
                _controls.Player.SetCallbacks(this);
            }
            _controls.Player.Enable();
        }

        private void OnDisable()
        {
            _controls.Player.Disable();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            Vector2 movement = context.ReadValue<Vector2>();
            OnMovementChange?.Invoke(movement);
        }

        public void OnAiming(InputAction.CallbackContext context)
        {
            if(context.started || context.performed)
                OnAimKeyPressed?.Invoke(true);
            else if(context.canceled)
                OnAimKeyPressed?.Invoke(false);
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnAttackKeyPressed?.Invoke();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if(context.started || context.performed)
                OnCrouchKeyPressed?.Invoke(true);
            else if(context.canceled)
                OnCrouchKeyPressed?.Invoke(false);
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnJumpKeyPressed?.Invoke();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSprintKeyPressed?.Invoke(true);
            else if(context.canceled)
                OnSprintKeyPressed?.Invoke(false);
        }

        public void OnTiltRight(InputAction.CallbackContext context)
        {
            if (context.started || context.performed)
                _isLeaningRight = true;
            else if (context.canceled)
                _isLeaningRight = false;

            RaiseLeanChanged();
        }

        public void OnTiltLeft(InputAction.CallbackContext context)
        {
            if (context.started || context.performed)
                _isLeaningLeft = true;
            else if (context.canceled)
                _isLeaningLeft = false;

            RaiseLeanChanged();
        }

        public void OnSkill(InputAction.CallbackContext context)
        {
            int keyCode = context.action.GetBindingIndexForControl(context.control);
            
            if(context.performed)
                OnSkillKeyPressed?.Invoke(keyCode, true);
            else if(context.canceled)
                OnSkillKeyPressed?.Invoke(keyCode, false);
        }

        private void RaiseLeanChanged()
        {
            float lean = 0f;

            if (_isLeaningRight && !_isLeaningLeft)
                lean = 1f;
            else if (_isLeaningLeft && !_isLeaningRight)
                lean = -1f;

            OnLeanChanged?.Invoke(lean);
        }

       
    }
}
