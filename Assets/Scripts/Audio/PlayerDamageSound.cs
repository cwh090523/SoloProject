using UnityEngine;

namespace Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class PlayerDamageSound : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] damageClips;
        [SerializeField, Range(0f, 20f)] private float volume = 1f;
        [SerializeField] private float minPlayInterval = 0.08f;

        private float _lastPlayTime = -999f;

        private void Awake()
        {
            ResolveReferences();
            ConfigureAudioSource();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (playerHealth != null)
                playerHealth.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.Damaged -= HandleDamaged;
        }

        private void HandleDamaged(float damage)
        {
            if (damage <= 0f || damageClips == null || damageClips.Length == 0)
                return;

            if (Time.unscaledTime - _lastPlayTime < minPlayInterval)
                return;

            ResolveReferences();
            if (audioSource == null)
                return;

            AudioClip clip = damageClips[UnityEngine.Random.Range(0, damageClips.Length)];
            if (clip == null)
                return;

            _lastPlayTime = Time.unscaledTime;
            audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        private void ResolveReferences()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (playerHealth == null)
                playerHealth = GetComponent<Health>();

            if (playerHealth == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                    playerHealth = player.GetComponent<Health>();
            }
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
                return;

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }
}
