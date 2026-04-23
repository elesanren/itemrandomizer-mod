using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(CollectableItemPickup), "DoPickupAction")]
public class PickupPatch
{
    private static readonly Dictionary<string, string> ToolToSkillField = new Dictionary<string, string>()
    {
        { "Silk Spear", "hasNeedleThrow" },
        { "Thread Sphere", "hasThreadSphere" },
        { "Harpoon Dash", "hasHarpoonDash" },
        { "Silk Charge", "hasSilkCharge" },
        { "Silk Bomb", "hasSilkBomb" },
        { "Silk Boss Needle", "hasSilkBossNeedle" },
        { "Needolin", "hasNeedolin" },
        { "Dash", "hasDash" },
        { "Brolly", "hasBrolly" },
        { "DoubleJump", "hasDoubleJump" },
        { "Charge Slash", "hasChargeSlash" },
        { "SuperJump", "hasSuperJump" },
        { "Wall Jump", "hasWallJump" }
    };

    private static void Prefix(CollectableItemPickup __instance, ref bool __runOriginal)
    {
        try
        {
            if (!__runOriginal || __instance == null)
                return;

            SavedItem originalItem = __instance.Item;
            if (originalItem == null)
                return;

            if (ItemRandomizer.ExcludedNames.Contains(originalItem.name))
            {
                Plugin.Log.LogInfo($"任务物品 {originalItem.name} 不参与随机，保持原物品");
                return;
            }

            SavedItem randomItem = ItemRandomizer.GetRandomItem();
            if (randomItem == null)
                return;

            // 生成唯一键，坐标使用 F1 精度
            string key = $"{__instance.gameObject.scene.name}_{__instance.transform.position.x:F1}_{__instance.transform.position.y:F1}_{__instance.transform.position.z:F1}";

            // 特殊点（圣物或工具）处理
            if (originalItem is CollectableRelic || originalItem is ToolItem)
            {
                Plugin.AddDestroyedPickupKey(key);
                Plugin.Log.LogInfo($"特殊点 {originalItem.name} 强制销毁，给予随机物品 {randomItem.name}");

                bool success = false;

                if (randomItem is ToolItem toolItem)
                {
                    string skillFieldName = GetSkillFieldName(toolItem.name);
                    if (!string.IsNullOrEmpty(skillFieldName))
                    {
                        PlayerData pd = PlayerData.instance;
                        if (pd != null)
                        {
                            FieldInfo field = pd.GetType().GetField(skillFieldName, BindingFlags.Instance | BindingFlags.Public);
                            if (field != null && field.FieldType == typeof(bool))
                            {
                                field.SetValue(pd, true);
                                success = true;
                            }
                        }
                    }

                    try
                    {
                        MethodInfo method = typeof(ToolItem).GetMethod("Unlock", new Type[] { typeof(Action), typeof(ToolItem.PopupFlags) });
                        if (method == null)
                            method = typeof(ToolItem).GetMethod("Unlock", Type.EmptyTypes);

                        if (method != null)
                        {
                            if (method.GetParameters().Length == 0)
                                method.Invoke(toolItem, null);
                            else
                                method.Invoke(toolItem, new object[] { null, (ToolItem.PopupFlags)3 });

                            success = true;
                        }
                        else
                        {
                            Plugin.Log.LogError("无法找到 ToolItem.Unlock 方法");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError($"Unlock 调用失败: {ex}");
                    }
                }
                else
                {
                    success = randomItem.TryGet(false, true);
                }

                if (success)
                {
                    RecentItemsUI.AddItem(randomItem);
                    Plugin.Log.LogInfo($"特殊点物品 {randomItem.name} 给予成功");
                }
                else
                {
                    Plugin.Log.LogError($"特殊点物品 {randomItem.name} 给予失败！");
                }

                __runOriginal = false;
                UnityEngine.Object.Destroy(__instance.gameObject);
            }
            else
            {
                // 普通拾取点
                Plugin.AddDestroyedPickupKey(key);
                Plugin.Log.LogInfo($"普通点 {originalItem.name} 强制销毁，给予随机物品 {randomItem.name}");

                if (randomItem.TryGet(false, true))
                {
                    RecentItemsUI.AddItem(randomItem);
                    Plugin.Log.LogInfo($"普通点物品 {randomItem.name} 给予成功");
                }
                else
                {
                    Plugin.Log.LogError($"普通点物品 {randomItem.name} 给予失败！");
                }

                __runOriginal = false;
                UnityEngine.Object.Destroy(__instance.gameObject);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Exception in PickupPatch.Prefix: {ex}");
        }
    }

    private static string GetSkillFieldName(string toolName)
    {
        ToolToSkillField.TryGetValue(toolName, out string skillFieldName);
        return skillFieldName;
    }
}