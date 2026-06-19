using UnityEngine;

public static class GameSettings
{
    public const float MinMouseSensitivity = 0.001f;
    public const float MaxMouseSensitivity = 10f;

    private const string MouseSensitivityKey = "Settings.MouseSensitivity";
    private const string MasterVolumeKey = "Settings.MasterVolume";
    private const string BgmVolumeKey = "Settings.BgmVolume";
    private const string SfxVolumeKey = "Settings.SfxVolume";
    private const string TvVolumeKey = "Settings.TvVolume";
    private const string FullscreenKey = "Settings.Fullscreen";
    private const string ResolutionWidthKey = "Settings.ResolutionWidth";
    private const string ResolutionHeightKey = "Settings.ResolutionHeight";
    private const string ResolutionRefreshRateKey = "Settings.ResolutionRefreshRate";

    public static float MouseSensitivity
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(MouseSensitivityKey, 0.3f), MinMouseSensitivity, MaxMouseSensitivity);
        set => PlayerPrefs.SetFloat(MouseSensitivityKey, Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity));
    }

    public static float MasterVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        set => PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
    }

    public static float BgmVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
        set => PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
    }

    public static float SfxVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
    }

    public static float TvVolume
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat(TvVolumeKey, 1f));
        set => PlayerPrefs.SetFloat(TvVolumeKey, Mathf.Clamp01(value));
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
    }

    public static int ResolutionWidth
    {
        get => PlayerPrefs.GetInt(ResolutionWidthKey, Screen.currentResolution.width);
        set => PlayerPrefs.SetInt(ResolutionWidthKey, Mathf.Max(1, value));
    }

    public static int ResolutionHeight
    {
        get => PlayerPrefs.GetInt(ResolutionHeightKey, Screen.currentResolution.height);
        set => PlayerPrefs.SetInt(ResolutionHeightKey, Mathf.Max(1, value));
    }

    public static int ResolutionRefreshRate
    {
        get => PlayerPrefs.GetInt(ResolutionRefreshRateKey, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
        set => PlayerPrefs.SetInt(ResolutionRefreshRateKey, Mathf.Max(1, value));
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }

    public static void ApplyDisplay()
    {
        Screen.SetResolution(ResolutionWidth, ResolutionHeight, Fullscreen);
    }
}
