using System;
using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool startWithFullHealth = true;
    [SerializeField] private bool invulnerable;

    [Header("Death")]
    [SerializeField] private bool destroyOnDeath;
    [SerializeField] private float destroyDelay = 1.5f;
    [SerializeField] private bool autoResetAfterDeath;
    [SerializeField] private float resetDelay = 1.2f;

    private Coroutine _deathRoutine;

    public event Action<float, float> HealthChanged;
    public event Action<float> Damaged;
    public event Action<float> Healed;
    public event Action Died;
    public event Action ResetHealth;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float NormalizedHealth => maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
    public bool IsDead { get; private set; }
    public bool IsInvulnerable
    {
        get => invulnerable;
        set => invulnerable = value;
    }

    private void Awake()
    {
        maxHealth = Mathf.Max(1f, maxHealth);

        if (startWithFullHealth || currentHealth <= 0f)
            currentHealth = maxHealth;
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        IsDead = currentHealth <= 0f;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || invulnerable || damage <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Damaged?.Invoke(damage);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Healed?.Invoke(amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void IncreaseMaxHealth(float amount, bool healByIncreaseAmount)
    {
        if (amount <= 0f)
            return;

        maxHealth = Mathf.Max(1f, maxHealth + amount);

        if (healByIncreaseAmount && !IsDead)
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        else
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetDebugHealth(float newCurrentHealth, float newMaxHealth)
    {
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        maxHealth = Mathf.Max(1f, newMaxHealth);
        currentHealth = Mathf.Clamp(newCurrentHealth, 0f, maxHealth);
        IsDead = currentHealth <= 0f;

        if (!IsDead)
            ResetHealth?.Invoke();

        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void RestoreFullHealth()
    {
        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        currentHealth = maxHealth;
        IsDead = false;
        ResetHealth?.Invoke();
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Kill()
    {
        if (IsDead)
            return;

        currentHealth = 0f;
        HealthChanged?.Invoke(currentHealth, maxHealth);
        Die();
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        Died?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject, destroyDelay);
        else if (autoResetAfterDeath && _deathRoutine == null)
            _deathRoutine = StartCoroutine(ResetAfterDelayRoutine());
    }

    private IEnumerator ResetAfterDelayRoutine()
    {
        yield return new WaitForSeconds(resetDelay);
        RestoreFullHealth();
        _deathRoutine = null;
    }
}
