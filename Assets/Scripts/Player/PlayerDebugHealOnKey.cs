using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Health))]
public class PlayerDebugHealOnKey : MonoBehaviour
{
    [SerializeField] private float healAmount = 10f;

    private Health _health;

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.middleButton.wasPressedThisFrame)
            return;

        _health.Heal(healAmount);
    }
}
