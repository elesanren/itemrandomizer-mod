using System.Collections.Generic;

namespace StartingAbilityPicker;

public static class Locale
{
    private static bool? _forceChinese;

    public static void SetForceChinese(bool? force) => _forceChinese = force;

    public static bool IsChinese
    {
        get
        {
            if (_forceChinese.HasValue)
                return _forceChinese.Value;
            return DetectChinese();
        }
    }

    private static readonly Dictionary<string, string> TextMap = new()
    {
        // 窗口标题
        { "开局选项 & 场景随机", "Startup Options & Scene Randomizer" },
        // 左侧面板
        { "当前存档开局设置：", "Current Save Startup Settings:" },
        { "（此存档已设置过，只能查看）", "(This save has been configured, view only)" },
        { "重置本存档设置", "Reset This Save's Settings" },
        { "攻击方向选择：", "Attack Direction Selection:" },
        { "下劈 (默认)", "Down Slash (Default)" },
        { "上劈", "Up Slash" },
        { "左劈", "Left Slash" },
        { "右劈", "Right Slash" },
        { "技能随机模式：", "Skill Random Mode:" },
        { "总随机", "Total Mode" },
        { "分类随机", "Type Mode" },
        { "开局随机技能总数量：", "Total Random Skills:" },
        { "分类随机数量：", "Category Random Count:" },
        { "垂直技能", "Vertical Skills" },
        { "水平技能", "Horizontal Skills" },
        { "特殊技能", "Special Skills" },
        { "攻击技能", "Attack Skills" },
        { "开局随机物品数量：", "Starting Random Item Count:" },
        { "种子", "Seed" },
        { "重置种子世界（含技能触发器）", "Reset Seed World (incl. Skill Triggers)" },
        { "警告：重置后当前种子世界将重新生成，所有拾取点会重生，技能触发器也会重置。", "Warning: This will regenerate the seed world, respawn all pickups, and reset skill triggers." },
        { "确认", "Confirm" },
        { "关闭", "Close" },
        { "提示: 按 F7 呼出此窗口", "Tip: Press F7 to open this window" },
        { "彻底疯狂", "Crazy Mode" },
        { "纹章诅咒", "Crest Curse" },
        { "启用纹章诅咒（每个纹章带有偏科效果）", "Enable Crest Curse (each crest has specialized effects)" },
        { "丝之心、疾跑和上冲", "Silk Heart, Dash & Super Jump" },
        { "[开]", "[ON]" },
        { "[关]", "[OFF]" },
        { "启用物品随机", "Enable Item Randomizer" },
        { "场景随机未加载", "Scene Randomizer Not Loaded" },
        { "场景随机设置", "Scene Randomizer" },
        { "启用场景随机", "Enable Randomizer" },
        { "启用陷阱随机", "Enable Trap Randomizer" },
        { "陷阱、房间随机建议丝之心，疾跑和上冲", "Trap & Room Randomizer: Silk Heart, Dash & Super Jump recommended" },
        { "陷阱难度：", "Trap Difficulty:" },
        { "初猎", "Beginner" },
        { "专注", "Focused" },
        { "满溢", "Overflow" },
        { "当前种子", "Current Seed" },
        { "当前场景", "Current Scene" },
        { "修改种子:", "Modify Seed:" },
        { "应用种子", "Apply Seed" },
        { "场景传送:", "Teleport:" },
        { "传送", "Teleport" },
        { "稍后传送至", "Teleporting to " },
        { "场景连接已重新生成", "Regenerated" },
        { "显示选项:", "Display:" },
        { "显示当前场景名", "Show Scene Name" },
        { "显示当前种子", "Show Seed" },
        { "陷阱生命化", "Trap Life" },
        { "完成度", "Completion" },
        // ★ 新增条目
        { "直接应用可随机", "Apply directly to randomize" },
    };

    public static string Get(string chineseText)
    {
        if (string.IsNullOrEmpty(chineseText)) return chineseText;
        if (IsChinese) return chineseText;
        return TextMap.TryGetValue(chineseText, out var english) ? english : chineseText;
    }

    private static bool DetectChinese()
    {
        // 你原来的检测逻辑：优先 FontManager._currentLanguage
        try
        {
            var fmType = System.Type.GetType("FontManager, Assembly-CSharp");
            if (fmType != null)
            {
                var field = fmType.GetField("_currentLanguage",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(null);
                    if (val != null)
                    {
                        string code = val.ToString().ToUpper();
                        if (code == "ZH" || code == "ZH_TW")
                            return true;
                    }
                }
            }
        }
        catch { }

        // 备用：从游戏设置读取
        try
        {
            var gm = GameManager.instance;
            var gs = gm?.gameSettings;
            if (gs != null)
            {
                var field = gs.GetType().GetField("language",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (field != null)
                {
                    var val = field.GetValue(gs);
                    if (val != null)
                    {
                        string code = val.ToString().ToUpper();
                        if (code == "ZH" || code == "ZH_TW")
                            return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }
}