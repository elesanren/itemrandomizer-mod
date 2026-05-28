using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CurrencyObjectBase), "Collect")]
public static class CurrencyCollectPatch
{
    private static int _consecutiveMisses;
    private const int PityThreshold = 200;
    private static int _dropCount;
    private static bool _hasGivenKey;
    private const int KeyGuaranteeMax = 30;
    private const string KeyName = "Simple Key";
    private static readonly string KeyGivenFilePath = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "key_given.txt");

    static CurrencyCollectPatch()
    {
        LoadBoolFromFile(KeyGivenFilePath, ref _hasGivenKey);
    }

    public static void ResetCounters()
    {
        _consecutiveMisses = 0;
        _dropCount = 0;
        _hasGivenKey = false;
        Plugin.Log.LogInfo("货币保底计数器已重置");
    }

    public static void ResetKeyState()
    {
        _hasGivenKey = false;
        if (File.Exists(KeyGivenFilePath))
            File.Delete(KeyGivenFilePath);
    }

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
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, value ? "true" : "false");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存状态文件失败 {path}: {ex}");
        }
    }

    private static void Postfix(bool __result)
    {
        if (!__result) return;

        _dropCount++;

        // 钥匙保底（不变）
        if (!_hasGivenKey && _dropCount <= KeyGuaranteeMax)
        {
            var key = Resources.FindObjectsOfTypeAll<SavedItem>().FirstOrDefault(i => i.name == KeyName);
            if (key != null)
            {
                key.TryGet(false, true);
                Plugin.Log.LogInfo($"钥匙保底触发（第{_dropCount}次）");
                _hasGivenKey = true;
                SaveBoolToFile(KeyGivenFilePath, true);
            }
        }
        // 普通随机保底：改为混合奖励
        else if (_consecutiveMisses >= PityThreshold - 1 || ItemRandomizer.RandomChance(1f / PityThreshold))
        {
            ItemRandomizer.GiveRandomReward();   // ★ 修改点
            _consecutiveMisses = 0;
        }
        else
        {
            _consecutiveMisses++;
        }
    }
}