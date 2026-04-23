using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(ShopItemStats), "SetPurchased")]
public static class ShopItemStats_Purchase_Patch
{
    private static void Prefix()
    {
        try
        {
            ToolUnlockPatch.IsShopPurchase = true;
        }
        catch { }
    }

    private static void Postfix(ShopItemStats __instance)
    {
        try
        {
            ToolUnlockPatch.IsShopPurchase = false;

            if (__instance?.Item == null)
            {
                Plugin.Log.LogWarning("购买实例或物品为空，跳过处理");
                return;
            }

            string name = __instance.Item.name;
            if (string.IsNullOrEmpty(name) || !name.Contains("_"))
            {
                Plugin.Log.LogWarning($"永久ID格式错误: {name}，仅隐藏物体");
                __instance.gameObject.SetActive(false);
                return;
            }

            ShopMenuStock_BuildItemList_Patch.SetCount(name, 0);
            ShopMenuStock shop = __instance.GetComponentInParent<ShopMenuStock>();
            if (shop == null)
            {
                Plugin.Log.LogError("无法获取 ShopMenuStock");
                return;
            }

            __instance.gameObject.SetActive(false);
            shop.StartCoroutine(DelayedRebuild(shop));
            Plugin.Log.LogInfo($"永久ID {name} 已购买，触发商店重建");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"购买后处理异常: {ex}");
            if (__instance != null)
                __instance.gameObject.SetActive(false);
        }
    }

    private static IEnumerator DelayedRebuild(ShopMenuStock shop)
    {
        yield return null;
        try
        {
            MethodInfo buildMethod = typeof(ShopMenuStock).GetMethod("BuildItemList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (buildMethod != null)
            {
                buildMethod.Invoke(shop, null);
                Plugin.Log.LogInfo("商店重建完成");
            }
            else
            {
                Plugin.Log.LogError("未找到 BuildItemList 方法");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"商店重建异常: {ex}");
        }
    }
}