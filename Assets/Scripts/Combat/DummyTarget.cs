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

    private float _currentHealth;
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

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        _lastHitTime = Time.time;

        if (_currentHealth <= 0f)
        {
            _isDead = true;
            SetColor(deadColor);
            return;
        }

        SetColor(hitColor);
    }

    private void ResetTarget()
    {
        _currentHealth = maxHealth;
        _isDead = false;
        SetColor(idleColor);
    }

    private void SetColor(Color color)
    {
        if (_runtimeMaterial != null)
            _runtimeMaterial.color = color;
    }
}
