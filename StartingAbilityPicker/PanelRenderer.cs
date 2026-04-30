using System;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace StartingAbilityPicker;

public static class PanelRenderer
{
    // ===== 左侧面板 =====
    public static void DrawLeftPanel(Plugin p)
    {
        GUI.skin.label.fontSize = 20; GUI.skin.toggle.fontSize = 20; GUI.skin.button.fontSize = 24;
        GUI.skin.horizontalSlider.fontSize = 18; GUI.skin.textField.fontSize = 18;

        GUILayout.Label(Locale.Get("当前存档开局设置："), GUILayout.Height(40));
        GUILayout.Space(15);
        bool alreadyChosen = IsAlreadyChosen(p);
        if (alreadyChosen)
        {
            GUILayout.Label(Locale.Get("（此存档已设置过，只能查看）"), GUILayout.Height(35));
            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f);
            if (GUILayout.Button(Locale.Get("重置本存档设置"), GUILayout.Height(45)))
            {
                ResetProfile(p);
                return;
            }
            GUI.backgroundColor = Color.white;
        }
        GUILayout.Space(20);

        // 攻击方向
        DrawAttackDirection(p, alreadyChosen);

        GUILayout.Space(20);

        // 技能模式按钮行
        DrawSkillModeButtons(p, alreadyChosen);

        // 技能滑块或分类滑块
        GUILayout.Space(10);
        if (!p.skillMode)
            DrawTotalSlider(p, alreadyChosen);
        else
            DrawCategorySliders(p, alreadyChosen);

        GUILayout.Space(15);

        // 物品数量滑块
        DrawItemSlider(p, alreadyChosen);

        GUILayout.Space(25);

        // 种子输入
        DrawSeedInput(p, alreadyChosen);

        // 确认 / 关闭
        DrawConfirmAndClose(p, alreadyChosen);

