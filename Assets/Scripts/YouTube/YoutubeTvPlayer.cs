using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace YouTube
{
    [RequireComponent(typeof(VideoPlayer))]
    public class YoutubeTvPlayer : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private VideoClip videoClip;
        [SerializeField] private VideoClip[] playlist;
        [SerializeField] private string resourcesPlaylistPath;
        [SerializeField] private bool useExternalVideoFolder = true;
        [SerializeField] private string externalVideoFolderPath = "TVVideos";
        [SerializeField] private bool externalPathIsStreamingAssetsRelative = true;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnAwake;
        [SerializeField] private float minAudioDistance = 2f;
        [SerializeField] private float maxAudioDistance = 18f;
        [SerializeField] private bool pickRandomClipEachPlay = true;
        [SerializeField] private VideoAspectRatio aspectRatio = VideoAspectRatio.FitInside;

        private VideoClip[] _resourcePlaylist;
        private string[] _externalVideoPaths;
        private float _appliedTvVolume = -1f;

        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;

        private void Awake()
        {
            ResolveReferences();
            ConfigureVideoPlayer();

            if (playOnAwake)
                Play();
        }

        private void Update()
        {
            ApplyTvVolumeIfChanged();
        }

        public void TogglePlay()
        {
            if (IsPlaying)
            {
                Stop();
                return;
            }

            Play();
        }

        public void Play()
        {
            ResolveReferences();
            ConfigureVideoPlayer();

            if (videoPlayer == null)
                return;

            if (videoPlayer.clip == null && string.IsNullOrWhiteSpace(videoPlayer.url))
            {
                Debug.LogWarning($"{name} TV has no VideoClip or URL assigned.", this);
                return;
            }

            videoPlayer.Play();
        }

        public void Stop()
        {
            ResolveReferences();

            if (videoPlayer != null)
                videoPlayer.Stop();
        }

        public bool TryPlayUrl(string urlOrVideoId)
        {
            Debug.LogWarning($"{name} uses local video playback now. Assign a VideoClip to the TV prefab instead of a YouTube URL.", this);
            return false;
        }

        private void ResolveReferences()
        {
            if (videoPlayer == null)
                videoPlayer = GetComponent<VideoPlayer>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void ConfigureVideoPlayer()
        {
            if (videoPlayer == null)
                return;

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = loop;
            videoPlayer.aspectRatio = aspectRatio;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;

            if (audioSource != null)
            {
                videoPlayer.SetTargetAudioSource(0, audioSource);
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = minAudioDistance;
                audioSource.maxDistance = maxAudioDistance;
                audioSource.dopplerLevel = 0f;
                ApplyTvVolumeIfChanged(true);
            }

            if (TrySelectExternalVideoPath(out string selectedVideoPath))
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = selectedVideoPath;
                videoPlayer.clip = null;
                return;
            }

            VideoClip selectedClip = pickRandomClipEachPlay ? GetRandomClip() : GetDefaultClip();
            if (selectedClip != null)
            {
                videoPlayer.source = VideoSource.VideoClip;
                videoPlayer.clip = selectedClip;
                videoPlayer.url = string.Empty;
            }
        }

        private bool TrySelectExternalVideoPath(out string videoUrl)
        {
            videoUrl = string.Empty;

            string[] paths = GetExternalVideoPaths();
            if (paths.Length <= 0)
                return false;

            string selectedPath = pickRandomClipEachPlay ? paths[UnityEngine.Random.Range(0, paths.Length)] : paths[0];
            videoUrl = new Uri(selectedPath).AbsoluteUri;
            return true;
        }

        private string[] GetExternalVideoPaths()
        {
            if (!useExternalVideoFolder || string.IsNullOrWhiteSpace(externalVideoFolderPath))
                return Array.Empty<string>();

            if (_externalVideoPaths != null)
                return _externalVideoPaths;

            string folderPath = GetExternalVideoFolderFullPath();
            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"{name} external TV video folder not found: {folderPath}", this);
                _externalVideoPaths = Array.Empty<string>();
                return _externalVideoPaths;
            }

            _externalVideoPaths = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
            _externalVideoPaths = Array.FindAll(_externalVideoPaths, IsSupportedVideoFile);
            return _externalVideoPaths;
        }

        private string GetExternalVideoFolderFullPath()
        {
            string folderPath = externalVideoFolderPath.Trim().Trim('/', '\\');
            if (!externalPathIsStreamingAssetsRelative || Path.IsPathRooted(folderPath))
                return folderPath;

            return Path.Combine(Application.streamingAssetsPath, folderPath);
        }

        private static bool IsSupportedVideoFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".webm", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".avi", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyTvVolumeIfChanged(bool force = false)
        {
            if (audioSource == null)
                return;

            float tvVolume = GameSettings.TvVolume;
            if (!force && Mathf.Approximately(_appliedTvVolume, tvVolume))
                return;

            _appliedTvVolume = tvVolume;
            audioSource.volume = tvVolume;
        }

        private VideoClip GetRandomClip()
        {
            VideoClip[] availableClips = GetAvailablePlaylist();
            if (availableClips.Length <= 0)
                return videoClip;

            return availableClips[UnityEngine.Random.Range(0, availableClips.Length)];
        }

        private VideoClip GetDefaultClip()
        {
            VideoClip[] availableClips = GetAvailablePlaylist();
            if (availableClips.Length > 0)
                return availableClips[0];

            return videoClip;
        }

        private VideoClip[] GetAvailablePlaylist()
        {
            if (playlist != null && playlist.Length > 0)
                return playlist;

            if (_resourcePlaylist != null)
                return _resourcePlaylist;

            if (string.IsNullOrWhiteSpace(resourcesPlaylistPath))
            {
                _resourcePlaylist = System.Array.Empty<VideoClip>();
                return _resourcePlaylist;
            }

            _resourcePlaylist = Resources.LoadAll<VideoClip>(resourcesPlaylistPath.Trim().Trim('/'));
            return _resourcePlaylist;
        }
    }
}
