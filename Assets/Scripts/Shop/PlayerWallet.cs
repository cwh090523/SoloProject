using System;
using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    [SerializeField] private int startingMoney;

    private int _money;

    public event Action<int> MoneyChanged;

    public int Money => _money;

    private void Awake()
    {
        _money = Mathf.Max(0, startingMoney);
        MoneyChanged?.Invoke(_money);
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0)
            return;

        _money += amount;
        MoneyChanged?.Invoke(_money);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        if (_money < amount)
            return false;

        _money -= amount;
        MoneyChanged?.Invoke(_money);
        return true;
    }
}
