using System.IO;
using System.Text.RegularExpressions;
using BepInEx;

namespace SilksongItemRandomizer;

/// <summary>
/// 在游戏主体加载前，强制修改 Architect 配置，启用全量素材加载。
/// 这样 Architect 初始化时就能直接使用全部 600+ 预制体。
/// </summary>
[BepInPlugin("SilksongItemRandomizer.TrapPreloader", "Trap Preloader", "1.0.0.0")]
public class TrapPreloader : BaseUnityPlugin
{
    // 预加载插件 Awake 会在普通插件之前执行
    private void Awake()
    {
        // 1. 定位 Architect 配置文件
        string configFile = Path.Combine(Paths.ConfigPath, "com.cometcake575.architect.cfg");

        if (!File.Exists(configFile))
        {
            // 如果 Architect 尚未生成配置文件，则无法预修改，但不影响后续使用
            Logger.LogWarning("TrapPreloader: Architect 配置文件不存在，跳过预修改。");
            // 正常流程中，Mod 自身的 Initialize 会再次尝试
            return;
        }

        // 2. 读取并替换 LoadAllAssets 选项
        string content = File.ReadAllText(configFile);
        string newContent = Regex.Replace(content, @"LoadAllAssets\s*=\s*false",
            "LoadAllAssets = true", RegexOptions.IgnoreCase);

        if (newContent != content)
        {
            File.WriteAllText(configFile, newContent);
            Logger.LogInfo("TrapPreloader: 已将 Architect 全量加载预置为 true。");
        }
    }
}