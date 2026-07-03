using UnityEngine;

public class PlayerWeaponAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerWeapon weapon;

    private void Awake()
    {
        ResolveWeapon();
    }

    public void Bind(PlayerWeapon targetWeapon)
    {
        weapon = targetWeapon;
    }

    public void DropMag()
    {
        ResolveWeapon();
        weapon?.DropMag();
    }

    public void InputMag()
    {
        ResolveWeapon();
        weapon?.InputMag();
    }

    public void LockMag()
    {
        ResolveWeapon();
        weapon?.LockMag();
    }

    public void Reload()
    {
        ResolveWeapon();
        weapon?.Reload();
    }

    public void SecondaryMeleeHit()
    {
        ResolveWeapon();
        weapon?.SecondaryMeleeHit();
    }

    private void ResolveWeapon()
    {
        if (weapon == null)
            weapon = GetComponentInParent<PlayerWeapon>();
    }
}
