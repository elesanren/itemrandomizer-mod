using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CurrencyObjectBase), "Collect")]
public class CurrencyCollectPatch
{
    private static int _consecutiveMisses;
    private const int PITY_THRESHOLD = 105;
    private static int _dropCount;
    private static bool _hasGivenKey;
    private const int KEY_GUARANTEE_MAX = 30;
    private static readonly string keyName = "Simple Key";
    private static int _silkSpearAttempts;
    private static bool _silkSpearGiven;
    private const int SILK_SPEAR_PITY = 200;
    private static readonly SavedItem _silkSpearItem = Resources.FindObjectsOfTypeAll<SavedItem>().FirstOrDefault(item => item.name == "Silk Spear");

    private static string KeyGivenFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "key_given.txt");

    private static void LoadBoolFromFile(string path, ref bool target)
    {
        try
        {
            if (File.Exists(path))
                target = File.ReadAllText(path).Trim() == "true";
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"加载状态文件失败 {path}: {ex}");
        }
    }

    private static void SaveBoolToFile(string path, bool value)
    {
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, value ? "true" : "false");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存状态文件失败 {path}: {ex}");
        }
    }

    public static void ResetKeyState()
    {
        _hasGivenKey = false;
        string path = KeyGivenFilePath;
        if (File.Exists(path)) File.Delete(path);
    }

    static CurrencyCollectPatch()
    {
        LoadBoolFromFile(KeyGivenFilePath, ref _hasGivenKey);

        if (_silkSpearItem == null)
            Plugin.Log.LogWarning("未找到丝矛物品（Silk Spear），丝矛200次保底将禁用。");
        else
            Plugin.Log.LogInfo($"丝矛物品已找到: {_silkSpearItem.name}");
    }

    public static void ResetCounters()
    {
        _consecutiveMisses = 0;
        _dropCount = 0;
        _hasGivenKey = false;
        _silkSpearAttempts = 0;
        _silkSpearGiven = false;
        Plugin.Log.LogInfo("货币保底计数器已重置");
    }

    private static void Postfix(bool __result)
    {
        try
        {
            if (!__result) return;

            _dropCount++;
            _silkSpearAttempts++;

            if (!_hasGivenKey && _dropCount <= KEY_GUARANTEE_MAX)
            {
                SavedItem key = Resources.FindObjectsOfTypeAll<SavedItem>().FirstOrDefault(item => item.name == keyName);
                if (key != null)
                {
                    key.TryGet(false, true);
                    Plugin.Log.LogInfo($"钥匙保底触发（第{_dropCount}次）");
                    _hasGivenKey = true;
                    SaveBoolToFile(KeyGivenFilePath, true);
                }
            }

            if (_silkSpearItem != null && !_silkSpearGiven && _silkSpearAttempts >= SILK_SPEAR_PITY)
            {
                Plugin.Log.LogInfo("丝矛保底触发（直接写入PlayerData）");
                ToolUnlockPatch.IsGivingPityItem = true;
                try
                {
                    PlayerData pd = PlayerData.instance;
                    var field = typeof(PlayerData).GetField("hasSilkSpear", BindingFlags.Instance | BindingFlags.Public);
                    if (pd != null && field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(pd, true);
                        Plugin.Log.LogInfo("丝矛保底成功给予");
                        Plugin.ShowNotification("获得丝矛！");
                    }
                    else
                    {
                        Plugin.Log.LogError("丝矛保底失败：找不到PlayerData或hasSilkSpear字段");
                    }
                }
                finally
                {
                    ToolUnlockPatch.IsGivingPityItem = false;
                }
                _silkSpearGiven = true;
            }
            else if (_consecutiveMisses >= PITY_THRESHOLD - 1 || ItemRandomizer.RandomChance(1f / PITY_THRESHOLD))
            {
                SavedItem randomItem = ItemRandomizer.GetRandomItem();
                if (randomItem != null)
                    randomItem.TryGet(false, true);
                _consecutiveMisses = 0;
            }
            else
            {
                _consecutiveMisses++;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Exception in CurrencyCollectPatch: {ex}");
        }
    }
}