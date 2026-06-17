using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float radius = 0.08f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private bool destroyOnHit = true;

    private Transform _owner;
    private Vector3 _velocity;
    private float _damage;
    private float _lifeTimer;
    private bool _launched;

    private void Awake()
    {
        Collider hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
    }

    private void Update()
    {
        if (!_launched)
            return;

        float step = Time.deltaTime;
        Vector3 start = transform.position;
        Vector3 movement = _velocity * step;

        if (movement.sqrMagnitude > 0f && Physics.SphereCast(start, radius, movement.normalized, out RaycastHit hit,
                movement.magnitude, hitLayers, QueryTriggerInteraction.Ignore))
        {
            if (!IsOwner(hit.transform))
            {
                ApplyHit(hit);
                return;
            }
        }

        transform.position = start + movement;

        if (_velocity.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(_velocity.normalized);

        _lifeTimer -= step;
        if (_lifeTimer <= 0f)
            Destroy(gameObject);
    }

    public void Launch(Transform owner, Vector3 velocity, float damage, float lifeTime, LayerMask targetLayers)
    {
        _owner = owner;
        _velocity = velocity;
        _damage = damage;
        _lifeTimer = Mathf.Max(0.1f, lifeTime);
        hitLayers = targetLayers;
        _launched = true;
    }

    private void ApplyHit(RaycastHit hit)
    {
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        damageable?.TakeDamage(_damage);

        if (destroyOnHit)
            Destroy(gameObject);
    }

    private bool IsOwner(Transform hitTransform)
    {
        return _owner != null && (hitTransform == _owner || hitTransform.IsChildOf(_owner));
    }
}
