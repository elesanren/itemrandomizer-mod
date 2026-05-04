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
    public static bool IsGivingPityItem;   // ★ 保底标志，为 true 时不随机

    public static void Initialize()
    {
        _allTools = Resources.FindObjectsOfTypeAll<ToolItem>().ToList();
        if (_allTools.Count == 0)
            Plugin.Log.LogWarning("No ToolItem found! Tool randomizer may not work.");
        else
            Plugin.Log.LogInfo($"Found {_allTools.Count} ToolItem instances for randomization.");
    }

    [HarmonyPatch(typeof(ToolItem), "Unlock")]
    [HarmonyPrefix]
    private static bool Prefix(ToolItem __instance, Action afterTutorialMsg, ToolItem.PopupFlags popupFlags)
    {
        try
        {
            if (IsGivingPityItem)    // ★ 保底给予丝矛时直接放行
                return true;

            if (IsShopPurchase)
            {
                Plugin.Log.LogInfo($"商店购买工具 {__instance?.name}，跳过随机");
                return true;
            }

            if (_allTools == null || _allTools.Count == 0)
            {
                Initialize();
                if (_allTools == null || _allTools.Count == 0)
                {
                    Plugin.Log.LogWarning("Tool list empty, cannot randomize.");
                    return true;
                }
            }

            ToolItem randomTool = _allTools[ItemRandomizer.Rng.Next(_allTools.Count)];
            if (randomTool == __instance)
                return true;

            Plugin.Log.LogInfo($"Randomizing tool: {__instance?.name ?? "null"} -> {randomTool.name}");
            randomTool.Unlock(afterTutorialMsg, popupFlags);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Exception in ToolUnlockPatch.Prefix: {ex}");
            return true;
        }
    }
}