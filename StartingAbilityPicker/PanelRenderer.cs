using System;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace StartingAbilityPicker;

public static class PanelRenderer
{
    private static readonly Color BrightWhite = new(0.95f, 0.97f, 1.0f);

    // 陷阱反射缓存 - 静态初始化
    private static readonly Type TrapType;
    private static readonly PropertyInfo TrapEnabledProp;
    private static readonly PropertyInfo TrapMovementEnabledProp;
    private static readonly MethodInfo SpawnTrapsMethod;
    private static readonly MethodInfo ClearAllMethod;
    private static readonly MethodInfo InitializeMethod;
    private static readonly FieldInfo TrapDifficultyField;
    private static readonly MethodInfo SetDifficultyMethod;
    private static readonly Type TrapDifficultyEnumType;

    static PanelRenderer()
    {
        TrapType = Type.GetType("SilksongItemRandomizer.TrapRandomizer, SilksongItemRandomizer");
        if (TrapType != null)
        {
            TrapEnabledProp = TrapType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Static);
            TrapMovementEnabledProp = TrapType.GetProperty("MovementEnabled", BindingFlags.Public | BindingFlags.Static);
            SpawnTrapsMethod = TrapType.GetMethod("SpawnTraps", BindingFlags.Public | BindingFlags.Static);
            ClearAllMethod = TrapType.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Static);
            InitializeMethod = TrapType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
            TrapDifficultyField = TrapType.GetField("CurrentDifficulty", BindingFlags.Public | BindingFlags.Static);
            SetDifficultyMethod = TrapType.GetMethod("SetDifficulty", BindingFlags.Public | BindingFlags.Static);
            var preloaderType = Type.GetType("SilksongItemRandomizer.TrapPreloader, SilksongItemRandomizer");
            TrapDifficultyEnumType = preloaderType?.GetNestedType("TrapDifficulty");
        }
    }

    // GUI 样式缓存
    private static GUIStyle _titleStyle, _statusStyle, _diffTitleStyle, _diffStatusStyle;

    // 滚动位置缓存（用于分类随机模式的滑块区域）
    private static Vector2 _skillScrollPos;

    private static void EnsureStyles()
    {
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = BrightWhite } };
            _statusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 11, normal = { textColor = BrightWhite } };
            _diffTitleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = BrightWhite } };
            _diffStatusStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 15, normal = { textColor = BrightWhite } };
        }
    }

    // ===== 左侧面板 =====
    public static void DrawLeftPanel(Plugin p)
    {
        var originalLabelColor = GUI.skin.label.normal.textColor;
        GUI.skin.label.normal.textColor = BrightWhite;
        GUI.skin.toggle.normal.textColor = BrightWhite;
        GUI.skin.button.normal.textColor = BrightWhite;
        GUI.skin.horizontalSlider.normal.textColor = BrightWhite;
        GUI.skin.textField.normal.textColor = BrightWhite;

        GUI.skin.label.fontSize = 20;
        GUI.skin.toggle.fontSize = 20;
        GUI.skin.button.fontSize = 24;
        GUI.skin.horizontalSlider.fontSize = 18;
        GUI.skin.textField.fontSize = 18;

        GUILayout.Label(Locale.Get("当前存档开局设置："), GUILayout.Height(40));
        GUILayout.Space(15);
        var alreadyChosen = IsAlreadyChosen(p);
        if (alreadyChosen)
        {
            GUILayout.Label(Locale.Get("（此存档已设置过，只能查看）"), GUILayout.Height(35));
            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f);
            if (GUILayout.Button(Locale.Get("重置本存档设置"), GUILayout.Height(45)))
            {
                ResetProfile(p);
                GUI.backgroundColor = Color.white;
                GUI.skin.label.normal.textColor = originalLabelColor;
                GUI.skin.toggle.normal.textColor = originalLabelColor;
                GUI.skin.button.normal.textColor = originalLabelColor;
                GUI.skin.horizontalSlider.normal.textColor = originalLabelColor;
                GUI.skin.textField.normal.textColor = originalLabelColor;
                return;
            }
            GUI.backgroundColor = Color.white;
        }
        GUILayout.Space(20);

        DrawAttackDirection(p, alreadyChosen);
        GUILayout.Space(20);
        DrawSkillModeButtons(p, alreadyChosen);
        GUILayout.Space(10);

        // ★ 滚动区域：解决分类随机模式下面板过高的问题 ★
        GUILayout.BeginVertical(GUILayout.Height(200)); // 固定高度，可根据需要调整
        _skillScrollPos = GUILayout.BeginScrollView(_skillScrollPos, GUILayout.ExpandHeight(true));

        if (!p.skillMode)
            DrawTotalSlider(p, alreadyChosen);
        else
            DrawCategorySliders(p, alreadyChosen);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(15);
        DrawItemSlider(p, alreadyChosen);
        GUILayout.Space(25);
        DrawSeedInput(p, alreadyChosen);
        DrawConfirmAndClose(p, alreadyChosen);

        GUILayout.Space(15);
        var originalFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 26;
        GUI.color = Color.yellow;
        GUILayout.Label(Locale.Get("提示: 按 F7 呼出此窗口"), GUILayout.Height(40));
        GUI.color = BrightWhite;
        GUI.skin.label.fontSize = originalFontSize;

        GUI.skin.label.normal.textColor = originalLabelColor;
        GUI.skin.toggle.normal.textColor = originalLabelColor;
        GUI.skin.button.normal.textColor = originalLabelColor;
        GUI.skin.horizontalSlider.normal.textColor = originalLabelColor;
        GUI.skin.textField.normal.textColor = originalLabelColor;
    }

    // ===== 右侧面板 =====
    public static void DrawScenePanel(Plugin p)
    {
        var originalLabelColor = GUI.skin.label.normal.textColor;
        GUI.skin.label.normal.textColor = BrightWhite;
        GUI.skin.button.normal.textColor = BrightWhite;
        GUI.skin.toggle.normal.textColor = BrightWhite;

        if (!p.SceneRandomAvailable)
        {
            GUILayout.Label(Locale.Get("场景随机未加载"), GUILayout.Height(30));
            GUI.skin.label.normal.textColor = originalLabelColor;
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
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        DrawTrapMovementToggle(p);

        GUILayout.Space(5);
        var prevFont = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 20;
        GUI.color = Color.yellow;
        GUILayout.Label(Locale.Get("陷阱、房间随机建议丝之心，疾跑和上冲"), GUILayout.Height(60));
        GUI.color = BrightWhite;
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

        GUI.skin.label.normal.textColor = originalLabelColor;
        GUI.skin.button.normal.textColor = originalLabelColor;
        GUI.skin.toggle.normal.textColor = originalLabelColor;
    }

    // ===== 辅助方法（部分） =====
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
        set?.Remove(currentProfileID);
        typeof(Plugin).GetMethod("SaveChosenProfiles", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(p, null);
        p.allowUpward = false;
        p.allowLeft = false;
        p.allowRight = false;
        p.itemCount = 0;
        p.resetPickups = false;
        p.skillMode = false;
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

        bool newLeft = DrawModeButton(Locale.Get("左劈"), p.allowRight, 18, GUILayout.Width(220), GUILayout.Height(60));
        if (newLeft != p.allowRight && !alreadyChosen) p.allowRight = newLeft;

        bool newRight = DrawModeButton(Locale.Get("右劈"), p.allowLeft, 18, GUILayout.Width(220), GUILayout.Height(60));
        if (newRight != p.allowLeft && !alreadyChosen) p.allowLeft = newRight;
    }

    private static void DrawSkillModeButtons(Plugin p, bool alreadyChosen)
    {
        GUILayout.Label(Locale.Get("技能随机模式："), GUILayout.Height(35));
        GUILayout.BeginHorizontal();

        bool newSkillMode = DrawModeButton(Locale.Get("总随机"), !p.skillMode, 18, GUILayout.Width(130), GUILayout.Height(60));
        if (newSkillMode != !p.skillMode && !alreadyChosen) p.skillMode = false;

        bool newTypeMode = DrawModeButton(Locale.Get("分类随机"), p.skillMode, 18, GUILayout.Width(130), GUILayout.Height(60));
        if (newTypeMode != p.skillMode && !alreadyChosen) p.skillMode = true;

        bool newCrazy = DrawModeButton(Locale.Get("彻底疯狂"), false, 18, GUILayout.Width(160), GUILayout.Height(60));
        if (newCrazy && !alreadyChosen) CrazyRandomizer.Apply(p);

        bool newRoomMode = DrawModeButton(Locale.Get("丝之心、疾跑和上冲"), RoomRandomMode.IsPending, 14, GUILayout.Width(160), GUILayout.Height(60));
        if (newRoomMode != RoomRandomMode.IsPending && !alreadyChosen) RoomRandomMode.Toggle();

        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        bool crestCurseEnabled = p.GetCrestCurseEnabled();
        GUI.enabled = !alreadyChosen;
        bool newCrestCurse = DrawModeButton(Locale.Get("纹章诅咒"), crestCurseEnabled, 18, GUILayout.Width(160), GUILayout.Height(60));
        if (newCrestCurse != crestCurseEnabled && !alreadyChosen) p.SetCrestCurseEnabled(newCrestCurse);

        bool forceCompletion = p.GetForceCompletionDisplay();
        bool newForceCompletion = DrawModeButton(Locale.Get("完成度"), forceCompletion, 18, GUILayout.Width(160), GUILayout.Height(60));
        if (newForceCompletion != forceCompletion && !alreadyChosen) p.SetForceCompletionDisplay(newForceCompletion);

        GUI.enabled = true;
        GUILayout.EndHorizontal();

        if (crestCurseEnabled)
        {
            GUILayout.Space(5);
            GUI.color = Color.yellow;
            GUILayout.Label(Locale.Get("启用纹章诅咒（每个纹章带有偏科效果）"), GUILayout.Height(30));
            GUI.color = BrightWhite;
        }
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

        GUI.color = p.resetPickups ? Color.red : BrightWhite;
        bool newReset = GUILayout.Toggle(p.resetPickups, Locale.Get("重置种子世界（含技能触发器）"), GUILayout.Height(40));
        if (newReset != p.resetPickups && !alreadyChosen) p.resetPickups = newReset;
        GUI.color = BrightWhite;
        if (p.resetPickups)
        {
            GUI.color = Color.yellow;
            GUILayout.Label(Locale.Get("警告：重置后当前种子世界将重新生成，所有拾取点会重生，技能触发器也会重置。"), GUILayout.Height(70));
            GUI.color = BrightWhite;
        }
        GUILayout.Space(35);
    }

    private static void DrawConfirmAndClose(Plugin p, bool alreadyChosen)
    {
        GUI.enabled = !alreadyChosen;
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("确认"), GUILayout.Height(50)) && !alreadyChosen)
            typeof(Plugin).GetMethod("ApplySettings", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(p, null);
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
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
        if (TrapType == null || TrapDifficultyEnumType == null) return;

        int currentDiff = GetTrapDifficulty();
        GUILayout.Label(Locale.Get("陷阱难度："), GUILayout.Height(25));

        GUILayout.BeginHorizontal();
        Sprite[] icons = { Plugin.BeginnerIcon, Plugin.FocusedIcon, Plugin.OverflowIcon };
        string[] diffNames = { "初猎", "专注", "满溢" };

        float btnHeight = 50f;
        if (Plugin.OverflowIcon != null && Plugin.OverflowIcon.texture != null)
        {
            float w = Plugin.OverflowIcon.rect.width;
            float h = Plugin.OverflowIcon.rect.height;
            btnHeight = 90f * (h / w);
        }

        EnsureStyles();

        for (int i = 0; i < 3; i++)
        {
            bool isOn = currentDiff == i;
            Sprite icon = icons[i];
            var btnRect = GUILayoutUtility.GetRect(90f, btnHeight, GUILayout.Width(90), GUILayout.Height(btnHeight));

            Color bgColor = isOn ? new Color(0.2f, 1.0f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;
            GUI.Box(btnRect, "");
            GUI.backgroundColor = oldBg;

            if (icon?.texture != null)
            {
                float imgW = icon.rect.width;
                float imgH = icon.rect.height;
                float drawW, drawH, offsetX, offsetY;
                if (i == 0)
                {
                    drawH = btnRect.height;
                    drawW = drawH * (imgW / imgH);
                    offsetX = btnRect.x + (btnRect.width - drawW) / 2f;
                    offsetY = btnRect.y;
                }
                else
                {
                    drawW = 90f;
                    drawH = drawW * (imgH / imgW);
                    offsetX = btnRect.x + (btnRect.width - drawW) / 2f;
                    offsetY = btnRect.y + (btnRect.height - drawH) / 2f;
                }
                GUI.DrawTexture(new Rect(offsetX, offsetY, drawW, drawH), icon.texture, ScaleMode.StretchToFill);
            }

            if (GUI.Button(btnRect, "", GUIStyle.none))
            {
                SetTrapDifficulty(i);
                if (GetTrapEnabled()) { TriggerTrapClear(); TriggerTrapSpawn(); }
            }

            var titleRect = new Rect(btnRect.x, btnRect.y, btnRect.width, btnRect.height / 2f);
            var statusRect = new Rect(btnRect.x, btnRect.y + btnRect.height / 2f, btnRect.width, btnRect.height / 2f);
            GUI.Label(titleRect, Locale.Get(diffNames[i]), _diffTitleStyle);
            GUI.Label(statusRect, isOn ? Locale.Get("[开]") : Locale.Get("[关]"), _diffStatusStyle);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private static void DrawTrapMovementToggle(Plugin p)
    {
        if (TrapType == null || TrapMovementEnabledProp == null) return;

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

        string newText = GUILayout.TextField(p.SceneSeedInput, GUILayout.Width(260), GUILayout.Height(30));

        if (string.IsNullOrEmpty(newText) && GUI.GetNameOfFocusedControl() != "SeedTextField")
        {
            var rect = GUILayoutUtility.GetLastRect();
            GUI.SetNextControlName("SeedTextField");
            var originalColor = GUI.color;
            GUI.color = Color.yellow;
            var placeholderStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = GUI.color }
            };
            GUI.Label(rect, Locale.Get("直接应用可随机"), placeholderStyle);
            GUI.color = originalColor;
        }
        else
        {
            GUI.SetNextControlName("SeedTextField");
        }

        p.SceneSeedInput = newText;

        GUILayout.Space(4);
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button(Locale.Get("应用种子"), GUILayout.Height(32), GUILayout.Width(260)))
        {
            int newSeed;
            string input = p.SceneSeedInput.Trim();
            if (string.IsNullOrEmpty(input)) newSeed = new Random().Next(1, int.MaxValue);
            else if (!int.TryParse(input, out newSeed)) return;

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

        GUI.color = showSceneLabel ? Color.green : BrightWhite;
        if (GUILayout.Toggle(showSceneLabel, Locale.Get("显示当前场景名"), GUILayout.Height(30)))
        { if (!showSceneLabel) p.SetShowSceneLabel(true); }
        else { if (showSceneLabel) p.SetShowSceneLabel(false); }
        GUI.color = BrightWhite;
        GUILayout.Space(6);

        GUI.color = showSeedLabel ? Color.green : BrightWhite;
        if (GUILayout.Toggle(showSeedLabel, Locale.Get("显示当前种子"), GUILayout.Height(30)))
        { if (!showSeedLabel) p.SetShowSceneLabel(true, true); }
        else { if (showSeedLabel) p.SetShowSceneLabel(false, true); }
        GUI.color = BrightWhite;
    }

    private static bool DrawModeButton(string label, bool isOn, int titleFontSize = 18, params GUILayoutOption[] options)
    {
        string statusText = isOn ? Locale.Get("[开]") : Locale.Get("[关]");
        Color bgColor = isOn ? new Color(0.2f, 1.0f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
        var oldBg = GUI.backgroundColor;
        GUI.backgroundColor = bgColor;

        bool clicked = GUILayout.Button("", options);
        var buttonRect = GUILayoutUtility.GetLastRect();

        EnsureStyles();
        var titleStyle = new GUIStyle(_titleStyle) { fontSize = titleFontSize };
        var statusStyle = new GUIStyle(_statusStyle) { fontSize = titleFontSize - 3 };

        GUI.Label(new Rect(buttonRect.x, buttonRect.y, buttonRect.width, buttonRect.height / 2), label, titleStyle);
        GUI.Label(new Rect(buttonRect.x, buttonRect.y + buttonRect.height / 2, buttonRect.width, buttonRect.height / 2), statusText, statusStyle);

        GUI.backgroundColor = oldBg;

        return clicked ? !isOn : isOn;
    }

    // ===== 陷阱辅助 =====
    private static bool GetTrapEnabled()
    {
        if (TrapEnabledProp == null) return false;
        try { return (bool)TrapEnabledProp.GetValue(null); }
        catch { return false; }
    }

    private static void SetTrapEnabled(bool enabled)
    {
        try { TrapEnabledProp?.SetValue(null, enabled); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱开关设置失败: {ex}"); }
    }

    private static void SetTrapSeed(int seed)
    {
        try { InitializeMethod?.Invoke(null, new object[] { seed }); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱种子设置失败: {ex}"); }
    }

    private static void TriggerTrapSpawn()
    {
        try { SpawnTrapsMethod?.Invoke(null, null); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱生成失败: {ex}"); }
    }

    private static void TriggerTrapClear()
    {
        try { ClearAllMethod?.Invoke(null, null); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱清除失败: {ex}"); }
    }

    private static int GetTrapDifficulty()
    {
        if (TrapDifficultyField == null) return 0;
        try { return Convert.ToInt32(TrapDifficultyField.GetValue(null)); }
        catch { return 0; }
    }

    private static void SetTrapDifficulty(int difficultyIndex)
    {
        if (SetDifficultyMethod == null || TrapDifficultyEnumType == null) return;
        try
        {
            var enumValue = Enum.ToObject(TrapDifficultyEnumType, difficultyIndex);
            SetDifficultyMethod.Invoke(null, new[] { enumValue });
        }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱难度设置失败: {ex}"); }
    }

    private static bool GetTrapMovementEnabled()
    {
        if (TrapMovementEnabledProp == null) return true;
        try { return (bool)TrapMovementEnabledProp.GetValue(null); }
        catch { return true; }
    }

    private static void SetTrapMovementEnabled(bool value)
    {
        try { TrapMovementEnabledProp?.SetValue(null, value); }
        catch (Exception ex) { Plugin.Log.LogError($"陷阱移动开关设置失败: {ex}"); }
    }
}