using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public static class CrestRandomizer
{
    private static Dictionary<string, string> _crestMappings = new();
    private static List<ToolCrest> _allCrests;
    private static HashSet<string> _unlockedCrests = new();

    private static readonly string MappingsPath = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "crest_mappings.json");
    private static readonly string UnlockedPath = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "unlocked_crests.json");

    // 排除池（初始纹章不参与随机）
    public static readonly HashSet<string> ExcludeFromPool = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hunter", "Hunter_v2", "Hunter_v3", "Witch_v2", "Cursed"
    };

    // 随机开关（禁用期间不产生新映射）
    private static bool _randomEnabled = true;
    public static void DisableRandom() => _randomEnabled = false;
    public static void EnableRandom() => _randomEnabled = true;

    // 新增：最后解锁的纹章（用于重生时直接替换）
    public static string LastUnlockedCrest { get; private set; } = null;

    public static List<ToolCrest> CrestList => _allCrests;

    public static void Initialize()
    {
        _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();
        LoadMappings();
        LoadUnlockedCrests();
        EnsureInitialCrests();
        Plugin.Log.LogInfo($"纹章随机初始化完成，已有 {_crestMappings.Count} 个映射，已解锁 {_unlockedCrests.Count} 个纹章，最后解锁: {LastUnlockedCrest}");
    }

    public static void ResetMappings()
    {
        _crestMappings.Clear();
        if (File.Exists(MappingsPath)) File.Delete(MappingsPath);
        ResetUnlockedCrests();
    }

    private static void LoadMappings()
    {
        try
        {
            if (File.Exists(MappingsPath))
            {
                var json = File.ReadAllText(MappingsPath);
                _crestMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
            }
            else
            {
                _crestMappings.Clear();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"加载纹章映射失败: {ex}");
            _crestMappings.Clear();
        }
    }

    private static void SaveMappings()
    {
        try
        {
            var dir = Path.GetDirectoryName(MappingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(_crestMappings, Formatting.Indented);
            File.WriteAllText(MappingsPath, json);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存纹章映射失败: {ex}");
        }
    }

    private static void LoadUnlockedCrests()
    {
        try
        {
            if (File.Exists(UnlockedPath))
            {
                var json = File.ReadAllText(UnlockedPath);
                _unlockedCrests = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
            }
            else
            {
                _unlockedCrests = new HashSet<string>();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"加载已解锁纹章列表失败: {ex}");
            _unlockedCrests = new HashSet<string>();
        }
    }

    private static void SaveUnlockedCrests()
    {
        try
        {
            var dir = Path.GetDirectoryName(UnlockedPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(_unlockedCrests, Formatting.Indented);
            File.WriteAllText(UnlockedPath, json);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存已解锁纹章列表失败: {ex}");
        }
    }

    private static void EnsureInitialCrests()
    {
        if (_unlockedCrests.Count == 0)
        {
            _unlockedCrests.Add("Hunter");
            SaveUnlockedCrests();
            LastUnlockedCrest = "Hunter";   // 初始纹章
        }
        foreach (string crestName in _unlockedCrests)
        {
            var crest = _allCrests.FirstOrDefault(c => c.name == crestName);
            if (crest != null && !crest.IsUnlocked)
            {
                UnlockCrestDirectly(crest);
            }
        }
    }

    private static void UnlockCrestDirectly(ToolCrest crest)
    {
        var data = crest.SaveData;
        data.IsUnlocked = true;
        crest.SaveData = data;
    }

    public static void AddUnlockedCrest(string crestName)
    {
        if (_unlockedCrests.Add(crestName))
        {
            SaveUnlockedCrests();
            var crest = _allCrests.FirstOrDefault(c => c.name == crestName);
            if (crest != null && !crest.IsUnlocked)
            {
                UnlockCrestDirectly(crest);
            }
            // 记录最后解锁的纹章
            LastUnlockedCrest = crestName;
            Plugin.Log.LogInfo($"记录最后解锁纹章: {LastUnlockedCrest}");
        }
    }

    public static void ResetUnlockedCrests()
    {
        _unlockedCrests.Clear();
        if (File.Exists(UnlockedPath)) File.Delete(UnlockedPath);
        EnsureInitialCrests();
    }

    // 用于解锁时调用，防止链式映射
    public static string GetMappedCrestName(string sourceCrestName)
    {
        // 随机禁用期间，不进行任何映射
        if (!_randomEnabled)
            return sourceCrestName;

        // 已经解锁的纹章不再映射（避免链式）
        if (_unlockedCrests.Contains(sourceCrestName))
            return sourceCrestName;

        if (_crestMappings.TryGetValue(sourceCrestName, out var existing))
            return existing;

        if (_allCrests == null || _allCrests.Count == 0)
            _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();

        var candidates = _allCrests
            .Where(c => !ExcludeFromPool.Contains(c.name) && !c.name.Contains("_v") && c.name != sourceCrestName)
            .ToList();

        string targetName;
        if (candidates.Count == 0)
        {
            targetName = sourceCrestName;
        }
        else
        {
            var rng = new Random(Plugin.RandomSeed.Value ^ sourceCrestName.GetHashCode());
            targetName = candidates[rng.Next(candidates.Count)].name;
        }

        _crestMappings[sourceCrestName] = targetName;
        SaveMappings();
        Plugin.Log.LogInfo($"新映射: {sourceCrestName} -> {targetName}");
        return targetName;
    }

    // 用于重生时调用，强制获取映射目标（绕过已解锁检查）
    public static string GetMappedCrestNameForRespawn(string sourceCrestName)
    {
        // 随机禁用期间，不进行任何映射
        if (!_randomEnabled)
            return sourceCrestName;

        // 直接查表，不检查 _unlockedCrests
        if (_crestMappings.TryGetValue(sourceCrestName, out var existing))
            return existing;

        // 没有映射才生成新映射
        if (_allCrests == null || _allCrests.Count == 0)
            _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();

        var candidates = _allCrests
            .Where(c => !ExcludeFromPool.Contains(c.name) && !c.name.Contains("_v") && c.name != sourceCrestName)
            .ToList();

        string targetName;
        if (candidates.Count == 0)
        {
            targetName = sourceCrestName;
        }
        else
        {
            var rng = new Random(Plugin.RandomSeed.Value ^ sourceCrestName.GetHashCode());
            targetName = candidates[rng.Next(candidates.Count)].name;
        }

        _crestMappings[sourceCrestName] = targetName;
        SaveMappings();
        Plugin.Log.LogInfo($"新映射: {sourceCrestName} -> {targetName}");
        return targetName;
    }

    // 根据目标纹章查找源纹章（用于重生时找到正确的映射关系）
    public static string GetSourceCrestByTarget(string targetCrestName)
    {
        foreach (var kvp in _crestMappings)
        {
            if (kvp.Value == targetCrestName)
                return kvp.Key;
        }
        return null;
    }
}