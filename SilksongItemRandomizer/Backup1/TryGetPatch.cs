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

        List<string> missing = new List<string>();
        if (!hasUpward) missing.Add("upward");
        if (!hasLeft) missing.Add("left");
        if (!hasRight) missing.Add("right");

        if (missing.Count == 0 || ItemRandomizer.Rng == null || ItemRandomizer.Rng.NextDouble() > 0.05)
            return;

        string chosen = missing[ItemRandomizer.Rng.Next(missing.Count)];
        PlayerData pd = PlayerData.instance;
        if (pd == null) return;

        switch (chosen)
        {
            case "upward":
                upwardField.SetValue(null, true);
                pd.SetBool("AllowUpwardAttack", true);
                Plugin.Log.LogInfo("Attack direction unlocked via item: upward (saved to PlayerData)");
                break;
            case "left":
                leftField.SetValue(null, true);
                pd.SetBool("AllowLeftAttack", true);
                Plugin.Log.LogInfo("Attack direction unlocked via item: left (saved to PlayerData)");
                break;
            case "right":
                rightField.SetValue(null, true);
                pd.SetBool("AllowRightAttack", true);
                Plugin.Log.LogInfo("Attack direction unlocked via item: right (saved to PlayerData)");
                break;
        }
    }
}