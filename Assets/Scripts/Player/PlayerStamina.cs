using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [Header("Stamina")]
    [SerializeField] private float maxStamina = 400f;
    [SerializeField] private float currentStamina;
    [SerializeField] private bool startWithFullStamina = true;

    [Header("Sprint Cost")]
    [SerializeField] private float sprintDrainPerSecond = 55f;
    [SerializeField] private float recoveryPerSecond = 45f;
    [SerializeField] private float recoveryDelay = 0.7f;
    [SerializeField, Range(0f, 1f)] private float exhaustedResumeRatio = 0.25f;

    private float _recoverTimer;
    private bool _isExhausted;

    public event Action<float, float> StaminaChanged;

    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public float NormalizedStamina => maxStamina <= 0f ? 0f : Mathf.Clamp01(currentStamina / maxStamina);
    public bool CanSprint => !_isExhausted && currentStamina > 0f;

    private void Awake()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = startWithFullStamina || currentStamina <= 0f
            ? maxStamina
            : Mathf.Clamp(currentStamina, 0f, maxStamina);

        _isExhausted = currentStamina <= 0f;
        StaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    public void Tick(bool isSprinting, float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        if (isSprinting && CanSprint)
        {
            _recoverTimer = recoveryDelay;
            SetStamina(currentStamina - sprintDrainPerSecond * deltaTime);

            if (currentStamina <= 0f)
                _isExhausted = true;

            return;
        }

        if (_recoverTimer > 0f)
        {
            _recoverTimer -= deltaTime;
            return;
        }

        if (currentStamina >= maxStamina)
            return;

        SetStamina(currentStamina + recoveryPerSecond * deltaTime);

        if (_isExhausted && NormalizedStamina >= exhaustedResumeRatio)
            _isExhausted = false;
    }

    public void RestoreFullStamina()
    {
        _recoverTimer = 0f;
        _isExhausted = false;
        SetStamina(maxStamina);
    }

    private void SetStamina(float value)
    {
        float nextStamina = Mathf.Clamp(value, 0f, maxStamina);
        if (Mathf.Approximately(currentStamina, nextStamina))
            return;

        currentStamina = nextStamina;
        StaminaChanged?.Invoke(currentStamina, maxStamina);
    }
}
