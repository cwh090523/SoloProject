using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DummyTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float resetDelay = 1.2f;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color idleColor = new Color(0.75f, 0.65f, 0.45f);
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color deadColor = Color.black;
    [SerializeField] private float headshotHeightRatio = 0.72f;
    [SerializeField] private float headshotMultiplier = 2f;

    private float _lastHitTime;
    private Material _runtimeMaterial;
    private DamageHitReaction _hitReaction;
    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();
        if (_health == null)
            _health = gameObject.AddComponent<Health>();

        _hitReaction = GetComponent<DamageHitReaction>();
        if (_hitReaction == null && GetComponentInChildren<Animator>() != null)
            _hitReaction = gameObject.AddComponent<DamageHitReaction>();

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
            _runtimeMaterial = targetRenderer.material;

        _health.HealthChanged += HandleHealthChanged;
        _health.Damaged += HandleDamaged;
        _health.Died += HandleDied;
        _health.ResetHealth += HandleResetHealth;
        _health.RestoreFullHealth();
    }

    private void OnDestroy()
    {
        if (_health == null)
            return;

        _health.HealthChanged -= HandleHealthChanged;
        _health.Damaged -= HandleDamaged;
        _health.Died -= HandleDied;
        _health.ResetHealth -= HandleResetHealth;
    }

    private void Update()
    {
        if (_health != null && _health.IsDead)
        {
            if (Time.time - _lastHitTime >= resetDelay)
                _health.RestoreFullHealth();

            return;
        }

        if (_runtimeMaterial != null && Time.time - _lastHitTime > 0.12f)
            _runtimeMaterial.color = idleColor;
    }

    public void TakeDamage(float damage)
    {
        if (_health == null)
            return;

        _health.TakeDamage(damage);
    }

    public float GetDamageMultiplier(Collider hitCollider, Vector3 hitPoint, out bool isHeadshot)
    {
        Hitbox hitbox = hitCollider != null ? hitCollider.GetComponent<Hitbox>() : null;
        if (hitbox != null)
        {
            isHeadshot = hitbox.IsHeadshot;
            return hitbox.DamageMultiplier;
        }

        isHeadshot = IsPointInHeadArea(hitPoint);
        return isHeadshot ? headshotMultiplier : 1f;
    }

    private bool IsPointInHeadArea(Vector3 hitPoint)
    {
        Collider targetCollider = GetComponent<Collider>();
        if (targetCollider == null)
            return false;

        Bounds bounds = targetCollider.bounds;
        if (bounds.size.y <= 0f)
            return false;

        float heightRatio = Mathf.InverseLerp(bounds.min.y, bounds.max.y, hitPoint.y);
        return heightRatio >= headshotHeightRatio;
    }

    private void HandleHealthChanged(float currentHealth, float healthMax)
    {
        maxHealth = healthMax;
    }

    private void HandleDamaged(float damage)
    {
        _lastHitTime = Time.time;
    }

    private void HandleDied()
    {
        _lastHitTime = Time.time;
        SetColor(deadColor);
    }

    private void HandleResetHealth()
    {
        SetColor(idleColor);
    }

    private void SetColor(Color color)
    {
        if (_runtimeMaterial != null)
            _runtimeMaterial.color = color;
    }
}
