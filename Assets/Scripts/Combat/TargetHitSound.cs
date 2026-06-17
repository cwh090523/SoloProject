using UnityEngine;

[RequireComponent(typeof(Health))]
public class TargetHitSound : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] hitClips;
    [SerializeField, Range(0f, 20f)] private float volume = 1f;
    [SerializeField] private float minPlayInterval = 0.05f;

    private float _lastPlayTime = -999f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (health != null)
            health.Damaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Damaged -= HandleDamaged;
    }

    private void ResolveReferences()
    {
        if (health == null)
            health = GetComponent<Health>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void HandleDamaged(float damage)
    {
        if (hitClips == null || hitClips.Length == 0)
            return;

        if (Time.time - _lastPlayTime < minPlayInterval)
            return;

        AudioClip clip = hitClips[Random.Range(0, hitClips.Length)];
        if (clip == null)
            return;

        _lastPlayTime = Time.time;
        audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }
}
