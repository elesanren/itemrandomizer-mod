// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.SilkSpearPityPatch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (CurrencyObjectBase), "Collect")]
public static class SilkSpearPityPatch
{
  private static int _silkSpearAttempts = 0;
  private static bool _silkSpearGiven = false;
  private const int SILK_SPEAR_PITY = 200;
  private static SavedItem? _silkSpearItem;
  public static bool IsGivingSilkSpear = false;

  static SilkSpearPityPatch()
  {
    SilkSpearPityPatch._silkSpearItem = ((IEnumerable<SavedItem>) Resources.FindObjectsOfTypeAll<SavedItem>()).FirstOrDefault<SavedItem>((Func<SavedItem, bool>) (item => ((Object) item).name == "Silk Spear"));
    if (Object.op_Equality((Object) SilkSpearPityPatch._silkSpearItem, (Object) null))
      Plugin.Log.LogWarning((object) "[丝矛独立补丁] 未找到丝矛物品，保底将禁用。");
    else
      Plugin.Log.LogInfo((object) ("[丝矛独立补丁] 丝矛物品已找到: " + ((Object) SilkSpearPityPatch._silkSpearItem).name));
  }

  public static void ResetCounter()
  {
    SilkSpearPityPatch._silkSpearAttempts = 0;
    SilkSpearPityPatch._silkSpearGiven = false;
    Plugin.Log.LogInfo((object) "[丝矛独立补丁] 计数器已重置");
  }

  private static void Postfix(bool __result)
  {
    if (!__result || Object.op_Equality((Object) SilkSpearPityPatch._silkSpearItem, (Object) null) || SilkSpearPityPatch._silkSpearGiven)
      return;
    ++SilkSpearPityPatch._silkSpearAttempts;
    if (SilkSpearPityPatch._silkSpearGiven || SilkSpearPityPatch._silkSpearAttempts < 200)
      return;
    Plugin.Log.LogInfo((object) $"[丝矛独立补丁] 保底触发（第{SilkSpearPityPatch._silkSpearAttempts}次）");
    bool flag = false;
    SilkSpearPityPatch.IsGivingSilkSpear = true;
    if (SilkSpearPityPatch._silkSpearItem is ToolItem silkSpearItem)
    {
      try
      {
        MethodInfo method = typeof (ToolItem).GetMethod("Unlock", Type.EmptyTypes);
        if ((object) method == null)
          method = typeof (ToolItem).GetMethod("Unlock", new Type[2]
          {
            typeof (Action),
            typeof (ToolItem.PopupFlags)
          });
        MethodInfo methodInfo = method;
        if (methodInfo != (MethodInfo) null)
        {
          if (methodInfo.GetParameters().Length == 0)
            methodInfo.Invoke((object) silkSpearItem, (object[]) null);
          else
            methodInfo.Invoke((object) silkSpearItem, new object[2]
            {
              null,
              (object) (ToolItem.PopupFlags) 3
            });
          flag = true;
        }
      }
      catch (Exception ex)
      {
        Plugin.Log.LogError((object) $"[丝矛独立补丁] Unlock 失败: {ex}");
      }
    }
    else
      flag = SilkSpearPityPatch._silkSpearItem.TryGet(false, true);
    SilkSpearPityPatch.IsGivingSilkSpear = false;
    if (flag)
    {
      Plugin.Log.LogInfo((object) "[丝矛独立补丁] 丝矛成功给予");
      Plugin.ShowNotification("获得丝矛！");
      SilkSpearPityPatch._silkSpearGiven = true;
    }
    else
    {
      Plugin.Log.LogError((object) "[丝矛独立补丁] 丝矛给予失败");
      SilkSpearPityPatch._silkSpearGiven = true;
    }
  }
}
