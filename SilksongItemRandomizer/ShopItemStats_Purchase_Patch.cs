using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace SilksongItemRandomizer;

public static class ShopSlotHelper
{
    private static FieldInfo _spawnedStockField;

    static ShopSlotHelper()
    {
        _spawnedStockField = AccessTools.Field(typeof(ShopMenuStock), "spawnedStock");
    }

    public static string GetSlotId(ShopItemStats stats)
    {
        var shop = stats.GetComponentInParent<ShopMenuStock>();
        if (shop == null || _spawnedStockField == null) return null;
        var spawnedStock = _spawnedStockField.GetValue(shop) as IList;
        if (spawnedStock == null) return null;
        int index = spawnedStock.IndexOf(stats);
        if (index < 0) return null;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        return $"{sceneName}_{index}";
    }
}

[HarmonyPatch(typeof(ShopItemStats), "SetPurchased")]
public static class ShopItemStats_Purchase_Patch
{
    private static MethodInfo _buildItemListMethod;

    static ShopItemStats_Purchase_Patch()
    {
        _buildItemListMethod = typeof(ShopMenuStock).GetMethod("BuildItemList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    [HarmonyPrefix]
    private static bool Prefix(ShopItemStats __instance, Action onComplete, int subItemIndex)
    {
        if (!Plugin.ItemRandomEnabled.Value) return true;

        string permanentId = ShopSlotHelper.GetSlotId(__instance);
        if (string.IsNullOrEmpty(permanentId)) return true;

        if (ShopMenuStock_BuildItemList_Patch.GetCount(permanentId) <= 0)
            return false; // 阻止重复购买

        ShopMenuStock_BuildItemList_Patch.SetCount(permanentId, 0);
        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(ShopItemStats __instance)
    {
        if (!Plugin.ItemRandomEnabled.Value) return;
        var shop = __instance.GetComponentInParent<ShopMenuStock>();
        if (shop != null)
            shop.StartCoroutine(DelayedRebuildAndLayout(shop));
    }

    private static IEnumerator DelayedRebuildAndLayout(ShopMenuStock shop)
    {
        yield return null;
        try
        {
            _buildItemListMethod?.Invoke(shop, null);
            var layout = shop.GetComponent<LayoutGroup>();
            if (layout != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(layout.GetComponent<RectTransform>());
        }
        catch (Exception) { }
    }
}

[HarmonyPatch(typeof(ShopItemStats), "IsAvailable", MethodType.Getter)]
public static class ShopItemStats_IsAvailable_Patch
{
    [HarmonyPrefix]
    private static bool Prefix(ShopItemStats __instance, ref bool __result)
    {
        if (!Plugin.ItemRandomEnabled.Value) return true;
        string permanentId = ShopSlotHelper.GetSlotId(__instance);
        if (string.IsNullOrEmpty(permanentId)) return true;
        __result = ShopMenuStock_BuildItemList_Patch.GetCount(permanentId) > 0;
        return false;
    }
}