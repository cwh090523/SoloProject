using System;
using AI.EnemySpawner;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameStateManager : MonoBehaviour
{
    [SerializeField] private EnemyWaveSpawner waveSpawner;
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private PlayerWeapon playerWeapon;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private AimTargetScanner aimTargetScanner;
    [SerializeField] private PlayerDebugHealOnKey debugHealOnKey;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private string playerObjectName = "Player3";

    public event Action<GameState> StateChanged;

    public GameState CurrentState { get; private set; } = GameState.Boot;
    public bool IsGameOver => CurrentState == GameState.GameOver;
    public bool IsStageClear => CurrentState == GameState.StageClear;
    public bool IsPaused => CurrentState == GameState.Paused;
    public bool IsPlaying => CurrentState == GameState.Combat || CurrentState == GameState.Restock;

    public string StateText => CurrentState switch
    {
        GameState.Boot => "READY",
        GameState.Combat => "IN PROGRESS",
        GameState.Restock => "RESTOCK",
        GameState.Paused => "PAUSED",
        GameState.GameOver => "GAME OVER",
        GameState.StageClear => "STAGE CLEAR",
        _ => "READY"
    };

    private GameState _stateBeforePause = GameState.Combat;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (playerHealth != null)
            playerHealth.Died += HandlePlayerDied;

        if (waveSpawner != null)
        {
            waveSpawner.StageCleared += HandleStageCleared;
            waveSpawner.RestockStarted += HandleRestockStarted;
            waveSpawner.RestockEnded += HandleRestockEnded;
        }

        SetState(GameState.Combat);
        SetPlayerControlEnabled(true);
    }

    private void Update()
    {
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            HandleStageCleared();
    }

    private void OnDisable()
    {
        if (IsPaused)
            Time.timeScale = 1f;

        SetAudioPaused(false);

        if (playerHealth != null)
            playerHealth.Died -= HandlePlayerDied;

        if (waveSpawner != null)
        {
            waveSpawner.StageCleared -= HandleStageCleared;
            waveSpawner.RestockStarted -= HandleRestockStarted;
            waveSpawner.RestockEnded -= HandleRestockEnded;
        }
    }

    public void BeginRestock()
    {
        if (IsGameOver || IsStageClear)
            return;

        SetState(GameState.Restock);
    }

    public void BeginCombat()
    {
        if (IsGameOver || IsStageClear)
            return;

        SetState(GameState.Combat);
    }

    public bool CanPause()
    {
        return IsPlaying;
    }

    public void PauseGame()
    {
        if (!CanPause())
            return;

        _stateBeforePause = CurrentState;
        Time.timeScale = 0f;
        SetAudioPaused(true);
        SetState(GameState.Paused);
        SetPlayerControlEnabled(false);
    }

    public void ResumeGame()
    {
        if (!IsPaused)
            return;

        Time.timeScale = 1f;
        SetAudioPaused(false);
        SetState(_stateBeforePause == GameState.Paused ? GameState.Combat : _stateBeforePause);
        SetPlayerControlEnabled(true);
    }

    private void ResolveReferences()
    {
        if (waveSpawner == null)
            waveSpawner = FindFirstObjectByType<EnemyWaveSpawner>();

        GameObject player = GameObject.Find(playerObjectName);
        if (player != null)
        {
            if (playerHealth == null)
                playerHealth = player.GetComponent<Health>();

            if (playerController == null)
                playerController = player.GetComponent<PlayerController>();

            if (playerCamera == null)
                playerCamera = player.GetComponent<PlayerCamera>();

            if (playerWeapon == null)
                playerWeapon = player.GetComponent<PlayerWeapon>();

            if (playerAnimation == null)
                playerAnimation = player.GetComponentInChildren<PlayerAnimation>();

            if (aimTargetScanner == null)
                aimTargetScanner = player.GetComponent<AimTargetScanner>();

            if (debugHealOnKey == null)
                debugHealOnKey = player.GetComponent<PlayerDebugHealOnKey>();

            if (playerRigidbody == null)
                playerRigidbody = player.GetComponent<Rigidbody>();
        }

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        if (playerController != null && playerHealth == null)
            playerHealth = playerController.GetComponent<Health>();

        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<PlayerCamera>();

        if (playerWeapon == null)
            playerWeapon = FindFirstObjectByType<PlayerWeapon>();

        if (playerAnimation == null)
            playerAnimation = FindFirstObjectByType<PlayerAnimation>();

        if (aimTargetScanner == null)
            aimTargetScanner = FindFirstObjectByType<AimTargetScanner>();

        if (debugHealOnKey == null)
            debugHealOnKey = FindFirstObjectByType<PlayerDebugHealOnKey>();

        if (playerRigidbody == null && playerController != null)
            playerRigidbody = playerController.GetComponent<Rigidbody>();
    }

    private void HandlePlayerDied()
    {
        if (IsStageClear)
            return;

        SetState(GameState.GameOver);
        Time.timeScale = 1f;
        SetAudioPaused(false);

        if (waveSpawner != null)
            waveSpawner.StopWaves();

        StopPlayerAnimationAtIdle();
        SetPlayerControlEnabled(false);
    }

    private void HandleStageCleared()
    {
        if (IsGameOver)
            return;

        SetState(GameState.StageClear);
        Time.timeScale = 1f;
        SetAudioPaused(false);
        StopPlayerAnimationAtIdle();
        SetPlayerControlEnabled(false);
    }

    private void HandleRestockStarted(float duration)
    {
        BeginRestock();
    }

    private void HandleRestockEnded()
    {
        BeginCombat();
    }

    private void StopPlayerAnimationAtIdle()
    {
        if (playerAnimation != null)
            playerAnimation.PlayIdleAndStopUpdating();
    }

    private void SetState(GameState nextState)
    {
        if (CurrentState == nextState)
            return;

        CurrentState = nextState;
        StateChanged?.Invoke(CurrentState);
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (playerController != null)
            playerController.enabled = isEnabled;

        if (playerCamera != null)
            playerCamera.enabled = isEnabled;

        if (playerWeapon != null)
            playerWeapon.enabled = isEnabled;

        if (aimTargetScanner != null)
            aimTargetScanner.enabled = isEnabled;

        if (debugHealOnKey != null)
            debugHealOnKey.enabled = isEnabled;

        if (!isEnabled && playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void SetAudioPaused(bool isPaused)
    {
        AudioListener.pause = isPaused;
    }
}
