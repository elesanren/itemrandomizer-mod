using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace StartingAbilityPicker;

public static class ItemRandomizer
{
    private static List<SavedItem> _allItems;
    private static readonly Random _rng = new Random();

    public static SavedItem GetRandomItem()
    {
        if (_allItems == null)
        {
            _allItems = Resources.FindObjectsOfTypeAll<SavedItem>()
                .Where(item => item is CollectableItem || item is ToolBase)
                .ToList();
        }
        return _allItems.Count == 0 ? null : _allItems[_rng.Next(_allItems.Count)];
    }
}