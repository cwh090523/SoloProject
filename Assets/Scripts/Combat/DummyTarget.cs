using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DummyTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField]private float currentHealth;
    [SerializeField] private float resetDelay = 1.2f;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color idleColor = new Color(0.75f, 0.65f, 0.45f);
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private Color deadColor = Color.black;
    [SerializeField] private float headshotHeightRatio = 0.72f;
    [SerializeField] private float headshotMultiplier = 2f;

    private float _lastHitTime;
    private bool _isDead;
    private Material _runtimeMaterial;

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
            _runtimeMaterial = targetRenderer.material;

        ResetTarget();
    }

    private void Update()
    {
        if (_isDead)
        {
            if (Time.time - _lastHitTime >= resetDelay)
                ResetTarget();

            return;
        }

        if (_runtimeMaterial != null && Time.time - _lastHitTime > 0.12f)
            _runtimeMaterial.color = idleColor;
    }

    public void TakeDamage(float damage)
    {
        if (_isDead)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        _lastHitTime = Time.time;

        if (currentHealth <= 0f)
        {
            _isDead = true;
            SetColor(deadColor);
            return;
        }

        SetColor(hitColor);
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

    private void ResetTarget()
    {
        currentHealth = maxHealth;
        _isDead = false;
        SetColor(idleColor);
    }

    private void SetColor(Color color)
    {
        if (_runtimeMaterial != null)
            _runtimeMaterial.color = color;
    }
}
