// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.ShopRandomizer
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

public static class ShopRandomizer
{
  private static Dictionary<string, SavedItem> _shopItemCache = new Dictionary<string, SavedItem>();
  private static Dictionary<string, int> _shopPriceCache = new Dictionary<string, int>();

  public static void ResetCache()
  {
    ShopRandomizer._shopItemCache.Clear();
    ShopRandomizer._shopPriceCache.Clear();
    Plugin.Log.LogInfo((object) "商店随机缓存已清空");
  }

  public static SavedItem GetOrCreateShopItem(string permanentId, out int price)
  {
    SavedItem shopItem;
    if (ShopRandomizer._shopItemCache.TryGetValue(permanentId, out shopItem))
    {
      if (!ShopRandomizer.IsItemOwned(shopItem))
      {
        int num;
        price = ShopRandomizer._shopPriceCache.TryGetValue(permanentId, out num) ? num : ShopRandomizer.GetDefaultPrice(shopItem);
        return shopItem;
      }
      Plugin.Log.LogInfo((object) $"商店槽位 {permanentId} 的物品 {((Object) shopItem).name} 已拥有，重新生成");
    }
    string[] source = permanentId.Split('_');
    int price1;
    SavedItem randomShopItem = ShopRandomizer.GenerateRandomShopItem(string.Join("_", ((IEnumerable<string>) source).Take<string>(source.Length - 1)), int.Parse(((IEnumerable<string>) source).Last<string>()), permanentId, out price1);
    ShopRandomizer._shopItemCache[permanentId] = randomShopItem;
    ShopRandomizer._shopPriceCache[permanentId] = price1;
    price = price1;
    return randomShopItem;
  }

  public static SavedItem GetOrCreateShopItem(string sceneName, int slotIndex, out int price)
  {
    return ShopRandomizer.GetOrCreateShopItem($"{sceneName}_{slotIndex}", out price);
  }

  private static SavedItem GenerateRandomShopItem(
    string sceneName,
    int originalIndex,
    string permanentId,
    out int price)
  {
    Random rng = new Random(Plugin.RandomSeed.Value ^ permanentId.GetHashCode());
    List<SavedItem> allItems = ItemRandomizer.GetAllItems();
    if (allItems == null || allItems.Count == 0)
    {
      price = 0;
      return (SavedItem) null;
    }
    List<SavedItem> list = allItems.Where<SavedItem>((Func<SavedItem, bool>) (item => !ShopRandomizer.IsItemOwned(item) && !(item is ToolCrest))).ToList<SavedItem>();
    if (list.Count == 0)
    {
      Plugin.Log.LogWarning((object) $"商店随机池为空，使用所有物品中的第一个 (永久ID: {permanentId})");
      price = ShopRandomizer.GetDefaultPrice(allItems[0]);
      return allItems[0];
    }
    int index = rng.Next(list.Count);
    SavedItem randomShopItem = list[index];
    price = ShopRandomizer.GenerateRandomPrice(randomShopItem, rng);
    return randomShopItem;
  }

  private static bool IsItemOwned(SavedItem item)
  {
    try
    {
      if (!item.CanGetMore())
        return true;
    }
    catch
    {
    }
    return false;
  }

  private static int GenerateRandomPrice(SavedItem item, Random rng)
  {
    return Mathf.RoundToInt((float) ShopRandomizer.GetDefaultPrice(item) * (float) (rng.NextDouble() * 1.5 + 0.5));
  }

  private static int GetDefaultPrice(SavedItem item)
  {
    FieldInfo field = item.GetType().GetField("cost", BindingFlags.Instance | BindingFlags.Public);
    return field != (FieldInfo) null && field.FieldType == typeof (int) ? (int) field.GetValue((object) item) : 100;
  }
}
