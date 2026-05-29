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
        public event Action OnCrouchKeyPressed;
        
        public delegate void SkillKeyPress(int keyCode, bool isPressed);
        public event SkillKeyPress OnSkillKeyPressed;

        [SerializeField] private LayerMask whatIsGround;
        private Controls _controls;
        

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
            throw new NotImplementedException();
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnAttackKeyPressed?.Invoke();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            throw new NotImplementedException();
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnCrouchKeyPressed?.Invoke();
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            throw new NotImplementedException();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            throw new NotImplementedException();
        }
        
        public void OnSkill(InputAction.CallbackContext context)
        {
            int keyCode = context.action.GetBindingIndexForControl(context.control);
            
            if(context.performed)
                OnSkillKeyPressed?.Invoke(keyCode, true);
            else if(context.canceled)
                OnSkillKeyPressed?.Invoke(keyCode, false);
        }

       
    }
}