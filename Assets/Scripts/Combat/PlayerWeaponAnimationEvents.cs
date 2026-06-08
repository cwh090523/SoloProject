using UnityEngine;

public class PlayerWeaponAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerWeapon weapon;

    private void Awake()
    {
        ResolveWeapon();
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

    private void ResolveWeapon()
    {
        if (weapon == null)
            weapon = GetComponentInParent<PlayerWeapon>();
    }
}
