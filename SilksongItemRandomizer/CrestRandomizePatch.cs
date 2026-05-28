using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(ToolCrest), "Unlock")]
public static class CrestRandomizePatch
{
    private static List<ToolCrest> _allCrests;
    private static readonly HashSet<int> ProcessedInstanceIds = new();

    private static HashSet<string> _disabledChapels = new();
    private const string DisabledChapelsKey = "CrestRandomize_DisabledChapels";

    private static readonly string[] DisableKeywords = {
        "shrine", "crest", "church", "chapel", "door", "gate", "altar", "pedestal",
        "weaver", "bell", "bind", "orb", "rune", "memory", "statue", "pillar",
        "candle", "bench", "lever", "switch", "transition", "entrance", "exit"
    };

    public static void Initialize()
    {
        _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();
        LoadDisabledChapels();
    }

    public static void ResetProcessedIds() => ProcessedInstanceIds.Clear();

    public static void ResetPersistentData()
    {
        _disabledChapels.Clear();
        PlayerPrefs.DeleteKey(DisabledChapelsKey);
        PlayerPrefs.Save();
        Plugin.Log.LogInfo("已清除教堂禁用持久化数据");
    }

    private static void LoadDisabledChapels()
    {
        string json = PlayerPrefs.GetString(DisabledChapelsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _disabledChapels = Newtonsoft.Json.JsonConvert.DeserializeObject<HashSet<string>>(json) ?? new HashSet<string>();
            }
            catch { _disabledChapels = new HashSet<string>(); }
        }
        else
        {
            _disabledChapels = new HashSet<string>();
        }
    }

    private static void SaveDisabledChapels()
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(_disabledChapels);
        PlayerPrefs.SetString(DisabledChapelsKey, json);
        PlayerPrefs.Save();
    }

    private static void DisableChurchObjectsInScene(Scene scene)
    {
        if (!_disabledChapels.Contains(scene.name)) return;

        Plugin.Log.LogInfo($"禁用场景 '{scene.name}' 中的教堂相关物体");
        int disabledCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string name = t.gameObject.name.ToLower();
                if (DisableKeywords.Any(kw => name.Contains(kw)))
                {
                    foreach (var c in t.GetComponents<Collider2D>()) c.enabled = false;
                    foreach (var c in t.GetComponents<Collider>()) c.enabled = false;
                    foreach (var fsm in t.GetComponents<PlayMakerFSM>()) fsm.enabled = false;
                    foreach (var anim in t.GetComponents<Animator>()) anim.enabled = false;
                    foreach (var rend in t.GetComponents<Renderer>()) rend.enabled = false;
                    disabledCount++;
                    Plugin.Log.LogInfo($"  已禁用: {t.gameObject.name}");
                }
            }
        }
        Plugin.Log.LogInfo($"共禁用 {disabledCount} 个物体");
    }

    public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableChurchObjectsInScene(scene);
    }

    [HarmonyPrefix]
    private static bool Prefix(ToolCrest __instance)
    {
        try
        {
            int instanceId = __instance.GetInstanceID();
            if (ProcessedInstanceIds.Contains(instanceId)) return true;
            ProcessedInstanceIds.Add(instanceId);

            string sourceName = __instance.name;
            string targetName = CrestRandomizer.GetMappedCrestName(sourceName);
            if (string.IsNullOrEmpty(targetName) || targetName == sourceName)
            {
                Plugin.Log.LogInfo($"纹章 {sourceName} 映射到自身，放行原解锁");
                return true;
            }

            if (_allCrests == null || _allCrests.Count == 0) Initialize();
            ToolCrest targetCrest = _allCrests.FirstOrDefault(c => c.name == targetName);
            if (targetCrest == null)
            {
                Plugin.Log.LogWarning($"无法找到目标纹章 {targetName}，放行原解锁");
                return true;
            }

            // 先记录已获得纹章（累加）
            CrestRandomizer.AddUnlockedCrest(targetName);

            // 禁用随机，防止解锁过程中触发链式映射
            CrestRandomizer.DisableRandom();
            targetCrest.Unlock();
            CrestRandomizer.EnableRandom();

            // 标记教堂禁用
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene.Contains("Chapel") && !_disabledChapels.Contains(currentScene))
            {
                _disabledChapels.Add(currentScene);
                SaveDisabledChapels();
                Plugin.Log.LogInfo($"标记教堂场景 '{currentScene}'，下次进入时将禁用所有教堂物体");
            }

            Plugin.Log.LogInfo($"纹章替换: {sourceName} -> {targetName} (源纹章未解锁)");
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"CrestRandomizePatch异常: {ex}");
            return true;
        }
    }
}