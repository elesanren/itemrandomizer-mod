using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public static class ShopRandomizer
{
    private static Dictionary<string, SavedItem> _shopItemCache = new();
    private static Dictionary<string, int> _shopPriceCache = new();

    public static void ResetCache()
    {
        _shopItemCache.Clear();
        _shopPriceCache.Clear();
        Plugin.Log.LogInfo("商店随机缓存已清空");
    }

    public static SavedItem GetOrCreateShopItem(string permanentId, out int price)
    {
        if (_shopItemCache.TryGetValue(permanentId, out SavedItem cachedItem) && !IsItemOwned(cachedItem))
        {
            price = _shopPriceCache.TryGetValue(permanentId, out int cachedPrice) ? cachedPrice : GetDefaultPrice(cachedItem);
            return cachedItem;
        }

        string[] parts = permanentId.Split('_');
        string sceneName = string.Join("_", parts.Take(parts.Length - 1));
        int originalIndex = int.Parse(parts.Last());

        SavedItem newItem = GenerateRandomShopItem(sceneName, originalIndex, permanentId, out price);
        _shopItemCache[permanentId] = newItem;
        _shopPriceCache[permanentId] = price;
        return newItem;
    }

    public static SavedItem GetOrCreateShopItem(string sceneName, int slotIndex, out int price)
    {
        return GetOrCreateShopItem($"{sceneName}_{slotIndex}", out price);
    }

    private static SavedItem GenerateRandomShopItem(string sceneName, int originalIndex, string permanentId, out int price)
    {
        Random rng = new Random(Plugin.RandomSeed.Value ^ permanentId.GetHashCode());
        List<SavedItem> allItems = ItemRandomizer.GetAllItems();

        if (allItems == null || allItems.Count == 0)
        {
            price = 0;
            return null;
        }

        List<SavedItem> candidates = allItems.Where(item => !IsItemOwned(item) && !(item is ToolCrest)).ToList();
        if (candidates.Count == 0)
        {
            Plugin.Log.LogWarning($"商店随机池为空，使用所有物品中的第一个 (永久ID: {permanentId})");
            price = GetDefaultPrice(allItems[0]);
            return allItems[0];
        }

        int index = rng.Next(candidates.Count);
        SavedItem selected = candidates[index];
        price = GenerateRandomPrice(selected, rng);
        return selected;
    }

    private static bool IsItemOwned(SavedItem item)
    {
        try
        {
            return !item.CanGetMore();
        }
        catch
        {
            return false;
        }
    }

    private static int GenerateRandomPrice(SavedItem item, Random rng)
    {
        int basePrice = GetDefaultPrice(item);
        float multiplier = (float)(rng.NextDouble() * 1.5 + 0.5);
        return Mathf.RoundToInt(basePrice * multiplier);
    }

    private static int GetDefaultPrice(SavedItem item)
    {
        FieldInfo field = item.GetType().GetField("cost", BindingFlags.Instance | BindingFlags.Public);
        if (field != null && field.FieldType == typeof(int))
            return (int)field.GetValue(item);
        return 100;
    }
}