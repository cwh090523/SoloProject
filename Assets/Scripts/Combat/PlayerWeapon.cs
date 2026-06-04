using System;
using System.Collections;
using System.Collections.Generic;
using ScriptableObjectScripts;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = ScriptableObjectScripts.PlayerInput;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private PlayerInput playerInput;

    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private AudioSource audioSource;

    [Header("Weapon")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireRate = 9f;
    [SerializeField] private float autoFireHoldDelay = 0.3f;
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Accuracy")]
    [SerializeField] private float spreadIncreasePerShot = 0.45f;
    [SerializeField] private float maxSpread = 2.25f;
    [SerializeField] private float spreadRecoverySpeed = 1.8f;
    [SerializeField] private float spreadAnglePerPoint = 1.15f;
    [SerializeField] private float aimSpreadMultiplier = 0.02f;
    [SerializeField] private float aimSpreadIncreasePerShot = 0f;
    [SerializeField] private float aimSpreadRecoverySpeed = 8f;

    [Header("Ammo")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private int reserveAmmo = 90;
    [SerializeField] private float reloadTime = 1.7f;

    [Header("Feedback")]
    [SerializeField] private float recoilVertical = 1.4f;
    [SerializeField] private float recoilHorizontal = 0.35f;
    [SerializeField] private string fireStateName = "Firing Rifle";
    [SerializeField] private string reloadStateName = "Reloading";
    [SerializeField] private float fireAnimationDuration = 0.12f;
    [SerializeField] private bool drawDebugRay = true;
    // [SerializeField] private Light muzzleFlashLight;
    [SerializeField] private ParticleSystem[] muzzleParticles;
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private int maxBulletHoles = 15;
    [SerializeField] private float hitParticleLifetime = 2f;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip reloadClip;

    private static readonly Queue<GameObject> BulletHolePool = new Queue<GameObject>();

    private int _currentAmmo;
    private float _nextFireTime;
    private bool _isReloading;
    private Coroutine _muzzleFlashRoutine;
    private float _currentSpread;
    private float _attackHeldTime;
    private bool _isAiming;

    public event Action Fired;
    public event Action ReloadStarted;
    public event Action ReloadFinished;
    public event Action HitConfirmed;
    public event Action AmmoChanged;

    public int CurrentAmmo => _currentAmmo;
    public int MagazineSize => magazineSize;
    public int ReserveAmmo => reserveAmmo;
    public bool IsReloading => _isReloading;
    public bool IsAiming => _isAiming;
    public float CurrentSpread => _currentSpread;
    public float MaxSpread => maxSpread;
    public float SpreadRatio => maxSpread <= 0f ? 0f : Mathf.Clamp01(GetEffectiveSpread() / maxSpread);

    private void Awake()
    {
        _currentAmmo = magazineSize;
        ResolveReferences();
        // EnsureFeedbackObjects();
        AmmoChanged?.Invoke();
    }

    private void OnEnable()
    {
        if (playerInput != null)
        {
            playerInput.OnAttackKeyPressed += TryFire;
            playerInput.OnAimKeyPressed += HandleAim;
        }
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            playerInput.OnAttackKeyPressed -= TryFire;
            playerInput.OnAimKeyPressed -= HandleAim;
        }
    }

    private void Update()
    {
        RecoverSpread();
        UpdateAutoFire();

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            TryReload();
    }

    private void UpdateAutoFire()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
        {
            _attackHeldTime = 0f;
            return;
        }

        _attackHeldTime += Time.deltaTime;
        if (_attackHeldTime < autoFireHoldDelay)
            return;

        TryFire();
    }

    private void TryFire()
    {
        if (_isReloading || Time.time < _nextFireTime)
            return;

        if (_currentAmmo <= 0)
        {
            TryReload();
            return;
        }

        _nextFireTime = Time.time + 1f / fireRate;
        _currentAmmo--;
        AmmoChanged?.Invoke();
        IncreaseSpread();

        Fired?.Invoke();
        PlayAction(fireStateName, fireAnimationDuration);
        ApplyRecoil();
        PlayFireFeedback();
        FireRaycast();
    }

    private void FireRaycast()
    {
        if (aimCamera == null)
            return;

        Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        ray.direction = GetSpreadDirection(ray.direction);
        if (drawDebugRay)
            Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 0.25f);

        if (!Physics.Raycast(ray, out RaycastHit hit, range, hitLayers, QueryTriggerInteraction.Ignore))
            return;

        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        bool isHeadshot;
        float finalDamage = GetFinalDamage(hit, out isHeadshot);

        bool canTakeDamage = damageable != null;
        SpawnHitEffect(hit.point, hit.normal, isHeadshot, !canTakeDamage);
        HitConfirmed?.Invoke();

        if (!canTakeDamage)
            return;

        damageable.TakeDamage(finalDamage);
        PlayDamageHitReaction(hit, isHeadshot);
        DamageTextSpawner.ShowDamage(hit.point, hit.normal, finalDamage, aimCamera, hit.distance, range);

        if (isHeadshot)
            Debug.Log($"Headshot! Damage: {finalDamage}");
    }

    private void TryReload()
    {
        if (_isReloading || _currentAmmo >= magazineSize || reserveAmmo <= 0)
            return;

        StartCoroutine(ReloadRoutine());
    }

    private float GetFinalDamage(RaycastHit hit, out bool isHeadshot)
    {
        isHeadshot = false;

        Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
        if (hitbox != null)
        {
            isHeadshot = hitbox.IsHeadshot;
            return damage * hitbox.DamageMultiplier;
        }

        DummyTarget dummyTarget = hit.collider.GetComponentInParent<DummyTarget>();
        if (dummyTarget != null)
            return damage * dummyTarget.GetDamageMultiplier(hit.collider, hit.point, out isHeadshot);

        return damage;
    }

    private void PlayDamageHitReaction(RaycastHit hit, bool isHeadshot)
    {
        DamageHitReaction hitReaction = hit.collider.GetComponentInParent<DamageHitReaction>();
        if (hitReaction != null)
        {
            hitReaction.PlayHitReaction(isHeadshot);
            return;
        }

        Animator targetAnimator = hit.collider.GetComponentInParent<Animator>();
        if (targetAnimator == null)
            return;

        DamageHitReaction addedHitReaction = targetAnimator.GetComponent<DamageHitReaction>();
        if (addedHitReaction == null)
            addedHitReaction = targetAnimator.gameObject.AddComponent<DamageHitReaction>();

        addedHitReaction.PlayHitReaction(isHeadshot);
    }

    private IEnumerator ReloadRoutine()
    {
        _isReloading = true;
        ReloadStarted?.Invoke();
        PlayAction(reloadStateName, reloadTime);
        PlayReloadFeedback();

        yield return new WaitForSeconds(reloadTime);

        int neededAmmo = magazineSize - _currentAmmo;
        int loadedAmmo = Mathf.Min(neededAmmo, reserveAmmo);
        _currentAmmo += loadedAmmo;
        reserveAmmo -= loadedAmmo;
        _isReloading = false;

        AmmoChanged?.Invoke();
        ReloadFinished?.Invoke();
    }

    private void ResolveReferences()
    {
        if (playerInput == null)
        {
            PlayerInput[] inputs = Resources.FindObjectsOfTypeAll<PlayerInput>();
            playerInput = inputs.Length > 0 ? inputs[0] : null;
        }

        if (playerCamera == null)
            playerCamera = GetComponent<PlayerCamera>();

        if (aimCamera == null)
            aimCamera = playerCamera != null ? playerCamera.Camera : Camera.main;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerAnimation == null)
            playerAnimation = GetComponentInChildren<PlayerAnimation>();

        if (muzzlePoint == null && aimCamera != null)
            muzzlePoint = aimCamera.transform;

        if ((muzzleParticles == null || muzzleParticles.Length == 0) && muzzlePoint != null)
            muzzleParticles = muzzlePoint.GetComponentsInChildren<ParticleSystem>(true);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void IncreaseSpread()
    {
        float increase = _isAiming ? aimSpreadIncreasePerShot : spreadIncreasePerShot;
        _currentSpread = Mathf.Min(maxSpread, _currentSpread + increase);
    }

    private void RecoverSpread()
    {
        if (_currentSpread <= 0f)
            return;

        float recoverySpeed = _isAiming ? aimSpreadRecoverySpeed : spreadRecoverySpeed;
        _currentSpread = Mathf.MoveTowards(_currentSpread, 0f, recoverySpeed * Time.deltaTime);
    }

    private Vector3 GetSpreadDirection(Vector3 baseDirection)
    {
        float effectiveSpread = GetEffectiveSpread();
        if (effectiveSpread <= 0f || aimCamera == null)
            return baseDirection;

        Vector2 randomPoint = UnityEngine.Random.insideUnitCircle * (effectiveSpread * spreadAnglePerPoint);
        Quaternion spreadRotation = Quaternion.AngleAxis(randomPoint.x, aimCamera.transform.up) *
                                    Quaternion.AngleAxis(randomPoint.y, aimCamera.transform.right);

        return (spreadRotation * baseDirection).normalized;
    }

    private void HandleAim(bool isPressed)
    {
        _isAiming = isPressed;
        if (_isAiming)
            _currentSpread = Mathf.Min(_currentSpread, maxSpread * aimSpreadMultiplier);
    }

    private float GetEffectiveSpread()
    {
        return _isAiming ? _currentSpread * aimSpreadMultiplier : _currentSpread;
    }

    // private void EnsureFeedbackObjects()
    // {
    //     if (audioSource == null)
    //         audioSource = gameObject.AddComponent<AudioSource>();
    //
    //     audioSource.playOnAwake = false;
    //     audioSource.spatialBlend = 0f;
    //
    //     if (fireClip == null)
    //         fireClip = CreateToneClip("Procedural Fire", 900f, 0.055f, 0.35f);
    //
    //     if (reloadClip == null)
    //         reloadClip = CreateToneClip("Procedural Reload", 260f, 0.12f, 0.2f);
    //
    //     if (muzzleFlashLight != null || muzzlePoint == null)
    //         return;
    //
    //     GameObject lightObject = new GameObject("Muzzle Flash Light");
    //     lightObject.transform.SetParent(muzzlePoint, false);
    //     lightObject.transform.localPosition = Vector3.forward * 0.35f;
    //     muzzleFlashLight = lightObject.AddComponent<Light>();
    //     muzzleFlashLight.type = LightType.Point;
    //     muzzleFlashLight.color = new Color(1f, 0.78f, 0.35f);
    //     muzzleFlashLight.range = 3f;
    //     muzzleFlashLight.intensity = 0f;
    // }

    private void ApplyRecoil()
    {
        if (playerCamera == null)
            return;

        float horizontal = UnityEngine.Random.Range(-recoilHorizontal, recoilHorizontal);
        playerCamera.AddRecoil(recoilVertical, horizontal);
    }

    private void PlayFireFeedback()
    {
        if (audioSource != null && fireClip != null)
            audioSource.PlayOneShot(fireClip);

        PlayMuzzleParticles();

        if (_muzzleFlashRoutine != null)
            StopCoroutine(_muzzleFlashRoutine);

        // _muzzleFlashRoutine = StartCoroutine(MuzzleFlashRoutine());
    }

    private void PlayMuzzleParticles()
    {
        if (muzzleParticles == null)
            return;

        for (int i = 0; i < muzzleParticles.Length; i++)
        {
            ParticleSystem particle = muzzleParticles[i];
            if (particle == null)
                continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }

    private void PlayReloadFeedback()
    {
        if (audioSource != null && reloadClip != null)
            audioSource.PlayOneShot(reloadClip);
    }

    // private IEnumerator MuzzleFlashRoutine()
    // {
    //     if (muzzleFlashLight == null)
    //         yield break;
    //
    //     muzzleFlashLight.intensity = 12f;
    //     yield return new WaitForSeconds(muzzleFlashDuration);
    //     muzzleFlashLight.intensity = 0f;
    // }

    private void SpawnHitEffect(Vector3 point, Vector3 normal, bool isHeadshot, bool spawnBulletHole)
    {
        Quaternion surfaceRotation = Quaternion.LookRotation(-normal);

        if (bulletHolePrefab != null && spawnBulletHole)
        {
            SpawnBulletHole(point, normal, surfaceRotation);
        }

        if (hitParticlePrefab != null && isHeadshot)
        {
            GameObject hitParticle = Instantiate(hitParticlePrefab, point + normal * 0.03f, Quaternion.LookRotation(normal));
            Destroy(hitParticle, hitParticleLifetime);
        }
    }

    private void SpawnBulletHole(Vector3 point, Vector3 normal, Quaternion surfaceRotation)
    {
        GameObject bulletHole;
        int poolLimit = Mathf.Max(1, maxBulletHoles);

        if (BulletHolePool.Count < poolLimit)
        {
            bulletHole = Instantiate(bulletHolePrefab);
        }
        else
        {
            bulletHole = BulletHolePool.Dequeue();

            if (bulletHole == null)
            {
                bulletHole = Instantiate(bulletHolePrefab);
            }
            else
            {
                bulletHole.SetActive(true);
            }
        }

        bulletHole.name = bulletHolePrefab.name;
        bulletHole.transform.position = point + normal * 0.01f;
        bulletHole.transform.rotation = surfaceRotation;
        bulletHole.transform.Rotate(0f, 0f, UnityEngine.Random.Range(0f, 360f), Space.Self);

        BulletHolePool.Enqueue(bulletHole);
    }

    private void PlayAction(string stateName, float duration)
    {
        if (playerAnimation != null)
        {
            playerAnimation.PlayActionState(stateName, duration);
            return;
        }

        if (animator != null && !string.IsNullOrWhiteSpace(stateName))
            animator.CrossFadeInFixedTime(stateName, 0.05f);
    }

    private static AudioClip CreateToneClip(string clipName, float frequency, float duration, float volume)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)sampleRate;
            float fade = 1f - i / (float)sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * volume * fade;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
