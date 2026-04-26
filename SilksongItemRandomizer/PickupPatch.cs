using HarmonyLib;
using System;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CollectableItemPickup), "DoPickupAction")]
public class PickupPatch
{
    // ========== 特殊点拦截（Prefix）==========
    private static void Prefix(CollectableItemPickup __instance, ref bool __runOriginal)
    {
        try
        {
            if (!__runOriginal || __instance == null)
                return;

            SavedItem originalItem = __instance.Item;
            if (originalItem == null || ItemRandomizer.ExcludedNames.Contains(originalItem.name))
                return;

            // ★ 只有 CollectableRelic 或 ToolItem 才拦截 ★
            if (!(originalItem is CollectableRelic) && !(originalItem is ToolItem))
                return;   // 普通点放行

            // —— 以下是特殊点处理 ——
            SavedItem randomItem = ItemRandomizer.GetRandomItem();
            if (randomItem == null) return;

            // 记录坐标并加入销毁列表
            string key = $"{__instance.gameObject.scene.name}_{__instance.transform.position.x:F1}_{__instance.transform.position.y:F1}_{__instance.transform.position.z:F1}";
            Plugin.AddDestroyedPickupKey(key);

            Plugin.Log.LogInfo($"特殊点 {originalItem.name} → 替换为 {randomItem.name}");

            // 统一使用 TryGet 给予
            if (randomItem.TryGet(false, true))
            {
                RecentItemsUI.AddItem(randomItem);
                Plugin.Log.LogInfo($"特殊点物品 {randomItem.name} 给予成功");
            }
            else
            {
                Plugin.Log.LogError($"特殊点物品 {randomItem.name} 给予失败！");
            }

            // 阻止原生拾取并销毁拾取点
            __runOriginal = false;
            UnityEngine.Object.Destroy(__instance.gameObject);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"PickupPatch.Prefix 出错: {ex}");
        }
    }

    // ========== 普通点额外给予（Postfix）==========
    private static void Postfix(CollectableItemPickup __instance)
    {
        try
        {
            if (__instance == null) return;

            SavedItem originalItem = __instance.Item;
            if (originalItem == null) return;

            // 排除物品不处理
            if (ItemRandomizer.ExcludedNames.Contains(originalItem.name)) return;

            // 特殊点不在这里处理
            if (originalItem is CollectableRelic || originalItem is ToolItem) return;

            SavedItem randomItem = ItemRandomizer.GetRandomItem();
            if (randomItem == null) return;

            // 统一使用 TryGet 额外给予（不阻止原生拾取）
            if (randomItem.TryGet(false, true))
            {
                RecentItemsUI.AddItem(randomItem);
                Plugin.Log.LogInfo($"普通点 {originalItem.name} → 额外给予 {randomItem.name}");
            }
            else
            {
                Plugin.Log.LogError($"普通点物品 {randomItem.name} 给予失败！");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"PickupPatch.Postfix 出错: {ex}");
        }
    }
}