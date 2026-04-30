using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    private bool _sceneRandomAvailable = false;
    private object _sceneLoader;
    private object _roomRando;
    private object _seedManager;
    private PropertyInfo _cfgShowSceneLabelProp;
    private MethodInfo _getSeedMethod;
    private FieldInfo _currentSceneNameField;

    private string sceneSeedInput = "";
    private string sceneTeleportInput = "";
    private bool _pendingTeleport = false;
    private string _pendingTeleportScene = "";

    // ===== 公开访问器 =====
    public bool ShowUI { get => showUI; set => showUI = value; }
    public string SceneSeedInput { get => sceneSeedInput; set => sceneSeedInput = value; }
    public string SceneTeleportInput { get => sceneTeleportInput; set => sceneTeleportInput = value; }
    public bool PendingTeleport { get => _pendingTeleport; set => _pendingTeleport = value; }
    public string PendingTeleportScene { get => _pendingTeleportScene; set => _pendingTeleportScene = value; }
    public object SeedManager => _seedManager;
    public bool SceneRandomAvailable => _sceneRandomAvailable;
    public static int MaxVerticalDynamic => MaxVertical;
    public static int MaxHorizontalDynamic => MaxHorizontal;
    public static int MaxSpecialDynamic => MaxSpecial;
    public static int MaxAttackDynamic => MaxAttack;

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
            LoadPlayerDataSettings();
            InitSceneRandomRefs();
        }
        else if (scene.name == "Menu_Title" || scene.name == "Menu") _lastSceneWasMenu = true;
    }

    private void LoadPlayerDataSettings()
    {
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;
            skillMode = pd.GetBool("SkillRandomMode");
            skillTotal = pd.GetInt("SkillTotalCount");
            skillV = pd.GetInt("SkillVerticalCount");
            skillH = pd.GetInt("SkillHorizontalCount");
            skillS = pd.GetInt("SkillSpecialCount");
            skillA = pd.GetInt("SkillAttackCount");
            if (!skillMode && skillTotal == 0) skillTotal = StartingSkillCount.Value;
        }
        catch (Exception ex) { Log.LogError($"加载技能随机设置失败: {ex}"); }
    }

    private void InitSceneRandomRefs()
    {
        _sceneRandomAvailable = false;
        try
        {
            var roomRandoGo = GameObject.Find("__RoomRando");
            var seedManagerGo = GameObject.Find("__SeedManager");
            if (roomRandoGo == null || seedManagerGo == null) return;

            Type roomRandoType = Type.GetType("HKSilksong_Randomizer.RoomRando, HKSilksong_SceneRandomizer");
            Type seedManagerType = Type.GetType("HKSilksong_Randomizer.SeedManager, HKSilksong_SceneRandomizer");
            Type sceneLoaderType = Type.GetType("HKSilksong_Randomizer.RandomSceneLoader, HKSilksong_SceneRandomizer");
            if (roomRandoType == null || seedManagerType == null || sceneLoaderType == null) return;

            _roomRando = roomRandoGo.GetComponent(roomRandoType);
            _seedManager = seedManagerGo.GetComponent(seedManagerType);
            _sceneLoader = _seedManager.GetType().GetField("sceneLoader", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager);
            if (_sceneLoader == null) return;

            _cfgShowSceneLabelProp = sceneLoaderType.GetProperty("cfgShowSceneLabel", BindingFlags.Instance | BindingFlags.NonPublic);
            _getSeedMethod = roomRandoType.GetMethod("GetGenerationSeed");
            _currentSceneNameField = sceneLoaderType.GetField("currentSceneName", BindingFlags.Instance | BindingFlags.NonPublic);
            _sceneRandomAvailable = true;
        }
        catch (Exception ex) { Log.LogWarning($"场景随机模块初始化失败: {ex.Message}"); }
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

        if (_pendingTeleport)
        {
            _pendingTeleport = false;
            StartCoroutine(DelayedTeleport(_pendingTeleportScene));
        }
    }

    private IEnumerator DelayedTeleport(string sceneName)
    {
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

        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(620));
        DrawLeftPanel();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(300));
        DrawSceneRandomPanel();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    // ===== 面板转发 =====
    private void DrawLeftPanel()
    {
        PanelRenderer.DrawLeftPanel(this);
    }

    private void DrawSceneRandomPanel()
    {
        PanelRenderer.DrawScenePanel(this);
    }

    // ===== 确认按钮逻辑 =====
    private void ApplySettings()
    {
        AllowUpwardAttack = allowUpward; AllowLeftAttack = allowLeft; AllowRightAttack = allowRight;
        SaveAbilityConfig();

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

        StartingSkillCount.Value = skillTotal; StartingItemCount.Value = itemCount;
        Config.Save();
        chosenProfileSet.Add(currentProfileID);
        SaveChosenProfiles();
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
        RoomRandomMode.TryApply();
        showUI = false;
    }

    // ===== 场景随机辅助方法 =====
    public int GetCurrentSeed()
    {
        try { return (int)_getSeedMethod.Invoke(_roomRando, null); } catch { return 0; }
    }

    public string GetCurrentSceneName()
    {
        try { return (string)_currentSceneNameField.GetValue(_sceneLoader); } catch { return ""; }
    }

    public bool GetSceneRandomEnabled()
    {
        try
        {
            var type = Type.GetType("HKSilksong_Randomizer.RandomSceneLoader, HKSilksong_SceneRandomizer");
            return (bool)type?.GetField("EnableRandomization", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }
        catch { return true; }
    }

    public void SetSceneRandomEnabled(bool enabled)
    {
        try
        {
            var loaderType = Type.GetType("HKSilksong_Randomizer.RandomSceneLoader, HKSilksong_SceneRandomizer");
            if (loaderType == null) return;
            var field = loaderType.GetField("EnableRandomization", BindingFlags.Public | BindingFlags.Static);
            field?.SetValue(null, enabled);
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

    public bool GetShowSceneLabel()
    {
        try { return (bool)_cfgShowSceneLabelProp.GetValue(_sceneLoader); } catch { return true; }
    }

    public void SetShowSceneLabel(bool value, bool isSeed = false)
    {
        try
        {
            if (!isSeed)
                _cfgShowSceneLabelProp.SetValue(_sceneLoader, value, null);
            else
            {
                var field = _seedManager.GetType().GetField("cfgShowSeedOnScreen", BindingFlags.Instance | BindingFlags.NonPublic);
                var entry = field.GetValue(_seedManager);
                entry.GetType().GetProperty("Value").SetValue(entry, value, null);
            }
        }
        catch { }
    }

    public bool GetShowSeedLabel()
    {
        try
        {
            var seedShowEntry = _seedManager.GetType().GetField("cfgShowSeedOnScreen", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_seedManager);
            return (bool)seedShowEntry?.GetType().GetProperty("Value")?.GetValue(seedShowEntry);
        }
        catch { return true; }
    }

    // ===== 种子重置 =====
    private void ResetSeedWorld()
    {
        if (!int.TryParse(seedInput, out int inputSeed) || inputSeed == RandomSeed.Value)
            inputSeed = new Random().Next(1, int.MaxValue);
        seedInput = inputSeed.ToString();
        RandomSeed.Value = inputSeed;
        Config.Save();
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
}