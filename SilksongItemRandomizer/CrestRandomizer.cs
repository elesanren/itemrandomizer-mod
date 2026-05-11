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
    // ★ 改为“已生成的映射”，不再是全部预生成
    private static Dictionary<string, string> _crestMappings = new();
    private static List<ToolCrest> _allCrests;

    private static string FilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "crest_mappings.json");

    public static List<ToolCrest> CrestList => _allCrests;

    private static readonly HashSet<string> ExcludeFromPool = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hunter", "Hunter_v2", "Hunter_v3", "Witch_v2"
    };

    public static void Initialize()
    {
        _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();
        LoadMappings();
        // ★ 不再检查映射完整性，不再预生成
        Plugin.Log.LogInfo($"纹章随机初始化完成，已有 {_crestMappings.Count} 个映射");
    }

    public static void ResetMappings()
    {
        _crestMappings.Clear();
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }

    private static void LoadMappings()
    {
        try
        {
            if (File.Exists(FilePath))
                _crestMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(FilePath))
                                 ?? new Dictionary<string, string>();
            else
                _crestMappings.Clear();
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
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_crestMappings, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存纹章映射失败: {ex}");
        }
    }

    // ★ 按需生成：只在第一次遇到某个源纹章时，才为它随机选一个目标
    private static string GetOrCreateMapping(string sourceCrestName)
    {
        // 已经有了就直接返回
        if (_crestMappings.TryGetValue(sourceCrestName, out string existing))
            return existing;

        // 需要新建映射
        if (_allCrests == null || _allCrests.Count == 0)
            _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();

        // 构建候选池：排除猎手、升级版、以及源纹章自身
        List<ToolCrest> candidates = _allCrests
            .Where(c => !ExcludeFromPool.Contains(c.name)
                        && !c.name.Contains("_v")
                        && c.name != sourceCrestName)
            .ToList();

        if (candidates.Count == 0)
        {
            // 没有候选，映射到自身（等于不随机）
            _crestMappings[sourceCrestName] = sourceCrestName;
        }
        else
        {
            Random rng = new Random(Plugin.RandomSeed.Value ^ sourceCrestName.GetHashCode());
            _crestMappings[sourceCrestName] = candidates[rng.Next(candidates.Count)].name;
        }

        SaveMappings();
        Plugin.Log.LogInfo($"新映射: {sourceCrestName} -> {_crestMappings[sourceCrestName]}");
        return _crestMappings[sourceCrestName];
    }

    // ★ 这个方法会在补丁中被调用
    public static string GetMappedCrestName(string sourceCrestName)
    {
        return GetOrCreateMapping(sourceCrestName);
    }
}