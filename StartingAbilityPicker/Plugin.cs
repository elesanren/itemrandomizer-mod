using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace StartingAbilityPicker;

[BepInPlugin("YourName.StartingAbilityPicker", "Starting Ability Picker", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    public static bool AllowLeftAttack = false;
    public static bool AllowRightAttack = false;
    public static bool AllowUpwardAttack = false;

    private static Dictionary<string, string> _abilityConfig = new();

    internal bool skillMode = false;
    internal int skillTotal = 0, skillV = 0, skillH = 0, skillS = 0, skillA = 0;
    internal const int MaxVertical = 5;
    internal const int MaxHorizontal = 4;
    internal const int MaxSpecial = 4;
    internal const int MaxAttack = 5;

    private bool _lastSceneWasMenu = true;
    private bool showUI = false;
    private Rect uiWindowRect;
    internal bool allowUpward = false, allowLeft = false, allowRight = false;
    internal int itemCount = 0;
    internal bool resetPickups = false;
    internal string seedInput = "";
    private static HashSet<int> chosenProfileSet = new();

    private static string _notificationMessage = null;
    private static float _notificationEndTime = 0.0f;
    private static GUIStyle _notificationStyle;

    public static ConfigEntry<string> ChosenProfiles { get; private set; }
    public static ConfigEntry<int> StartingSkillCount { get; private set; }
    public static ConfigEntry<int> StartingItemCount { get; private set; }
    public static ConfigEntry<int> RandomSeed { get; private set; }

    private static string AbilityConfigPath => Path.Combine(Paths.ConfigPath, "StartingAbilityPicker", "ability_config.json");
    private int currentProfileID => GameManager.instance?.profileID ?? -1;

    // 场景随机反射缓存
    private bool _sceneRandomAvailable = false;
    private object _sceneLoader;
    private object _roomRando;
    private object _seedManager;
    private PropertyInfo _cfgShowSceneLabelProp;
    private PropertyInfo _cfgShowSeedProp;
    private PropertyInfo _cfgNewSeedProp;
    private PropertyInfo _cfgRegenerateTriggerProp;
    private MethodInfo _getSeedMethod;
    private FieldInfo _currentSceneNameField;

    // 场景随机输入框持久化字段
    private string sceneSeedInput = "";
    private string sceneTeleportInput = "";

    // 延迟传送
    private bool _pendingTeleport = false;
    private string _pendingTeleportScene = "";

    public static void ShowNotification(string message, float duration = 3f)
    {
        _notificationMessage = message;
        _notificationEndTime = Time.time + duration;
    }

    private Texture2D MakeTexture(int width, int height, Color col)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo("StartingAbilityPicker loaded! Press F7 to open starting options.");
        ChosenProfiles = Config.Bind<string>("General", "ChosenProfiles", "", "已选择过开局选项的存档ID列表");
        StartingSkillCount = Config.Bind<int>("General", "StartingSkillCount", 0, "开局随机技能数量 (0-5)");
        StartingItemCount = Config.Bind<int>("General", "StartingItemCount", 0, "开局随机物品数量 (0-5)");
        RandomSeed = Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
        LoadChosenProfiles();
        EnsureRandomSeed();
        LoadAbilityConfig();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Harmony.CreateAndPatchAll(typeof(AttackPatch));
    }

    private void LoadAbilityConfig()
    {
        try
        {
            if (File.Exists(AbilityConfigPath))
            {
                string json = File.ReadAllText(AbilityConfigPath);
                _abilityConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
            }
            else _abilityConfig.Clear();

            AllowUpwardAttack = _abilityConfig.TryGetValue("AllowUpwardAttack", out string up) && string.Equals(up, "true", StringComparison.OrdinalIgnoreCase);
            AllowLeftAttack = _abilityConfig.TryGetValue("AllowLeftAttack", out string left) && string.Equals(left, "true", StringComparison.OrdinalIgnoreCase);
            AllowRightAttack = _abilityConfig.TryGetValue("AllowRightAttack", out string right) && string.Equals(right, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) { Log.LogError($"加载配置失败: {ex}"); }
    }

    private void SaveAbilityConfig()
    {
        try
        {
            _abilityConfig["AllowUpwardAttack"] = AllowUpwardAttack.ToString();
            _abilityConfig["AllowLeftAttack"] = AllowLeftAttack.ToString();
            _abilityConfig["AllowRightAttack"] = AllowRightAttack.ToString();
            string dir = Path.GetDirectoryName(AbilityConfigPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(AbilityConfigPath, JsonConvert.SerializeObject(_abilityConfig, Formatting.Indented));
        }
        catch (Exception ex) { Log.LogError($"保存配置失败: {ex}"); }
    }

    private void EnsureRandomSeed()
    {
        if (RandomSeed.Value != 0) return;
        int newSeed = new Random().Next(1, int.MaxValue);
        RandomSeed.Value = newSeed;
        Config.Save();
    }

    private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Menu_Title" && scene.name != "Menu" && scene.name != "Loading" && HeroController.instance != null)
        {
            if (_lastSceneWasMenu) { StartCoroutine(ShowUIAuto()); _lastSceneWasMenu = false; }
            LoadAbilityConfig();
            try
            {
                PlayerData pd = PlayerData.instance;
                if (pd != null)
                {
                    skillMode = pd.GetBool("SkillRandomMode");
                    skillTotal = pd.GetInt("SkillTotalCount");
                    skillV = pd.GetInt("SkillVerticalCount");
                    skillH = pd.GetInt("SkillHorizontalCount");
                    skillS = pd.GetInt("SkillSpecialCount");
                    skillA = pd.GetInt("SkillAttackCount");
                    if (!skillMode && skillTotal == 0) skillTotal = StartingSkillCount.Value;
                }
            }
            catch (Exception ex) { Log.LogError($"加载技能随机设置失败: {ex}"); }
            LoadAbilityConfig();
            // 每次进入游戏场景时尝试获取场景随机组件
            _sceneRandomAvailable = false;
            try
            {
                var roomRandoGo = GameObject.Find("__RoomRando");
                var seedManagerGo = GameObject.Find("__SeedManager");
                if (roomRandoGo != null && seedManagerGo != null)
                {
                    Type roomRandoType = Type.GetType("HKSilksong_Randomizer.RoomRando, HKSilksong_SceneRandomizer");
                    Type seedManagerType = Type.GetType("HKSilksong_Randomizer.SeedManager, HKSilksong_SceneRandomizer");
                    Type sceneLoaderType = Type.GetType("HKSilksong_Randomizer.RandomSceneLoader, HKSilksong_SceneRandomizer");
                    if (roomRandoType != null && seedManagerType != null && sceneLoaderType != null)
                    {
                        _roomRando = roomRandoGo.GetComponent(roomRandoType);
                        _seedManager = seedManagerGo.GetComponent(seedManagerType);
                        // 通过SeedManager获取RandomSceneLoader实例
                        _sceneLoader = _seedManager.GetType().GetField("sceneLoader", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager);
                        if (_sceneLoader != null)
                        {
                            _cfgShowSceneLabelProp = sceneLoaderType.GetProperty("cfgShowSceneLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                            _getSeedMethod = roomRandoType.GetMethod("GetGenerationSeed");
                            _currentSceneNameField = sceneLoaderType.GetField("currentSceneName", BindingFlags.Instance | BindingFlags.NonPublic);
                            _cfgShowSeedProp = seedManagerType.GetField("cfgShowSeedOnScreen", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager)?.GetType().GetProperty("Value");
                            _cfgNewSeedProp = seedManagerType.GetField("cfgNewSeed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager)?.GetType().GetProperty("Value");
                            _cfgRegenerateTriggerProp = seedManagerType.GetField("cfgRegenerateTrigger", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager)?.GetType().GetProperty("Value");
                            _sceneRandomAvailable = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"场景随机模块初始化失败: {ex.Message}");
            }
        }
        else if (scene.name == "Menu_Title" || scene.name == "Menu") _lastSceneWasMenu = true;
    }

    private IEnumerator ShowUIAuto()
    {
        yield return null;
        if (currentProfileID != -1)
        {
            allowUpward = AllowUpwardAttack; allowLeft = AllowLeftAttack; allowRight = AllowRightAttack;
            itemCount = StartingItemCount.Value; resetPickups = false;
            seedInput = RandomSeed.Value.ToString(); showUI = true;
        }
    }

    private void LoadChosenProfiles()
    {
        chosenProfileSet.Clear();
        foreach (string s in ChosenProfiles.Value.Split(','))
            if (int.TryParse(s.Trim(), out int id)) chosenProfileSet.Add(id);
    }

    private void SaveChosenProfiles()
    {
        ChosenProfiles.Value = string.Join(",", chosenProfileSet);
        Config.Save();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F7) && currentProfileID != -1)
        {
            showUI = !showUI;
            if (showUI)
            {
                allowUpward = AllowUpwardAttack; allowLeft = AllowLeftAttack; allowRight = AllowRightAttack;
                itemCount = StartingItemCount.Value; resetPickups = false;
                seedInput = RandomSeed.Value.ToString();
            }
        }

        // 延迟传送处理
        if (_pendingTeleport)
        {
            _pendingTeleport = false;
            StartCoroutine(DelayedTeleport(_pendingTeleportScene));
        }
    }

    private IEnumerator DelayedTeleport(string sceneName)
    {
        // 等待 0.5 秒，确保菜单已关闭
        yield return new WaitForSeconds(0.5f);

        try
        {
            var sceneEntry = _sceneLoader.GetType().GetField("cfgTeleportScene", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_sceneLoader);
            var confirmEntry = _sceneLoader.GetType().GetField("cfgTeleportConfirm", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_sceneLoader);
            sceneEntry?.GetType().GetProperty("Value")?.SetValue(sceneEntry, sceneName, null);
            confirmEntry?.GetType().GetProperty("Value")?.SetValue(confirmEntry, true, null);
            ShowNotification($"正在传送到 {sceneName} ...");
        }
        catch (Exception ex) { Log.LogError($"传送失败: {ex}"); }
    }

    private void OnGUI()
    {
        if (showUI)
        {
            // 调整窗口大小：左侧原有区域 + 右侧场景随机区域
            uiWindowRect = new Rect(20f, (Screen.height - 950) / 2, 950f, 950f);
            uiWindowRect = GUILayout.Window(100, uiWindowRect, DrawUIWindow, "开局选项 & 场景随机");
        }
        if (_notificationMessage != null && Time.time <= _notificationEndTime)
        {
            if (_notificationStyle == null)
            {
                _notificationStyle = new GUIStyle(GUI.skin.box) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
                _notificationStyle.normal.textColor = Color.white;
                _notificationStyle.normal.background = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f));
            }
            float w = 600f, h = 120f;
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height / 2 - 100, w, h), _notificationMessage, _notificationStyle);
        }
        else _notificationMessage = null;
    }

    private void DrawUIWindow(int windowID)
    {
        Color originalColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, uiWindowRect.width, uiWindowRect.height), Texture2D.whiteTexture);
        GUI.color = originalColor;

        // 左右分栏
        GUILayout.BeginHorizontal();
        // 左侧原有面板
        GUILayout.BeginVertical(GUILayout.Width(620));
        DrawLeftPanel();
        GUILayout.EndVertical();

        // 右侧场景随机面板
        GUILayout.BeginVertical(GUILayout.Width(300));
        DrawSceneRandomPanel();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    // ===== 左侧原有面板（完整保留，未做任何修改）=====
    private void DrawLeftPanel()
    {
        GUI.skin.label.fontSize = 20; GUI.skin.toggle.fontSize = 20; GUI.skin.button.fontSize = 24;
        GUI.skin.horizontalSlider.fontSize = 18; GUI.skin.textField.fontSize = 18;

        GUILayout.Label(Locale.Get("当前存档开局设置："), GUILayout.Height(40));
        GUILayout.Space(15);
        bool alreadyChosen = chosenProfileSet.Contains(currentProfileID);
        if (alreadyChosen)
        {
            GUILayout.Label(Locale.Get("（此存档已设置过，只能查看）"), GUILayout.Height(35));
            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f);
            if (GUILayout.Button(Locale.Get("重置本存档设置"), GUILayout.Height(45)))
            {
                chosenProfileSet.Remove(currentProfileID); SaveChosenProfiles();
                allowUpward = false; allowLeft = false; allowRight = false;
                itemCount = 0; resetPickups = false; skillMode = false;
                skillTotal = skillV = skillH = skillS = skillA = 0;
                return;
            }
            GUI.backgroundColor = Color.white;
        }
        GUILayout.Space(20);
        GUILayout.Label(Locale.Get("攻击方向选择："), GUILayout.Height(35));
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUI.color = Color.gray; GUILayout.Label(Locale.Get("下劈 (默认)"), GUILayout.Width(200), GUILayout.Height(40));
        GUI.enabled = false; GUILayout.Toggle(true, "", GUILayout.Width(40), GUILayout.Height(40));
        GUI.enabled = !alreadyChosen; GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUI.color = allowUpward ? Color.green : Color.white;
        bool newUp = GUILayout.Toggle(allowUpward, Locale.Get("上劈"), GUILayout.Height(40));
        if (newUp != allowUpward && !alreadyChosen) allowUpward = newUp;
        GUI.color = allowLeft ? Color.green : Color.white;
        bool newLeft = GUILayout.Toggle(allowLeft, Locale.Get("左劈"), GUILayout.Height(40));
        if (newLeft != allowLeft && !alreadyChosen) allowLeft = newLeft;
        GUI.color = allowRight ? Color.green : Color.white;
        bool newRight = GUILayout.Toggle(allowRight, Locale.Get("右劈"), GUILayout.Height(40));
        if (newRight != allowRight && !alreadyChosen) allowRight = newRight;
        GUI.color = Color.white;

        GUILayout.Space(20);
        GUILayout.Label(Locale.Get("技能随机模式："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUI.color = !skillMode ? Color.green : Color.white;
        if (GUILayout.Button(Locale.Get("总随机"), GUILayout.Height(40), GUILayout.Width(130)) && !alreadyChosen) skillMode = false;
        GUI.color = skillMode ? Color.green : Color.white;
        if (GUILayout.Button(Locale.Get("分类随机"), GUILayout.Height(40), GUILayout.Width(130)) && !alreadyChosen) skillMode = true;

        // ★ 彻底疯狂按钮
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button(Locale.Get("彻底疯狂"), GUILayout.Height(40), GUILayout.Width(160)) && !alreadyChosen)
        {
            CrazyRandomizer.Apply(this);
        }
        GUI.backgroundColor = Color.white;
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
        GUILayout.Space(10);

        if (!skillMode)
        {
            GUILayout.Label(Locale.Get("开局随机技能总数量："), GUILayout.Height(35));
            GUILayout.BeginHorizontal();
            GUILayout.Label(skillTotal.ToString(), GUILayout.Width(35), GUILayout.Height(40));
            int newTotal = (int)GUILayout.HorizontalSlider(skillTotal, 0, 13, GUILayout.Width(220));
            if (newTotal != skillTotal && !alreadyChosen) skillTotal = newTotal;
            GUILayout.EndHorizontal(); GUILayout.Space(10);
        }
        else
        {
            GUILayout.Label(Locale.Get("分类随机数量："), GUILayout.Height(35));
            DrawCategorySlider(Locale.Get("垂直技能"), ref skillV, MaxVertical, alreadyChosen);
            DrawCategorySlider(Locale.Get("水平技能"), ref skillH, MaxHorizontal, alreadyChosen);
            DrawCategorySlider(Locale.Get("特殊技能"), ref skillS, MaxSpecial, alreadyChosen);
            DrawCategorySlider(Locale.Get("攻击技能"), ref skillA, MaxAttack, alreadyChosen);
        }

        GUILayout.Space(15);
        GUILayout.Label(Locale.Get("开局随机物品数量："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUILayout.Label(itemCount.ToString(), GUILayout.Width(35), GUILayout.Height(40));
        int newItem = (int)GUILayout.HorizontalSlider(itemCount, 0, 10, GUILayout.Width(220));
        if (newItem != itemCount && !alreadyChosen) itemCount = newItem;
        GUILayout.EndHorizontal();

        GUILayout.Space(25);
        GUILayout.BeginHorizontal();
        GUILayout.Label(Locale.Get("种子"), GUILayout.Width(60), GUILayout.Height(40));
        seedInput = GUILayout.TextField(seedInput, GUILayout.Width(160), GUILayout.Height(40));
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        GUI.color = resetPickups ? Color.red : Color.white;
        bool newReset = GUILayout.Toggle(resetPickups, Locale.Get("重置种子世界（含技能触发器）"), GUILayout.Height(40));
        if (newReset != resetPickups && !alreadyChosen) resetPickups = newReset;
        GUI.color = Color.white;
        if (resetPickups) { GUI.color = Color.yellow; GUILayout.Label(Locale.Get("警告：重置后当前种子世界将重新生成，所有拾取点会重生，技能触发器也会重置。"), GUILayout.Height(70)); GUI.color = Color.white; }
        GUILayout.Space(35);

        GUI.enabled = !alreadyChosen; GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("确认"), GUILayout.Height(50)) && !alreadyChosen)
        {
            AllowUpwardAttack = allowUpward; AllowLeftAttack = allowLeft; AllowRightAttack = allowRight; SaveAbilityConfig();
            try
            {
                PlayerData pd = PlayerData.instance;
                if (pd != null)
                {
                    pd.SetBool("AllowUpwardAttack", allowUpward); pd.SetBool("AllowLeftAttack", allowLeft); pd.SetBool("AllowRightAttack", allowRight);
                    pd.SetBool("SkillRandomMode", skillMode); pd.SetInt("SkillTotalCount", skillTotal);
                    pd.SetInt("SkillVerticalCount", skillV); pd.SetInt("SkillHorizontalCount", skillH);
                    pd.SetInt("SkillSpecialCount", skillS); pd.SetInt("SkillAttackCount", skillA);
                }
            }
            catch (Exception ex) { Log.LogError($"保存到 PlayerData 失败: {ex}"); }
            StartingSkillCount.Value = skillTotal; StartingItemCount.Value = itemCount; Config.Save();
            chosenProfileSet.Add(currentProfileID); SaveChosenProfiles();
            if (resetPickups) ResetSeedWorld();

            if (!skillMode)
                for (int i = 0; i < skillTotal; i++) SkillRandomizer.GiveRandomSkill();
            else
            {
                for (int i = 0; i < skillV; i++) SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.VerticalSkills);
                for (int i = 0; i < skillH; i++) SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.HorizontalSkills);
                for (int i = 0; i < skillS; i++) SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.SpecialSkills);
                for (int i = 0; i < skillA; i++) SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.AttackSkills);
            }
            for (int i = 0; i < itemCount; i++) { SavedItem item = ItemRandomizer.GetRandomItem(); if (item != null) item.TryGet(false, true); }
            showUI = false;
        }
        GUI.backgroundColor = Color.white; GUI.enabled = true;
        GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        if (GUILayout.Button(Locale.Get("关闭"), GUILayout.Height(40))) showUI = false;
        GUI.backgroundColor = Color.white;
        GUILayout.Space(15);
        int originalFontSize = GUI.skin.label.fontSize; GUI.skin.label.fontSize = 26; GUI.color = Color.yellow;
        GUILayout.Label(Locale.Get("提示: 按 F7 呼出此窗口"), GUILayout.Height(40));
        GUI.color = Color.white; GUI.skin.label.fontSize = originalFontSize;
    }

    private void DrawCategorySlider(string label, ref int value, int max, bool disabled)
    {
        GUILayout.Label($"{label} (0-{max})", GUILayout.Height(30));
        GUILayout.BeginHorizontal();
        GUILayout.Label(value.ToString(), GUILayout.Width(35), GUILayout.Height(40));
        int newVal = (int)GUILayout.HorizontalSlider(value, 0, max, GUILayout.Width(220));
        if (newVal != value && !disabled) value = newVal;
        GUILayout.EndHorizontal();
    }

    private void ResetSeedWorld()
    {
        if (!int.TryParse(seedInput, out int inputSeed) || inputSeed == RandomSeed.Value)
            inputSeed = new Random().Next(1, int.MaxValue);
        seedInput = inputSeed.ToString(); RandomSeed.Value = inputSeed; Config.Save();
        TrySyncOtherMods(inputSeed);
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd != null)
            {
                FieldInfo[] fields = typeof(PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public);
                int count = 0;
                foreach (var f in fields)
                    if (f.Name.StartsWith("SkillTriggered_")) { f.SetValue(pd, false); count++; }
                Log.LogInfo($"已重置 {count} 个技能触发器记录");
            }
        }
        catch (Exception ex) { Log.LogError($"重置技能触发器失败: {ex}"); }
    }

    private void TrySyncOtherMods(int newSeed)
    {
        try
        {
            Type srPlugin = Type.GetType("SilksongItemRandomizer.Plugin, SilksongItemRandomizer");
            if (srPlugin != null)
            {
                var prop = srPlugin.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Static);
                if (prop != null) { var entry = prop.GetValue(null) as ConfigEntry<int>; if (entry != null) entry.Value = newSeed; }
                srPlugin.GetMethod("ResetAllStaticData", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            }
        }
        catch { }
        try
        {
            Type stPlugin = Type.GetType("SkillTriggerMod.Plugin, SkillTriggerMod");
            if (stPlugin != null)
            {
                var prop = stPlugin.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Static);
                if (prop != null) { var entry = prop.GetValue(null) as ConfigEntry<int>; if (entry != null) entry.Value = newSeed; }
                Type.GetType("SkillTriggerMod.SkillRandomizer, SkillTriggerMod")?.GetMethod("SetSeed", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { newSeed });
                stPlugin.GetMethod("ResetAllRecords", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            }
            string triggerFilePath = Path.Combine(Paths.ConfigPath, "SkillTriggerMod", "trigger_records.json");
            string dir = Path.GetDirectoryName(triggerFilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(triggerFilePath, "[]");
        }
        catch { }
        try
        {
            Type crestRandomizer = Type.GetType("SilksongItemRandomizer.CrestRandomizer, SilksongItemRandomizer");
            crestRandomizer?.GetMethod("ResetMappings", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            crestRandomizer?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            Type.GetType("SilksongItemRandomizer.CrestRandomizePatch, SilksongItemRandomizer")?.GetMethod("ResetProcessedIds", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            Type.GetType("SilksongItemRandomizer.BenchRespawnPatch, SilksongItemRandomizer")?.GetMethod("ResetCooldown", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
        catch { }
    }

    // ===== 右侧场景随机面板（最终版）=====
    // ===== 右侧场景随机面板（最终版）=====
    private void DrawSceneRandomPanel()
    {
        if (!_sceneRandomAvailable)
        {
            GUILayout.Label(Locale.Get("场景随机未加载"), GUILayout.Height(30));
            return;
        }

        // ★ 语言测试按钮（方便验证中英切换）
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CN", GUILayout.Height(20), GUILayout.Width(60)))
            Locale.SetForceChinese(true);
        if (GUILayout.Button("EN", GUILayout.Height(20), GUILayout.Width(60)))
            Locale.SetForceChinese(false);
        if (GUILayout.Button("Auto", GUILayout.Height(20), GUILayout.Width(60)))
            Locale.SetForceChinese(null);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        GUILayout.Label(Locale.Get("场景随机设置"), GUILayout.Height(30));
        GUILayout.Space(12);

        // 全局开关
        bool enableRandom = true;
        try
        {
            var loaderType = Type.GetType("HKSilksong_Randomizer.RandomSceneLoader, HKSilksong_SceneRandomizer");
            enableRandom = (bool)loaderType?.GetField("EnableRandomization", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }
        catch { }

        GUI.color = enableRandom ? Color.green : Color.white;
        if (GUILayout.Toggle(enableRandom, Locale.Get("启用场景随机"), GUILayout.Height(30)))
        {
            if (!enableRandom) SetSceneRandomEnabled(true);
        }
        else
        {
            if (enableRandom) SetSceneRandomEnabled(false);
        }
        GUI.color = Color.white;

        GUILayout.Space(12);

        // 当前种子
        int seed = 0;
        try { seed = (int)_getSeedMethod.Invoke(_roomRando, null); } catch { }
        GUILayout.Label($"{Locale.Get("当前种子")}: {seed}", GUILayout.Height(30));
        GUILayout.Space(8);

        // 当前场景名
        string sceneName = "";
        try { sceneName = (string)_currentSceneNameField.GetValue(_sceneLoader); } catch { }
        GUILayout.Label($"{Locale.Get("当前场景")}: {sceneName}", GUILayout.Height(30));

        GUILayout.Space(18);

        // 修改种子
        GUILayout.Label(Locale.Get("修改种子:"), GUILayout.Height(25));
        sceneSeedInput = GUILayout.TextField(sceneSeedInput, GUILayout.Width(260), GUILayout.Height(30));
        GUILayout.Space(4);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("应用种子"), GUILayout.Height(32), GUILayout.Width(260)))
        {
            int newSeed;
            string input = sceneSeedInput.Trim();

            if (string.IsNullOrEmpty(input))
            {
                // 空输入 → 自动随机种子（生成一个非零随机值）
                newSeed = new Random().Next(1, int.MaxValue);
            }
            else if (int.TryParse(input, out newSeed))
            {
                // 有效数字（包括0）→ 使用输入值，newSeed 已经是解析结果
            }
            else
            {
                // 输入不是数字，直接忽略本次点击
                return;
            }

            try
            {
                var seedEntry = _seedManager.GetType().GetField("cfgNewSeed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager);
                var triggerEntry = _seedManager.GetType().GetField("cfgRegenerateTrigger", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager);
                seedEntry.GetType().GetProperty("Value").SetValue(seedEntry, newSeed, null);
                triggerEntry.GetType().GetProperty("Value").SetValue(triggerEntry, true, null);
                ShowNotification(Locale.Get("场景连接已重新生成"));
            }
            catch (Exception ex) { Log.LogError($"场景种子更新失败: {ex}"); }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(20);

        // 场景传送
        GUILayout.Label(Locale.Get("场景传送:"), GUILayout.Height(25));
        sceneTeleportInput = GUILayout.TextField(sceneTeleportInput, GUILayout.Width(260), GUILayout.Height(30));
        GUILayout.Space(4);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("传送"), GUILayout.Height(32), GUILayout.Width(260)))
        {
            if (!string.IsNullOrWhiteSpace(sceneTeleportInput))
            {
                _pendingTeleport = true;
                _pendingTeleportScene = sceneTeleportInput;
                showUI = false;
                ShowNotification(Locale.Get("稍后传送至") + sceneTeleportInput + " ...");
            }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(20);

        // 显示开关
        bool showSceneLabel = true;
        bool showSeedLabel = true;
        try
        {
            showSceneLabel = (bool)_cfgShowSceneLabelProp.GetValue(_sceneLoader);
            var seedShowEntry = _seedManager.GetType().GetField("cfgShowSeedOnScreen", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager);
            showSeedLabel = (bool)seedShowEntry?.GetType().GetProperty("Value")?.GetValue(seedShowEntry);
        }
        catch { }

        GUILayout.Label(Locale.Get("显示选项:"), GUILayout.Height(25));
        GUILayout.Space(4);

        GUI.color = showSceneLabel ? Color.green : Color.white;
        if (GUILayout.Toggle(showSceneLabel, Locale.Get("显示当前场景名"), GUILayout.Height(30)))
        {
            if (!showSceneLabel) _cfgShowSceneLabelProp.SetValue(_sceneLoader, true, null);
        }
        else
        {
            if (showSceneLabel) _cfgShowSceneLabelProp.SetValue(_sceneLoader, false, null);
        }
        GUI.color = Color.white;
        GUILayout.Space(6);

        GUI.color = showSeedLabel ? Color.green : Color.white;
        if (GUILayout.Toggle(showSeedLabel, Locale.Get("显示当前种子"), GUILayout.Height(30)))
        {
            if (!showSeedLabel)
            {
                var field = _seedManager.GetType().GetField("cfgShowSeedOnScreen", BindingFlags.Instance | BindingFlags.NonPublic);
                var entry = field.GetValue(_seedManager);
                entry.GetType().GetProperty("Value").SetValue(entry, true, null);
            }
        }
        else
        {
            if (showSeedLabel)
            {
                var field = _seedManager.GetType().GetField("cfgShowSeedOnScreen", BindingFlags.Instance | BindingFlags.NonPublic);
                var entry = field.GetValue(_seedManager);
                entry.GetType().GetProperty("Value").SetValue(entry, false, null);
            }
        }
        GUI.color = Color.white;
    }

    // ★ 新增：设置场景随机开关
    private void SetSceneRandomEnabled(bool enabled)
    {
        try
        {
            var loaderType = Type.GetType("HKSilksong_Randomizer.RandomSceneLoader, HKSilksong_SceneRandomizer");
            if (loaderType == null) return;

            // 设置静态字段 EnableRandomization
            var field = loaderType.GetField("EnableRandomization", BindingFlags.Public | BindingFlags.Static);
            field?.SetValue(null, enabled);

            // 同步配置项 cfgEnableRandomization，保证重启后设置保留
            var cfgField = loaderType.GetField("cfgEnableRandomization", BindingFlags.Instance | BindingFlags.NonPublic);
            var instance = UnityEngine.Object.FindAnyObjectByType(loaderType);
            if (instance != null && cfgField != null)
            {
                var cfg = cfgField.GetValue(instance);
                cfg?.GetType().GetProperty("Value")?.SetValue(cfg, enabled, null);
            }
        }
        catch { }
    }
}