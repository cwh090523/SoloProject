using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Splines;

[RequireComponent(typeof(NavMeshLink))]
public class NavMeshLinkSplineHeight : MonoBehaviour
{
    public enum HeightMode
    {
        WorldY,
        OffsetFromLinearY
    }

    [SerializeField] private NavMeshLink navMeshLink;
    [SerializeField] private SplineContainer spline;
    [SerializeField] private int splineIndex;
    [SerializeField] private HeightMode heightMode = HeightMode.WorldY;
    [SerializeField] private float endpointMatchDistance = 1.5f;

    public float EndpointMatchDistance => Mathf.Max(0.05f, endpointMatchDistance);

    private void Reset()
    {
        navMeshLink = GetComponent<NavMeshLink>();
        spline = GetComponent<SplineContainer>();
    }

    private void Awake()
    {
        if (navMeshLink == null)
            navMeshLink = GetComponent<NavMeshLink>();

        if (spline == null)
            spline = GetComponent<SplineContainer>();
    }

    public bool TryGetLinkPositions(out Vector3 start, out Vector3 end)
    {
        if (navMeshLink == null)
            navMeshLink = GetComponent<NavMeshLink>();

        if (navMeshLink == null)
        {
            start = default;
            end = default;
            return false;
        }

        start = navMeshLink.startTransform != null
            ? navMeshLink.startTransform.position
            : navMeshLink.transform.TransformPoint(navMeshLink.startPoint);

        end = navMeshLink.endTransform != null
            ? navMeshLink.endTransform.position
            : navMeshLink.transform.TransformPoint(navMeshLink.endPoint);

        return true;
    }

    public bool TrySampleY(float normalizedTime, bool reverse, float linearY, out float y)
    {
        y = linearY;

        if (spline == null || spline.Splines == null || spline.Splines.Count == 0)
            return false;

        int index = Mathf.Clamp(splineIndex, 0, spline.Splines.Count - 1);
        float t = reverse ? 1f - normalizedTime : normalizedTime;
        Vector3 splinePosition = spline.EvaluatePosition(index, Mathf.Clamp01(t));

        y = heightMode == HeightMode.OffsetFromLinearY
            ? linearY + splinePosition.y
            : splinePosition.y;

        return true;
    }

    public static NavMeshLinkSplineHeight FindBest(Vector3 traversalStart, Vector3 traversalEnd, out bool reverse)
    {
        reverse = false;

        NavMeshLinkSplineHeight[] links = FindObjectsByType<NavMeshLinkSplineHeight>(FindObjectsSortMode.None);
        NavMeshLinkSplineHeight bestLink = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < links.Length; i++)
        {
            NavMeshLinkSplineHeight link = links[i];
            if (link == null || !link.isActiveAndEnabled || !link.TryGetLinkPositions(out Vector3 linkStart, out Vector3 linkEnd))
                continue;

            float forwardScore = (traversalStart - linkStart).sqrMagnitude + (traversalEnd - linkEnd).sqrMagnitude;
            float reverseScore = (traversalStart - linkEnd).sqrMagnitude + (traversalEnd - linkStart).sqrMagnitude;
            bool useReverse = reverseScore < forwardScore;
            float score = useReverse ? reverseScore : forwardScore;
            float maxScore = link.EndpointMatchDistance * link.EndpointMatchDistance * 2f;

            if (score > maxScore || score >= bestScore)
                continue;

            bestScore = score;
            bestLink = link;
            reverse = useReverse;
        }

        return bestLink;
    }
}
