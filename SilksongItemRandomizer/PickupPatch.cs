using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongItemRandomizer;

[HarmonyPatch]
public static class PickupPatch
{
    // ========== 已捡记录管理 ==========
    private static readonly string PickedKeysFilePath = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "picked_keys.json");
    private static HashSet<string> _pickedKeys = new();
    private static bool _isDirty = false;

    // ========== 唯一键生成 ==========
    public static string GetPickupKey(CollectableItemPickup pickup)
    {
        var pbi = pickup.GetComponent<PersistentBoolItem>();
        if (pbi != null)
        {
            var itemDataField = typeof(PersistentBoolItem).GetField("itemData", BindingFlags.Instance | BindingFlags.NonPublic);
            if (itemDataField != null)
            {
                var itemData = itemDataField.GetValue(pbi);
                if (itemData != null)
                {
                    var idField = itemData.GetType().GetField("ID", BindingFlags.Instance | BindingFlags.Public);
                    if (idField != null && idField.GetValue(itemData) is string id && !string.IsNullOrEmpty(id))
                        return $"{pickup.gameObject.scene.name}_{id}";
                }
            }
        }
        var pos = pickup.transform.position;
        return $"{pickup.gameObject.scene.name}_{pos.x:F2}_{pos.y:F2}_{pos.z:F2}";
    }

    // ========== 文件读写 ==========
    private static void LoadPickedKeys()
    {
        try
        {
            if (File.Exists(PickedKeysFilePath))
            {
                var json = File.ReadAllText(PickedKeysFilePath);
                _pickedKeys = JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new();
            }
            else
            {
                _pickedKeys.Clear();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"加载已捡记录失败: {ex}");
            _pickedKeys.Clear();
        }
    }

    private static void SavePickedKeys()
    {
        if (!_isDirty) return;
        try
        {
            var dir = Path.GetDirectoryName(PickedKeysFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(_pickedKeys, Formatting.Indented);
            File.WriteAllText(PickedKeysFilePath, json);
            _isDirty = false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"保存已捡记录失败: {ex}");
        }
    }

    // ========== 禁用所有 PersistentBoolItem ==========
    private static void DisablePersistentBoolItems()
    {
        var allPickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>();
        foreach (var p in allPickups)
        {
            var pbi = p.GetComponent<PersistentBoolItem>();
            if (pbi != null && pbi.enabled)
                pbi.enabled = false;
        }
        Plugin.Log.LogInfo("已禁用所有 PersistentBoolItem");
    }

    // ========== 恢复原生组件 ==========
    private static void RestorePersistentBoolItems()
    {
        var allPickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>();
        foreach (var p in allPickups)
        {
            var pbi = p.GetComponent<PersistentBoolItem>();
            if (pbi != null && !pbi.enabled)
                pbi.enabled = true;
        }
        Plugin.Log.LogInfo("已恢复所有 PersistentBoolItem");
    }

    // ========== 开关控制 ==========
    public static void EnableRandomizer()
    {
        DisablePersistentBoolItems();
        var scene = SceneManager.GetActiveScene();
        ApplyStateToScene(scene);
        Plugin.Log.LogInfo("物品随机器已启用，拾取点已接管");
    }

    public static void DisableRandomizer()
    {
        RestorePersistentBoolItems();
        Plugin.Log.LogInfo("物品随机器已禁用，拾取点已恢复原生行为");
    }

    // ========== 应用状态到场景 ==========
    public static void ApplyStateToScene(Scene scene)
    {
        var pickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>()
            .Where(p => p.gameObject.scene == scene).ToList();
        foreach (var p in pickups)
        {
            var key = GetPickupKey(p);
            bool isPicked = _pickedKeys.Contains(key);
            p.gameObject.SetActive(!isPicked);
        }
        Plugin.Log.LogInfo($"应用拾取点状态: 场景 {scene.name}, 共 {pickups.Count} 个点, 已捡 {_pickedKeys.Count} 个");
    }

    public static void ApplyStateToSceneWithDelay(Scene scene, float delay = 0.2f)
    {
        ApplyStateToScene(scene);
        if (Plugin.Instance != null)
            Plugin.Instance.StartCoroutine(DelayedApply(scene, delay));
    }

    private static IEnumerator DelayedApply(Scene scene, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (SceneManager.GetActiveScene().name == scene.name)
        {
            ApplyStateToScene(scene);
            Plugin.Log.LogInfo("强制重生：延迟应用拾取点状态完成");
        }
    }

    // ========== 标记点为已捡 ==========
    public static void MarkAsPicked(CollectableItemPickup pickup)
    {
        var key = GetPickupKey(pickup);
        if (_pickedKeys.Add(key))
        {
            _isDirty = true;
            SavePickedKeys();
            pickup.gameObject.SetActive(false);
            Plugin.Log.LogInfo($"标记点为已捡: {key}");
        }
    }

    // ========== 重置所有点 ==========
    public static void ResetAll()
    {
        if (File.Exists(PickedKeysFilePath))
            File.Delete(PickedKeysFilePath);
        _pickedKeys.Clear();
        _isDirty = true;
        SavePickedKeys();
        Plugin.Log.LogInfo("已捡记录已清空，所有拾取点将重新出现");
        WarpToLastBench();
    }

    private static void WarpToLastBench()
    {
        try
        {
            var pd = PlayerData.instance;
            if (pd == null) return;
            var sceneName = pd.respawnScene;
            if (string.IsNullOrEmpty(sceneName)) return;
            Plugin.Log.LogInfo($"[Warp] 传送至重生点: {sceneName}");
            var gm = GameManager.instance;
            gm.SaveGame(success =>
            {
                if (success) gm.LoadGameFromUI(gm.profileID);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"传送失败: {ex}");
        }
    }

    // ========== 初始化 ==========
    public static void Initialize()
    {
        LoadPickedKeys();
    }

    // ========== 补丁：拦截游戏内部隐藏逻辑 ==========
    [HarmonyPatch(typeof(CollectableItemPickup), "CheckActivation")]
    [HarmonyPrefix]
    private static bool Prefix_CheckActivation(CollectableItemPickup __instance)
    {
        if (!Plugin.ItemRandomEnabled.Value) return true;
        return false;
    }

    [HarmonyPatch(typeof(CollectableItemPickup), "SetPlayerDataBool")]
    [HarmonyPrefix]
    private static bool Prefix_SetPlayerDataBool(CollectableItemPickup __instance, string boolName)
    {
        if (!Plugin.ItemRandomEnabled.Value) return true;
        return false;
    }

    // ========== 补丁：拾取点随机替换（使用混合奖励） ==========
    [HarmonyPatch(typeof(CollectableItemPickup), "DoPickupAction")]
    [HarmonyPrefix]
    private static void Prefix_DoPickupAction(CollectableItemPickup __instance, ref bool __runOriginal)
    {
        try
        {
            if (!__runOriginal || __instance == null) return;

            var originalItem = __instance.Item;
            if (originalItem == null || ItemRandomizer.ExcludedNames.Contains(originalItem.name)) return;

            // 记录坐标（兼容旧逻辑）
            var key = $"{__instance.gameObject.scene.name}_{__instance.transform.position.x:F1}_{__instance.transform.position.y:F1}_{__instance.transform.position.z:F1}";
            Plugin.AddDestroyedPickupKey(key);

            // 使用混合奖励
            bool success = ItemRandomizer.GiveRandomReward();

            if (success)
            {
                Plugin.Log.LogInfo($"拾取点 {originalItem.name} 被替换为混合奖励");
            }
            else
            {
                Plugin.Log.LogError($"拾取点 {originalItem.name} 混合奖励失败！");
            }

            MarkAsPicked(__instance);
            __runOriginal = false;
            UnityEngine.Object.Destroy(__instance.gameObject);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"PickupPatch.Prefix 出错: {ex}");
        }
    }

    [HarmonyPatch(typeof(CollectableItemPickup), "DoPickupAction")]
    [HarmonyPostfix]
    private static void Postfix_DoPickupAction(CollectableItemPickup __instance)
    {
        // 所有拾取点已在 Prefix 中处理完毕
    }

    public static bool IsKeyPicked(string key) => _pickedKeys.Contains(key);
}