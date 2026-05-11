using System;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace StartingAbilityPicker;

public static class PanelRenderer
{
    // 反射缓存（陷阱相关）
    private static Type _trapType;
    private static PropertyInfo _trapEnabledProp;
    private static PropertyInfo _trapMovementEnabledProp;
    private static MethodInfo _spawnTrapsMethod;
    private static MethodInfo _clearAllMethod;
    private static MethodInfo _initializeMethod;
    private static FieldInfo _trapDifficultyField;
    private static MethodInfo _setDifficultyMethod;
    private static Type _trapDifficultyEnumType;
    private static MethodInfo _respawnTrapsMethod;

    private static void InitTrapReflection()
    {
        if (_trapType != null) return;
        _trapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
        if (_trapType == null) return;

        _trapEnabledProp = _trapType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Static);
        _trapMovementEnabledProp = _trapType.GetProperty("MovementEnabled", BindingFlags.Public | BindingFlags.Static);
        _spawnTrapsMethod = _trapType.GetMethod("SpawnTraps", BindingFlags.Public | BindingFlags.Static);
        _clearAllMethod = _trapType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Static);
        _initializeMethod = _trapType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
        _trapDifficultyField = _trapType.GetField("CurrentDifficulty", BindingFlags.Public | BindingFlags.Static);
        _setDifficultyMethod = _trapType.GetMethod("SetDifficulty", BindingFlags.Public | BindingFlags.Static);
        _respawnTrapsMethod = _trapType.GetMethod("RespawnTraps", BindingFlags.Public | BindingFlags.Static);

        var preloaderType = Type.GetType("SilksongItemRandomizer.TrapPreloader, SilksongItemRandomizer");
        _trapDifficultyEnumType = preloaderType?.GetNestedType("TrapDifficulty");
    }

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

        DrawAttackDirection(p, alreadyChosen);

        GUILayout.Space(20);

        DrawSkillModeButtons(p, alreadyChosen);

        GUILayout.Space(10);
        if (!p.skillMode)
            DrawTotalSlider(p, alreadyChosen);
        else
            DrawCategorySliders(p, alreadyChosen);

        GUILayout.Space(15);
        DrawItemSlider(p, alreadyChosen);

        GUILayout.Space(25);
        DrawSeedInput(p, alreadyChosen);
        DrawConfirmAndClose(p, alreadyChosen);

        GUILayout.Space(15);
        int originalFontSize = GUI.skin.label.fontSize; GUI.skin.label.fontSize = 26; GUI.color = Color.yellow;
        GUILayout.Label(Locale.Get("提示: 按 F7 呼出此窗口"), GUILayout.Height(40));
        GUI.color = Color.white; GUI.skin.label.fontSize = originalFontSize;
    }

    // ===== 右侧面板 =====
    public static void DrawScenePanel(Plugin p)
    {
        if (!p.SceneRandomAvailable)
        {
            GUILayout.Label(Locale.Get("场景随机未加载"), GUILayout.Height(30));
            return;
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CN", GUILayout.Height(20), GUILayout.Width(60))) Locale.SetForceChinese(true);
        if (GUILayout.Button("EN", GUILayout.Height(20), GUILayout.Width(60))) Locale.SetForceChinese(false);
        if (GUILayout.Button("Auto", GUILayout.Height(20), GUILayout.Width(60))) Locale.SetForceChinese(null);
        GUILayout.EndHorizontal();
        GUILayout.Space(8);

        GUILayout.Label(Locale.Get("场景随机设置"), GUILayout.Height(30));
        GUILayout.Space(12);

        DrawItemRandomToggle(p);
        DrawSceneToggle(p);
        DrawTrapToggle(p);
        DrawTrapDifficulty(p);

        // ★ 关键修复：在难度下方单独一行放置“陷阱生命化”按钮
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        DrawTrapMovementToggle(p);

        GUILayout.Space(5);
        int prevFont = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 20;
        GUI.color = Color.yellow;
        GUILayout.Label(Locale.Get("陷阱、房间随机建议丝之心，疾跑和上冲"), GUILayout.Height(60));
        GUI.color = Color.white;
        GUI.skin.label.fontSize = prevFont;
        GUILayout.Space(5);

        GUILayout.Space(12);

        int seed = p.GetCurrentSeed();
        GUILayout.Label($"{Locale.Get("当前种子")}: {seed}", GUILayout.Height(30));
        GUILayout.Space(8);

        string sceneName = p.GetCurrentSceneName();
        GUILayout.Label($"{Locale.Get("当前场景")}: {sceneName}", GUILayout.Height(30));
        GUILayout.Space(18);

        DrawSeedChanger(p);
        GUILayout.Space(20);
        DrawTeleporter(p);
        GUILayout.Space(20);
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

        GUI.enabled = false;
        DrawModeButton("下劈 (默认)", true, 18, GUILayout.Width(220), GUILayout.Height(60));
        GUI.enabled = !alreadyChosen;

        bool newUp = DrawModeButton(Locale.Get("上劈"), p.allowUpward, 18, GUILayout.Width(220), GUILayout.Height(60));
        if (newUp != p.allowUpward && !alreadyChosen) p.allowUpward = newUp;

        bool newLeft = DrawModeButton(Locale.Get("左劈"), p.allowLeft, 18, GUILayout.Width(220), GUILayout.Height(60));
        if (newLeft != p.allowLeft && !alreadyChosen) p.allowLeft = newLeft;

        bool newRight = DrawModeButton(Locale.Get("右劈"), p.allowRight, 18, GUILayout.Width(220), GUILayout.Height(60));
        if (newRight != p.allowRight && !alreadyChosen) p.allowRight = newRight;
    }

    private static void DrawSkillModeButtons(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("技能随机模式："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();

        bool newSkillMode = DrawModeButton(Locale.Get("总随机"), !p.skillMode, 18, GUILayout.Width(130), GUILayout.Height(60));
        if (newSkillMode != !p.skillMode && !alreadyChosen) p.skillMode = false;

        bool newTypeMode = DrawModeButton(Locale.Get("分类随机"), p.skillMode, 18, GUILayout.Width(130), GUILayout.Height(60));
        if (newTypeMode != p.skillMode && !alreadyChosen) p.skillMode = true;

        bool crazyOn = false;
        bool newCrazy = DrawModeButton(Locale.Get("彻底疯狂"), crazyOn, 18, GUILayout.Width(160), GUILayout.Height(60));
        if (newCrazy && !alreadyChosen) { CrazyRandomizer.Apply(p); }

        bool newRoomMode = DrawModeButton(Locale.Get("丝之心、疾跑和上冲"), RoomRandomMode.IsPending, 14, GUILayout.Width(160), GUILayout.Height(60));
        if (newRoomMode != RoomRandomMode.IsPending && !alreadyChosen) RoomRandomMode.Toggle();

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
        bool newValue = DrawModeButton(Locale.Get("启用场景随机"), enableRandom, 18, GUILayout.Width(260), GUILayout.Height(60));
        if (newValue != enableRandom) p.SetSceneRandomEnabled(newValue);
    }

    private static void DrawTrapToggle(Plugin p)
    {
        bool trapEnabled = GetTrapEnabled();
        bool newValue = DrawModeButton(Locale.Get("启用陷阱随机"), trapEnabled, 18, GUILayout.Width(260), GUILayout.Height(60));
        if (newValue != trapEnabled)
        {
            SetTrapEnabled(newValue);
            if (newValue) TriggerTrapSpawn();
            else TriggerTrapClear();
        }
    }

    private static void DrawItemRandomToggle(Plugin p)
    {
        bool itemEnabled = p.GetItemRandomEnabled();
        bool newValue = DrawModeButton(Locale.Get("启用物品随机"), itemEnabled, 18, GUILayout.Width(260), GUILayout.Height(60));
        if (newValue != itemEnabled) p.SetItemRandomEnabled(newValue);
    }

    private static void DrawTrapDifficulty(Plugin p)
    {
        InitTrapReflection();
        if (_trapType == null || _trapDifficultyEnumType == null) return;

        int currentDiff = GetTrapDifficulty();
        GUILayout.Label(Locale.Get("陷阱难度："), GUILayout.Height(25));

        GUILayout.BeginHorizontal();
        Sprite[] icons = { Plugin.BeginnerIcon, Plugin.FocusedIcon, Plugin.OverflowIcon };
        string[] diffNames = { "初猎", "专注", "满溢" };

        // 以 3 号图片的显示高度作为所有按钮的统一高度
        float btnHeight = 50f;
        if (Plugin.OverflowIcon != null && Plugin.OverflowIcon.texture != null)
        {
            float w = Plugin.OverflowIcon.rect.width;
            float h = Plugin.OverflowIcon.rect.height;
            btnHeight = 90f * (h / w);
        }

        // 文字样式（复制 DrawModeButton 的样式）
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerCenter,
            fontSize = 11,
            fontStyle = FontStyle.Normal,
            normal = { textColor = Color.white }
        };

        for (int i = 0; i < 3; i++)
        {
            bool isOn = (currentDiff == i);
            Sprite icon = icons[i];

            // 按钮区域，高度统一
            Rect btnRect = GUILayoutUtility.GetRect(90f, btnHeight, GUILayout.Width(90), GUILayout.Height(btnHeight));

            // 1. 先画背景色（选中绿，未选中灰）
            Color bgColor = isOn ? new Color(0.2f, 1.0f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            GUI.Box(btnRect, "");   // 画一个纯色底
            GUI.backgroundColor = oldBg;

            // 2. 在底色之上画图片（宽度固定 90，水平居中，垂直居中）
            if (icon != null && icon.texture != null)
            {
                float imgW = icon.rect.width;
                float imgH = icon.rect.height;
                float drawW = 90f;
                float drawH = drawW * (imgH / imgW);
                float offsetX = btnRect.x + (btnRect.width - drawW) / 2f;
                float offsetY = btnRect.y + (btnRect.height - drawH) / 2f;
                Rect imgRect = new Rect(offsetX, offsetY, drawW, drawH);
                GUI.DrawTexture(imgRect, icon.texture, ScaleMode.StretchToFill);
            }

            // 3. 透明按钮（接收点击）
            if (GUI.Button(btnRect, "", GUIStyle.none))
            {
                SetTrapDifficulty(i);
                if (GetTrapEnabled()) { TriggerTrapClear(); TriggerTrapSpawn(); }
            }

            // 4. 最顶层：文字
            Rect titleRect = new Rect(btnRect.x, btnRect.y, btnRect.width, btnRect.height / 2f);
            Rect statusRect = new Rect(btnRect.x, btnRect.y + btnRect.height / 2f, btnRect.width, btnRect.height / 2f);
            GUI.Label(titleRect, Locale.Get(diffNames[i]), titleStyle);
            GUI.Label(statusRect, isOn ? Locale.Get("[开]") : Locale.Get("[关]"), statusStyle);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private static void DrawTrapMovementToggle(Plugin p)
    {
        InitTrapReflection();
        if (_trapType == null || _trapMovementEnabledProp == null) return;

        bool moveEnabled = GetTrapMovementEnabled();
        bool newValue = DrawModeButton(Locale.Get("陷阱生命化"), moveEnabled, 18, GUILayout.Width(260), GUILayout.Height(60));
        if (newValue != moveEnabled)
        {
            SetTrapMovementEnabled(newValue);
            if (GetTrapEnabled()) { TriggerTrapClear(); TriggerTrapSpawn(); }
        }
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
            if (string.IsNullOrEmpty(input)) newSeed = new Random().Next(1, int.MaxValue);
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

                if (GetTrapEnabled()) { TriggerTrapClear(); SetTrapSeed(newSeed); TriggerTrapSpawn(); }
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
        else { if (showSceneLabel) p.SetShowSceneLabel(false); }
        GUI.color = Color.white;
        GUILayout.Space(6);

        GUI.color = showSeedLabel ? Color.green : Color.white;
        if (GUILayout.Toggle(showSeedLabel, Locale.Get("显示当前种子"), GUILayout.Height(30)))
        {
            if (!showSeedLabel) p.SetShowSceneLabel(true, isSeed: true);
        }
        else { if (showSeedLabel) p.SetShowSceneLabel(false, isSeed: true); }
        GUI.color = Color.white;
    }

    private static bool DrawModeButton(string label, bool isOn, int titleFontSize = 18, params GUILayoutOption[] options)
    {
        string statusText = isOn ? Locale.Get("[开]") : Locale.Get("[关]");
        Color bgColor = isOn ? new Color(0.2f, 1.0f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
        Color oldBg = GUI.backgroundColor;
        GUI.backgroundColor = bgColor;

        bool clicked = GUILayout.Button("", options);
        Rect buttonRect = GUILayoutUtility.GetLastRect();

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = titleFontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerCenter,
            fontSize = titleFontSize - 3,
            fontStyle = FontStyle.Normal,
            normal = { textColor = isOn ? Color.white : new Color(0.9f, 0.9f, 0.9f) }
        };

        GUI.Label(new Rect(buttonRect.x, buttonRect.y, buttonRect.width, buttonRect.height / 2), label, titleStyle);
        GUI.Label(new Rect(buttonRect.x, buttonRect.y + buttonRect.height / 2, buttonRect.width, buttonRect.height / 2), statusText, statusStyle);

        GUI.backgroundColor = oldBg;

        if (clicked) return !isOn;
        return isOn;
    }

    // ===== 陷阱随机辅助方法 =====

    private static bool GetTrapEnabled()
    {
        InitTrapReflection();
        if (_trapEnabledProp == null) return false;
        try { return (bool)_trapEnabledProp.GetValue(null); }
        catch { return false; }
    }

    private static void SetTrapEnabled(bool enabled)
    {
        InitTrapReflection();
        try { _trapEnabledProp?.SetValue(null, enabled); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱开关设置失败: {ex}"); }
    }

    private static void SetTrapSeed(int seed)
    {
        InitTrapReflection();
        try { _initializeMethod?.Invoke(null, new object[] { seed }); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱种子设置失败: {ex}"); }
    }

    private static void TriggerTrapSpawn()
    {
        InitTrapReflection();
        try { _spawnTrapsMethod?.Invoke(null, null); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱生成失败: {ex}"); }
    }

    private static void TriggerTrapClear()
    {
        InitTrapReflection();
        try { _clearAllMethod?.Invoke(null, null); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱清除失败: {ex}"); }
    }

    private static int GetTrapDifficulty()
    {
        InitTrapReflection();
        if (_trapDifficultyField == null) return 0;
        try { return Convert.ToInt32(_trapDifficultyField.GetValue(null)); }
        catch { return 0; }
    }

    private static void SetTrapDifficulty(int difficultyIndex)
    {
        InitTrapReflection();
        if (_setDifficultyMethod == null || _trapDifficultyEnumType == null) return;
        try
        {
            var enumValue = Enum.ToObject(_trapDifficultyEnumType, difficultyIndex);
            _setDifficultyMethod.Invoke(null, new[] { enumValue });
        }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱难度设置失败: {ex}"); }
    }

    private static bool GetTrapMovementEnabled()
    {
        InitTrapReflection();
        if (_trapMovementEnabledProp == null) return true;
        try { return (bool)_trapMovementEnabledProp.GetValue(null); }
        catch { return true; }
    }

    private static void SetTrapMovementEnabled(bool value)
    {
        InitTrapReflection();
        try { _trapMovementEnabledProp?.SetValue(null, value); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱移动开关设置失败: {ex}"); }
    }
}