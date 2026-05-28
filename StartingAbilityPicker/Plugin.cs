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
using HutongGames.PlayMaker;
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

    public static Plugin Instance { get; private set; }

    private static Dictionary<string, string> _abilityConfig = new();

    internal bool skillMode = false;
    internal int skillTotal = 0, skillV = 0, skillH = 0, skillS = 0, skillA = 0;
    internal const int MaxVertical = 5;
    internal const int MaxHorizontal = 4;
    internal const int MaxSpecial = 4;
    internal const int MaxAttack = 5;

    private bool _lastSceneWasMenu = true;
    private bool showUI = false;
    internal bool allowUpward = false, allowLeft = false, allowRight = false;
    internal int itemCount = 0;
    internal bool resetPickups = false;
    internal string seedInput = "";
    private static HashSet<int> chosenProfileSet = new();

    private static string _notificationMessage = null;
    private static float _notificationEndTime = 0.0f;
    private static GUIStyle _notificationStyle;

    private static Sprite _backgroundSprite;
    private static Sprite _beginnerIcon;
    private static Sprite _focusedIcon;
    private static Sprite _overflowIcon;
    private static bool _bgLoaded = false;

    // 窗口矩形（支持拖动）
    private static Rect _windowRect;
    private static float _lastScreenHeight;

    public static ConfigEntry<string> ChosenProfiles { get; private set; }
    public static ConfigEntry<int> StartingSkillCount { get; private set; }
    public static ConfigEntry<int> StartingItemCount { get; private set; }
    public static ConfigEntry<int> RandomSeed { get; private set; }

    public static ConfigEntry<bool> CfgCrestCurseEnabled { get; private set; }
    internal bool crestCurseEnabled = false;

    // 按存档存储完成度显示开关
    private static readonly string CompletionConfigPath = Path.Combine(Paths.ConfigPath, "StartingAbilityPicker", "completion_per_profile.json");
    private static Dictionary<int, bool> _profileCompletionDisplay = new Dictionary<int, bool>();

    public bool GetCrestCurseEnabled() => crestCurseEnabled;
    public void SetCrestCurseEnabled(bool value)
    {
        crestCurseEnabled = value;
        CfgCrestCurseEnabled.Value = value;
        try
        {
            Type effectType = Type.GetType("SilksongItemRandomizer.ToolEffectRandomizer, SilksongItemRandomizer");
            effectType?.GetMethod("SetEnabled", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { value });
        }
        catch { }
        Log.LogInfo($"纹章诅咒开关: {(value ? "启用" : "禁用")}");
    }

    public bool GetForceCompletionDisplay()
    {
        int pid = currentProfileID;
        if (pid == -1) return true;
        if (_profileCompletionDisplay.TryGetValue(pid, out bool value))
            return value;
        return true;
    }

    public void SetForceCompletionDisplay(bool value)
    {
        int pid = currentProfileID;
        if (pid == -1) return;

        bool current = GetForceCompletionDisplay();
        if (current == value) return;

        _profileCompletionDisplay[pid] = value;
        SaveCompletionConfig();
        Log.LogInfo($"强制完成度显示开关 (存档 {pid}): {(value ? "启用（显示）" : "禁用（隐藏）")}");

        var pd = PlayerData.instance;
        if (pd != null)
        {
            if (value)
            {
                pd.ConstructedFarsight = true;
                RefreshCompletionUI(true);
            }
            else
            {
                ClearFsmCompletionVariables();
            }
        }
    }

    private void RefreshCompletionUI(bool forceShow = false)
    {
        if (!GetForceCompletionDisplay() && !forceShow)
        {
            ClearFsmCompletionVariables();
            return;
        }

        var pd = PlayerData.instance;
        if (pd == null) return;

        string percentStr = Mathf.RoundToInt(pd.completionPercentage) + "%";
        string[] panelNames = { "Inv", "Tools", "Journal", "Quests" };
        foreach (string name in panelNames)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;
            PlayMakerFSM fsm = go.GetComponent<PlayMakerFSM>();
            if (fsm == null) continue;
            var fsmString = fsm.FsmVariables.FindFsmString("Completion Percentage Str");
            if (fsmString != null)
                fsmString.Value = percentStr;
        }
    }

    private void LoadCompletionConfig()
    {
        try
        {
            if (File.Exists(CompletionConfigPath))
            {
                string json = File.ReadAllText(CompletionConfigPath);
                _profileCompletionDisplay = JsonConvert.DeserializeObject<Dictionary<int, bool>>(json) ?? new Dictionary<int, bool>();
            }
            else
            {
                _profileCompletionDisplay.Clear();
            }
        }
        catch (Exception ex) { Log.LogWarning($"加载完成度配置失败: {ex}"); }
    }

    private void SaveCompletionConfig()
    {
        try
        {
            string dir = Path.GetDirectoryName(CompletionConfigPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(CompletionConfigPath, JsonConvert.SerializeObject(_profileCompletionDisplay, Formatting.Indented));
        }
        catch (Exception ex) { Log.LogWarning($"保存完成度配置失败: {ex}"); }
    }

    private void ClearFsmCompletionVariables()
    {
        try
        {
            string[] panelNames = { "Inv", "Tools", "Journal", "Quests" };
            foreach (string name in panelNames)
            {
                GameObject go = GameObject.Find(name);
                if (go == null) continue;
                PlayMakerFSM fsm = go.GetComponent<PlayMakerFSM>();
                if (fsm == null) continue;

                foreach (var sv in fsm.FsmVariables.StringVariables)
                {
                    if (sv.Name.IndexOf("Completion", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        sv.Value = "";
                    }
                }
            }
        }
        catch (Exception ex) { Log.LogWarning($"清空完成度变量失败: {ex}"); }
    }

    private static string AbilityConfigPath => Path.Combine(Paths.ConfigPath, "StartingAbilityPicker", "ability_config.json");
    private int currentProfileID => PlayerData.instance?.profileID ?? -1;

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

    public static Sprite BeginnerIcon => _beginnerIcon;
    public static Sprite FocusedIcon => _focusedIcon;
    public static Sprite OverflowIcon => _overflowIcon;

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
        Instance = this;
        Log = Logger;
        Log.LogInfo("StartingAbilityPicker loaded! Press F7 to open starting options.");
        ChosenProfiles = Config.Bind<string>("General", "ChosenProfiles", "", "已选择过开局选项的存档ID列表");
        StartingSkillCount = Config.Bind<int>("General", "StartingSkillCount", 0, "开局随机技能数量 (0-5)");
        StartingItemCount = Config.Bind<int>("General", "StartingItemCount", 0, "开局随机物品数量 (0-5)");
        RandomSeed = Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");

        CfgCrestCurseEnabled = Config.Bind<bool>("General", "CrestCurseEnabled", false, "启用纹章诅咒（每个纹章带有偏科效果）");
        crestCurseEnabled = CfgCrestCurseEnabled.Value;
        try
        {
            Type effectType = Type.GetType("SilksongItemRandomizer.ToolEffectRandomizer, SilksongItemRandomizer");
            effectType?.GetMethod("SetEnabled", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { crestCurseEnabled });
        }
        catch { }

        LoadCompletionConfig();
        LoadChosenProfiles();
        EnsureRandomSeed();
        LoadAbilityConfig();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Harmony.CreateAndPatchAll(typeof(AttackPatch));
        Harmony.CreateAndPatchAll(typeof(SaveStats_GetCompletionPercentage_Patch));
        Harmony.CreateAndPatchAll(typeof(SaveStats_UnlockedCompletionRate_Patch));
        Harmony.CreateAndPatchAll(typeof(PlayerData_CountGameCompletion_Patch));
        Harmony.CreateAndPatchAll(typeof(ConvertFloatToString_Patch));
        Harmony.CreateAndPatchAll(typeof(BuildString_Patch));
        StartCoroutine(LoadAllImages());
    }

    private IEnumerator LoadAllImages()
    {
        string folder = Path.Combine(Paths.PluginPath, "elesanren-Hard_Item_Randomizer", "nandu");

        string bgPath = Path.Combine(folder, "4.png");
        if (File.Exists(bgPath))
        {
            using (WWW www = new WWW("file://" + bgPath))
            {
                yield return www;
                if (string.IsNullOrEmpty(www.error) && www.texture != null)
                {
                    _backgroundSprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height), new Vector2(0.5f, 0.5f));
                }
            }
        }

        string[] iconFiles = { "1.png", "2.png", "3.png" };
        for (int i = 0; i < 3; i++)
        {
            string iconPath = Path.Combine(folder, iconFiles[i]);
            if (File.Exists(iconPath))
            {
                using (WWW www = new WWW("file://" + iconPath))
                {
                    yield return www;
                    if (string.IsNullOrEmpty(www.error) && www.texture != null)
                    {
                        Sprite sprite = Sprite.Create(www.texture, new Rect(0, 0, www.texture.width, www.texture.height), new Vector2(0.5f, 0.5f));
                        switch (i)
                        {
                            case 0: _beginnerIcon = sprite; break;
                            case 1: _focusedIcon = sprite; break;
                            case 2: _overflowIcon = sprite; break;
                        }
                    }
                }
            }
        }

        _bgLoaded = true;
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

            var pd = PlayerData.instance;
            if (pd != null)
            {
                if (pd.GetBool("AllowUpwardAttack")) AllowUpwardAttack = true;
                if (pd.GetBool("AllowLeftAttack")) AllowLeftAttack = true;
                if (pd.GetBool("AllowRightAttack")) AllowRightAttack = true;
            }
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

    public static void SaveAttackDirections()
    {
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd != null)
            {
                pd.SetBool("AllowUpwardAttack", AllowUpwardAttack);
                pd.SetBool("AllowLeftAttack", AllowLeftAttack);
                pd.SetBool("AllowRightAttack", AllowRightAttack);
            }
            Instance.SaveAbilityConfig();
            Instance.Config.Save();
            Log.LogInfo("攻击方向配置已完整保存");
        }
        catch (Exception ex) { Log.LogError($"SaveAttackDirections 失败: {ex}"); }
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

            if (GetForceCompletionDisplay())
            {
                var pd = PlayerData.instance;
                if (pd != null && !pd.ConstructedFarsight)
                {
                    pd.ConstructedFarsight = true;
                    Log.LogInfo($"已强制解锁完成度显示 (ConstructedFarsight) for profile {currentProfileID}");
                }
                RefreshCompletionUI(true);
            }
        }
        else if (scene.name == "Menu_Title" || scene.name == "Menu") _lastSceneWasMenu = true;

        if (!GetForceCompletionDisplay())
            ClearFsmCompletionVariables();
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

        if (Input.GetKeyDown(KeyCode.Tab) && !GetForceCompletionDisplay())
        {
            ClearFsmCompletionVariables();
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
            // 初始化窗口位置（仅首次或屏幕高度变化时）
            if (_windowRect.width == 0 || Mathf.Abs(Screen.height - _lastScreenHeight) > 1f)
            {
                float screenH = Screen.height;
                float winH = 1080f;          // 原 1200 * 0.9
                float winW = 925f;           // 原 950 * 0.9
                float bottomMargin = screenH / 8f;   // 底部留空 1/16
                float y = screenH - winH - bottomMargin;
                if (y < 0) y = 0;
                _windowRect = new Rect(20f, y, winW, winH);
                _lastScreenHeight = screenH;
            }

            _windowRect = GUILayout.Window(100, _windowRect, DrawUIWindow, "开局选项 & 场景随机");
        }

        // 通知消息
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
        // 绘制背景（使用当前窗口矩形的尺寸）
        if (_backgroundSprite != null && _backgroundSprite.texture != null)
        {
            GUI.DrawTexture(new Rect(0, 0, _windowRect.width, _windowRect.height), _backgroundSprite.texture, ScaleMode.StretchToFill);
        }
        else
        {
            Color originalColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, _windowRect.width, _windowRect.height), Texture2D.whiteTexture);
            GUI.color = originalColor;
        }

        // 内部布局（按比例缩小后的宽度）
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(558));   // 原 620 * 0.9
        DrawLeftPanel();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(270));   // 原 300 * 0.9
        DrawSceneRandomPanel();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    private void DrawLeftPanel() => PanelRenderer.DrawLeftPanel(this);
    private void DrawSceneRandomPanel() => PanelRenderer.DrawScenePanel(this);

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
        catch { return false; }
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
                entry?.GetType().GetProperty("Value")?.SetValue(entry, value, null);
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

    public bool GetItemRandomEnabled()
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

    public void SetItemRandomEnabled(bool value)
    {
        try
        {
            var type = Type.GetType("SilksongItemRandomizer.Plugin, SilksongItemRandomizer");
            var prop = type?.GetProperty("PublicItemRandomEnabled", BindingFlags.Public | BindingFlags.Static);
            prop?.SetValue(null, value);
        }
        catch { }
    }
}

