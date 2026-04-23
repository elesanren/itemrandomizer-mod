// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.CurrencyCollectPatch
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

[HarmonyPatch(typeof (CurrencyObjectBase), "Collect")]
public class CurrencyCollectPatch
{
  private static int _consecutiveMisses = 0;
  private const int PITY_THRESHOLD = 105;
  private static int _dropCount = 0;
  private static bool _hasGivenKey = false;
  private const int KEY_GUARANTEE_MAX = 30;
  private static string keyName = "Simple Key";
  private static int _silkSpearAttempts = 0;
  private static bool _silkSpearGiven = false;
  private const int SILK_SPEAR_PITY = 200;
  private static SavedItem? _silkSpearItem = ((IEnumerable<SavedItem>) Resources.FindObjectsOfTypeAll<SavedItem>()).FirstOrDefault<SavedItem>((Func<SavedItem, bool>) (item => ((Object) item).name == "Silk Spear"));

  static CurrencyCollectPatch()
  {
    if (Object.op_Equality((Object) CurrencyCollectPatch._silkSpearItem, (Object) null))
      Plugin.Log.LogWarning((object) "未找到丝矛物品（Silk Spear），丝矛200次保底将禁用。");
    else
      Plugin.Log.LogInfo((object) ("丝矛物品已找到: " + ((Object) CurrencyCollectPatch._silkSpearItem).name));
  }

  public static void ResetCounters()
  {
    CurrencyCollectPatch._consecutiveMisses = 0;
    CurrencyCollectPatch._dropCount = 0;
    CurrencyCollectPatch._hasGivenKey = false;
    CurrencyCollectPatch._silkSpearAttempts = 0;
    CurrencyCollectPatch._silkSpearGiven = false;
    Plugin.Log.LogInfo((object) "货币保底计数器已重置");
  }

  private static void Postfix(bool __result)
  {
    try
    {
      if (!__result)
        return;
      ++CurrencyCollectPatch._dropCount;
      ++CurrencyCollectPatch._silkSpearAttempts;
      if (!CurrencyCollectPatch._hasGivenKey && CurrencyCollectPatch._dropCount <= 30)
      {
        SavedItem savedItem = ((IEnumerable<SavedItem>) Resources.FindObjectsOfTypeAll<SavedItem>()).FirstOrDefault<SavedItem>((Func<SavedItem, bool>) (item => ((Object) item).name == CurrencyCollectPatch.keyName));
        if (Object.op_Inequality((Object) savedItem, (Object) null))
        {
          savedItem.TryGet(false, true);
          Plugin.Log.LogInfo((object) $"钥匙保底触发（第{CurrencyCollectPatch._dropCount}次）");
          CurrencyCollectPatch._hasGivenKey = true;
        }
      }
      if (Object.op_Inequality((Object) CurrencyCollectPatch._silkSpearItem, (Object) null) && !CurrencyCollectPatch._silkSpearGiven && CurrencyCollectPatch._silkSpearAttempts >= 200)
      {
        Plugin.Log.LogInfo((object) "丝矛保底触发");
        if (CurrencyCollectPatch._silkSpearItem.TryGet(false, true))
        {
          Plugin.Log.LogInfo((object) "丝矛保底成功给予");
          Plugin.ShowNotification("获得丝矛！");
          CurrencyCollectPatch._silkSpearGiven = true;
        }
        else
        {
          Plugin.Log.LogError((object) "丝矛保底失败");
          CurrencyCollectPatch._silkSpearGiven = true;
        }
      }
      else if (CurrencyCollectPatch._consecutiveMisses >= 104 || ItemRandomizer.RandomChance(0.00952381f))
      {
        SavedItem randomItem = ItemRandomizer.GetRandomItem();
        if (Object.op_Inequality((Object) randomItem, (Object) null))
          randomItem.TryGet(false, true);
        CurrencyCollectPatch._consecutiveMisses = 0;
      }
      else
        ++CurrencyCollectPatch._consecutiveMisses;
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"Exception in CurrencyCollectPatch: {ex}");
    }
  }
}
