using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;
namespace SilksongItemRandomizer;

public static class ItemRandomizer
{
    private static List<SavedItem> _allItems;
    private static Random _rng;

    public static Random Rng => _rng;

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

    public static void Initialize(int seed)
    {
        _allItems = Resources.FindObjectsOfTypeAll<SavedItem>()
            .Where(item => (item is CollectableItem || item is ToolBase || item is CollectableRelic)
                           && !ExcludedNames.Contains(item.name))
            .ToList();

        if (_allItems.Count == 0)
        {
            Plugin.Log.LogWarning("No eligible items found! Current scene: " + SceneManager.GetActiveScene().name);
        }
        else
        {
            Plugin.Log.LogInfo($"Found {_allItems.Count} eligible items.");
            _rng = (seed == 0) ? new Random() : new Random(seed);
        }
    }

    public static SavedItem GetRandomItem()
    {
        if (_allItems == null || _allItems.Count == 0 || _rng == null)
        {
            Plugin.Log.LogWarning("GetRandomItem: _allItems or _rng is null");
            return null;
        }

        for (int i = 0; i < 100; i++)
        {
            int index = _rng.Next(_allItems.Count);
            SavedItem item = _allItems[index];
            if (item != null && !ExcludedNames.Contains(item.name))
                return item;
        }

        Plugin.Log.LogWarning("GetRandomItem failed after 100 attempts, returning null");
        return null;
    }

    public static bool RandomChance(float probability)
    {
        return _rng != null && _rng.NextDouble() < probability;
    }

    public static SavedItem PeekRandomItem(Random rng)
    {
        if (_allItems == null || _allItems.Count == 0 || rng == null)
            return null;

        for (int i = 0; i < 100; i++)
        {
            int index = rng.Next(_allItems.Count);
            SavedItem item = _allItems[index];
            if (item != null && !ExcludedNames.Contains(item.name))
                return item;
        }

        return null;
    }

    public static List<SavedItem> GetAllItems() => _allItems;
}