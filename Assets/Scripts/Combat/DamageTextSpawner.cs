using UnityEngine;

public class DamageTextSpawner : MonoBehaviour
{
    private static DamageTextSpawner _instance;

    [SerializeField] private Color damageColor = new Color(1f, 0.85f, 0.15f, 1f);
    [SerializeField] private float spawnNormalOffset = 0.18f;
    [SerializeField] private float spawnUpOffset = 0.18f;
    [SerializeField] private float fontSize = 42f;
    [SerializeField] private float minTextScale = 1f;
    [SerializeField] private float maxTextScale = 5f;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    public static void ShowDamage(Vector3 hitPoint, Vector3 hitNormal, float damage, Camera targetCamera)
    {
        ShowDamage(hitPoint, hitNormal, damage, targetCamera, 0f, 0f);
    }

    public static void ShowDamage(Vector3 hitPoint, Vector3 hitNormal, float damage, Camera targetCamera, float rayDistance, float maxRayDistance)
    {
        if (_instance == null)
            CreateInstance();

        _instance.Spawn(hitPoint, hitNormal, damage, targetCamera, rayDistance, maxRayDistance);
    }

    private static void CreateInstance()
    {
        GameObject spawnerObject = new GameObject("Damage Text Spawner");
        _instance = spawnerObject.AddComponent<DamageTextSpawner>();
    }

    private void Spawn(Vector3 hitPoint, Vector3 hitNormal, float damage, Camera targetCamera, float rayDistance, float maxRayDistance)
    {
        Vector3 spawnPosition = hitPoint + hitNormal.normalized * spawnNormalOffset + Vector3.up * spawnUpOffset;
        GameObject textObject = new GameObject("Damage Text");
        textObject.transform.position = spawnPosition;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = Mathf.RoundToInt(damage).ToString();

        DamageText3D damageText = textObject.AddComponent<DamageText3D>();
        damageText.Initialize(damage, targetCamera, damageColor, fontSize, GetDistanceScale(rayDistance, maxRayDistance));
    }

    private float GetDistanceScale(float rayDistance, float maxRayDistance)
    {
        if (maxRayDistance <= 0f)
            return maxTextScale;

        float distanceRatio = Mathf.Clamp01(rayDistance / maxRayDistance);
        return Mathf.Lerp(minTextScale, maxTextScale, distanceRatio);
    }
}
