// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.ItemRandomizer
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable
namespace SilksongItemRandomizer;

public static class ItemRandomizer
{
  private static List<SavedItem>? _allItems;
  private static Random? _rng;
  public static readonly HashSet<string> ExcludedNames = new HashSet<string>()
  {
    "Steel Spines",
    "Common Spine",
    "Plasmium",
    "Sliver Bell",
    "Seared Organ",
    "Shredded Organ",
    "Skewered Organ",
    "Ragpelt"
  };

  public static Random? Rng => ItemRandomizer._rng;

  public static void Initialize(int seed)
  {
    ItemRandomizer._allItems = ((IEnumerable<SavedItem>) Resources.FindObjectsOfTypeAll<SavedItem>()).Where<SavedItem>((Func<SavedItem, bool>) (item =>
    {
      switch (item)
      {
        case CollectableItem _:
        case ToolBase _:
        case CollectableRelic _:
          return !ItemRandomizer.ExcludedNames.Contains(((Object) item).name);
        default:
          return false;
      }
    })).ToList<SavedItem>();
    if (ItemRandomizer._allItems.Count == 0)
    {
      ManualLogSource log = Plugin.Log;
      Scene activeScene = SceneManager.GetActiveScene();
      string str = "No eligible items found! Current scene: " + ((Scene) ref activeScene).name;
      log.LogWarning((object) str);
    }
    else
    {
      Plugin.Log.LogInfo((object) $"Found {ItemRandomizer._allItems.Count} eligible items.");
      ItemRandomizer._rng = seed == 0 ? new Random() : new Random(seed);
    }
  }

  public static SavedItem? GetRandomItem()
  {
    if (ItemRandomizer._allItems == null || ItemRandomizer._allItems.Count == 0 || ItemRandomizer._rng == null)
    {
      Plugin.Log.LogWarning((object) "GetRandomItem: _allItems or _rng is null");
      return (SavedItem) null;
    }
    for (int index1 = 0; index1 < 100; ++index1)
    {
      int index2 = ItemRandomizer._rng.Next(ItemRandomizer._allItems.Count);
      SavedItem allItem = ItemRandomizer._allItems[index2];
      if (!Object.op_Equality((Object) allItem, (Object) null) && !ItemRandomizer.ExcludedNames.Contains(((Object) allItem).name))
        return allItem;
    }
    Plugin.Log.LogWarning((object) "GetRandomItem failed after 100 attempts, returning null");
    return (SavedItem) null;
  }

  public static bool RandomChance(float probability)
  {
    return ItemRandomizer._rng != null && ItemRandomizer._rng.NextDouble() < (double) probability;
  }

  public static SavedItem? PeekRandomItem(Random rng)
  {
    if (ItemRandomizer._allItems == null || ItemRandomizer._allItems.Count == 0 || rng == null)
      return (SavedItem) null;
    for (int index1 = 0; index1 < 100; ++index1)
    {
      int index2 = rng.Next(ItemRandomizer._allItems.Count);
      SavedItem allItem = ItemRandomizer._allItems[index2];
      if (!Object.op_Equality((Object) allItem, (Object) null) && !ItemRandomizer.ExcludedNames.Contains(((Object) allItem).name))
        return allItem;
    }
    return (SavedItem) null;
  }

  public static List<SavedItem> GetAllItems() => ItemRandomizer._allItems;
}
