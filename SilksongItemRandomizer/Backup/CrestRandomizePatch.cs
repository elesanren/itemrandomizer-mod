// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.CrestRandomizePatch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (ToolCrest), "Unlock")]
public static class CrestRandomizePatch
{
  private static List<ToolCrest>? _allCrests;
  private static HashSet<int> _processedInstanceIds = new HashSet<int>();

  public static List<ToolCrest>? CrestList => CrestRandomizePatch._allCrests;

  public static void Initialize()
  {
    CrestRandomizePatch._allCrests = ((IEnumerable<ToolCrest>) Resources.FindObjectsOfTypeAll<ToolCrest>()).ToList<ToolCrest>();
  }

  public static void ResetProcessedIds() => CrestRandomizePatch._processedInstanceIds.Clear();

  [HarmonyPrefix]
  private static bool Prefix(ToolCrest __instance)
  {
    try
    {
      if (CrestRandomizePatch._processedInstanceIds.Contains(((Object) __instance).GetInstanceID()))
        return true;
      CrestRandomizePatch._processedInstanceIds.Add(((Object) __instance).GetInstanceID());
      string name = __instance.name;
      string mappedCrestName = CrestRandomizer.GetMappedCrestName(name);
      if (string.IsNullOrEmpty(mappedCrestName))
        return true;
      if (mappedCrestName == name)
      {
        Plugin.Log.LogInfo((object) $"纹章 {name} 映射到自身，放行原解锁");
        return true;
      }
      if (CrestRandomizePatch._allCrests == null || CrestRandomizePatch._allCrests.Count == 0)
        CrestRandomizePatch.Initialize();
      ToolCrest toolCrest = (ToolCrest) null;
      if (CrestRandomizePatch._allCrests != null)
      {
        foreach (ToolCrest allCrest in CrestRandomizePatch._allCrests)
        {
          if (allCrest.name == mappedCrestName)
          {
            toolCrest = allCrest;
            break;
          }
        }
      }
      if (Object.op_Equality((Object) toolCrest, (Object) null))
      {
        Plugin.Log.LogWarning((object) $"无法找到目标纹章 {mappedCrestName}，放行原解锁");
        return true;
      }
      toolCrest.Unlock();
      PlayerData instance = PlayerData.instance;
      if (instance != null && instance.CurrentCrestID == name)
        typeof (ToolItemManager).GetMethod("SetEquippedCrest", new Type[1]
        {
          typeof (string)
        })?.Invoke((object) null, new object[1]
        {
          (object) mappedCrestName
        });
      return false;
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"CrestRandomizePatch异常: {ex}");
      return true;
    }
  }
}