// ================== 完成度显示补丁（存档界面） ==================
[HarmonyPatch(typeof(SaveStats), "GetCompletionPercentage")]
public static class SaveStats_GetCompletionPercentage_Patch
{
    [HarmonyPostfix]
    private static void Postfix(ref string __result)
    {
        if (Plugin.Instance != null && !Plugin.Instance.GetForceCompletionDisplay())
            __result = "";
    }
}

[HarmonyPatch(typeof(SaveStats), "get_UnlockedCompletionRate")]
public static class SaveStats_UnlockedCompletionRate_Patch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (Plugin.Instance != null && !Plugin.Instance.GetForceCompletionDisplay())
            __result = false;
    }
}

[HarmonyPatch(typeof(PlayerData), "CountGameCompletion")]
public static class PlayerData_CountGameCompletion_Patch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return !(Plugin.Instance != null && !Plugin.Instance.GetForceCompletionDisplay());
    }
}

// ================== 拦截背包完成度字符串赋值 ==================
[HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ConvertFloatToString), "OnEnter")]
public static class ConvertFloatToString_Patch
{
    private static void Postfix(HutongGames.PlayMaker.Actions.ConvertFloatToString __instance)
    {
        if (Plugin.Instance != null && !Plugin.Instance.GetForceCompletionDisplay())
        {
            if (__instance.stringVariable != null && __instance.stringVariable.Name == "Completion Percentage Str")
            {
                __instance.stringVariable.Value = "";
            }
        }
    }
}

[HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.BuildString), "OnEnter")]
public static class BuildString_Patch
{
    private static void Postfix(HutongGames.PlayMaker.Actions.BuildString __instance)
    {
        if (Plugin.Instance != null && !Plugin.Instance.GetForceCompletionDisplay())
        {
            if (__instance.storeResult != null && __instance.storeResult.Name == "Completion Percentage Str")
            {
                __instance.storeResult.Value = "";
            }
        }
    }
}