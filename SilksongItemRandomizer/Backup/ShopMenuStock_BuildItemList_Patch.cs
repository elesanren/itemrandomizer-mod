// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.ShopMenuStock_BuildItemList_Patch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (ShopMenuStock), "BuildItemList")]
public static class ShopMenuStock_BuildItemList_Patch
{
  private static Dictionary<string, int> _slotCounts = new Dictionary<string, int>();

  private static string FilePath
  {
    get => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "shop_slots.json");
  }

  private static void LoadCounts()
  {
    try
    {
      if (File.Exists(ShopMenuStock_BuildItemList_Patch.FilePath))
        ShopMenuStock_BuildItemList_Patch._slotCounts = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(ShopMenuStock_BuildItemList_Patch.FilePath)) ?? new Dictionary<string, int>();
      else
        ShopMenuStock_BuildItemList_Patch._slotCounts.Clear();
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"加载商店槽位计数失败: {ex}");
      ShopMenuStock_BuildItemList_Patch._slotCounts.Clear();
    }
  }

  private static void SaveCounts()
  {
    try
    {
      string directoryName = Path.GetDirectoryName(ShopMenuStock_BuildItemList_Patch.FilePath);
      if (!Directory.Exists(directoryName))
        Directory.CreateDirectory(directoryName);
      File.WriteAllText(ShopMenuStock_BuildItemList_Patch.FilePath, JsonConvert.SerializeObject((object) ShopMenuStock_BuildItemList_Patch._slotCounts, Formatting.Indented));
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"保存商店槽位计数失败: {ex}");
    }
  }

  public static int GetCount(string permanentId)
  {
    int num;
    return ShopMenuStock_BuildItemList_Patch._slotCounts.TryGetValue(permanentId, out num) ? num : 1;
  }

  public static void SetCount(string permanentId, int count)
  {
    ShopMenuStock_BuildItemList_Patch._slotCounts[permanentId] = count;
    ShopMenuStock_BuildItemList_Patch.SaveCounts();
  }

  public static void ResetAllCounts()
  {
    ShopMenuStock_BuildItemList_Patch._slotCounts.Clear();
    if (File.Exists(ShopMenuStock_BuildItemList_Patch.FilePath))
      File.Delete(ShopMenuStock_BuildItemList_Patch.FilePath);
    Plugin.Log.LogInfo((object) "商店槽位计数已重置");
  }

  private static void Postfix(ShopMenuStock __instance)
  {
    try
    {
      if (Object.op_Equality((Object) __instance, (Object) null))
        return;
      Scene activeScene = SceneManager.GetActiveScene();
      string name = ((Scene) ref activeScene).name;
      ShopMenuStock_BuildItemList_Patch.LoadCounts();
      __instance.availableStock.Clear();
      float num1 = 0.0f;
      foreach (ShopItemStats shopItemStats in __instance.spawnedStock)
      {
        if (!Object.op_Equality((Object) shopItemStats, (Object) null))
        {
          string permanentId = ((Object) shopItemStats.Item)?.name;
          if (string.IsNullOrEmpty(permanentId) || !permanentId.Contains("_"))
          {
            int num2 = __instance.spawnedStock.IndexOf(shopItemStats);
            permanentId = $"{name}_{num2}";
          }
          if (ShopMenuStock_BuildItemList_Patch.GetCount(permanentId) <= 0)
          {
            ((Component) shopItemStats).gameObject.SetActive(false);
          }
          else
          {
            int price;
            SavedItem shopItem1 = ShopRandomizer.GetOrCreateShopItem(permanentId, out price);
            if (Object.op_Equality((Object) shopItem1, (Object) null))
            {
              Plugin.Log.LogWarning((object) $"商店随机：永久ID {permanentId} 获取物品失败，跳过");
            }
            else
            {
              ShopItem shopItem2 = ShopMenuStock_BuildItemList_Patch.CreateShopItem(shopItem1, price, permanentId);
              if (!Object.op_Equality((Object) shopItem2, (Object) null))
              {
                shopItemStats.SetItem(shopItem2);
                ((Component) shopItemStats).transform.localPosition = new Vector3(0.0f, num1, 0.0f);
                shopItemStats.ItemNumber = __instance.availableStock.Count;
                __instance.availableStock.Add(shopItemStats);
                num1 += __instance.yDistance;
                ((Component) shopItemStats).gameObject.SetActive(true);
                shopItemStats.UpdateAppearance();
                Plugin.Log.LogInfo((object) $"商店重建：永久ID {permanentId} -> {((Object) shopItem1).name} 价格 {price}");
              }
            }
          }
        }
      }
      foreach (Component spawnedSubItem in __instance.spawnedSubItems)
        spawnedSubItem.gameObject.SetActive(false);
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"商店重建补丁异常: {ex}");
    }
  }

  private static ShopItem CreateShopItem(SavedItem savedItem, int price, string permanentId)
  {
    ShopItem temp = ShopItem.CreateTemp(((Object) savedItem).name);
    ((Object) temp).name = permanentId;
    FieldInfo field1 = typeof (ShopItem).GetField(nameof (savedItem), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    if (field1 != (FieldInfo) null)
    {
      field1.SetValue((object) temp, (object) savedItem);
      FieldInfo field2 = typeof (ShopItem).GetField("cost", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
      if (field2 != (FieldInfo) null)
      {
        field2.SetValue((object) temp, (object) price);
        FieldInfo field3 = typeof (ShopItem).GetField("costReference", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field3 != (FieldInfo) null)
          field3.SetValue((object) temp, (object) null);
        FieldInfo field4 = typeof (ShopItem).GetField("currencyType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field4 != (FieldInfo) null)
          field4.SetValue((object) temp, (object) (CurrencyType) 0);
        ShopMenuStock_BuildItemList_Patch.ClearField((object) temp, "playerDataBoolName");
        ShopMenuStock_BuildItemList_Patch.ClearField((object) temp, "playerDataIntName");
        ShopMenuStock_BuildItemList_Patch.ClearField((object) temp, "requiredItem");
        ShopMenuStock_BuildItemList_Patch.ClearField((object) temp, "upgradeFromItem");
        if (temp.questsAppearConditions == null)
          temp.questsAppearConditions = new QuestTest[0];
        return temp;
      }
      Plugin.Log.LogError((object) "未找到 cost 字段");
      return (ShopItem) null;
    }
    Plugin.Log.LogError((object) "未找到 savedItem 字段");
    return (ShopItem) null;
  }

  private static void ClearField(object obj, string fieldName)
  {
    FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    if (!(field != (FieldInfo) null) || !field.FieldType.IsClass)
      return;
    field.SetValue(obj, (object) null);
  }
}
