// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.ToolUnlockPatch
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

public static class ToolUnlockPatch
{
  private static List<ToolItem>? _allTools;
  public static bool IsShopPurchase;

  public static void Initialize()
  {
    ToolUnlockPatch._allTools = ((IEnumerable<ToolItem>) Resources.FindObjectsOfTypeAll<ToolItem>()).ToList<ToolItem>();
    if (ToolUnlockPatch._allTools.Count == 0)
      Plugin.Log.LogWarning((object) "No ToolItem found! Tool randomizer may not work.");
    else
      Plugin.Log.LogInfo((object) $"Found {ToolUnlockPatch._allTools.Count} ToolItem instances for randomization.");
  }

  [HarmonyPatch(typeof (ToolItem), "Unlock")]
  [HarmonyPrefix]
  private static bool Prefix(
    ToolItem __instance,
    Action afterTutorialMsg,
    ToolItem.PopupFlags popupFlags)
  {
    try
    {
      if (ToolUnlockPatch.IsShopPurchase)
      {
        Plugin.Log.LogInfo((object) $"商店购买工具 {__instance?.name}，跳过随机");
        return true;
      }
      if (ToolUnlockPatch._allTools == null || ToolUnlockPatch._allTools.Count == 0)
      {
        ToolUnlockPatch.Initialize();
        if (ToolUnlockPatch._allTools == null || ToolUnlockPatch._allTools.Count == 0)
        {
          Plugin.Log.LogWarning((object) "Tool list empty, cannot randomize.");
          return true;
        }
      }
      ToolItem allTool = ToolUnlockPatch._allTools[ItemRandomizer.Rng.Next(ToolUnlockPatch._allTools.Count)];
      if (Object.op_Equality((Object) allTool, (Object) __instance))
        return true;
      Plugin.Log.LogInfo((object) $"Randomizing tool: {__instance?.name ?? "null"} -> {allTool.name}");
      allTool.Unlock(afterTutorialMsg, popupFlags);
      return false;
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"Exception in ToolUnlockPatch.Prefix: {ex}");
      return true;
    }
  }
}
