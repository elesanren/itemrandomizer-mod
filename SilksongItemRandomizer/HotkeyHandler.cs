using UnityEngine;
using SilksongItemRandomizer;
using System;
using System.Reflection;

/// <summary>
/// 全局快捷键处理器
/// F6: 传送至上一次坐的椅子（脱离卡死，手动保存+加载）
/// F8: 显示/隐藏最近获得物品 UI
/// F9: 转储所有随机映射到控制台
/// ESC: 刷新 Benchwarp 菜单（如果已打开）
/// </summary>
public class HotkeyHandler : MonoBehaviour
{
    private GUIStyle _tipStyle;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F6))
            WarpToLastBench();

        if (Input.GetKeyDown(KeyCode.F8))
        {
            RecentItemsUI.Toggle();
            Plugin.Log.LogInfo("最近获得物品UI " + (RecentItemsUI.IsVisible ? "显示" : "隐藏"));
        }

        if (Input.GetKeyDown(KeyCode.F9))
            Plugin.Instance?.DumpAllMappings();

        if (Input.GetKeyDown(KeyCode.Escape))
            Plugin.Instance?.RefreshBenchwarpUI();
    }

    /// <summary>
    /// 手动保存存档并重新加载，实现传送到上次坐的椅子
    /// 完全不依赖 Benchwarp，也不依赖任何菜单
    /// </summary>
    private void WarpToLastBench()
    {
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null)
            {
                Plugin.Log.LogError("[HotkeyHandler] PlayerData 不可用");
                return;
            }

            string sceneName = pd.respawnScene;
            if (string.IsNullOrEmpty(sceneName))
            {
                Plugin.Log.LogWarning("[HotkeyHandler] 重生点场景为空，请先坐一次椅子");
                return;
            }

            Plugin.Log.LogInfo($"[HotkeyHandler] 传送至重生点: {sceneName} / {pd.respawnMarkerName}");

            GameManager gm = GameManager.instance;
            gm.SaveGame((bool success) =>
            {
                if (!success)
                {
                    Plugin.Log.LogError("[HotkeyHandler] 保存游戏失败");
                    return;
                }
                gm.LoadGameFromUI(gm.profileID);
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[HotkeyHandler] 传送异常: {ex.Message}");
        }
    }

    private void OnGUI()
    {
        // 检测 Benchwarp 菜单是否打开
        bool menuVisible = IsBenchwarpMenuVisible();
        if (!menuVisible) return;

        // 初始化样式（只做一次）
        if (_tipStyle == null)
        {
            _tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.red }
            };
        }

        // 根据语言获取提示文字
        string tipText = IsChinese() ? "按 F6 可直接传送" : "Press F6 to warp directly";

        // 位置：屏幕高度的 2/3 处（下三分之一）
        float textWidth = 600f;
        float textHeight = 80f;
        float x = (Screen.width - textWidth) / 2f;
        float y = Screen.height * 0.66f;  // 下三分之一

        GUI.Label(new Rect(x, y, textWidth, textHeight), tipText, _tipStyle);
    }

    private bool IsBenchwarpMenuVisible()
    {
        try
        {
            var guiController = Benchwarp.Components.GUIController.Instance;
            if (guiController != null && guiController.IsDisplaying)
                return true;

            GameObject menu = GameObject.Find("WarpMenu") ?? GameObject.Find("BenchwarpMenu");
            if (menu != null && menu.activeInHierarchy)
                return true;
        }
        catch { }
        return false;
    }

    private bool IsChinese()
    {
        try
        {
            var fmType = Type.GetType("FontManager, Assembly-CSharp");
            if (fmType != null)
            {
                var field = fmType.GetField("_currentLanguage",
                    BindingFlags.Static | BindingFlags.NonPublic);
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

        try
        {
            var gm = GameManager.instance;
            var gs = gm?.gameSettings;
            if (gs != null)
            {
                var field = gs.GetType().GetField("language",
                    BindingFlags.Instance | BindingFlags.Public);
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