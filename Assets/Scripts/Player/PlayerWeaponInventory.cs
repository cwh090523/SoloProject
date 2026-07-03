using System;
using System.Collections.Generic;
using ScriptableObjectScripts;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerWeapon))]
public class PlayerWeaponInventory : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private PlayerWeapon playerWeapon;

    [Header("Switching")]
    [SerializeField] private bool equipPurchasedWeapon = true;
    [SerializeField] private float scrollThreshold = 0.01f;
    [SerializeField] private int sharedReserveAmmo = -1;

    private readonly List<WeaponEntry> _weapons = new List<WeaponEntry>();
    private int _currentIndex = -1;

    public event Action<WeaponDataSO> WeaponUnlocked;
    public event Action<WeaponDataSO> WeaponEquipped;

    public int UnlockedWeaponCount => _weapons.Count;
    public WeaponDataSO CurrentWeaponData =>
        _currentIndex >= 0 && _currentIndex < _weapons.Count ? _weapons[_currentIndex].Data : null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (playerWeapon != null)
            playerWeapon.AmmoChanged += SyncSharedAmmoFromWeapon;
    }

    private void OnDisable()
    {
        if (playerWeapon != null)
            playerWeapon.AmmoChanged -= SyncSharedAmmoFromWeapon;
    }

    private void Update()
    {
        if (_weapons.Count <= 1 || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
            return;

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < scrollThreshold)
            return;

        EquipRelative(scroll > 0f ? 1 : -1);
    }

    public void InitializeLegacyWeapon(WeaponDataSO data, GameObject weaponObject)
    {
        ResolveReferences();

        if (data == null || weaponObject == null || IsUnlocked(data))
            return;

        WeaponInstance instance = weaponObject.GetComponent<WeaponInstance>();
        if (instance == null)
            instance = weaponObject.AddComponent<WeaponInstance>();
        instance.ResolveReferences();

        WeaponRuntimeState state = new WeaponRuntimeState(data.magazineSize, data.startingReserveAmmo);
        _weapons.Add(new WeaponEntry(data, instance, state));
        _currentIndex = 0;

        if (sharedReserveAmmo < 0)
            sharedReserveAmmo = Mathf.Max(0, data.startingReserveAmmo);

        playerWeapon.EquipWeapon(data, instance, state, sharedReserveAmmo);
        SaveEquippedRuntimeState();
        WeaponUnlocked?.Invoke(data);
        WeaponEquipped?.Invoke(data);
    }

    public bool Unlock(WeaponDataSO data)
    {
        ResolveReferences();

        if (data == null || IsUnlocked(data))
            return false;

        if (data.weaponPrefab == null || weaponSocket == null)
        {
            Debug.LogWarning($"Cannot unlock weapon '{data.name}'. Weapon Prefab or Weapon Socket is missing.", this);
            return false;
        }

        GameObject weaponObject = Instantiate(data.weaponPrefab, weaponSocket, false);
        weaponObject.name = string.IsNullOrWhiteSpace(data.weaponName) ? data.name : data.weaponName;

        WeaponInstance instance = weaponObject.GetComponent<WeaponInstance>();
        if (instance == null)
            instance = weaponObject.AddComponent<WeaponInstance>();
        instance.ResolveReferences();
        weaponObject.SetActive(false);

        WeaponRuntimeState state = new WeaponRuntimeState(data.magazineSize, 0);
        _weapons.Add(new WeaponEntry(data, instance, state));
        WeaponUnlocked?.Invoke(data);

        if (_currentIndex < 0 || equipPurchasedWeapon)
            Equip(_weapons.Count - 1);

        return true;
    }

    public bool IsUnlocked(WeaponDataSO data)
    {
        if (data == null)
            return false;

        for (int i = 0; i < _weapons.Count; i++)
        {
            if (_weapons[i].Data == data)
                return true;
        }

        return false;
    }

    public void EquipRelative(int direction)
    {
        if (_weapons.Count == 0 || direction == 0)
            return;

        int nextIndex = (_currentIndex + direction) % _weapons.Count;
        if (nextIndex < 0)
            nextIndex += _weapons.Count;

        Equip(nextIndex);
    }

    public void Equip(int index)
    {
        if (index < 0 || index >= _weapons.Count || index == _currentIndex)
            return;

        if (_currentIndex >= 0 && _currentIndex < _weapons.Count)
        {
            WeaponEntry previous = _weapons[_currentIndex];
            playerWeapon.SaveRuntimeState(previous.State);
            sharedReserveAmmo = playerWeapon.ReserveAmmo;
            if (previous.Instance != null)
                previous.Instance.gameObject.SetActive(false);
        }

        _currentIndex = index;
        WeaponEntry current = _weapons[_currentIndex];
        current.Instance.gameObject.SetActive(true);
        playerWeapon.EquipWeapon(current.Data, current.Instance, current.State, Mathf.Max(0, sharedReserveAmmo));
        SaveEquippedRuntimeState();
        WeaponEquipped?.Invoke(current.Data);
    }

    private void SyncSharedAmmoFromWeapon()
    {
        if (playerWeapon == null)
            return;

        sharedReserveAmmo = playerWeapon.ReserveAmmo;
        SaveEquippedRuntimeState();
    }

    private void SaveEquippedRuntimeState()
    {
        if (playerWeapon == null || _currentIndex < 0 || _currentIndex >= _weapons.Count)
            return;

        playerWeapon.SaveRuntimeState(_weapons[_currentIndex].State);
    }

    private void ResolveReferences()
    {
        if (playerWeapon == null)
            playerWeapon = GetComponent<PlayerWeapon>();

        if (weaponSocket != null)
            return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (string.Equals(children[i].name, "WeaponSocket", StringComparison.OrdinalIgnoreCase))
            {
                weaponSocket = children[i];
                break;
            }
        }
    }

    private sealed class WeaponEntry
    {
        public WeaponEntry(WeaponDataSO data, WeaponInstance instance, WeaponRuntimeState state)
        {
            Data = data;
            Instance = instance;
            State = state;
        }

        public WeaponDataSO Data { get; }
        public WeaponInstance Instance { get; }
        public WeaponRuntimeState State { get; }
    }
}

[Serializable]
public sealed class WeaponRuntimeState
{
    public WeaponRuntimeState(int currentAmmo, int reserveAmmo)
    {
        CurrentAmmo = currentAmmo;
        ReserveAmmo = reserveAmmo;
    }

    public int CurrentAmmo;
    public int ReserveAmmo;
    public float CurrentSpread;
}
