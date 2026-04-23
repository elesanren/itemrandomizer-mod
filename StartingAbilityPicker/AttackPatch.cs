using HarmonyLib;
using System;
using System.Reflection;

namespace StartingAbilityPicker;

[HarmonyPatch(typeof(HeroController), "DoAttack")]
public class AttackPatch
{
    private static FieldInfo _inputHandlerField;
    private static FieldInfo _inputActionsField;
    private static FieldInfo _upField;
    private static FieldInfo _downField;
    private static PropertyInfo _isPressedProp;
    private static FieldInfo _cStateField;
    private static FieldInfo _onGroundField;
    private static FieldInfo _allowAttackCancellingDownspikeRecoveryField;
    private static FieldInfo _facingRightField;

    private static void Prefix(HeroController __instance, ref bool __runOriginal)
    {
        try
        {
            InitReflectionCache();

            // --- 1. 获取输入状态（与原版完全一致）---
            object inputHandler = _inputHandlerField?.GetValue(__instance);
            if (inputHandler == null) return;
            object inputActions = _inputActionsField?.GetValue(inputHandler);
            if (inputActions == null) return;

            object upAction = _upField?.GetValue(inputActions);
            object downAction = _downField?.GetValue(inputActions);
            bool upPressed = upAction != null && _isPressedProp != null && (bool)_isPressedProp.GetValue(upAction);
            bool downPressed = downAction != null && _isPressedProp != null && (bool)_isPressedProp.GetValue(downAction);

            // --- 2. 获取游戏状态（用于判断 downward 是否转为 normal）---
            object cState = _cStateField?.GetValue(__instance);
            bool onGround = cState != null && _onGroundField != null && (bool)_onGroundField.GetValue(cState);
            bool allowCancel = false;
            if (_allowAttackCancellingDownspikeRecoveryField != null)
                allowCancel = (bool)_allowAttackCancellingDownspikeRecoveryField.GetValue(__instance);

            // --- 3. 获取角色朝向 ---
            bool facingRight = true;
            if (_facingRightField != null)
                facingRight = (bool)_facingRightField.GetValue(__instance);
            else
                facingRight = __instance.transform.localScale.x > 0;

            // --- 4. 确定游戏将要执行的实际攻击方向（完全模拟原版 DoAttack 逻辑）---
            string intendedDirection; // "upward", "downward", "normal_left", "normal_right"

            if (upPressed)
            {
                intendedDirection = "upward";
            }
            else if (!downPressed)
            {
                // 没按方向键 → 普通攻击，方向由朝向决定
                intendedDirection = facingRight ? "normal_right" : "normal_left";
            }
            else // downPressed == true
            {
                if (allowCancel || !onGround)
                    intendedDirection = "downward";
                else
                    intendedDirection = facingRight ? "normal_right" : "normal_left";
            }

            // --- 5. 从 PlayerData 读取实时解锁状态（兼容 TryGetPatch 随机解锁）---
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;

            bool allowUp = pd.GetBool("AllowUpwardAttack");
            bool allowLeft = pd.GetBool("AllowLeftAttack");
            bool allowRight = pd.GetBool("AllowRightAttack");

            // --- 6. 权限检查：如果该方向未被允许，则拦截攻击 ---
            bool allowed = true;
            switch (intendedDirection)
            {
                case "upward":
                    allowed = allowUp;
                    break;
                case "downward":
                    // 下劈视为侧劈，根据朝向决定需要左劈还是右劈权限
                    allowed = facingRight ? allowRight : allowLeft;
                    break;
                case "normal_right":
                    allowed = allowRight;
                    break;
                case "normal_left":
                    allowed = allowLeft;
                    break;
            }

            // ######################################################
            // ### 改动开始：实时调试数据采集（只加了这一个调用） ###
            // ######################################################
            //Plugin.UpdateDebugAttackState(upPressed, downPressed, facingRight, onGround, allowCancel,
            //                              allowUp, allowLeft, allowRight, intendedDirection, allowed);
            // ######################################################
            // ###                改动结束                         ###
            // ######################################################

            if (!allowed)
            {
                __runOriginal = false; // 拦截攻击
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AttackPatch 出错: {ex}");
            __runOriginal = true; // 出错时放行，避免卡死
        }
    }

    private static void InitReflectionCache()
    {
        if (_inputHandlerField != null) return;

        var heroType = typeof(HeroController);
        _inputHandlerField = heroType.GetField("inputHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        _cStateField = heroType.GetField("cState", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        _facingRightField = heroType.GetField("facingRight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        _allowAttackCancellingDownspikeRecoveryField = heroType.GetField("allowAttackCancellingDownspikeRecovery", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (_inputHandlerField != null)
        {
            var inputHandlerType = _inputHandlerField.FieldType;
            _inputActionsField = inputHandlerType.GetField("inputActions", BindingFlags.Instance | BindingFlags.Public);
            if (_inputActionsField != null)
            {
                var inputActionsType = _inputActionsField.FieldType;
                _upField = inputActionsType.GetField("Up");
                _downField = inputActionsType.GetField("Down");
                if (_upField != null)
                    _isPressedProp = _upField.FieldType.GetProperty("IsPressed");
            }
        }

        if (_cStateField != null)
        {
            _onGroundField = _cStateField.FieldType.GetField("onGround", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
    }
}