        GUILayout.Space(15);
        int originalFontSize = GUI.skin.label.fontSize; GUI.skin.label.fontSize = 26; GUI.color = Color.yellow;
        GUILayout.Label(Locale.Get("提示: 按 F7 呼出此窗口"), GUILayout.Height(40));
        GUI.color = Color.white; GUI.skin.label.fontSize = originalFontSize;
    }

    // ===== 右侧面板（场景随机 + 陷阱随机）=====
    public static void DrawScenePanel(Plugin p)
    {
        if (!p.SceneRandomAvailable)
        {
            GUILayout.Label(Locale.Get("场景随机未加载"), GUILayout.Height(30));
            return;
        }

        // 语言测试按钮
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CN", GUILayout.Height(20), GUILayout.Width(60))) Locale.SetForceChinese(true);
        if (GUILayout.Button("EN", GUILayout.Height(20), GUILayout.Width(60))) Locale.SetForceChinese(false);
        if (GUILayout.Button("Auto", GUILayout.Height(20), GUILayout.Width(60))) Locale.SetForceChinese(null);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        GUILayout.Label(Locale.Get("场景随机设置"), GUILayout.Height(30));
        GUILayout.Space(12);

        // 场景随机总开关
        DrawSceneToggle(p);

        // 陷阱随机开关
        DrawTrapToggle(p);

        GUILayout.Space(12);

        // 当前种子
        int seed = p.GetCurrentSeed();
        GUILayout.Label($"{Locale.Get("当前种子")}: {seed}", GUILayout.Height(30));
        GUILayout.Space(8);

        // 当前场景
        string sceneName = p.GetCurrentSceneName();
        GUILayout.Label($"{Locale.Get("当前场景")}: {sceneName}", GUILayout.Height(30));
        GUILayout.Space(18);

        // 修改种子
        DrawSeedChanger(p);

        GUILayout.Space(20);

        // 场景传送
        DrawTeleporter(p);

        GUILayout.Space(20);

        // 显示开关
        DrawDisplayToggles(p);
    }

    // ===== 子模块 =====

    private static bool IsAlreadyChosen(Plugin p)
    {
        var currentProfileID = GameManager.instance?.profileID ?? -1;
        var field = typeof(Plugin).GetField("chosenProfileSet", BindingFlags.Instance | BindingFlags.NonPublic);
        var set = field?.GetValue(p) as System.Collections.Generic.HashSet<int>;
        return set != null && set.Contains(currentProfileID);
    }

    private static void ResetProfile(Plugin p)
    {
        var currentProfileID = GameManager.instance?.profileID ?? -1;
        var setField = typeof(Plugin).GetField("chosenProfileSet", BindingFlags.Instance | BindingFlags.NonPublic);
        var set = setField?.GetValue(p) as System.Collections.Generic.HashSet<int>;
        if (set != null) set.Remove(currentProfileID);

        typeof(Plugin).GetMethod("SaveChosenProfiles", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(p, null);
        p.allowUpward = false; p.allowLeft = false; p.allowRight = false;
        p.itemCount = 0; p.resetPickups = false; p.skillMode = false;
        p.skillTotal = p.skillV = p.skillH = p.skillS = p.skillA = 0;
        RoomRandomMode.ResetPending();
    }

    private static void DrawAttackDirection(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("攻击方向选择："), GUILayout.Height(35));
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUI.color = Color.gray; GUILayout.Label(Locale.Get("下劈 (默认)"), GUILayout.Width(200), GUILayout.Height(40));
        GUI.enabled = false; GUILayout.Toggle(true, "", GUILayout.Width(40), GUILayout.Height(40));
        GUI.enabled = !alreadyChosen; GUI.color = Color.white;
        GUILayout.EndHorizontal();

        GUI.color = p.allowUpward ? Color.green : Color.white;
        bool newUp = GUILayout.Toggle(p.allowUpward, Locale.Get("上劈"), GUILayout.Height(40));
        if (newUp != p.allowUpward && !alreadyChosen) p.allowUpward = newUp;
        GUI.color = p.allowLeft ? Color.green : Color.white;
        bool newLeft = GUILayout.Toggle(p.allowLeft, Locale.Get("左劈"), GUILayout.Height(40));
        if (newLeft != p.allowLeft && !alreadyChosen) p.allowLeft = newLeft;
        GUI.color = p.allowRight ? Color.green : Color.white;
        bool newRight = GUILayout.Toggle(p.allowRight, Locale.Get("右劈"), GUILayout.Height(40));
        if (newRight != p.allowRight && !alreadyChosen) p.allowRight = newRight;
        GUI.color = Color.white;
    }

    private static void DrawSkillModeButtons(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("技能随机模式："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUI.color = !p.skillMode ? Color.green : Color.white;
        if (GUILayout.Button(Locale.Get("总随机"), GUILayout.Height(40), GUILayout.Width(130)) && !alreadyChosen) p.skillMode = false;
        GUI.color = p.skillMode ? Color.green : Color.white;
        if (GUILayout.Button(Locale.Get("分类随机"), GUILayout.Height(40), GUILayout.Width(130)) && !alreadyChosen) p.skillMode = true;

        // 彻底疯狂
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button(Locale.Get("彻底疯狂"), GUILayout.Height(40), GUILayout.Width(160)) && !alreadyChosen)
            CrazyRandomizer.Apply(p);
        GUI.backgroundColor = Color.white;
        GUI.color = Color.white;

        // 房随模式
        GUI.backgroundColor = RoomRandomMode.IsPending ? new Color(0.2f, 1f, 0.2f) : new Color(0.2f, 0.4f, 1f);
        if (GUILayout.Button(Locale.Get("房随模式"), GUILayout.Height(40), GUILayout.Width(160)) && !alreadyChosen)
            RoomRandomMode.Toggle();
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        // 房随模式说明（现在也使用本地化）
        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUI.skin.label.fontSize = 14;
        GUI.color = Color.white;
        GUILayout.Label(Locale.Get("给予疾风步、升腾与一格丝之心"), GUILayout.Height(20));
        GUI.skin.label.fontSize = 20;
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
    }

    private static void DrawTotalSlider(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("开局随机技能总数量："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUILayout.Label(p.skillTotal.ToString(), GUILayout.Width(35), GUILayout.Height(40));
        int newTotal = (int)GUILayout.HorizontalSlider(p.skillTotal, 0, 13, GUILayout.Width(220));
        if (newTotal != p.skillTotal && !alreadyChosen) p.skillTotal = newTotal;
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
    }

    private static void DrawCategorySliders(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("分类随机数量："), GUILayout.Height(35));
        DrawOneSlider(Locale.Get("垂直技能"), ref p.skillV, Plugin.MaxVerticalDynamic, alreadyChosen);
        DrawOneSlider(Locale.Get("水平技能"), ref p.skillH, Plugin.MaxHorizontalDynamic, alreadyChosen);
        DrawOneSlider(Locale.Get("特殊技能"), ref p.skillS, Plugin.MaxSpecialDynamic, alreadyChosen);
        DrawOneSlider(Locale.Get("攻击技能"), ref p.skillA, Plugin.MaxAttackDynamic, alreadyChosen);
    }

    private static void DrawOneSlider(string label, ref int value, int max, bool disabled)
    {
        GUILayout.Label($"{label} (0-{max})", GUILayout.Height(30));
        GUILayout.BeginHorizontal();
        GUILayout.Label(value.ToString(), GUILayout.Width(35), GUILayout.Height(40));
        int newVal = (int)GUILayout.HorizontalSlider(value, 0, max, GUILayout.Width(220));
        if (newVal != value && !disabled) value = newVal;
        GUILayout.EndHorizontal();
    }

    private static void DrawItemSlider(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("开局随机物品数量："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();
        GUILayout.Label(p.itemCount.ToString(), GUILayout.Width(35), GUILayout.Height(40));
        int newItem = (int)GUILayout.HorizontalSlider(p.itemCount, 0, 10, GUILayout.Width(220));
        if (newItem != p.itemCount && !alreadyChosen) p.itemCount = newItem;
        GUILayout.EndHorizontal();
    }

    private static void DrawSeedInput(Plugin p, bool alreadyChosen)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(Locale.Get("种子"), GUILayout.Width(60), GUILayout.Height(40));
        p.seedInput = GUILayout.TextField(p.seedInput, GUILayout.Width(160), GUILayout.Height(40));
        GUILayout.EndHorizontal();
        GUILayout.Space(5);

        GUI.color = p.resetPickups ? Color.red : Color.white;
        bool newReset = GUILayout.Toggle(p.resetPickups, Locale.Get("重置种子世界（含技能触发器）"), GUILayout.Height(40));
        if (newReset != p.resetPickups && !alreadyChosen) p.resetPickups = newReset;
        GUI.color = Color.white;
        if (p.resetPickups)
        {
            GUI.color = Color.yellow;
            GUILayout.Label(Locale.Get("警告：重置后当前种子世界将重新生成，所有拾取点会重生，技能触发器也会重置。"), GUILayout.Height(70));
            GUI.color = Color.white;
        }
        GUILayout.Space(35);
    }

    private static void DrawConfirmAndClose(Plugin p, bool alreadyChosen)
    {
        GUI.enabled = !alreadyChosen; GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("确认"), GUILayout.Height(50)) && !alreadyChosen)
        {
            typeof(Plugin).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(p, null);
        }
        GUI.backgroundColor = Color.white; GUI.enabled = true;
        GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
        if (GUILayout.Button(Locale.Get("关闭"), GUILayout.Height(40))) p.ShowUI = false;
        GUI.backgroundColor = Color.white;
    }

    // ===== 右侧子模块 =====

    private static void DrawSceneToggle(Plugin p)
    {
        bool enableRandom = p.GetSceneRandomEnabled();
        GUI.color = enableRandom ? Color.green : Color.white;
        if (GUILayout.Toggle(enableRandom, Locale.Get("启用场景随机"), GUILayout.Height(30)))
        {
            if (!enableRandom) p.SetSceneRandomEnabled(true);
        }
        else
        {
            if (enableRandom) p.SetSceneRandomEnabled(false);
        }
        GUI.color = Color.white;
    }

    private static void DrawTrapToggle(Plugin p)
    {
        bool trapEnabled = GetTrapEnabled();
        GUI.color = trapEnabled ? Color.green : Color.white;
        if (GUILayout.Toggle(trapEnabled, Locale.Get("启用陷阱随机"), GUILayout.Height(30)))
        {
            if (!trapEnabled)
            {
                SetTrapEnabled(true);
                TriggerTrapSpawn();
            }
        }
        else
        {
            if (trapEnabled)
            {
                SetTrapEnabled(false);
                TriggerTrapClear();
            }
        }
        GUI.color = Color.white;
    }

    private static void DrawSeedChanger(Plugin p)
    {
        GUILayout.Label(Locale.Get("修改种子:"), GUILayout.Height(25));
        p.SceneSeedInput = GUILayout.TextField(p.SceneSeedInput, GUILayout.Width(260), GUILayout.Height(30));
        GUILayout.Space(4);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("应用种子"), GUILayout.Height(32), GUILayout.Width(260)))
        {
            int newSeed;
            string input = p.SceneSeedInput.Trim();
            if (string.IsNullOrEmpty(input))
                newSeed = new Random().Next(1, int.MaxValue);
            else if (int.TryParse(input, out newSeed)) { }
            else return;

            try
            {
                var seedManager = p.SeedManager;
                var seedEntry = seedManager.GetType().GetField("cfgNewSeed", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(seedManager);
                var triggerEntry = seedManager.GetType().GetField("cfgRegenerateTrigger", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(seedManager);
                seedEntry.GetType().GetProperty("Value").SetValue(seedEntry, newSeed, null);
                triggerEntry.GetType().GetProperty("Value").SetValue(triggerEntry, true, null);
                Plugin.ShowNotification(Locale.Get("场景连接已重新生成"));

                // 如果陷阱开启则重新生成
                if (GetTrapEnabled())
                {
                    TriggerTrapClear();
                    SetTrapSeed(newSeed);
                    TriggerTrapSpawn();
                }
            }
            catch (Exception ex) { Plugin.Log.LogError($"场景种子更新失败: {ex}"); }
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawTeleporter(Plugin p)
    {
        GUILayout.Label(Locale.Get("场景传送:"), GUILayout.Height(25));
        p.SceneTeleportInput = GUILayout.TextField(p.SceneTeleportInput, GUILayout.Width(260), GUILayout.Height(30));
        GUILayout.Space(4);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("传送"), GUILayout.Height(32), GUILayout.Width(260)))
        {
            if (!string.IsNullOrWhiteSpace(p.SceneTeleportInput))
            {
                p.PendingTeleport = true;
                p.PendingTeleportScene = p.SceneTeleportInput;
                p.ShowUI = false;
                Plugin.ShowNotification(Locale.Get("稍后传送至") + p.SceneTeleportInput + " ...");
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private static void DrawDisplayToggles(Plugin p)
    {
        bool showSceneLabel = p.GetShowSceneLabel();
        bool showSeedLabel = p.GetShowSeedLabel();

        GUILayout.Label(Locale.Get("显示选项:"), GUILayout.Height(25));
        GUILayout.Space(4);

        GUI.color = showSceneLabel ? Color.green : Color.white;
        if (GUILayout.Toggle(showSceneLabel, Locale.Get("显示当前场景名"), GUILayout.Height(30)))
        {
            if (!showSceneLabel) p.SetShowSceneLabel(true);
        }
        else
        {
            if (showSceneLabel) p.SetShowSceneLabel(false);
        }
        GUI.color = Color.white;
        GUILayout.Space(6);

        GUI.color = showSeedLabel ? Color.green : Color.white;
        if (GUILayout.Toggle(showSeedLabel, Locale.Get("显示当前种子"), GUILayout.Height(30)))
        {
            if (!showSeedLabel) p.SetShowSceneLabel(true, isSeed: true);
        }
        else
        {
            if (showSeedLabel) p.SetShowSceneLabel(false, isSeed: true);
        }
        GUI.color = Color.white;
    }

    // ===== 陷阱随机辅助方法 =====

    private static bool GetTrapEnabled()
    {
        try
        {
            Type trapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
            var field = trapType?.GetField("Enabled", BindingFlags.Public | BindingFlags.Static);
            return field != null && (bool)field.GetValue(null);
        }
        catch { return false; }
    }

    private static void SetTrapEnabled(bool enabled)
    {
        try
        {
            Type trapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
            trapType?.GetField("Enabled", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, enabled);
        }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱开关设置失败: {ex}"); }
    }

    private static void SetTrapSeed(int seed)
    {
        try
        {
            Type trapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
            trapType?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { seed });
        }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱种子设置失败: {ex}"); }
    }

    private static void TriggerTrapSpawn()
    {
        try
        {
            Type trapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
            trapType?.GetMethod("SpawnTraps", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱生成失败: {ex}"); }
    }

    private static void TriggerTrapClear()
    {
        try
        {
            Type trapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
            trapType?.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱清除失败: {ex}"); }
    }
}