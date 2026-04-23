using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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
using Random = System.Random;

namespace StartingAbilityPicker;

[BepInPlugin("YourName.StartingAbilityPicker", "Starting Ability Picker", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    public static bool AllowLeftAttack = false;
    public static bool AllowRightAttack = false;
    public static bool AllowUpwardAttack = false;

    private static Dictionary<string, string> _abilityConfig = new Dictionary<string, string>();

    // 技能分类相关字段
    private bool skillMode = false;
    private int skillTotal = 0;
    private int skillV = 0;
    private int skillH = 0;
    private int skillS = 0;
    private int skillA = 0;
    private const int MaxVertical = 5;
    private const int MaxHorizontal = 4;
    private const int MaxSpecial = 4;
    private const int MaxAttack = 5;

    private bool _lastSceneWasMenu = true;
    private bool showUI = false;
    private Rect uiWindowRect;
    private bool allowUpward = false;
    private bool allowLeft = false;
    private bool allowRight = false;
    private int itemCount = 0;
    private bool resetPickups = false;
    private string seedInput = "";
    private static HashSet<int> chosenProfileSet = new HashSet<int>();

    private static string _notificationMessage = null;
    private static float _notificationEndTime = 0.0f;
    private static GUIStyle _notificationStyle;

    public static ConfigEntry<string> ChosenProfiles { get; private set; }
    public static ConfigEntry<int> StartingSkillCount { get; private set; }
    public static ConfigEntry<int> StartingItemCount { get; private set; }
    public static ConfigEntry<int> RandomSeed { get; private set; }

    private static string AbilityConfigPath => Path.Combine(Paths.ConfigPath, "StartingAbilityPicker", "ability_config.json");

    private int currentProfileID => GameManager.instance?.profileID ?? -1;

    // ######################################################
    // ### 改动开始：实时调试状态出口（数据存储字段）     ###
    // ######################################################
    //public static bool Debug_UpPressed = false;
    //public static bool Debug_DownPressed = false;
    //public static bool Debug_FacingRight = true;
    //public static bool Debug_OnGround = true;
    //public static bool Debug_AllowCancel = false;
    //public static bool Debug_AllowUp = false;
    //public static bool Debug_AllowLeft = false;
    //public static bool Debug_AllowRight = false;
    //public static string Debug_IntendedDirection = "";
    //public static bool Debug_AttackAllowed = true;
    //public static float Debug_LastUpdateTime = 0f;
    // ######################################################
    // ###                改动结束                         ###
    // ######################################################

    public static void ShowNotification(string message, float duration = 3f)
    {
        _notificationMessage = message;
        _notificationEndTime = Time.time + duration;
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
                _abilityConfig = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(AbilityConfigPath)) ?? new Dictionary<string, string>();
            else
                _abilityConfig.Clear();

            AllowUpwardAttack = _abilityConfig.TryGetValue("AllowUpwardAttack", out string up) && up == "true";
            AllowLeftAttack = _abilityConfig.TryGetValue("AllowLeftAttack", out string left) && left == "true";
            AllowRightAttack = _abilityConfig.TryGetValue("AllowRightAttack", out string right) && right == "true";

            Log.LogInfo($"[LoadAbilityConfig] Loaded: up={AllowUpwardAttack}, left={AllowLeftAttack}, right={AllowRightAttack}");
        }
        catch (Exception ex)
        {
            Log.LogError($"加载配置失败: {ex}");
        }
    }

    private void SaveAbilityConfig()
    {
        try
        {
            _abilityConfig["AllowUpwardAttack"] = AllowUpwardAttack.ToString();
            _abilityConfig["AllowLeftAttack"] = AllowLeftAttack.ToString();
            _abilityConfig["AllowRightAttack"] = AllowRightAttack.ToString();

            string dir = Path.GetDirectoryName(AbilityConfigPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(AbilityConfigPath, JsonConvert.SerializeObject(_abilityConfig, Formatting.Indented));
            Log.LogInfo($"[SaveAbilityConfig] Saved: up={AllowUpwardAttack}, left={AllowLeftAttack}, right={AllowRightAttack}");
        }
        catch (Exception ex)
        {
            Log.LogError($"保存配置失败: {ex}");
        }
    }

    private void EnsureRandomSeed()
    {
        if (RandomSeed.Value != 0) return;
        int newSeed = new Random().Next(1, int.MaxValue);
        RandomSeed.Value = newSeed;
        Config.Save();
        Log.LogInfo($"已自动生成真实种子: {newSeed}");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Menu_Title" && scene.name != "Menu" && scene.name != "Loading" && HeroController.instance != null)
        {
            if (_lastSceneWasMenu)
            {
                StartCoroutine(ShowUIAuto());
                _lastSceneWasMenu = false;
            }
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
                    if (!skillMode && skillTotal == 0)
                        skillTotal = StartingSkillCount.Value;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"加载技能随机设置失败: {ex}");
            }
        }
        else if (scene.name == "Menu_Title" || scene.name == "Menu")
        {
            _lastSceneWasMenu = true;
        }
    }

    private IEnumerator ShowUIAuto()
    {
        yield return null;
        if (currentProfileID != -1)
        {
            allowUpward = AllowUpwardAttack;
            allowLeft = AllowLeftAttack;
            allowRight = AllowRightAttack;
            itemCount = StartingItemCount.Value;
            resetPickups = false;
            seedInput = RandomSeed.Value.ToString();
            showUI = true;
            Log.LogInfo("加载存档，自动打开开局选项界面");
        }
    }

    private void LoadChosenProfiles()
    {
        chosenProfileSet.Clear();
        foreach (string s in ChosenProfiles.Value.Split(','))
        {
            if (int.TryParse(s.Trim(), out int id))
                chosenProfileSet.Add(id);
        }
    }

    private void SaveChosenProfiles()
    {
        ChosenProfiles.Value = string.Join(",", chosenProfileSet);
        Config.Save();
    }

    // ######################################################
    // ### 改动开始：供 AttackPatch 调用的数据更新方法    ###
    // ######################################################
    //public static void UpdateDebugAttackState(bool upPressed, bool downPressed, bool facingRight, bool onGround, bool allowCancel,
    //                                         bool allowUp, bool allowLeft, bool allowRight, string intendedDirection, bool allowed)
    //{
    //    Debug_UpPressed = upPressed;
    //    Debug_DownPressed = downPressed;
    //    Debug_FacingRight = facingRight;
    //    Debug_OnGround = onGround;
    //    Debug_AllowCancel = allowCancel;
    //    Debug_AllowUp = allowUp;
    //    Debug_AllowLeft = allowLeft;
    //    Debug_AllowRight = allowRight;
    //    Debug_IntendedDirection = intendedDirection;
    //    Debug_AttackAllowed = allowed;
    //    Debug_LastUpdateTime = Time.time;
    //}
    // ######################################################
    // ###                改动结束                         ###
    // ######################################################

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F7) || currentProfileID == -1) return;

        showUI = !showUI;
        if (showUI)
        {
            allowUpward = AllowUpwardAttack;
            allowLeft = AllowLeftAttack;
            allowRight = AllowRightAttack;
            itemCount = StartingItemCount.Value;
            resetPickups = false;
            seedInput = RandomSeed.Value.ToString();
        }

        // ######################################################
        // ### 改动开始：快捷键切换调试 UI（F8）              ###
        // ######################################################
        //if (Input.GetKeyDown(KeyCode.F5))
        //{
        //    DebugAttackUI.Toggle();
        //    Log.LogInfo("实时调试窗口 " + (DebugAttackUI.IsVisible ? "打开" : "关闭"));
        //}
        // ######################################################
        // ###                改动结束                         ###
        // ######################################################
    }

    private void OnGUI()
    {
        if (showUI)
        {
            uiWindowRect = new Rect(20f, (Screen.height - 950) / 2, 650f, 950f);
            uiWindowRect = GUILayout.Window(100, uiWindowRect, DrawUIWindow, "开局选项");
        }

        // ######################################################
        // ### 改动开始：调用独立调试 UI 的绘制方法           ###
        // ######################################################
        //DebugAttackUI.Draw();
        // ######################################################
        // ###                改动结束                         ###
        // ######################################################

        if (_notificationMessage != null && Time.time <= _notificationEndTime)
        {
            if (_notificationStyle == null)
            {
                _notificationStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 40,
                    alignment = TextAnchor.MiddleCenter
                };
                _notificationStyle.normal.textColor = Color.white;
                _notificationStyle.normal.background = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f));
            }
            float w = 600f, h = 120f;
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height / 2 - 100, w, h), _notificationMessage, _notificationStyle);
        }
        else
        {
            _notificationMessage = null;
        }
    }

    private void DrawUIWindow(int windowID)
    {
        Color originalColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, uiWindowRect.width, uiWindowRect.height), Texture2D.whiteTexture);
        GUI.color = originalColor;

        GUILayout.BeginVertical();

        GUI.skin.label.fontSize = 20;
        GUI.skin.toggle.fontSize = 20;
        GUI.skin.button.fontSize = 24;
        GUI.skin.horizontalSlider.fontSize = 18;
        GUI.skin.textField.fontSize = 18;

        GUILayout.Label("当前存档开局设置：", GUILayout.Height(40));
        GUILayout.Space(15);

        bool alreadyChosen = chosenProfileSet.Contains(currentProfileID);
        if (alreadyChosen)
        {
            GUILayout.Label("（此存档已设置过，只能查看）", GUILayout.Height(35));
            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f);
            if (GUILayout.Button("重置本存档设置", GUILayout.Height(45)))
            {
                chosenProfileSet.Remove(currentProfileID);
                SaveChosenProfiles();
                allowUpward = false;
                allowLeft = false;
                allowRight = false;
                itemCount = 0;
                resetPickups = false;
                skillMode = false;
                skillTotal = skillV = skillH = skillS = skillA = 0;
                return;
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(20);
        GUILayout.Label("攻击方向选择：", GUILayout.Height(35));
        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUI.color = Color.gray;
        GUILayout.Label("下劈 (默认)", GUILayout.Width(200), GUILayout.Height(40));
        GUI.enabled = false;
        GUILayout.Toggle(true, "", GUILayout.Width(40), GUILayout.Height(40));
        GUI.enabled = !alreadyChosen;
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUI.color = allowUpward ? Color.green : Color.white;
        bool newUp = GUILayout.Toggle(allowUpward, "上劈", GUILayout.Height(40));
        if (newUp != allowUpward && !alreadyChosen) allowUpward = newUp;

        GUI.color = allowLeft ? Color.green : Color.white;
        bool newLeft = GUILayout.Toggle(allowLeft, "左劈", GUILayout.Height(40));
        if (newLeft != allowLeft && !alreadyChosen) allowLeft = newLeft;

        GUI.color = allowRight ? Color.green : Color.white;
        bool newRight = GUILayout.Toggle(allowRight, "右劈", GUILayout.Height(40));
        if (newRight != allowRight && !alreadyChosen) allowRight = newRight;
        GUI.color = Color.white;

        GUILayout.Space(20);
        GUILayout.Label("技能随机模式：", GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUI.color = !skillMode ? Color.green : Color.white;
        if (GUILayout.Button("总随机", GUILayout.Height(40), GUILayout.Width(150)) && !alreadyChosen)
            skillMode = false;
        GUI.color = skillMode ? Color.green : Color.white;
        if (GUILayout.Button("分类随机", GUILayout.Height(40), GUILayout.Width(150)) && !alreadyChosen)
            skillMode = true;
        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        if (!skillMode)
        {
            GUILayout.Label("开局随机技能总数量：", GUILayout.Height(35));
            GUILayout.BeginHorizontal();
            GUILayout.Label(skillTotal.ToString(), GUILayout.Width(35), GUILayout.Height(40));
            int newTotal = (int)GUILayout.HorizontalSlider(skillTotal, 0, 13, GUILayout.Width(220));
            if (newTotal != skillTotal && !alreadyChosen) skillTotal = newTotal;
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
        else
        {
            GUILayout.Label("分类随机数量：", GUILayout.Height(35));
            DrawCategorySlider("垂直技能", ref skillV, MaxVertical, alreadyChosen);
            DrawCategorySlider("水平技能", ref skillH, MaxHorizontal, alreadyChosen);
            DrawCategorySlider("特殊技能", ref skillS, MaxSpecial, alreadyChosen);
            DrawCategorySlider("攻击技能", ref skillA, MaxAttack, alreadyChosen);
        }

        GUILayout.Space(15);
        GUILayout.Label("开局随机物品数量：", GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUILayout.Label(itemCount.ToString(), GUILayout.Width(35), GUILayout.Height(40));
        int newItem = (int)GUILayout.HorizontalSlider(itemCount, 0, 10, GUILayout.Width(220));
        if (newItem != itemCount && !alreadyChosen) itemCount = newItem;
        GUILayout.EndHorizontal();

        GUILayout.Space(25);
        GUILayout.BeginHorizontal();
        GUILayout.Label("种子", GUILayout.Width(60), GUILayout.Height(40));
        seedInput = GUILayout.TextField(seedInput, GUILayout.Width(160), GUILayout.Height(40));
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUI.color = resetPickups ? Color.red : Color.white;
        bool newReset = GUILayout.Toggle(resetPickups, "重置种子世界（含技能触发器）", GUILayout.Height(40));
        if (newReset != resetPickups && !alreadyChosen) resetPickups = newReset;
        GUI.color = Color.white;

        if (resetPickups)
        {
            GUI.color = Color.yellow;
            GUILayout.Label("警告：重置后当前种子世界将重新生成，所有拾取点会重生，技能触发器也会重置。", GUILayout.Height(70));
            GUI.color = Color.white;
        }

        GUILayout.Space(35);
        GUI.enabled = !alreadyChosen;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("确认", GUILayout.Height(50)) && !alreadyChosen)
        {
            AllowUpwardAttack = allowUpward;
            AllowLeftAttack = allowLeft;
            AllowRightAttack = allowRight;
            SaveAbilityConfig();

            try
            {
                PlayerData pd = PlayerData.instance;
                if (pd != null)
                {
                    pd.SetBool("AllowUpwardAttack", allowUpward);
                    pd.SetBool("AllowLeftAttack", allowLeft);
                    pd.SetBool("AllowRightAttack", allowRight);
                    pd.SetBool("SkillRandomMode", skillMode);
                    pd.SetInt("SkillTotalCount", skillTotal);
                    pd.SetInt("SkillVerticalCount", skillV);
                    pd.SetInt("SkillHorizontalCount", skillH);
                    pd.SetInt("SkillSpecialCount", skillS);
                    pd.SetInt("SkillAttackCount", skillA);
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"保存到 PlayerData 失败: {ex}");
            }

            StartingSkillCount.Value = skillTotal;
            StartingItemCount.Value = itemCount;
            Config.Save();

            chosenProfileSet.Add(currentProfileID);
            SaveChosenProfiles();

            if (resetPickups)
                ResetSeedWorld();

            // 给予技能
            if (!skillMode)
            {
                for (int i = 0; i < skillTotal; i++)
                    SkillRandomizer.GiveRandomSkill();
            }
            else
            {
                for (int i = 0; i < skillV; i++)
                    SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.VerticalSkills);
                for (int i = 0; i < skillH; i++)
                    SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.HorizontalSkills);
                for (int i = 0; i < skillS; i++)
                    SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.SpecialSkills);
                for (int i = 0; i < skillA; i++)
                    SkillRandomizer.GiveRandomSkillFromCategory(SkillRandomizer.AttackSkills);
            }

            // 给予物品
            for (int i = 0; i < itemCount; i++)
            {
                SavedItem item = ItemRandomizer.GetRandomItem();
                if (item != null) item.TryGet(false, true);
            }

            showUI = false;
            Log.LogInfo($"开局设置已保存: 上劈={allowUpward}, 左劈={allowLeft}, 右劈={allowRight}, 技能模式={(skillMode ? "分类" : "总")}, V={skillV}, H={skillH}, S={skillS}, A={skillA}, 物品={itemCount}, 重置种子={resetPickups}");
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        if (GUILayout.Button("关闭", GUILayout.Height(40)))
            showUI = false;
        GUI.backgroundColor = Color.white;

        GUILayout.Space(15);
        int originalFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 26;
        GUI.color = Color.yellow;
        GUILayout.Label("提示: 按 F7 呼出此窗口", GUILayout.Height(40));
        GUI.color = Color.white;
        GUI.skin.label.fontSize = originalFontSize;

        GUILayout.EndVertical();
        GUI.DragWindow();
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
        {
            inputSeed = new Random().Next(1, int.MaxValue);
            Log.LogInfo($"输入无效或与原种子相同，自动生成新种子: {inputSeed}");
        }

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
                {
                    if (f.Name.StartsWith("SkillTriggered_"))
                    {
                        f.SetValue(pd, false);
                        count++;
                    }
                }
                Log.LogInfo($"已重置 {count} 个技能触发器记录");
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"重置技能触发器失败: {ex}");
        }

        Log.LogInfo("请重载当前场景以使所有随机世界重置。");
    }

    private void TrySyncOtherMods(int newSeed)
    {
        // 同步 SilksongItemRandomizer
        try
        {
            Type srPlugin = Type.GetType("SilksongItemRandomizer.Plugin, SilksongItemRandomizer");
            if (srPlugin != null)
            {
                var prop = srPlugin.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    var entry = prop.GetValue(null) as ConfigEntry<int>;
                    if (entry != null) entry.Value = newSeed;
                }
                srPlugin.GetMethod("ResetAllStaticData", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                Log.LogInfo("已同步 SilksongItemRandomizer 种子");
            }
        }
        catch { }

        // 同步 SkillTriggerMod
        try
        {
            Type stPlugin = Type.GetType("SkillTriggerMod.Plugin, SkillTriggerMod");
            if (stPlugin != null)
            {
                var prop = stPlugin.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    var entry = prop.GetValue(null) as ConfigEntry<int>;
                    if (entry != null) entry.Value = newSeed;
                }
                Type.GetType("SkillTriggerMod.SkillRandomizer, SkillTriggerMod")
                    ?.GetMethod("SetSeed", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { newSeed });
                Log.LogInfo("已同步 SkillTriggerMod 种子");
            }
        }
        catch { }

        // 重置纹章相关
        try
        {
            Type crestRandomizer = Type.GetType("SilksongItemRandomizer.CrestRandomizer, SilksongItemRandomizer");
            crestRandomizer?.GetMethod("ResetMappings", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            crestRandomizer?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            Type.GetType("SilksongItemRandomizer.CrestRandomizePatch, SilksongItemRandomizer")?.GetMethod("ResetProcessedIds", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            Type.GetType("SilksongItemRandomizer.BenchRespawnPatch, SilksongItemRandomizer")?.GetMethod("ResetCooldown", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            Log.LogInfo("已重置纹章相关缓存");
        }
        catch { }
    }
}