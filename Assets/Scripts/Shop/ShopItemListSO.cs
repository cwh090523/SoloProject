using System.Collections.Generic;
using UnityEngine;

namespace Shop
{
    [CreateAssetMenu(fileName = "Shop Item List", menuName = "SO/Shop/Item List")]
    public class ShopItemListSO : ScriptableObject
    {
        [SerializeField] private ShopItemDefinition[] items;

        public IReadOnlyList<ShopItemDefinition> Items => items;
    }
}
