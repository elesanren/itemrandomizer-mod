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
    private static FieldInfo _facingRightField;
    private static FieldInfo _cStateField;
    private static FieldInfo _onGroundField;
    private static FieldInfo _allowAttackCancellingDownspikeRecoveryField;

    private static void Prefix(HeroController __instance, ref bool __runOriginal)
    {
        try
        {
            if (!__runOriginal) return;

            InitReflectionCache();

            // 1. 读取输入
            object inputHandler = _inputHandlerField?.GetValue(__instance);
            if (inputHandler == null) return;
            object inputActions = _inputActionsField?.GetValue(inputHandler);
            if (inputActions == null) return;

            object upAction = _upField?.GetValue(inputActions);
            object downAction = _downField?.GetValue(inputActions);
            bool upPressed = upAction != null && _isPressedProp != null && (bool)_isPressedProp.GetValue(upAction);
            bool downPressed = downAction != null && _isPressedProp != null && (bool)_isPressedProp.GetValue(downAction);

            // 2. 读取角色朝向
            bool facingRight = true;
            if (_facingRightField != null)
                facingRight = (bool)_facingRightField.GetValue(__instance);
            else
                facingRight = __instance.transform.localScale.x > 0;

            // 3. 读取状态 (用于判断下劈是否成立)
            bool onGround = true;
            object cState = _cStateField?.GetValue(__instance);
            if (cState != null && _onGroundField != null)
                onGround = (bool)_onGroundField.GetValue(cState);

            bool allowCancelDownspike = false;
            if (_allowAttackCancellingDownspikeRecoveryField != null)
                allowCancelDownspike = (bool)_allowAttackCancellingDownspikeRecoveryField.GetValue(__instance);

            // 4. 读取你的配置文件 (白名单)
            bool allowUp = Plugin.AllowUpwardAttack;
            bool allowLeft = Plugin.AllowLeftAttack;
            bool allowRight = Plugin.AllowRightAttack;

            // 5. 白名单逻辑：决定本次攻击是否允许
            bool allowed = false;

            if (upPressed)
            {
                // 上劈：只有勾选了上劈才允许
                allowed = allowUp;
            }
            else if (downPressed && (allowCancelDownspike || !onGround))
            {
                // 下劈：永远允许 (和旧版行为一致)
                allowed = true;
            }
            else
            {
                // 侧劈：根据朝向检查左/右权限
                if (facingRight)
                    allowed = allowRight;
                else
                    allowed = allowLeft;
            }

            // 6. 如果不允许，则拦截本次攻击
            if (!allowed)
            {
                __runOriginal = false;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AttackPatch 出错: {ex}");
            __runOriginal = true;
        }
    }

    private static void InitReflectionCache()
    {
        if (_inputHandlerField != null) return;

        var heroType = typeof(HeroController);
        _inputHandlerField = heroType.GetField("inputHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        _facingRightField = heroType.GetField("facingRight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        _cStateField = heroType.GetField("cState", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
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
            _onGroundField = _cStateField.FieldType.GetField("onGround", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }
}