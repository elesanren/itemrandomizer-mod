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

    public static readonly HashSet<string> ExcludedNames = new()
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
            Plugin.Log.LogWarning("没有找到符合条件的物品！当前场景：" + SceneManager.GetActiveScene().name);
        }
        else
        {
            Plugin.Log.LogInfo($"找到 {_allItems.Count} 个符合条件的物品。");
            _rng = seed == 0 ? new Random() : new Random(seed);
        }
    }

    public static SavedItem GetRandomItem()
    {
        if (_allItems == null || _allItems.Count == 0 || _rng == null)
        {
            Plugin.Log.LogWarning("GetRandomItem: _allItems 或 _rng 为空");
            return null;
        }

        for (var i = 0; i < 100; i++)
        {
            var index = _rng.Next(_allItems.Count);
            var item = _allItems[index];
            if (item != null && !ExcludedNames.Contains(item.name))
                return item;
        }

        Plugin.Log.LogWarning("GetRandomItem 尝试 100 次后失败，返回 null");
        return null;
    }

    public static bool RandomChance(float probability) =>
        _rng != null && _rng.NextDouble() < probability;

    public static SavedItem PeekRandomItem(Random rng)
    {
        if (_allItems == null || _allItems.Count == 0 || rng == null)
            return null;

        for (var i = 0; i < 100; i++)
        {
            var index = rng.Next(_allItems.Count);
            var item = _allItems[index];
            if (item != null && !ExcludedNames.Contains(item.name))
                return item;
        }

        return null;
    }

    public static List<SavedItem> GetAllItems() => _allItems;

    // ========== 混合奖励：物品70%，技能20%，纹章10% ==========
    public static bool GiveRandomReward()
    {
        if (_rng == null) return false;

        int roll = _rng.Next(100);
        Plugin.Log.LogInfo($"[混合奖励] 随机值: {roll}");

        // 物品 0-69 (70%)
        if (roll < 85)
        {
            var item = GetRandomItem();
            if (item != null)
            {
                item.TryGet(false, true);
                Plugin.Log.LogInfo($"[混合奖励] 获得物品: {item.name}");
                return true;
            }
        }
        // 技能 70-89 (20%)
        else if (roll < 95)
        {
            try
            {
                SkillTriggerMod.SkillRandomizer.GiveRandomSkill();
                Plugin.Log.LogInfo("[混合奖励] 获得技能");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"技能随机失败: {ex}");
            }
        }
        // 纹章 90-99 (10%)
        else
        {
            var allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>()
                .Where(c => !CrestRandomizer.ExcludeFromPool.Contains(c.name) && !c.IsUnlocked)
                .ToList();
            if (allCrests.Count > 0)
            {
                var crest = allCrests[_rng.Next(allCrests.Count)];
                crest.Unlock();
                Plugin.Log.LogInfo($"[混合奖励] 获得纹章: {crest.name}");
                return true;
            }
            else
            {
                var fallbackItem = GetRandomItem();
                if (fallbackItem != null)
                {
                    fallbackItem.TryGet(false, true);
                    Plugin.Log.LogInfo($"[混合奖励] 无可用纹章，降级为物品: {fallbackItem.name}");
                    return true;
                }
            }
        }

        Plugin.Log.LogWarning("[混合奖励] 未获得任何奖励");
        return false;
    }
}