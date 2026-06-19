using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BossHealthTarget : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private string displayName = "BOSS";
    [SerializeField] private bool showWhenFullHealth = true;
    [SerializeField] private bool hideOnDeath = true;

    public static event Action<BossHealthTarget> Registered;
    public static event Action<BossHealthTarget> Unregistered;

    public Health Health => health;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    public bool ShowWhenFullHealth => showWhenFullHealth;
    public bool HideOnDeath => hideOnDeath;

    private void Awake()
    {
        ResolveHealth();
    }

    private void OnEnable()
    {
        ResolveHealth();
        Registered?.Invoke(this);
    }

    private void OnDisable()
    {
        Unregistered?.Invoke(this);
    }

    public void Initialize(Health targetHealth, string targetDisplayName)
    {
        health = targetHealth;

        if (!string.IsNullOrWhiteSpace(targetDisplayName))
            displayName = targetDisplayName;

        if (isActiveAndEnabled)
            Registered?.Invoke(this);
    }

    private void ResolveHealth()
    {
        if (health != null)
            return;

        health = GetComponent<Health>();
        if (health == null)
            health = GetComponentInChildren<Health>();
        if (health == null)
            health = GetComponentInParent<Health>();
    }
}
