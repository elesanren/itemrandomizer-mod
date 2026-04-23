using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(ShopMenuStock), "BuildItemList")]
public static class ShopMenuStock_BuildItemList_Patch
{
    private static Dictionary<string, int> _slotCounts = new Dictionary<string, int>();

    // 反射缓存字段
    private static FieldInfo _availableStockField;
    private static FieldInfo _spawnedStockField;
    private static FieldInfo _yDistanceField;
    private static FieldInfo _spawnedSubItemsField;

    private static string FilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "shop_slots.json");

    private static void LoadCounts()
    {
        try
        {
            if (File.Exists(FilePath))
                _slotCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, int>();
            else
                _slotCounts.Clear();
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"加载商店槽位计数失败: {ex}");
            _slotCounts.Clear();
        }
    }

    private static void SaveCounts()
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_slotCounts, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存商店槽位计数失败: {ex}");
        }
    }

    public static int GetCount(string permanentId)
    {
        return _slotCounts.TryGetValue(permanentId, out int count) ? count : 1;
    }

    public static void SetCount(string permanentId, int count)
    {
        _slotCounts[permanentId] = count;
        SaveCounts();
    }

    public static void ResetAllCounts()
    {
        _slotCounts.Clear();
        if (File.Exists(FilePath))
            File.Delete(FilePath);
        Plugin.Log.LogInfo("商店槽位计数已重置");
    }

    private static void Postfix(ShopMenuStock __instance)
    {
        try
        {
            if (__instance == null) return;

            // 初始化反射字段（仅第一次执行）
            if (_availableStockField == null)
            {
                _availableStockField = AccessTools.Field(typeof(ShopMenuStock), "availableStock");
                _spawnedStockField = AccessTools.Field(typeof(ShopMenuStock), "spawnedStock");
                _yDistanceField = AccessTools.Field(typeof(ShopMenuStock), "yDistance");
                _spawnedSubItemsField = AccessTools.Field(typeof(ShopMenuStock), "spawnedSubItems");
            }

            string sceneName = SceneManager.GetActiveScene().name;
            LoadCounts();

            // 获取私有字段值
            var availableStock = _availableStockField.GetValue(__instance) as IList;
            var spawnedStock = _spawnedStockField.GetValue(__instance) as IList<ShopItemStats>;
            float yDistance = (float)_yDistanceField.GetValue(__instance);
            var spawnedSubItems = _spawnedSubItemsField.GetValue(__instance) as IEnumerable;

            availableStock?.Clear();
            float yOffset = 0f;

            if (spawnedStock != null)
            {
                foreach (ShopItemStats stats in spawnedStock)
                {
                    if (stats == null) continue;

                    string permanentId = stats.Item?.name;
                    if (string.IsNullOrEmpty(permanentId) || !permanentId.Contains("_"))
                    {
                        int index = spawnedStock.IndexOf(stats);
                        permanentId = $"{sceneName}_{index}";
                    }

                    if (GetCount(permanentId) <= 0)
                    {
                        stats.gameObject.SetActive(false);
                        continue;
                    }

                    SavedItem newItem = ShopRandomizer.GetOrCreateShopItem(permanentId, out int price);
                    if (newItem == null)
                    {
                        Plugin.Log.LogWarning($"商店随机：永久ID {permanentId} 获取物品失败，跳过");
                        continue;
                    }

                    ShopItem shopItem = CreateShopItem(newItem, price, permanentId);
                    if (shopItem == null) continue;

                    stats.SetItem(shopItem);
                    stats.transform.localPosition = new Vector3(0f, yOffset, 0f);
                    stats.ItemNumber = availableStock?.Count ?? 0;
                    availableStock?.Add(stats);
                    yOffset += yDistance;
                    stats.gameObject.SetActive(true);
                    stats.UpdateAppearance();

                    Plugin.Log.LogInfo($"商店重建：永久ID {permanentId} -> {newItem.name} 价格 {price}");
                }
            }

            if (spawnedSubItems != null)
            {
                foreach (Component sub in spawnedSubItems)
                    sub?.gameObject.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"商店重建补丁异常: {ex}");
        }
    }

    private static ShopItem CreateShopItem(SavedItem savedItem, int price, string permanentId)
    {
        ShopItem temp = ShopItem.CreateTemp(savedItem.name);
        temp.name = permanentId;

        FieldInfo savedItemField = typeof(ShopItem).GetField("savedItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (savedItemField == null)
        {
            Plugin.Log.LogError("未找到 savedItem 字段");
            return null;
        }
        savedItemField.SetValue(temp, savedItem);

        FieldInfo costField = typeof(ShopItem).GetField("cost", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (costField == null)
        {
            Plugin.Log.LogError("未找到 cost 字段");
            return null;
        }
        costField.SetValue(temp, price);

        FieldInfo costRefField = typeof(ShopItem).GetField("costReference", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        costRefField?.SetValue(temp, null);

        FieldInfo currencyField = typeof(ShopItem).GetField("currencyType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        currencyField?.SetValue(temp, 0); // 直接设 0 代替 CurrencyType.Geo

        ClearField(temp, "playerDataBoolName");
        ClearField(temp, "playerDataIntName");
        ClearField(temp, "requiredItem");
        ClearField(temp, "upgradeFromItem");

        // 安全处理 questsAppearConditions
        FieldInfo questsField = typeof(ShopItem).GetField("questsAppearConditions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (questsField != null && questsField.GetValue(temp) == null)
        {
            Type elementType = questsField.FieldType.GetElementType();
            questsField.SetValue(temp, Array.CreateInstance(elementType ?? typeof(object), 0));
        }

        return temp;
    }

    private static void ClearField(object obj, string fieldName)
    {
        FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null && field.FieldType.IsClass)
            field.SetValue(obj, null);
    }
}