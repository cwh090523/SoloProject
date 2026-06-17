using UnityEngine;

namespace Shop
{
    [CreateAssetMenu(fileName = "Shop Item", menuName = "SO/Shop/Item")]
    public class ShopItemDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Shop Item";
        [SerializeField, TextArea] private string description;
        [SerializeField] private ShopItemType itemType;
        [SerializeField, Min(0)] private int price = 100;
        [SerializeField] private float amount = 1f;
        [SerializeField] private bool uniquePurchase;
        [SerializeField] private string uniqueId;
        [SerializeField] private GameObject weaponPrefab;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public ShopItemType ItemType => itemType;
        public int Price => price;
        public float Amount => amount;
        public bool UniquePurchase => uniquePurchase || itemType == ShopItemType.Weapon;
        public GameObject WeaponPrefab => weaponPrefab;
        public string UniqueId => string.IsNullOrWhiteSpace(uniqueId) ? name : uniqueId;
    }
}
