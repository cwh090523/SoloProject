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
    [SerializeField] private string fireStateName = "FIRE";
    [SerializeField] private string aimFireStateName = "AIMMING_FIRE";
    [SerializeField] private string reloadStateName = "RELOAD";
    [SerializeField] private float fireAnimationDuration = 0.12f;
    [SerializeField] private bool drawDebugRay = true;
    // [SerializeField] private Light muzzleFlashLight;
    [SerializeField] private ParticleSystem[] muzzleParticles;
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private int maxBulletHoles = 15;
    [SerializeField] private float bulletHoleActiveTime = 12f;
    [SerializeField] private float hitParticleLifetime = 2f;
    [SerializeField] private AudioClip fireClip;
    [SerializeField] private AudioClip dropMagazineClip;
    [SerializeField] private AudioClip inputMagazineClip;
    [SerializeField] private AudioClip lockMagazineClip;

    [Header("Shell Ejection")]
    [SerializeField] private GameObject shellPrefab;
    [SerializeField] private Transform shellEjectPoint;
    [SerializeField] private Vector3 fallbackShellLocalOffset = new Vector3(0.18f, -0.08f, 0.25f);
    [SerializeField] private float shellEjectForce = 1.25f;
    [SerializeField] private float shellUpwardForce = 0.45f;
    [SerializeField] private float shellBackwardForce = 0.15f;
    [SerializeField] private float shellRandomForce = 0.12f;
    [SerializeField] private float shellTorque = 4f;
    [SerializeField] private float shellLifetime = 4f;
    [SerializeField] private int maxShells = 15;

    private static readonly List<BulletHolePoolItem> BulletHolePool = new List<BulletHolePoolItem>();
    private readonly List<ShellPoolItem> _shellPool = new List<ShellPoolItem>();

    private int _currentAmmo;
    private float _nextFireTime;
    private bool _isReloading;
    private Coroutine _muzzleFlashRoutine;
    private float _currentSpread;
    private float _attackHeldTime;
    private bool _isAiming;
    private bool _reloadCompletedByEvent;

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
        PrewarmShellPool();
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

        if (playerAnimation != null)
            playerAnimation.SetAiming(false);
    }

    private void Update()
    {
        RecoverSpread();
        UpdateAutoFire();
        UpdateBulletHolePool();
        UpdateShellPool();

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
        PlayAction(GetFireStateName(), fireAnimationDuration);
        ApplyRecoil();
        PlayFireFeedback();
        FireRaycast();
    }

    private string GetFireStateName()
    {
        return _isAiming && !string.IsNullOrWhiteSpace(aimFireStateName) ? aimFireStateName : fireStateName;
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
        Health targetHealth = hit.collider.GetComponentInParent<Health>();
        bool isHeadshot;
        float finalDamage = GetFinalDamage(hit, out isHeadshot);

        bool canTakeDamage = damageable != null;
        SpawnHitEffect(hit.point, hit.normal, isHeadshot, !canTakeDamage);
        HitConfirmed?.Invoke();

        if (!canTakeDamage || (targetHealth != null && targetHealth.IsDead))
            return;

        damageable.TakeDamage(finalDamage);

        if (targetHealth == null || !targetHealth.IsDead)
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

    public void DropMag()
    {
        PlayOneShot(dropMagazineClip);
    }

    public void InputMag()
    {
        PlayOneShot(inputMagazineClip);
    }

    public void LockMag()
    {
        PlayOneShot(lockMagazineClip);
    }

    public void Reload()
    {
        if (!_isReloading || _reloadCompletedByEvent)
            return;

        int neededAmmo = magazineSize - _currentAmmo;
        if (neededAmmo <= 0 || reserveAmmo <= 0)
        {
            _reloadCompletedByEvent = true;
            return;
        }

        int loadedAmmo = Mathf.Min(neededAmmo, reserveAmmo);
        _currentAmmo += loadedAmmo;
        reserveAmmo -= loadedAmmo;
        _reloadCompletedByEvent = true;

        AmmoChanged?.Invoke();
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
        _reloadCompletedByEvent = false;
        ReloadStarted?.Invoke();
        PlayAction(reloadStateName, reloadTime);

        yield return new WaitForSeconds(reloadTime);

        if (!_reloadCompletedByEvent)
            Reload();

        _isReloading = false;

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
        if (playerAnimation != null)
            playerAnimation.SetAiming(_isAiming);

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
        PlayOneShot(fireClip);

        PlayMuzzleParticles();

        if (_muzzleFlashRoutine != null)
            StopCoroutine(_muzzleFlashRoutine);

        // _muzzleFlashRoutine = StartCoroutine(MuzzleFlashRoutine());
        EjectShell();
    }

    private void EjectShell()
    {
        if (shellPrefab == null)
            return;

        Transform reference = GetShellReferenceTransform();
        if (reference == null)
            return;

        Vector3 position = shellEjectPoint != null
            ? shellEjectPoint.position
            : reference.TransformPoint(fallbackShellLocalOffset);

        ShellPoolItem poolItem = GetShellPoolItem();
        if (poolItem == null)
            return;

        GameObject shell = poolItem.GameObject;
        if (shell == null)
            return;

        poolItem.LastUsedTime = Time.time;
        poolItem.IsActive = true;

        Quaternion rotation = shellEjectPoint != null ? shellEjectPoint.rotation : reference.rotation;
        shell.SetActive(false);
        shell.name = shellPrefab.name;
        shell.transform.SetPositionAndRotation(position, rotation);
        shell.SetActive(true);

        Rigidbody shellRigidbody = EnsureShellRigidbody(shell);
        shellRigidbody.linearVelocity = Vector3.zero;
        shellRigidbody.angularVelocity = Vector3.zero;

        Vector3 ejectDirection = reference.right * shellEjectForce;
        ejectDirection += Vector3.up * shellUpwardForce;
        ejectDirection -= reference.forward * shellBackwardForce;
        ejectDirection += UnityEngine.Random.insideUnitSphere * shellRandomForce;

        shellRigidbody.linearVelocity = ejectDirection;
        shellRigidbody.AddTorque(UnityEngine.Random.insideUnitSphere * shellTorque, ForceMode.Impulse);
    }

    private Transform GetShellReferenceTransform()
    {
        if (shellEjectPoint != null)
            return shellEjectPoint;

        if (muzzlePoint != null)
            return muzzlePoint;

        return aimCamera != null ? aimCamera.transform : transform;
    }

    private Rigidbody EnsureShellRigidbody(GameObject shell)
    {
        Rigidbody shellRigidbody = shell.GetComponent<Rigidbody>();
        if (shellRigidbody == null)
            shellRigidbody = shell.AddComponent<Rigidbody>();

        shellRigidbody.mass = 0.08f;
        shellRigidbody.linearDamping = 0.05f;
        shellRigidbody.angularDamping = 0.05f;
        shellRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (shell.GetComponentInChildren<Collider>() == null)
        {
            BoxCollider boxCollider = shell.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(0.035f, 0.035f, 0.09f);
        }

        return shellRigidbody;
    }

    private void PrewarmShellPool()
    {
        if (shellPrefab == null)
            return;

        int poolLimit = Mathf.Max(1, maxShells);
        while (_shellPool.Count < poolLimit)
        {
            ShellPoolItem item = CreateShellPoolItem();
            if (item == null)
                return;

            _shellPool.Add(item);
        }
    }

    private ShellPoolItem GetShellPoolItem()
    {
        int poolLimit = Mathf.Max(1, maxShells);

        if (_shellPool.Count < poolLimit)
        {
            ShellPoolItem createdItem = CreateShellPoolItem();
            if (createdItem == null)
                return null;

            _shellPool.Add(createdItem);
            return createdItem;
        }

        ShellPoolItem oldestItem = null;
        for (int i = 0; i < _shellPool.Count; i++)
        {
            ShellPoolItem item = _shellPool[i];
            if (item.GameObject == null)
            {
                ShellPoolItem replacementItem = CreateShellPoolItem();
                if (replacementItem == null)
                    return null;

                item.GameObject = replacementItem.GameObject;
                item.IsActive = false;
                item.LastUsedTime = 0f;
                return item;
            }

            if (!item.IsActive)
                return item;

            if (oldestItem == null || item.LastUsedTime < oldestItem.LastUsedTime)
                oldestItem = item;
        }

        return oldestItem;
    }

    private ShellPoolItem CreateShellPoolItem()
    {
        if (shellPrefab == null)
            return null;

        GameObject shell = Instantiate(shellPrefab);
        shell.name = shellPrefab.name;
        EnsureShellRigidbody(shell);
        shell.SetActive(false);
        return new ShellPoolItem(shell);
    }

    private void UpdateShellPool()
    {
        if (shellLifetime <= 0f)
            return;

        for (int i = 0; i < _shellPool.Count; i++)
        {
            ShellPoolItem item = _shellPool[i];
            if (!item.IsActive || item.GameObject == null)
                continue;

            if (Time.time - item.LastUsedTime < shellLifetime)
                continue;

            Rigidbody shellRigidbody = item.GameObject.GetComponent<Rigidbody>();
            if (shellRigidbody != null)
            {
                shellRigidbody.linearVelocity = Vector3.zero;
                shellRigidbody.angularVelocity = Vector3.zero;
            }

            item.GameObject.SetActive(false);
            item.IsActive = false;
        }
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

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, GameSettings.SfxVolume);
    }

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
        BulletHolePoolItem poolItem = GetBulletHolePoolItem();
        GameObject bulletHole = poolItem.GameObject;
        if (bulletHole == null)
            return;

        poolItem.LastUsedTime = Time.time;
        poolItem.IsActive = true;

        bulletHole.SetActive(true);
        bulletHole.name = bulletHolePrefab.name;
        bulletHole.transform.position = point + normal * 0.01f;
        bulletHole.transform.rotation = surfaceRotation;
        bulletHole.transform.Rotate(0f, 0f, UnityEngine.Random.Range(0f, 360f), Space.Self);
    }

    private BulletHolePoolItem GetBulletHolePoolItem()
    {
        int poolLimit = Mathf.Max(1, maxBulletHoles);

        if (BulletHolePool.Count < poolLimit)
        {
            BulletHolePoolItem createdItem = new BulletHolePoolItem(Instantiate(bulletHolePrefab));
            BulletHolePool.Add(createdItem);
            return createdItem;
        }

        BulletHolePoolItem oldestItem = null;
        for (int i = 0; i < BulletHolePool.Count; i++)
        {
            BulletHolePoolItem item = BulletHolePool[i];
            if (item.GameObject == null)
            {
                item.GameObject = Instantiate(bulletHolePrefab);
                item.IsActive = false;
                item.LastUsedTime = 0f;
                return item;
            }

            if (!item.IsActive)
                return item;

            if (oldestItem == null || item.LastUsedTime < oldestItem.LastUsedTime)
                oldestItem = item;
        }

        return oldestItem;
    }

    private void UpdateBulletHolePool()
    {
        if (bulletHoleActiveTime <= 0f)
            return;

        for (int i = 0; i < BulletHolePool.Count; i++)
        {
            BulletHolePoolItem item = BulletHolePool[i];
            if (!item.IsActive || item.GameObject == null)
                continue;

            if (Time.time - item.LastUsedTime < bulletHoleActiveTime)
                continue;

            item.GameObject.SetActive(false);
            item.IsActive = false;
        }
    }

    private sealed class BulletHolePoolItem
    {
        public BulletHolePoolItem(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        public GameObject GameObject;
        public float LastUsedTime;
        public bool IsActive;
    }

    private sealed class ShellPoolItem
    {
        public ShellPoolItem(GameObject gameObject)
        {
            GameObject = gameObject;
        }

        public GameObject GameObject;
        public float LastUsedTime;
        public bool IsActive;
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
