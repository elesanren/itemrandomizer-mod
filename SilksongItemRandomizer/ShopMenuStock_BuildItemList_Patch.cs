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
using UnityEngine.UI;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(ShopMenuStock), "BuildItemList")]
public static class ShopMenuStock_BuildItemList_Patch
{
    private static Dictionary<string, int> _slotCounts = new();
    private static FieldInfo _availableStockField;
    private static FieldInfo _spawnedStockField;
    private static FieldInfo _yDistanceField;
    private static FieldInfo _spawnedSubItemsField;

    // 全局偏移量（应用于第一个被购买槽位）
    private const float EXTRA_OFFSET = 1.5f;

    private static string FilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "shop_slots.json");

    private static void LoadCounts()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _slotCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(json) ?? new();
            }
            else _slotCounts.Clear();
        }
        catch { _slotCounts.Clear(); }
    }

    private static void SaveCounts()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(_slotCounts, Formatting.Indented));
        }
        catch { }
    }

    public static int GetCount(string permanentId) =>
        _slotCounts.TryGetValue(permanentId, out var c) ? c : 1;

    public static void SetCount(string permanentId, int count)
    {
        _slotCounts[permanentId] = count;
        SaveCounts();
    }

    public static void ResetAllCounts()
    {
        _slotCounts.Clear();
        if (File.Exists(FilePath)) File.Delete(FilePath);
        Plugin.Log.LogInfo("商店槽位计数已重置");
    }

    private static void EnsureReflectionFields()
    {
        if (_spawnedStockField == null)
        {
            _availableStockField = AccessTools.Field(typeof(ShopMenuStock), "availableStock");
            _spawnedStockField = AccessTools.Field(typeof(ShopMenuStock), "spawnedStock");
            _yDistanceField = AccessTools.Field(typeof(ShopMenuStock), "yDistance");
            _spawnedSubItemsField = AccessTools.Field(typeof(ShopMenuStock), "spawnedSubItems");
        }
    }

    [HarmonyPostfix]
    private static void Postfix(ShopMenuStock __instance)
    {
        if (!Plugin.ItemRandomEnabled.Value) return;

        try
        {
            EnsureReflectionFields();
            string sceneName = SceneManager.GetActiveScene().name;
            LoadCounts();

            var availableStock = _availableStockField.GetValue(__instance) as IList;
            var spawnedStock = _spawnedStockField.GetValue(__instance) as IList<ShopItemStats>;
            var yDistance = (float)_yDistanceField.GetValue(__instance);
            var spawnedSubItems = _spawnedSubItemsField.GetValue(__instance) as IEnumerable;

            availableStock?.Clear();
            float yOffset = 0f;
            bool firstPurchasedFound = false;

            if (spawnedStock != null)
            {
                for (int i = 0; i < spawnedStock.Count; i++)
                {
                    var stats = spawnedStock[i];
                    if (stats == null) continue;

                    string permanentId = $"{sceneName}_{i}";
                    bool isPurchased = GetCount(permanentId) <= 0;

                    if (isPurchased)
                    {
                        // 如果是第一个被购买的槽位，增加一个全局偏移量
                        if (!firstPurchasedFound)
                        {
                            firstPurchasedFound = true;
                            yOffset += EXTRA_OFFSET;
                            Plugin.Log.LogInfo($"第一个被购买槽位 (索引 {i})，应用额外偏移 {EXTRA_OFFSET}");
                        }

                        // 禁用 Shift_pos FSM
                        var shiftFsm = stats.GetComponent<PlayMakerFSM>();
                        if (shiftFsm != null && shiftFsm.FsmName == "Shift_pos")
                            shiftFsm.enabled = false;

                        // 设置已购买槽位的位置移出视野（不影响布局，因为高度已设0）
                        stats.transform.localPosition = new Vector3(0f, -100f, 0f);

                        // 将高度设为0，让 LayoutGroup 忽略此物体
                        var rt = stats.GetComponent<RectTransform>();
                        if (rt != null)
                            rt.sizeDelta = new Vector2(rt.sizeDelta.x, 0);

                        stats.gameObject.SetActive(false);
                        continue;
                    }

                    // 未购买槽位的正常处理
                    var newItem = ShopRandomizer.GetOrCreateShopItem(permanentId, out int price);
                    if (newItem == null)
                    {
                        stats.gameObject.SetActive(false);
                        continue;
                    }

                    var shopItem = CreateShopItem(newItem, price, permanentId);
                    if (shopItem == null)
                    {
                        stats.gameObject.SetActive(false);
                        continue;
                    }

                    stats.SetItem(shopItem);
                    stats.transform.localPosition = new Vector3(0f, yOffset, 0f);
                    stats.ItemNumber = availableStock?.Count ?? 0;
                    availableStock?.Add(stats);
                    yOffset += yDistance;
                    stats.gameObject.SetActive(true);
                    stats.UpdateAppearance();

                    // 同步 FSM 变量
                    var fsm = stats.GetComponent<PlayMakerFSM>();
                    if (fsm != null && fsm.FsmName == "Shift_pos")
                    {
                        fsm.enabled = true;
                        var posOriginal = fsm.FsmVariables.FindFsmVector3("Pos Original");
                        var posDown = fsm.FsmVariables.FindFsmVector3("Pos Down");
                        var posUp = fsm.FsmVariables.FindFsmVector3("Pos Up");
                        var tweenVec = fsm.FsmVariables.FindFsmVector3("Tween Vector");
                        if (posOriginal != null) posOriginal.Value = stats.transform.localPosition;
                        if (posDown != null) posDown.Value = stats.transform.localPosition + new Vector3(0f, yDistance, 0f);
                        if (posUp != null) posUp.Value = stats.transform.localPosition - new Vector3(0f, yDistance, 0f);
                        if (tweenVec != null) tweenVec.Value = stats.transform.localPosition;
                    }

                    Plugin.Log.LogInfo($"商店重建：永久ID {permanentId} -> {newItem.name} 价格 {price}");
                }
            }

            if (spawnedSubItems != null)
            {
                foreach (Component sub in spawnedSubItems)
                    sub?.gameObject.SetActive(false);
            }

            // 调整 ScrollRect.content 高度（基于实际可见槽位）
            var scrollRect = __instance.GetComponentInChildren<ScrollRect>();
            if (scrollRect != null && scrollRect.content != null)
            {
                int visibleCount = availableStock?.Count ?? 0;
                float contentHeight = visibleCount * Mathf.Abs(yDistance);
                contentHeight += 20f;
                // 如果应用了额外偏移，需要加回到 content 高度中
                if (firstPurchasedFound)
                    contentHeight += EXTRA_OFFSET;
                var contentRt = scrollRect.content.GetComponent<RectTransform>();
                if (contentRt != null)
                {
                    contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, contentHeight);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"商店重建补丁异常: {ex}");
        }
    }

    private static ShopItem CreateShopItem(SavedItem savedItem, int price, string permanentId)
    {
        var temp = ShopItem.CreateTemp(savedItem.name);
        temp.name = permanentId;

        string boolName = $"ShopBought_{permanentId}";
        var playerDataBoolField = typeof(ShopItem).GetField("playerDataBoolName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (playerDataBoolField != null)
            playerDataBoolField.SetValue(temp, boolName);

        var savedItemField = typeof(ShopItem).GetField("savedItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (savedItemField == null) return null;
        savedItemField.SetValue(temp, savedItem);

        var costField = typeof(ShopItem).GetField("cost", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (costField == null) return null;
        costField.SetValue(temp, price);

        var costRefField = typeof(ShopItem).GetField("costReference", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        costRefField?.SetValue(temp, null);

        var currencyField = typeof(ShopItem).GetField("currencyType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        currencyField?.SetValue(temp, 0);

        ClearField(temp, "requiredItem");
        ClearField(temp, "upgradeFromItem");

        var questsField = typeof(ShopItem).GetField("questsAppearConditions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (questsField != null && questsField.GetValue(temp) == null)
        {
            var elementType = questsField.FieldType.GetElementType();
            questsField.SetValue(temp, Array.CreateInstance(elementType ?? typeof(object), 0));
        }

        return temp;
    }

    private static void ClearField(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null && field.FieldType.IsClass)
            field.SetValue(obj, null);
    }
}