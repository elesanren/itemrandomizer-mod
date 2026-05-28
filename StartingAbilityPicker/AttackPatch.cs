using HarmonyLib;
using System;
using System.Reflection;

namespace StartingAbilityPicker;

[HarmonyPatch(typeof(HeroController), "DoAttack")]
public static class AttackPatch
{
    private static readonly FieldInfo InputHandlerField;
    private static readonly FieldInfo InputActionsField;
    private static readonly FieldInfo UpField;
    private static readonly FieldInfo DownField;
    private static readonly PropertyInfo IsPressedProp;
    private static readonly FieldInfo FacingRightField;
    private static readonly FieldInfo CStateField;
    private static readonly FieldInfo OnGroundField;
    private static readonly FieldInfo AllowAttackCancellingDownspikeRecoveryField;

    static AttackPatch()
    {
        var heroType = typeof(HeroController);
        InputHandlerField = heroType.GetField("inputHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        FacingRightField = heroType.GetField("facingRight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        CStateField = heroType.GetField("cState", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        AllowAttackCancellingDownspikeRecoveryField = heroType.GetField("allowAttackCancellingDownspikeRecovery", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (InputHandlerField != null)
        {
            var inputHandlerType = InputHandlerField.FieldType;
            InputActionsField = inputHandlerType.GetField("inputActions", BindingFlags.Instance | BindingFlags.Public);
            if (InputActionsField != null)
            {
                var inputActionsType = InputActionsField.FieldType;
                UpField = inputActionsType.GetField("Up");
                DownField = inputActionsType.GetField("Down");
                if (UpField != null)
                    IsPressedProp = UpField.FieldType.GetProperty("IsPressed");
            }
        }

        if (CStateField != null)
            OnGroundField = CStateField.FieldType.GetField("onGround", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    [HarmonyPrefix]
    private static bool Prefix(HeroController __instance, ref bool __runOriginal)
    {
        try
        {
            if (!__runOriginal) return false;

            var inputHandler = InputHandlerField?.GetValue(__instance);
            if (inputHandler == null) return true;
            var inputActions = InputActionsField?.GetValue(inputHandler);
            if (inputActions == null) return true;

            var upAction = UpField?.GetValue(inputActions);
            var downAction = DownField?.GetValue(inputActions);
            bool upPressed = upAction != null && IsPressedProp != null && (bool)IsPressedProp.GetValue(upAction);
            bool downPressed = downAction != null && IsPressedProp != null && (bool)IsPressedProp.GetValue(downAction);

            bool facingRight = FacingRightField != null
                ? (bool)FacingRightField.GetValue(__instance)
                : __instance.transform.localScale.x > 0;

            bool onGround = true;
            var cState = CStateField?.GetValue(__instance);
            if (cState != null && OnGroundField != null)
                onGround = (bool)OnGroundField.GetValue(cState);

            bool allowCancelDownspike = AllowAttackCancellingDownspikeRecoveryField != null && (bool)AllowAttackCancellingDownspikeRecoveryField.GetValue(__instance);

            bool allowUp = Plugin.AllowUpwardAttack;
            bool allowLeft = Plugin.AllowLeftAttack;
            bool allowRight = Plugin.AllowRightAttack;

            bool allowed;
            if (upPressed)
                allowed = allowUp;
            else if (downPressed && (allowCancelDownspike || !onGround))
                allowed = true;
            else
                allowed = facingRight ? allowRight : allowLeft;

            if (!allowed)
                __runOriginal = false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"AttackPatch 出错: {ex}");
            __runOriginal = true;
        }
        return true;
    }
}