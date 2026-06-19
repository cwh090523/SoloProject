using UnityEngine;

namespace Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class SceneBgmPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bgmClip;
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private bool loop = true;

        private float _appliedVolume = -1f;

        private void Awake()
        {
            ResolveReferences();
            ConfigureAudioSource();
        }

        private void Start()
        {
            if (playOnAwake)
                Play();
        }

        private void Update()
        {
            ApplyVolumeIfChanged();
        }

        public void Play()
        {
            ResolveReferences();
            ConfigureAudioSource();

            if (audioSource == null || bgmClip == null)
                return;

            if (!audioSource.isPlaying)
                audioSource.Play();
        }

        private void ResolveReferences()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
                return;

            audioSource.clip = bgmClip;
            audioSource.playOnAwake = false;
            audioSource.loop = loop;
            audioSource.spatialBlend = 0f;
            ApplyVolumeIfChanged(true);
        }

        private void ApplyVolumeIfChanged(bool force = false)
        {
            if (audioSource == null)
                return;

            float volume = GameSettings.BgmVolume;
            if (!force && Mathf.Approximately(_appliedVolume, volume))
                return;

            _appliedVolume = volume;
            audioSource.volume = volume;
        }
    }
}
