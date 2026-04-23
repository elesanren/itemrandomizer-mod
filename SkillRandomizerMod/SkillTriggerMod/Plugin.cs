using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SkillTriggerMod;

[BepInPlugin("YourName.SkillTriggerMod", "Skill Trigger Mod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static HashSet<string> _triggeredRecords = new HashSet<string>();
    private static string _notificationMessage = null;
    private static float _notificationEndTime = 0.0f;
    private static GUIStyle _notificationStyle;

    private readonly List<(string scene, float x, float y, float z)> targetPositions = new List<(string, float, float, float)>()
    {
        ("Mosstown_02", 86.922f, 52.568f, 0.004f),
        ("Crawl_05", 23.032f, 16.568f, 0.004f),
        ("Shellwood_10", 40.643f, 79.57f, 0.004f),
        ("Greymoor_22", 39.783f, 36.826f, 0.004f),
        ("Bone_East_05", 100.062f, 13.568f, 0.004f)
    };

    private readonly string[] shrineKeywords = new string[5]
    {
        "bind orb",
        "shrine weaver ability",
        "weaver_shrine",
        "bellshrine",
        "dash shrine"
    };

    public static ConfigEntry<int> RandomSeed { get; private set; }
    internal static Plugin Instance { get; private set; }

    private static string TriggerRecordsPath => Path.Combine(Paths.ConfigPath, "SkillTriggerMod", "trigger_records.json");

    public static void ShowNotification(string message, float duration = 3f)
    {
        _notificationMessage = message;
        _notificationEndTime = Time.time + duration;
    }

    private void OnGUI()
    {
        if (_notificationMessage == null || Time.time > _notificationEndTime)
        {
            _notificationMessage = null;
            return;
        }

        if (_notificationStyle == null)
        {
            _notificationStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _notificationStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0f, 0f, 0.8f));
            _notificationStyle.border = new RectOffset(20, 20, 10, 10);
        }

        float width = 600f;
        float height = 120f;
        float x = (Screen.width - width) / 2f;
        float y = Screen.height / 2 - 100;
        GUI.Box(new Rect(x, y, width, height), _notificationMessage, _notificationStyle);
    }

    private Texture2D MakeTexture(int width, int height, Color col)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = col;
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
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

    private void LoadTriggerRecords()
    {
        try
        {
            if (File.Exists(TriggerRecordsPath))
            {
                var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(TriggerRecordsPath));
                _triggeredRecords = list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
            else
            {
                _triggeredRecords.Clear();
            }
            Log.LogInfo($"技能触发器记录已加载，共 {_triggeredRecords.Count} 条");
        }
        catch (Exception ex)
        {
            Log.LogError($"加载技能触发器记录失败: {ex}");
            _triggeredRecords.Clear();
        }
    }

    internal void SaveTriggerRecords()
    {
        try
        {
            string dir = Path.GetDirectoryName(TriggerRecordsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(TriggerRecordsPath, JsonConvert.SerializeObject(_triggeredRecords.ToList(), Formatting.Indented));
            Log.LogInfo($"技能触发器记录已保存，共 {_triggeredRecords.Count} 条");
        }
        catch (Exception ex)
        {
            Log.LogError($"保存技能触发器记录失败: {ex}");
        }
    }

    private IEnumerator InitializeRandomizer()
    {
        yield return new WaitForSeconds(3f);
        SkillRandomizer.SetSeed(RandomSeed.Value);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private IEnumerator DisableShrinesAfterStart()
    {
        yield return new WaitForSeconds(5f);
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                DisableShrinesInScene(scene);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableShrinesInScene(scene);
        for (int i = 0; i < targetPositions.Count; i++)
        {
            var target = targetPositions[i];
            if (target.scene == scene.name)
            {
                StartCoroutine(CreateTriggerDelayed(target, i));
                break;
            }
        }
    }

    private void DisableShrinesInScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject obj = child.gameObject;
                string lower = obj.name.ToLower();
                foreach (string keyword in shrineKeywords)
                {
                    if (lower.Contains(keyword))
                    {
                        foreach (var col2d in obj.GetComponents<Collider2D>())
                            col2d.enabled = false;
                        foreach (var col in obj.GetComponents<Collider>())
                            col.enabled = false;
                        foreach (var fsm in obj.GetComponents<PlayMakerFSM>())
                            fsm.enabled = false;
                        break;
                    }
                }
            }
        }
    }

    private IEnumerator CreateTriggerDelayed((string scene, float x, float y, float z) target, int index)
    {
        yield return null;
        Vector3 pos = new Vector3(target.x, target.y, target.z);
        CreateTriggerAt(pos, target.scene, index);
    }

    private void CreateTriggerAt(Vector3 position, string sceneName, int index)
    {
        string recordKey = $"SkillTriggered_{sceneName}_{index}";
        if (_triggeredRecords.Contains(recordKey))
            return;

        GameObject obj = new GameObject($"SkillTrigger_{sceneName}_{index}");
        obj.transform.position = position;

        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(8f, 8f);

        obj.AddComponent<SkillTrigger>().SetInfo(sceneName, index, recordKey);

        Scene targetScene = SceneManager.GetSceneByName(sceneName);
        if (targetScene.IsValid())
        {
            SceneManager.MoveGameObjectToScene(obj, targetScene);
            Log.LogInfo($"触发器创建: {sceneName} 索引 {index}");
        }
        else
        {
            Log.LogError("触发器创建失败: " + sceneName);
            Destroy(obj);
        }
    }

    public static void ResetAllRecords()
    {
        _triggeredRecords.Clear();
        if (Instance != null)
            Instance.SaveTriggerRecords();
        Log.LogInfo("所有技能触发器记录已重置");
    }
}