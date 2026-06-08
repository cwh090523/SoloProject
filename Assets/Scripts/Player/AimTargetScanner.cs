using UnityEngine;

public class AimTargetScanner : MonoBehaviour
{
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float checkInterval = 0.05f;
    [SerializeField] private float checkRange = 100f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private bool drawDebugRay = true;

    private AimTargetHighlight _currentHighlight;
    private float _nextCheckTime;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (Time.time < _nextCheckTime)
            return;

        _nextCheckTime = Time.time + checkInterval;
        UpdateAimTarget();
    }

    private void OnDisable()
    {
        SetCurrentHighlight(null);
    }

    private void UpdateAimTarget()
    {
        AimTargetHighlight nextHighlight = FindAimTargetHighlight();
        SetCurrentHighlight(nextHighlight);
    }

    private AimTargetHighlight FindAimTargetHighlight()
    {
        if (aimCamera == null)
            return null;

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * checkRange, Color.cyan, checkInterval);

        if (!Physics.Raycast(ray, out RaycastHit hit, checkRange, targetLayers, QueryTriggerInteraction.Ignore))
            return null;

        Health health = hit.collider.GetComponentInParent<Health>();
        if (health == null || health.IsDead)
            return null;

        AimTargetHighlight highlight = health.GetComponent<AimTargetHighlight>();
        if (highlight == null)
            highlight = health.gameObject.AddComponent<AimTargetHighlight>();

        return highlight;
    }

    private void SetCurrentHighlight(AimTargetHighlight nextHighlight)
    {
        if (_currentHighlight == nextHighlight)
            return;

        if (_currentHighlight != null)
            _currentHighlight.SetHighlighted(false);

        _currentHighlight = nextHighlight;

        if (_currentHighlight != null)
            _currentHighlight.SetHighlighted(true);
    }
}
