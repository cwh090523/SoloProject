using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public class PlayerHitFeedbackPresenter : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private Health playerHealth;
    [SerializeField] private string playerObjectName = "Player3";
    [SerializeField] private string containerName = "PlayerHitContainer";
    [Header("ShakeCamera")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeStrength = 0.08f;
    [Header("Hit Flash")]
    [SerializeField, Range(0f, 1f)] private float hitOpacity = 0.85f;
    [SerializeField] private float fadeOutSpeed = 2.8f;

    [Header("Low Health")]
    [SerializeField] private bool keepVisibleAtLowHealth = true;
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 1f / 3f;
    [SerializeField, Range(0f, 1f)] private float lowHealthMaxOpacity = 0.22f;

    [Header("Post Processing Vignette")]
    [SerializeField] private Volume postProcessVolume;
    [SerializeField, Range(0f, 1f)] private float maxVignetteIntensity = 0.4f;

    private VisualElement _container;
    private Vignette _vignette;
    private float _currentOpacity;
    private float _currentVignetteIntensity;
    private float _defaultVignetteIntensity;
    private bool _hasVignette;
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        ResolveReferences();
        ResolveVignette();
        BindElements();
        ApplyOpacity(0f);
        ApplyVignetteIntensity(_defaultVignetteIntensity);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveVignette();
        BindElements();

        if (playerHealth != null)
            playerHealth.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.Damaged -= HandleDamaged;
    }

    private void Update()
    {
        float targetOpacity = GetLowHealthOpacity();
        _currentOpacity = Mathf.MoveTowards(_currentOpacity, targetOpacity, fadeOutSpeed * Time.deltaTime);
        ApplyOpacity(_currentOpacity);

        float targetVignetteIntensity = GetLowHealthVignetteIntensity();
        _currentVignetteIntensity = Mathf.MoveTowards(_currentVignetteIntensity, targetVignetteIntensity, fadeOutSpeed * Time.deltaTime);
        ApplyVignetteIntensity(_currentVignetteIntensity);
    }

    private void ResolveReferences()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (playerHealth != null)
            return;

        GameObject player = GameObject.Find(playerObjectName);
        if (player != null)
            playerHealth = player.GetComponent<Health>();

        if (playerHealth == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
                playerHealth = playerController.GetComponent<Health>();
        }
    }

    private void ResolveVignette()
    {
        if (_hasVignette)
            return;

        if (postProcessVolume == null)
            postProcessVolume = FindFirstObjectByType<Volume>();

        if (postProcessVolume == null || postProcessVolume.profile == null)
            return;

        _hasVignette = postProcessVolume.profile.TryGet(out _vignette);
        if (!_hasVignette || _vignette == null)
            return;

        _defaultVignetteIntensity = _vignette.intensity.value;
        _vignette.intensity.overrideState = true;
    }

    private void BindElements()
    {
        if (document == null || document.rootVisualElement == null)
            return;

        _container = document.rootVisualElement.Q<VisualElement>(containerName);
    }

    private void HandleDamaged(float damage)
    {
        if (damage <= 0f)
            return;

        _currentOpacity = hitOpacity;
        _currentVignetteIntensity = maxVignetteIntensity;
        ApplyOpacity(_currentOpacity);
        ApplyVignetteIntensity(_currentVignetteIntensity);
        if(_shakeRoutine != null) StopCoroutine(_shakeRoutine);

        _shakeRoutine = StartCoroutine(ShakeCameraCoroutine());
    }

    private IEnumerator ShakeCameraCoroutine()
    {
       if(cameraTransform == null) yield break;
       
       Vector3 baseLocalPosition = cameraTransform.localPosition;
       float elapsed = 0f;

       while (elapsed < shakeDuration)
       {
           elapsed += Time.deltaTime;
           
           float fade = 1f - Mathf.Clamp01(elapsed / shakeDuration);
           Vector2 randomOffset = Random.insideUnitCircle * shakeStrength * fade;
           
           cameraTransform.localPosition = baseLocalPosition + new Vector3(randomOffset.x,  randomOffset.y, 0);
           
           yield return null;
       }
       cameraTransform.localPosition = baseLocalPosition;
       _shakeRoutine = null;
    }

    private float GetLowHealthOpacity()
    {
        if (!keepVisibleAtLowHealth || playerHealth == null || playerHealth.IsDead)
            return 0f;

        return lowHealthMaxOpacity * GetLowHealthWeight();
    }

    private void ApplyOpacity(float opacity)
    {
        if (_container != null)
            _container.style.opacity = Mathf.Clamp01(opacity);
    }

    private float GetLowHealthVignetteIntensity()
    {
        float targetIntensity = Mathf.Lerp(_defaultVignetteIntensity, maxVignetteIntensity, GetLowHealthWeight());
        return Mathf.Min(targetIntensity, maxVignetteIntensity);
    }

    private float GetLowHealthWeight()
    {
        if (!keepVisibleAtLowHealth || playerHealth == null || playerHealth.IsDead)
            return 0f;

        float normalizedHealth = playerHealth.NormalizedHealth;
        if (normalizedHealth <= lowHealthThreshold)
            return 1f;

        return Mathf.InverseLerp(1f, lowHealthThreshold, normalizedHealth);
    }

    private void ApplyVignetteIntensity(float intensity)
    {
        if (_hasVignette && _vignette != null)
            _vignette.intensity.value = Mathf.Clamp(intensity, 0f, maxVignetteIntensity);
    }
}
