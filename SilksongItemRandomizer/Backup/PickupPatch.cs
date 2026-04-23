// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.PickupPatch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (CollectableItemPickup), "DoPickupAction")]
public class PickupPatch
{
  private static readonly Dictionary<string, string> ToolToSkillField = new Dictionary<string, string>()
  {
    {
      "Silk Spear",
      "hasNeedleThrow"
    },
    {
      "Thread Sphere",
      "hasThreadSphere"
    },
    {
      "Harpoon Dash",
      "hasHarpoonDash"
    },
    {
      "Silk Charge",
      "hasSilkCharge"
    },
    {
      "Silk Bomb",
      "hasSilkBomb"
    },
    {
      "Silk Boss Needle",
      "hasSilkBossNeedle"
    },
    {
      "Needolin",
      "hasNeedolin"
    },
    {
      "Dash",
      "hasDash"
    },
    {
      "Brolly",
      "hasBrolly"
    },
    {
      "DoubleJump",
      "hasDoubleJump"
    },
    {
      "Charge Slash",
      "hasChargeSlash"
    },
    {
      "SuperJump",
      "hasSuperJump"
    },
    {
      "Wall Jump",
      "hasWallJump"
    }
  };

  private static void Prefix(CollectableItemPickup __instance, ref bool __runOriginal)
  {
    try
    {
      if (!__runOriginal || Object.op_Equality((Object) __instance, (Object) null))
        return;
      SavedItem savedItem = __instance.Item;
      if (Object.op_Equality((Object) savedItem, (Object) null))
        return;
      if (ItemRandomizer.ExcludedNames.Contains(((Object) savedItem).name))
      {
        Plugin.Log.LogInfo((object) $"任务物品 {((Object) savedItem).name} 不参与随机，保持原物品");
      }
      else
      {
        SavedItem randomItem = ItemRandomizer.GetRandomItem();
        if (Object.op_Equality((Object) randomItem, (Object) null))
          return;
        if (savedItem is CollectableRelic || savedItem is ToolItem)
        {
          object[] objArray = new object[4];
          Scene scene = ((Component) __instance).gameObject.scene;
          objArray[0] = (object) ((Scene) ref scene).name;
          objArray[1] = (object) ((Component) __instance).transform.position.x;
          objArray[2] = (object) ((Component) __instance).transform.position.y;
          objArray[3] = (object) ((Component) __instance).transform.position.z;
          Plugin.AddDestroyedPickupKey(string.Format("{0}_{1:F2}_{2:F2}_{3:F2}", objArray));
          Plugin.Log.LogInfo((object) $"PickupPatch: 特殊点 {((Object) savedItem).name} 强制销毁，给予随机物品 {((Object) randomItem).name}");
          bool flag = false;
          if (randomItem is ToolItem toolItem)
          {
            string skillFieldName = PickupPatch.GetSkillFieldName(toolItem.name);
            if (!string.IsNullOrEmpty(skillFieldName))
            {
              PlayerData instance = PlayerData.instance;
              if (instance != null)
              {
                FieldInfo field = instance.GetType().GetField(skillFieldName, BindingFlags.Instance | BindingFlags.Public);
                if (field != (FieldInfo) null && field.FieldType == typeof (bool))
                {
                  field.SetValue((object) instance, (object) true);
                  Plugin.Log.LogInfo((object) $"设置 PlayerData.{skillFieldName} = true");
                  flag = true;
                }
                else
                  Plugin.Log.LogWarning((object) ("未找到技能字段 " + skillFieldName));
              }
            }
            try
            {
              MethodInfo method = typeof (ToolItem).GetMethod("Unlock", new Type[2]
              {
                typeof (Action),
                typeof (ToolItem.PopupFlags)
              });
              if ((object) method == null)
                method = typeof (ToolItem).GetMethod("Unlock", Type.EmptyTypes);
              MethodInfo methodInfo = method;
              if (methodInfo != (MethodInfo) null)
              {
                if (methodInfo.GetParameters().Length == 0)
                  methodInfo.Invoke((object) toolItem, (object[]) null);
                else
                  methodInfo.Invoke((object) toolItem, new object[2]
                  {
                    null,
                    (object) (ToolItem.PopupFlags) 3
                  });
                Plugin.Log.LogInfo((object) "调用 Unlock 成功");
                flag = true;
              }
              else
                Plugin.Log.LogError((object) "无法找到 ToolItem.Unlock 方法");
            }
            catch (Exception ex)
            {
              Plugin.Log.LogError((object) $"Unlock 调用失败: {ex}");
            }
          }
          else
            flag = randomItem.TryGet(false, true);
          if (flag)
          {
            RecentItemsUI.AddItem(randomItem);
            Plugin.Log.LogInfo((object) $"特殊点物品 {((Object) randomItem).name} 给予成功，已添加到UI");
          }
          else
            Plugin.Log.LogError((object) $"特殊点物品 {((Object) randomItem).name} 给予失败！");
          __runOriginal = false;
          Object.Destroy((Object) ((Component) __instance).gameObject);
        }
        else
        {
          FieldInfo fieldInfo = AccessTools.Field(typeof (CollectableItemPickup), "item");
          if (fieldInfo == (FieldInfo) null)
          {
            Plugin.Log.LogError((object) "item field not found!");
          }
          else
          {
            fieldInfo.SetValue((object) __instance, (object) randomItem);
            Plugin.Log.LogInfo((object) $"PickupPatch: 原物品 {((Object) savedItem).name} 被替换为 {((Object) randomItem).name}");
          }
        }
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"Exception in PickupPatch.Prefix: {ex}");
    }
  }

  private static string GetSkillFieldName(string toolName)
  {
    string skillFieldName;
    PickupPatch.ToolToSkillField.TryGetValue(toolName, out skillFieldName);
    return skillFieldName;
  }
}
