using UnityEngine;

namespace ScriptableObjectScripts
{
    public enum WeaponSecondaryActionType
    {
        Aim,
        None,
        MeleeHold
    }

    [CreateAssetMenu(fileName = "Weapon Data", menuName = "SO/Weapon Data", order = 0)]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string weaponName;
        public GameObject weaponPrefab;
        public Sprite icon;
        
        
        [Header("Audio")] 
        public float fireSoundInterval = 0.4f;

        [Header("Combat")]
        public float damage = 25f;
        public float range = 100f;
        public float fireRate = 9f;
        public bool automatic = true;
        public float autoFireHoldDelay = 0.3f;

        [Header("Ammo")]
        public int magazineSize = 30;
        public int startingReserveAmmo = 90;
        public float reloadTime = 1.7f;

        [Header("Accuracy")]
        public float spreadPerShot = 0.45f;
        public float maxSpread = 2.25f;
        public float spreadRecoverySpeed = 1.8f;
        public float spreadAnglePerPoint = 1.15f;
        public float aimSpreadMultiplier = 0.02f;
        public float aimSpreadIncreasePerShot;
        public float aimSpreadRecoverySpeed = 8f;

        [Header("Recoil")]
        public float verticalRecoil = 1.4f;
        public float horizontalRecoil = 0.35f;

        [Header("View")]
        public float aimFov = 42f;
        public bool useScopeOverlay;
        public Sprite scopeOverlaySprite;

        [Header("Secondary Action")]
        public WeaponSecondaryActionType secondaryActionType = WeaponSecondaryActionType.Aim;
        public string secondaryHoldStateName = "";
        public string secondaryReleaseStateName = "";
        public float secondaryAnimationDuration = 0.2f;
        public float secondaryMeleeDamage = 35f;
        public float secondaryMeleeRange = 2f;
        public float secondaryMeleeRadius = 0.35f;
        public float secondaryMeleeHitInterval = 0.35f;

        [Header("Animation")]
        public string equipStateName = "EQUIP";
        public float equipAnimationDuration = 0.35f;
        public string fireStateName = "FIRE";
        public string[] fireStateNames;
        public string aimFireStateName = "AIMMING_FIRE";
        public string reloadStateName = "RELOAD";
        public float fireAnimationDuration = 0.12f;

        [Header("Audio")]
        public AudioClip fireClip;
        public AudioClip reloadClip;
        public AudioClip dropMagazineClip;
        public AudioClip inputMagazineClip;
        public AudioClip lockMagazineClip;

        [Header("Shell")]
        public GameObject shellPrefab;
    }
}
