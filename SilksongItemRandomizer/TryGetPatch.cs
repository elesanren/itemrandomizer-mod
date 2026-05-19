using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(SavedItem), "TryGet")]
public class TryGetPatch
{
    private static bool _isProcessing;

    private static void Postfix(SavedItem __instance, bool __result)
    {
        try
        {
            if (_isProcessing || !__result)
                return;

            if (__instance.name != "Rosary_Set_Frayed")
                RecentItemsUI.AddItem(__instance);

            _isProcessing = true;
            try
            {
                UnlockRandomAttack();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Error unlocking attack: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Unhandled exception in TryGetPatch.Postfix: {ex}");
        }
    }

    private static void UnlockRandomAttack()
    {
        Type type = Type.GetType("StartingAbilityPicker.Plugin, StartingAbilityPicker");
        if (type == null) return;

        FieldInfo upwardField = type.GetField("AllowUpwardAttack", BindingFlags.Public | BindingFlags.Static);
        FieldInfo leftField = type.GetField("AllowLeftAttack", BindingFlags.Public | BindingFlags.Static);
        FieldInfo rightField = type.GetField("AllowRightAttack", BindingFlags.Public | BindingFlags.Static);

        if (upwardField == null || leftField == null || rightField == null) return;

        bool hasUpward = (bool)upwardField.GetValue(null);
        bool hasLeft = (bool)leftField.GetValue(null);
        bool hasRight = (bool)rightField.GetValue(null);

        List<string> missing = new();
        if (!hasUpward) missing.Add("upward");
        if (!hasLeft) missing.Add("left");
        if (!hasRight) missing.Add("right");

        if (missing.Count == 0 || ItemRandomizer.Rng == null || ItemRandomizer.Rng.NextDouble() > 0.10)
            return;

        string chosen = missing[ItemRandomizer.Rng.Next(missing.Count)];
        PlayerData pd = PlayerData.instance;
        if (pd == null) return;

        switch (chosen)
        {
            case "upward":
                upwardField.SetValue(null, true);
                pd.SetBool("AllowUpwardAttack", true);
                Plugin.ShowNotification("获得攻击方向: 上劈");
                Plugin.Log.LogInfo("Attack direction unlocked via item: upward (saved to PlayerData)");
                break;
            case "left":
                leftField.SetValue(null, true);
                pd.SetBool("AllowLeftAttack", true);
                Plugin.ShowNotification("获得攻击方向: 左劈");
                Plugin.Log.LogInfo("Attack direction unlocked via item: left (saved to PlayerData)");
                break;
            case "right":
                rightField.SetValue(null, true);
                pd.SetBool("AllowRightAttack", true);
                Plugin.ShowNotification("获得攻击方向: 右劈");
                Plugin.Log.LogInfo("Attack direction unlocked via item: right (saved to PlayerData)");
                break;
        }

        // ★ 关键修复：完整保存攻击方向配置（PlayerData + ability_config.json + Config.Save）
        MethodInfo saveAllMethod = type.GetMethod("SaveAttackDirections", BindingFlags.Public | BindingFlags.Static);
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
}