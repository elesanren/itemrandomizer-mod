using HutongGames.PlayMaker;
using SilksongItemRandomizer;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局快捷键处理器
/// F5: 输出当前场景所有拾取点的 FSM 信息（用于调试消失问题）
/// F6: 传送至上一次坐的椅子
/// F8: 显示/隐藏最近获得物品 UI
/// F9: 转储所有随机映射到控制台
/// ESC: 刷新 Benchwarp 菜单
/// </summary>
public class HotkeyHandler : MonoBehaviour
{
    private GUIStyle _tipStyle;
    private static bool _isChineseCache;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
            DumpPickupFSM();      // 新的拾取点 FSM 输出

        if (Input.GetKeyDown(KeyCode.F6))
            WarpToLastBench();

        if (Input.GetKeyDown(KeyCode.F8))
            ToggleRecentItemsUI();

        if (Input.GetKeyDown(KeyCode.F9))
            Plugin.Instance?.DumpAllMappings();

        if (Input.GetKeyDown(KeyCode.Escape))
            Plugin.Instance?.RefreshBenchwarpUI();
    }

    /// <summary>
    /// 输出当前场景所有拾取点 (CollectableItemPickup) 的 FSM 信息
    /// </summary>
    private static void DumpPickupFSM()
    {
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            var pickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>()
                .Where(p => p != null && p.gameObject.scene == scene)
                .ToList();

            Plugin.Log.LogInfo($"========== 当前场景 {scene.name} 中的拾取点信息（共 {pickups.Count} 个） ==========");

            if (pickups.Count == 0)
            {
                Plugin.Log.LogInfo("没有找到任何拾取点。");
                return;
            }

            foreach (var pickup in pickups)
            {
                var item = pickup.Item;
                string itemName = item != null ? item.name : "null";
                var pos = pickup.transform.position;
                Plugin.Log.LogInfo($"\n拾取点: {pickup.name} | 原物品: {itemName} | 位置: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
                Plugin.Log.LogInfo($"  自身 activeSelf: {pickup.gameObject.activeSelf}, activeInHierarchy: {pickup.gameObject.activeInHierarchy}");
                Plugin.Log.LogInfo($"  InstanceID: {pickup.gameObject.GetInstanceID()}");
                Plugin.Log.LogInfo($"  GameObject 路径: {GetGameObjectPath(pickup.gameObject)}");

                // 输出父物体信息
                Transform parent = pickup.transform.parent;
                if (parent != null)
                {
                    Plugin.Log.LogInfo($"  父物体: {parent.name}, activeSelf: {parent.gameObject.activeSelf}");
                    // 继续向上输出直到根
                    int depth = 0;
                    Transform cur = parent;
                    while (cur != null && depth < 10)
                    {
                        Plugin.Log.LogInfo($"  父级[{depth}]: {cur.name} activeSelf={cur.gameObject.activeSelf} activeInHierarchy={cur.gameObject.activeInHierarchy}");
                        cur = cur.parent;
                        depth++;
                    }
                }
                else
                {
                    Plugin.Log.LogInfo("  父物体: 无（根物体）");
                }

                // 输出所有组件（除了 Transform, CollectableItemPickup 等常见组件）
                var components = pickup.GetComponents<Component>();
                Plugin.Log.LogInfo($"  组件列表 ({components.Length}):");
                foreach (var comp in components)
                {
                    string compName = comp.GetType().Name;
                    if (comp is Transform) continue; // 忽略 Transform
                    if (comp is CollectableItemPickup) continue;
                    Plugin.Log.LogInfo($"    - {compName} (enabled: {(comp is Behaviour b ? b.enabled.ToString() : "N/A")})");
                }

                // 输出 PersistentBoolItem（可能挂载在父物体上？）
                var pbiInParent = pickup.GetComponentInParent<PersistentBoolItem>();
                if (pbiInParent != null && pbiInParent.gameObject != pickup.gameObject)
                {
                    Plugin.Log.LogInfo($"  父物体/祖先存在 PersistentBoolItem: {pbiInParent.name} enabled={pbiInParent.enabled}");
                }

                // 输出所有关联的 Collider2D
                var colliders = pickup.GetComponents<Collider2D>();
                if (colliders.Length > 0)
                {
                    Plugin.Log.LogInfo($"  Collider2D ({colliders.Length}):");
                    foreach (var col in colliders)
                        Plugin.Log.LogInfo($"    - {col.GetType().Name} enabled={col.enabled}");
                }
            }
            Plugin.Log.LogInfo("========== 扫描完成 ==========");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[DumpPickupFSM] 异常: {ex}");
        }
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private static void ToggleRecentItemsUI()
    {
        RecentItemsUI.Toggle();
        Plugin.Log.LogInfo($"最近获得物品UI {(RecentItemsUI.IsVisible ? "显示" : "隐藏")}");
    }

    private static void WarpToLastBench()
    {
        try
        {
            var pd = PlayerData.instance;
            if (pd == null)
            {
                Plugin.Log.LogError("[HotkeyHandler] PlayerData 不可用");
                return;
            }

            var sceneName = pd.respawnScene;
            if (string.IsNullOrEmpty(sceneName))
            {
                Plugin.Log.LogWarning("[HotkeyHandler] 重生点场景为空，请先坐一次椅子");
                return;
            }

            Plugin.Log.LogInfo($"[HotkeyHandler] 传送至重生点: {sceneName} / {pd.respawnMarkerName}");

            var gm = GameManager.instance;
            gm.SaveGame(success =>
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
        if (!IsBenchwarpMenuVisible()) return;

        _tipStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.red }
        };

        var tipText = IsChinese() ? "按 F6 可直接传送" : "Press F6 to warp directly";
        const float textWidth = 600f;
        const float textHeight = 80f;
        var x = (Screen.width - textWidth) / 2f;
        var y = Screen.height * 0.66f;

        GUI.Label(new Rect(x, y, textWidth, textHeight), tipText, _tipStyle);
    }

    private static bool IsBenchwarpMenuVisible()
    {
        try
        {
            var guiController = Benchwarp.Components.GUIController.Instance;
            if (guiController != null && guiController.IsDisplaying)
                return true;

            var menu = GameObject.Find("WarpMenu") ?? GameObject.Find("BenchwarpMenu");
            return menu != null && menu.activeInHierarchy;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsChinese()
    {
        if (_isChineseCache) return true;

        try
        {
            var fmType = Type.GetType("FontManager, Assembly-CSharp");
            if (fmType != null)
            {
                var field = fmType.GetField("_currentLanguage", BindingFlags.Static | BindingFlags.NonPublic);
                var val = field?.GetValue(null);
                if (val != null)
                {
                    var code = val.ToString().ToUpper();
                    if (code == "ZH" || code == "ZH_TW")
                    {
                        _isChineseCache = true;
                        return true;
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
                var field = gs.GetType().GetField("language", BindingFlags.Instance | BindingFlags.Public);
                var val = field?.GetValue(gs);
                if (val != null)
                {
                    var code = val.ToString().ToUpper();
                    if (code == "ZH" || code == "ZH_TW")
                    {
                        _isChineseCache = true;
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }
}