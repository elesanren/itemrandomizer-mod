using HarmonyLib;
using System;
using System.Collections.Generic;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CollectableItemPickup), "DoPickupAction")]
public class PickupPatch
{
    private static void Postfix(CollectableItemPickup __instance)
    {
        try
        {
            if (__instance == null) return;

            SavedItem originalItem = __instance.Item;
            if (originalItem == null) return;

            // 排除物品不做任何处理，直接返回
            if (ItemRandomizer.ExcludedNames.Contains(originalItem.name))
                return;

            // 获取随机替换物品
            SavedItem randomItem = ItemRandomizer.GetRandomItem();
            if (randomItem == null) return;

            // 给予随机物品
            randomItem.TryGet(false, true);

            Plugin.Log.LogInfo($"拾取点 {originalItem.name} → 额外给予 {randomItem.name}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"PickupPatch 出错: {ex}");
        }
    }
}