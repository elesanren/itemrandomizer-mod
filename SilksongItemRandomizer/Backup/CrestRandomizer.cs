// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.CrestRandomizer
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

public static class CrestRandomizer
{
  private static Dictionary<string, string> _crestMappings = new Dictionary<string, string>();
  private static List<ToolCrest>? _allCrests;

  private static string FilePath
  {
    get => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "crest_mappings.json");
  }

  public static List<ToolCrest>? CrestList => CrestRandomizer._allCrests;

  public static void Initialize()
  {
    CrestRandomizer._allCrests = ((IEnumerable<ToolCrest>) Resources.FindObjectsOfTypeAll<ToolCrest>()).ToList<ToolCrest>();
    CrestRandomizer.LoadMappings();
    if (CrestRandomizer._allCrests.Count > 0 && CrestRandomizer._crestMappings.Count < CrestRandomizer._allCrests.Count)
    {
      Plugin.Log.LogInfo((object) $"映射不完整（现有 {CrestRandomizer._crestMappings.Count}/{CrestRandomizer._allCrests.Count}），重新生成完整映射");
      CrestRandomizer.GenerateAllMappings();
    }
    Plugin.Log.LogInfo((object) $"纹章随机初始化完成，共 {CrestRandomizer._crestMappings.Count} 个映射");
  }

  public static void ResetMappings()
  {
    CrestRandomizer._crestMappings.Clear();
    if (File.Exists(CrestRandomizer.FilePath))
      File.Delete(CrestRandomizer.FilePath);
    Plugin.Log.LogInfo((object) "纹章映射已重置");
  }

  private static void LoadMappings()
  {
    try
    {
      if (File.Exists(CrestRandomizer.FilePath))
        CrestRandomizer._crestMappings = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(CrestRandomizer.FilePath)) ?? new Dictionary<string, string>();
      else
        CrestRandomizer._crestMappings.Clear();
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"加载纹章映射失败: {ex}");
      CrestRandomizer._crestMappings.Clear();
    }
  }

  private static void SaveMappings()
  {
    try
    {
      string directoryName = Path.GetDirectoryName(CrestRandomizer.FilePath);
      if (!Directory.Exists(directoryName))
        Directory.CreateDirectory(directoryName);
      File.WriteAllText(CrestRandomizer.FilePath, JsonConvert.SerializeObject((object) CrestRandomizer._crestMappings, Formatting.Indented));
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"保存纹章映射失败: {ex}");
    }
  }

  private static void GenerateAllMappings()
  {
    if (CrestRandomizer._allCrests == null || CrestRandomizer._allCrests.Count == 0)
      return;
    Random rng = new Random(Plugin.RandomSeed.Value);
    List<ToolCrest> list1 = CrestRandomizer._allCrests.OrderBy<ToolCrest, int>((Func<ToolCrest, int>) (x => rng.Next())).ToList<ToolCrest>();
    foreach (ToolCrest allCrest in CrestRandomizer._allCrests)
    {
      ToolCrest source = allCrest;
      List<ToolCrest> list2 = list1.Where<ToolCrest>((Func<ToolCrest, bool>) (c => c.name != source.name)).ToList<ToolCrest>();
      if (list2.Count == 0)
      {
        CrestRandomizer._crestMappings[source.name] = source.name;
      }
      else
      {
        int index = rng.Next(list2.Count);
        string name = list2[index].name;
        CrestRandomizer._crestMappings[source.name] = name;
      }
    }
    CrestRandomizer.SaveMappings();
    Plugin.Log.LogInfo((object) $"已为所有 {CrestRandomizer._allCrests.Count} 个纹章预生成映射");
  }

  public static string GetMappedCrestName(string sourceCrestName)
  {
    string mappedCrestName;
    if (CrestRandomizer._crestMappings.TryGetValue(sourceCrestName, out mappedCrestName))
      return mappedCrestName;
    Plugin.Log.LogWarning((object) $"警告：纹章 {sourceCrestName} 没有映射，返回自身");
    return sourceCrestName;
  }
}
