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
    private const int SilkSpearPity = 200;
    private static SavedItem _silkSpearItem;

    private static string SilkSpearGivenFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "silkspear_given.txt");

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
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, value ? "true" : "false");
        }
        catch { }
    }

    public static void ResetSilkSpearState()
    {
        _silkSpearAttempts = 0;
        _silkSpearGiven = false;
        if (File.Exists(SilkSpearGivenFilePath)) File.Delete(SilkSpearGivenFilePath);
        Plugin.Log.LogInfo("[丝矛独立补丁] 保底状态已重置");
    }

    public static void ResetCounter()
    {
        _silkSpearAttempts = 0;
        _silkSpearGiven = false;
        Plugin.Log.LogInfo("[丝矛独立补丁] 计数器已重置");
    }

    private static void Postfix(bool __result)
    {
        if (!__result || _silkSpearItem == null || _silkSpearGiven) return;

        _silkSpearAttempts++;
        if (_silkSpearAttempts < SilkSpearPity) return;

        Plugin.Log.LogInfo($"[丝矛独立补丁] 保底触发（第{_silkSpearAttempts}次）");

        // 直接给予丝矛，不再需要 ToolUnlockPatch 标志
        try
        {
            var pd = PlayerData.instance;
            var field = typeof(PlayerData).GetField("hasSilkSpear", BindingFlags.Instance | BindingFlags.Public);
            if (pd != null && field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(pd, true);
                Plugin.Log.LogInfo("[丝矛独立补丁] 丝矛成功给予");
                Plugin.ShowNotification("获得丝矛！");
            }
            else
            {
                Plugin.Log.LogError("[丝矛独立补丁] 丝矛给予失败：找不到PlayerData或hasSilkSpear字段");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[丝矛独立补丁] 直接写入hasSilkSpear异常: {ex}");
        }

        _silkSpearGiven = true;
        SaveBoolToFile(SilkSpearGivenFilePath, true);
    }
}