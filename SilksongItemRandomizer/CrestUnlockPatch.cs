using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(ToolCrest), "Unlock")]
public static class CrestUnlockPatch
{
    private static readonly HashSet<string> HunterSeries = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hunter", "Hunter_v2", "Hunter_v3"
    };

    private static List<ToolCrest> _allCrests;
    private static bool _isPatching;

    [HarmonyPrefix]
    private static bool Prefix(ToolCrest __instance)
    {
        if (_isPatching) return true;

        // 跳过猎人系列
        if (HunterSeries.Contains(__instance.name))
            return true;

        string mappedName = CrestRandomizer.GetMappedCrestName(__instance.name);
        if (mappedName == __instance.name)
            return true;

        if (_allCrests == null || _allCrests.Count == 0)
            _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();

        ToolCrest target = _allCrests.FirstOrDefault(c => c.name == mappedName);
        if (target == null || HunterSeries.Contains(target.name))
        {
            Plugin.Log.LogWarning($"目标纹章 {mappedName} 无效");
            return true;
        }

        _isPatching = true;
        try
        {
            // 1. 静默标记原始纹章为已解锁，让神龛关闭
            MarkCrestAsUnlocked(__instance);

            // 2. 真正触发目标纹章的解锁（这是获得物品的源头）
            Plugin.Log.LogInfo($"纹章给予替换: {__instance.name} -> {target.name}");
            target.Unlock();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"CrestUnlockPatch 出错: {ex}");
        }
        finally
        {
            _isPatching = false;
        }

        // 阻止原始 Unlock 执行
        return false;
    }

    private static void MarkCrestAsUnlocked(ToolCrest crest)
    {
        if (crest.IsUnlocked) return;
        try
        {
            ToolCrestsData.Data newData = default;
            newData.IsUnlocked = true;
            if (crest.Slots != null && crest.Slots.Length > 0)
            {
                newData.Slots = crest.Slots.Select(s => new ToolCrestsData.SlotData
                {
                    IsUnlocked = !s.IsLocked
                }).ToList();
            }
            else
            {
                newData.Slots = new List<ToolCrestsData.SlotData>();
            }
            PlayerData.instance.ToolEquips.SetData(crest.name, newData);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"标记纹章已解锁失败: {ex}");
        }
    }
}