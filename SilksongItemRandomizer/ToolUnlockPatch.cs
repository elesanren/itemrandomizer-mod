using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongItemRandomizer;

public static class ToolUnlockPatch
{
    private static List<ToolItem> _allTools;
    public static bool IsShopPurchase;
    public static bool IsGivingPityItem; // 保底标志，为 true 时不随机

    private static bool _isRandomizing; // 防止递归随机
    private static readonly HashSet<int> _processedInstanceIds = new(); // 记录已随机过的工具实例

    public static void Initialize()
    {
        _allTools = Resources.FindObjectsOfTypeAll<ToolItem>().ToList();
        if (_allTools.Count == 0)
            Plugin.Log.LogWarning("未找到 ToolItem 实例！工具随机化可能无法工作。");
        else
            Plugin.Log.LogInfo($"找到 {_allTools.Count} 个 ToolItem 实例用于随机化。");
    }

    public static void ResetProcessedIds()
    {
        _processedInstanceIds.Clear();
        Plugin.Log.LogInfo("工具随机化已处理实例记录已重置");
    }

    [HarmonyPatch(typeof(ToolItem), "Unlock")]
    [HarmonyPrefix]
    private static bool Prefix(ToolItem __instance, Action afterTutorialMsg, ToolItem.PopupFlags popupFlags)
    {
        try
        {
            // 如果正在递归随机中，直接放行原解锁（避免无限循环）
            if (_isRandomizing) return true;

            // 保底给予丝矛时直接放行
            if (IsGivingPityItem) return true;

            // 商店购买时跳过随机
            if (IsShopPurchase)
            {
                Plugin.Log.LogInfo($"商店购买工具 {__instance?.name}，跳过随机");
                return true;
            }

            // 如果该工具实例已经随机过，直接放行原解锁
            if (_processedInstanceIds.Contains(__instance.GetInstanceID()))
            {
                Plugin.Log.LogInfo($"工具 {__instance?.name} 已随机过，放行原解锁");
                return true;
            }

            // 确保工具列表已初始化
            if (_allTools == null || _allTools.Count == 0)
            {
                Initialize();
                if (_allTools == null || _allTools.Count == 0)
                {
                    Plugin.Log.LogWarning("工具列表为空，无法随机化。");
                    return true;
                }
            }

            var randomTool = _allTools[ItemRandomizer.Rng.Next(_allTools.Count)];
            if (randomTool == __instance)
            {
                // 随机到自身，记录已处理并放行原解锁
                _processedInstanceIds.Add(__instance.GetInstanceID());
                return true;
            }

            // 标记该工具实例已处理，避免重复随机
            _processedInstanceIds.Add(__instance.GetInstanceID());

            Plugin.Log.LogInfo($"随机化工具: {__instance?.name ?? "null"} -> {randomTool.name}");

            // 开始随机，设置递归标志
            _isRandomizing = true;
            try
            {
                randomTool.Unlock(afterTutorialMsg, popupFlags);
            }
            finally
            {
                _isRandomizing = false;
            }
            return false; // 阻止原始 Unlock
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"ToolUnlockPatch.Prefix 异常: {ex}");
            return true;
        }
    }
}