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

    private static string FilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "crest_mappings.json");

    public static List<ToolCrest> CrestList => _allCrests;

    public static void Initialize()
    {
        _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();
        LoadMappings();
        if (_allCrests.Count > 0 && _crestMappings.Count < _allCrests.Count)
        {
            Plugin.Log.LogInfo($"映射不完整（现有 {_crestMappings.Count}/{_allCrests.Count}），重新生成完整映射");
            GenerateAllMappings();
        }
        Plugin.Log.LogInfo($"纹章随机初始化完成，共 {_crestMappings.Count} 个映射");
    }

    public static void ResetMappings()
    {
        _crestMappings.Clear();
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        Plugin.Log.LogInfo("纹章映射已重置");
    }

    private static void LoadMappings()
    {
        try
        {
            if (File.Exists(FilePath))
                _crestMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, string>();
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
            string directoryName = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_crestMappings, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存纹章映射失败: {ex}");
        }
    }

    private static void GenerateAllMappings()
    {
        if (_allCrests == null || _allCrests.Count == 0)
            return;
        Random rng = new Random(Plugin.RandomSeed.Value);
        List<ToolCrest> shuffled = _allCrests.OrderBy(x => rng.Next()).ToList();
        foreach (ToolCrest source in _allCrests)
        {
            List<ToolCrest> candidates = shuffled.Where(c => c.name != source.name).ToList();
            if (candidates.Count == 0)
                _crestMappings[source.name] = source.name;
            else
                _crestMappings[source.name] = candidates[rng.Next(candidates.Count)].name;
        }
        SaveMappings();
        Plugin.Log.LogInfo($"已为所有 {_allCrests.Count} 个纹章预生成映射");
    }

    public static string GetMappedCrestName(string sourceCrestName)
    {
        if (_crestMappings.TryGetValue(sourceCrestName, out string mappedCrestName))
            return mappedCrestName;
        Plugin.Log.LogWarning($"警告：纹章 {sourceCrestName} 没有映射，返回自身");
        return sourceCrestName;
    }
}