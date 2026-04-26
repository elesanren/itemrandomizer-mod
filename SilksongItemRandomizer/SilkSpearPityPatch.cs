using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CurrencyObjectBase), "Collect")]
public static class SilkSpearPityPatch
{
    private static int _silkSpearAttempts;
    private static bool _silkSpearGiven;
    private const int SILK_SPEAR_PITY = 200;
    private static SavedItem _silkSpearItem;
    public static bool IsGivingSilkSpear;

    private static string SilkSpearGivenFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "silkspear_given.txt");

    private static void LoadBoolFromFile(string path, ref bool target)
    {
        try
        {
            if (File.Exists(path))
                target = File.ReadAllText(path).Trim() == "true";
        }
        catch { }
    }

    private static void SaveBoolToFile(string path, bool value)
    {
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, value ? "true" : "false");
        }
        catch { }
    }

    public static void ResetSilkSpearState()
    {
        _silkSpearAttempts = 0;
        _silkSpearGiven = false;
        string path = SilkSpearGivenFilePath;
        if (File.Exists(path)) File.Delete(path);
        Plugin.Log.LogInfo("[丝矛独立补丁] 保底状态已重置");
    }

    static SilkSpearPityPatch()
    {
        LoadBoolFromFile(SilkSpearGivenFilePath, ref _silkSpearGiven);

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
        SaveBoolToFile(SilkSpearGivenFilePath, true);
    }
}