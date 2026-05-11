using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkillTriggerMod;

[BepInPlugin("YourName.SkillTriggerMod", "Skill Trigger Mod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static HashSet<string> _triggeredRecords = new();

    // 弹窗相关
    private static Sprite _popupSprite;
    private static string _popupText;
    private static float _popupStartTime;
    private static float _popupEndTime;
    private const float PopupDuration = 8f;
    private const float FadeTime = 0.5f;
    private static GUIStyle _popupTextStyle;

    private readonly List<(string scene, float x, float y, float z)> targetPositions = new()
    {
        ("Mosstown_02", 86.922f, 52.568f, 0.004f),
        ("Crawl_05", 23.032f, 16.568f, 0.004f),
        ("Shellwood_10", 40.643f, 79.57f, 0.004f),
        ("Greymoor_22", 39.783f, 36.826f, 0.004f),
        ("Bone_East_05", 100.062f, 13.568f, 0.004f),
        ("Under_18", 26f, 13f, 0.004f)
    };

    private readonly string[] shrineKeywords = { "bind orb", "shrine weaver ability", "weaver_shrine", "bellshrine", "dash shrine" };

    public static ConfigEntry<int> RandomSeed { get; private set; }
    internal static Plugin Instance { get; private set; }
    private static string TriggerRecordsPath => Path.Combine(Paths.ConfigPath, "SkillTriggerMod", "trigger_records.json");

    /// <summary>弹出技能获得提示（全透明背景、淡入淡出、半屏 3/5 大图）</summary>
    public static void ShowSkillPopup(Sprite icon, string text)
    {
        _popupSprite = icon;
        _popupText = text;
        _popupStartTime = Time.time;
        _popupEndTime = Time.time + PopupDuration;
    }

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        RandomSeed = Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
        LoadTriggerRecords();
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(DisableShrinesAfterStart());
        StartCoroutine(InitializeRandomizer());
    }

    // Plugin.cs
    // ... (其他代码保持不变) ...

    private void OnGUI()
    {
        // 技能获得弹窗逻辑
        if (!string.IsNullOrEmpty(_popupText) && Time.time < _popupEndTime)
        {
            float elapsed = Time.time - _popupStartTime;

            // 修改: 分段计算透明度 Alpha
            float alpha = 0f;
            if (elapsed < 3f)
            {
                // 0-3秒: 淡入 (0 -> 1)
                alpha = elapsed / 3f;
            }
            else if (elapsed < 5f)
            {
                // 3-5秒: 保持全亮 (1)
                alpha = 1f;
            }
            else
            {
                // 5-8秒: 淡出 (1 -> 0)
                alpha = 1f - ((elapsed - 5f) / 3f);
            }

            if (_popupTextStyle == null)
            {
                _popupTextStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 42,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }

            // 修改: 基础尺寸缩小为原来的 3/4 (0.75)
            float baseSize = (Mathf.Min(Screen.width, Screen.height) * 0.3f) * 0.75f;

            float x = (Screen.width - baseSize) / 2f;
            float y = (Screen.height - baseSize) / 2f - 30f;

            Color prevColor = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);

            if (_popupSprite != null && _popupSprite.texture != null)
            {
                Rect texRect = _popupSprite.textureRect;
                Texture2D tex = _popupSprite.texture;
                float texW = tex.width;
                float texH = tex.height;

                // 修正纹理坐标
                Rect coords = new Rect(
                    texRect.x / texW,
                    texRect.y / texH,
                    texRect.width / texW,
                    texRect.height / texH
                );

                // 修复蛛攀术 (Wall_Jump_Prompt) 倒置
                if (_popupSprite.name == "Wall_Jump_Prompt")
                {
                    coords.y += coords.height;
                    coords.height = -coords.height;
                }

                // 计算目标绘制矩形
                Rect targetRect;

                // 疾风步 (hasDash) - 拉宽 1/2 (1.5倍)
                if (_popupSprite.name == "prompt_swiftstep")
                {
                    float wideSize = baseSize * 1.5f;
                    float wideX = x - (wideSize - baseSize) / 2;
                    targetRect = new Rect(wideX, y, wideSize, baseSize);
                }
                // 飞针 (hasHarpoonDash) - 拉宽 1/2 (1.5倍)
                else if (_popupSprite.name == "prompt_hornet_silk_dash")
                {
                    float wideSize = baseSize * 1.5f;
                    float wideX = x - (wideSize - baseSize) / 2;
                    targetRect = new Rect(wideX, y, wideSize, baseSize);
                }
                // 灵丝升腾 (hasSuperJump) - 保持原样
                else if (_popupSprite.name == "prompt_super_jump")
                {
                    targetRect = new Rect(x, y, baseSize, baseSize);
                }
                // 默认情况
                else
                {
                    targetRect = new Rect(x, y, baseSize, baseSize);
                }

                // 执行绘制
                GUI.DrawTextureWithTexCoords(targetRect, tex, coords);
            }
            else
            {
                // 默认占位符
                GUI.Label(new Rect(x, y, baseSize, baseSize), "?", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(baseSize * 0.5f),
                    normal = { textColor = Color.white }
                });
            }

            float textY = y + baseSize + 20f;
            GUI.Label(new Rect(0, textY, Screen.width, 80f), _popupText, _popupTextStyle);
            GUI.color = prevColor;
        }
    }

    // ... (其余代码如 Awake, LoadTriggerRecords 等完全保持不变) ...

    // ========== 以下方法完全保持不变 ==========

    public static bool IsItemRandomEnabled()
    {
        try
        {
            var type = Type.GetType("SilksongItemRandomizer.Plugin, SilksongItemRandomizer");
            var prop = type?.GetProperty("PublicItemRandomEnabled", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) return (bool)prop.GetValue(null);
        }
        catch { }
        return false;
    }

    private void LoadTriggerRecords()
    {
        try
        {
            if (File.Exists(TriggerRecordsPath))
                _triggeredRecords = JsonConvert.DeserializeObject<HashSet<string>>(File.ReadAllText(TriggerRecordsPath)) ?? new HashSet<string>();
        }
        catch { _triggeredRecords.Clear(); }
    }

    internal void SaveTriggerRecords()
    {
        try { File.WriteAllText(TriggerRecordsPath, JsonConvert.SerializeObject(_triggeredRecords.ToList(), Formatting.Indented)); } catch { }
    }

    private IEnumerator InitializeRandomizer() { yield return new WaitForSeconds(3f); SkillRandomizer.SetSeed(RandomSeed.Value); }
    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private IEnumerator DisableShrinesAfterStart()
    {
        yield return new WaitForSeconds(5f);
        if (!IsItemRandomEnabled()) yield break;
        for (int i = 0; i < SceneManager.sceneCount; i++) { var s = SceneManager.GetSceneAt(i); if (s.isLoaded) DisableShrinesInScene(s); }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsItemRandomEnabled()) DisableShrinesInScene(scene);
        for (int i = 0; i < targetPositions.Count; i++)
            if (targetPositions[i].scene == scene.name) { StartCoroutine(CreateTriggerDelayed(targetPositions[i], i)); break; }
    }

    private void DisableShrinesInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string low = t.gameObject.name.ToLower();
                if (shrineKeywords.Any(k => low.Contains(k)))
                {
                    foreach (var c in t.GetComponents<Collider2D>()) c.enabled = false;
                    foreach (var c in t.GetComponents<Collider>()) c.enabled = false;
                    foreach (var f in t.GetComponents<PlayMakerFSM>()) f.enabled = false;
                }
            }
    }

    private IEnumerator CreateTriggerDelayed((string, float, float, float) target, int idx)
    {
        yield return null;
        CreateTriggerAt(new Vector3(target.Item2, target.Item3, target.Item4), target.Item1, idx);
    }

    private void CreateTriggerAt(Vector3 pos, string sceneName, int index)
    {
        if (!IsItemRandomEnabled()) return;
        string key = $"SkillTriggered_{sceneName}_{index}";
        if (IsTriggeredInFile(key)) return;

        GameObject obj = new GameObject($"SkillTrigger_{sceneName}_{index}");
        obj.transform.position = pos;
        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(8f, 8f);
        obj.AddComponent<SkillTrigger>().SetInfo(sceneName, index, key);

        Scene targetScene = SceneManager.GetSceneByName(sceneName);
        if (targetScene.IsValid()) { SceneManager.MoveGameObjectToScene(obj, targetScene); Log.LogInfo($"触发器创建: {sceneName} 索引 {index}"); }
        else Destroy(obj);
    }

    private bool IsTriggeredInFile(string key)
    {
        try { return File.Exists(TriggerRecordsPath) && File.ReadAllText(TriggerRecordsPath).Contains($"\"{key}\""); }
        catch { return false; }
    }

    public static void ResetAllRecords()
    {
        _triggeredRecords.Clear();
        Instance?.SaveTriggerRecords();
    }
}