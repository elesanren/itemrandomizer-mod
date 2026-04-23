// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.ShopItemStats_Purchase_Patch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (ShopItemStats), "SetPurchased")]
public static class ShopItemStats_Purchase_Patch
{
  private static void Prefix()
  {
    try
    {
      ToolUnlockPatch.IsShopPurchase = true;
    }
    catch
    {
    }
  }

  private static void Postfix(ShopItemStats __instance)
  {
    try
    {
      ToolUnlockPatch.IsShopPurchase = false;
      if (Object.op_Equality((Object) __instance?.Item, (Object) null))
      {
        Plugin.Log.LogWarning((object) "购买实例或物品为空，跳过处理");
      }
      else
      {
        string name = ((Object) __instance.Item).name;
        if (string.IsNullOrEmpty(name) || !name.Contains("_"))
        {
          Plugin.Log.LogWarning((object) $"永久ID格式错误: {name}，仅隐藏物体");
          ((Component) __instance).gameObject.SetActive(false);
        }
        else
        {
          ShopMenuStock_BuildItemList_Patch.SetCount(name, 0);
          ShopMenuStock componentInParent = ((Component) __instance).GetComponentInParent<ShopMenuStock>();
          if (Object.op_Equality((Object) componentInParent, (Object) null))
          {
            Plugin.Log.LogError((object) "无法获取 ShopMenuStock");
          }
          else
          {
            ((Component) __instance).gameObject.SetActive(false);
            ((MonoBehaviour) componentInParent).StartCoroutine(ShopItemStats_Purchase_Patch.DelayedRebuild(componentInParent));
            Plugin.Log.LogInfo((object) $"永久ID {name} 已购买，触发商店重建");
          }
        }
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"购买后处理异常: {ex}");
      if (!Object.op_Inequality((Object) __instance, (Object) null))
        return;
      ((Component) __instance).gameObject.SetActive(false);
    }
  }

  private static IEnumerator DelayedRebuild(ShopMenuStock shop)
  {
    yield return (object) null;
    try
    {
      MethodInfo buildMethod = typeof (ShopMenuStock).GetMethod("BuildItemList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
      if (buildMethod != (MethodInfo) null)
      {
        buildMethod.Invoke((object) shop, (object[]) null);
        Plugin.Log.LogInfo((object) "商店重建完成");
      }
      else
        Plugin.Log.LogError((object) "未找到 BuildItemList 方法");
      buildMethod = (MethodInfo) null;
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"商店重建异常: {ex}");
    }
  }
}
