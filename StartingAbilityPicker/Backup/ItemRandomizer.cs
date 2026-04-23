// Decompiled with JetBrains decompiler
// Type: StartingAbilityPicker.ItemRandomizer
// Assembly: StartingAbilityPicker, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4695D065-A369-4338-8DBD-5D0C146838A7
// Assembly location: E:\a\HardItemRandomizer\plugins\StartingAbilityPicker.dll

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable enable
namespace StartingAbilityPicker;

public static class ItemRandomizer
{
  private static List<SavedItem> _allItems;
  private static Random _rng = new Random();

  public static SavedItem GetRandomItem()
  {
    if (ItemRandomizer._allItems == null)
      ItemRandomizer._allItems = ((IEnumerable<SavedItem>) Resources.FindObjectsOfTypeAll<SavedItem>()).Where<SavedItem>((Func<SavedItem, bool>) (item => item is CollectableItem || item is ToolBase)).ToList<SavedItem>();
    return ItemRandomizer._allItems.Count == 0 ? (SavedItem) null : ItemRandomizer._allItems[ItemRandomizer._rng.Next(ItemRandomizer._allItems.Count)];
  }
}
