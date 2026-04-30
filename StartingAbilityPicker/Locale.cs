using System.Collections.Generic;

namespace StartingAbilityPicker
{
    public static class Locale
    {
        private static bool? _forceChinese = null;

        // ★ 手动覆盖开关
        // null = 自动检测游戏语言
        // true = 强制中文
        // false = 强制英文
        public static void SetForceChinese(bool? force)
        {
            _forceChinese = force;
        }

        public static bool IsChinese
        {
            get
            {
                // 优先检查手动覆盖
                if (_forceChinese.HasValue)
                    return _forceChinese.Value;
                // 否则每次实时检测，不缓存
                return DetectChinese();
            }
        }

        private static readonly Dictionary<string, string> TextMap = new Dictionary<string, string>
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
            // ★ 房随模式（新增）
            { "房随模式", "Room Mode" },
            { "给予疾风步、升腾与一格丝之心", "Grants Dash, Super Jump & 1 Silk Regen" },

            // 场景随机面板
            { "场景随机未加载", "Scene Randomizer Not Loaded" },
            { "场景随机设置", "Scene Randomizer" },
            { "启用场景随机", "Enable Randomizer" },
            { "启用陷阱随机", "Enable Trap Randomizer" },  // ← 新增
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
        };

        public static string Get(string chineseText)
        {
            if (string.IsNullOrEmpty(chineseText)) return chineseText;
            if (IsChinese) return chineseText;
            if (TextMap.TryGetValue(chineseText, out string english))
                return english;
            return chineseText;
        }

        private static bool DetectChinese()
        {
            try
            {
                // 直接读 FontManager._currentLanguage 静态字段
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
                            return code == "ZH" || code == "ZH_TW";
                        }
                    }
                }
            }
            catch { }

            // 备用：GameSettings.language
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
                            return code == "ZH" || code == "ZH_TW";
                        }
                    }
                }
            }
            catch { }

            return false;
        }
    }
}