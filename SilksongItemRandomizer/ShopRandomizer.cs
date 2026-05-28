using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public static class ShopRandomizer
{
    private static readonly Dictionary<string, SavedItem> _shopItemCache = new();
    private static readonly Dictionary<string, int> _shopPriceCache = new();

    public static void ResetCache()
    {
        _shopItemCache.Clear();
        _shopPriceCache.Clear();
        Plugin.Log.LogInfo("商店随机缓存已清空");
    }

    public static SavedItem GetOrCreateShopItem(string permanentId, out int price)
    {
        if (_shopItemCache.TryGetValue(permanentId, out var cachedItem) && !IsItemOwned(cachedItem))
        {
            if (!_shopPriceCache.TryGetValue(permanentId, out price))
            {
                var rng = new Random(Plugin.RandomSeed.Value ^ permanentId.GetHashCode());
                price = GenerateRandomPrice(rng);
                _shopPriceCache[permanentId] = price;
            }
            return cachedItem;
        }

        var newItem = GenerateRandomShopItem(permanentId, out price);
        if (newItem == null)
        {
            // 保底：从所有物品中找一个可用的
            var allItems = ItemRandomizer.GetAllItems();
            if (allItems != null && allItems.Count > 0)
                newItem = allItems.FirstOrDefault(IsSafeForShop);
            if (newItem == null)
                return null;
        }
        _shopItemCache[permanentId] = newItem;
        _shopPriceCache[permanentId] = price;
        return newItem;
    }

    public static SavedItem GetOrCreateShopItem(string sceneName, int slotIndex, out int price)
    {
        return GetOrCreateShopItem($"{sceneName}_{slotIndex}", out price);
    }

    private static SavedItem GenerateRandomShopItem(string permanentId, out int price)
    {
        var rng = new Random(Plugin.RandomSeed.Value ^ permanentId.GetHashCode());
        var allItems = ItemRandomizer.GetAllItems();

        if (allItems == null || allItems.Count == 0)
        {
            price = 0;
            return null;
        }

        // 筛选符合条件的物品：未拥有且不是ToolCrest且安全（有图标且不在黑名单）
        var candidates = allItems.Where(item => !IsItemOwned(item) && IsSafeForShop(item)).ToList();
        if (candidates.Count == 0)
        {
            Plugin.Log.LogWarning($"商店随机池为空，使用所有物品中的第一个 (永久ID: {permanentId})");
            price = GenerateRandomPrice(rng);
            return allItems.FirstOrDefault(IsSafeForShop);
        }

        var index = rng.Next(candidates.Count);
        var selected = candidates[index];
        price = GenerateRandomPrice(rng);
        return selected;
    }

    private static bool IsItemOwned(SavedItem item)
    {
        try { return !item.CanGetMore(); }
        catch { return false; }
    }

    private static bool IsSafeForShop(SavedItem item)
    {
        if (item == null) return false;
        // 排除黑名单中的物品（复用 ItemRandomizer 的黑名单）
        if (ItemRandomizer.ExcludedNames.Contains(item.name))
            return false;
        // 排除 ToolCrest
        if (item is ToolCrest) return false;
        // 排除没有图标的物品（避免黑屏）
        try
        {
            if (item.GetPopupIcon() == null) return false;
        }
        catch { return false; }
        return true;
    }

    private static int GenerateRandomPrice(Random rng)
    {
        return rng.Next(2) == 0 ? rng.Next(1, 100) : rng.Next(100, 301);
    }
}