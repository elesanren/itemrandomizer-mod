using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CurrencyObjectBase), "Collect")]
public static class SilkSpearPityPatch
{
    private static int _silkSpearAttempts = 0;
    private static bool _silkSpearGiven = false;
    private const int SILK_SPEAR_PITY = 200;
    private static SavedItem _silkSpearItem;
    public static bool IsGivingSilkSpear = false;

    static SilkSpearPityPatch()
    {
        _silkSpearItem = Resources.FindObjectsOfTypeAll<SavedItem>()
            .FirstOrDefault(item => item.name == "Silk Spear");

        if (_silkSpearItem == null)
            Plugin.Log.LogWarning("[丝矛独立补丁] 未找到丝矛物品，保底将禁用。");
        else
            Plugin.Log.LogInfo("[丝矛独立补丁] 丝矛物品已找到: " + _silkSpearItem.name);
    }

    public static void ResetCounter()
    {
        _silkSpearAttempts = 0;
        _silkSpearGiven = false;
        Plugin.Log.LogInfo("[丝矛独立补丁] 计数器已重置");
    }

    private static void Postfix(bool __result)
    {
        if (!__result || _silkSpearItem == null || _silkSpearGiven)
            return;

        _silkSpearAttempts++;

        if (_silkSpearGiven || _silkSpearAttempts < SILK_SPEAR_PITY)
            return;

        Plugin.Log.LogInfo($"[丝矛独立补丁] 保底触发（第{_silkSpearAttempts}次）");

        bool success = false;
        IsGivingSilkSpear = true;

        if (_silkSpearItem is ToolItem toolItem)
        {
            try
            {
                MethodInfo method = typeof(ToolItem).GetMethod("Unlock", Type.EmptyTypes);
                if (method == null)
                    method = typeof(ToolItem).GetMethod("Unlock", new Type[] { typeof(Action), typeof(ToolItem.PopupFlags) });

                if (method != null)
                {
                    if (method.GetParameters().Length == 0)
                        method.Invoke(toolItem, null);
                    else
                        method.Invoke(toolItem, new object[] { null, (ToolItem.PopupFlags)3 });

                    success = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[丝矛独立补丁] Unlock 失败: {ex}");
            }
        }
        else
        {
            success = _silkSpearItem.TryGet(false, true);
        }

        IsGivingSilkSpear = false;

        if (success)
        {
            Plugin.Log.LogInfo("[丝矛独立补丁] 丝矛成功给予");
            Plugin.ShowNotification("获得丝矛！");
        }
        else
        {
            Plugin.Log.LogError("[丝矛独立补丁] 丝矛给予失败");
        }

        _silkSpearGiven = true;
    }
}