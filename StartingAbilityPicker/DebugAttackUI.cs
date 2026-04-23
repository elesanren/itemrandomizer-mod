//using UnityEngine;

//namespace StartingAbilityPicker;

//public static class DebugAttackUI
//{
//    private static bool _showUI = false;
//    private static Rect _windowRect = new Rect(Screen.width - 520f, 20f, 500f, 350f);

//    public static bool IsVisible => _showUI;

//    public static void Toggle()
//    {
//        _showUI = !_showUI;
//    }

//    public static void Draw()
//    {
//        if (!_showUI) return;

//        // 直接读取 Plugin 中的静态调试字段
//        bool upPressed = Plugin.Debug_UpPressed;
//        bool downPressed = Plugin.Debug_DownPressed;
//        bool facingRight = Plugin.Debug_FacingRight;
//        bool onGround = Plugin.Debug_OnGround;
//        bool allowCancel = Plugin.Debug_AllowCancel;
//        bool allowUp = Plugin.Debug_AllowUp;
//        bool allowLeft = Plugin.Debug_AllowLeft;
//        bool allowRight = Plugin.Debug_AllowRight;
//        string intendedDir = Plugin.Debug_IntendedDirection;
//        bool allowed = Plugin.Debug_AttackAllowed;
//        float lastTime = Plugin.Debug_LastUpdateTime;

//        _windowRect = GUILayout.Window(9999, _windowRect, (id) =>
//        {
//            GUILayout.BeginVertical();

//            GUILayout.Label("【攻击方向实时状态】", new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold });
//            GUILayout.Space(10);

//            GUILayout.Label($"按键输入:  上={BoolToStr(upPressed)}  下={BoolToStr(downPressed)}");
//            GUILayout.Label($"角色状态:  朝向={(facingRight ? "右" : "左")}  在地面={BoolToStr(onGround)}  允许取消下砸={BoolToStr(allowCancel)}");
//            GUILayout.Space(5);
//            GUILayout.Label($"权限配置:  上劈={BoolToStr(allowUp)}  左劈={BoolToStr(allowLeft)}  右劈={BoolToStr(allowRight)}");
//            GUILayout.Space(10);

//            GUILayout.Label($"游戏判定方向:  {intendedDir}");
//            GUILayout.Label($"本次攻击是否允许:  {(allowed ? "✅ 允许" : "❌ 阻止")}", new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
//            GUILayout.Space(15);

//            GUILayout.Label($"状态更新时间:  {Time.time - lastTime:F2} 秒前");
//            GUILayout.Label("按 F8 关闭此窗口", new GUIStyle(GUI.skin.label) { fontSize = 14 });

//            GUILayout.EndVertical();
//            GUI.DragWindow();
//        }, "实时调试");
//    }

//    private static string BoolToStr(bool b) => b ? "是" : "否";
//}