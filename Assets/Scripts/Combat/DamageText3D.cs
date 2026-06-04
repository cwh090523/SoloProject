using UnityEngine;

[RequireComponent(typeof(TextMesh))]
public class DamageText3D : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.65f;
    [SerializeField] private float riseSpeed = 0.75f;
    [SerializeField] private float sideDrift = 0.18f;
    [SerializeField] private float billboardScale = 1f;

    private TextMesh _textMesh;
    private Camera _camera;
    private Color _startColor;
    private float _age;
    private Vector3 _velocity;

    private void Awake()
    {
        _textMesh = GetComponent<TextMesh>();
        _camera = Camera.main;
        _startColor = _textMesh.color;
        _velocity = Vector3.up * riseSpeed + transform.right * Random.Range(-sideDrift, sideDrift);
    }

    private void Update()
    {
        _age += Time.deltaTime;
        transform.position += _velocity * Time.deltaTime;

        UpdateBillboardRotation();
        UpdateFade();

        if (_age >= lifetime)
            Destroy(gameObject);
    }

    public void Initialize(float damage, Camera targetCamera, Color color, float fontSize)
    {
        Initialize(damage, targetCamera, color, fontSize, billboardScale);
    }

    public void Initialize(float damage, Camera targetCamera, Color color, float fontSize, float sizeScale)
    {
        if (_textMesh == null)
            _textMesh = GetComponent<TextMesh>();

        _camera = targetCamera != null ? targetCamera : Camera.main;
        _startColor = color;
        _textMesh.text = Mathf.RoundToInt(damage).ToString();
        _textMesh.color = color;
        _textMesh.fontSize = Mathf.RoundToInt(fontSize);
        _textMesh.characterSize = 0.05f * Mathf.Max(0.01f, sizeScale);
        _textMesh.anchor = TextAnchor.MiddleCenter;
        _textMesh.alignment = TextAlignment.Center;
    }

    private void UpdateBillboardRotation()
    {
        if (_camera == null)
            return;

        Vector3 direction = transform.position - _camera.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void UpdateFade()
    {
        if (_textMesh == null || lifetime <= 0f)
            return;

        float ratio = Mathf.Clamp01(_age / lifetime);
        Color color = _startColor;
        color.a = 1f - ratio;
        _textMesh.color = color;
    }
}
