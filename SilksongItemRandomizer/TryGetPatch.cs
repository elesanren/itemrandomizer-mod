using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(SavedItem), "TryGet")]
public static class TryGetPatch
{
    private static bool _isProcessing;

    private static void Postfix(SavedItem __instance, bool __result)
    {
        try
        {
            if (_isProcessing || !__result) return;

            if (__instance.name != "Rosary_Set_Frayed")
                RecentItemsUI.AddItem(__instance);

            _isProcessing = true;
            try
            {
                UnlockRandomAttack();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"解锁攻击方向时出错: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"TryGetPatch.Postfix 未处理异常: {ex}");
        }
    }

    private static void UnlockRandomAttack()
    {
        var pluginType = Type.GetType("StartingAbilityPicker.Plugin, StartingAbilityPicker");
        if (pluginType == null) return;

        var upwardField = pluginType.GetField("AllowUpwardAttack", BindingFlags.Public | BindingFlags.Static);
        var leftField = pluginType.GetField("AllowLeftAttack", BindingFlags.Public | BindingFlags.Static);
        var rightField = pluginType.GetField("AllowRightAttack", BindingFlags.Public | BindingFlags.Static);

        if (upwardField == null || leftField == null || rightField == null) return;

        var hasUpward = (bool)upwardField.GetValue(null);
        var hasLeft = (bool)leftField.GetValue(null);
        var hasRight = (bool)rightField.GetValue(null);

        var missing = new List<string>();
        if (!hasUpward) missing.Add("upward");
        if (!hasLeft) missing.Add("left");
        if (!hasRight) missing.Add("right");

        if (missing.Count == 0 || ItemRandomizer.Rng == null || ItemRandomizer.Rng.NextDouble() > 0.10)
            return;

        var chosen = missing[ItemRandomizer.Rng.Next(missing.Count)];
        var playerData = PlayerData.instance;
        if (playerData == null) return;

        switch (chosen)
        {
            case "upward":
                upwardField.SetValue(null, true);
                playerData.SetBool("AllowUpwardAttack", true);
                ShowAttackNotification("上劈");
                break;
            case "left":
                leftField.SetValue(null, true);
                playerData.SetBool("AllowLeftAttack", true);
                ShowAttackNotification("左劈");
                break;
            case "right":
                rightField.SetValue(null, true);
                playerData.SetBool("AllowRightAttack", true);
                ShowAttackNotification("右劈");
                break;
        }

        var saveAllMethod = pluginType.GetMethod("SaveAttackDirections", BindingFlags.Public | BindingFlags.Static);
        if (saveAllMethod != null)
        {
            saveAllMethod.Invoke(null, null);
            Plugin.Log.LogInfo("攻击方向配置已通过 SaveAttackDirections 完整保存");
        }
        else
        {
            Plugin.Log.LogError("未找到 SaveAttackDirections 方法，请确保 StartingAbilityPicker 已更新");
        }
    }

    private static void ShowAttackNotification(string direction)
    {
        Plugin.ShowNotification($"获得攻击方向: {direction}");
        Plugin.Log.LogInfo($"Attack direction unlocked via item: {direction} (saved to PlayerData)");
    }
}