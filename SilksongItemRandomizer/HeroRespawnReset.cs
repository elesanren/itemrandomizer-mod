using GlobalEnums;
using HarmonyLib;
using System.Collections;
using System.Reflection;
using UnityEngine;

[HarmonyPatch(typeof(HeroController))]
public static class HeroRespawnReset
{
    private static float _protectionEndTime = 0f;

    // 重生后重置状态并给予保护期
    [HarmonyPatch("FinishedEnteringScene", typeof(bool), typeof(bool))]
    [HarmonyPostfix]
    private static void FinishedEnteringScenePostfix(HeroController __instance)
    {
        // 1. 恢复控制权
        __instance.controlReqlinquished = false;

        // 2. 重置受伤后摇和漂浮状态
        __instance.cState.recoiling = false;
        __instance.cState.recoilingLeft = false;
        __instance.cState.recoilingRight = false;
        __instance.cState.floating = false;

        // 3. 确保输入接受标志为 true
        var acceptingField = typeof(HeroController).GetField("acceptingInput",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (acceptingField != null)
            acceptingField.SetValue(__instance, true);

        // 4. 清空输入缓冲
        var inputHandlerField = typeof(HeroController).GetField("inputHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var inputHandler = inputHandlerField?.GetValue(__instance);
        if (inputHandler != null)
        {
            var inputActionsProp = inputHandler.GetType().GetProperty("inputActions");
            var inputActions = inputActionsProp?.GetValue(inputHandler);
            if (inputActions != null)
            {
                var clearMethod = inputActions.GetType().GetMethod("ClearInputState");
                clearMethod?.Invoke(inputActions, null);
            }
        }

        // 5. 重置伤害模式为完全伤害
        __instance.damageMode = (DamageMode)0;

        // 6. 强制恢复重力
        __instance.AffectedByGravity(true);

        // 7. 设置4秒保护期（期间免疫所有伤害）
        _protectionEndTime = Time.time + 4f;

        // 8. 同时给予无敌标志（双重保险）
        __instance.cState.invulnerable = true;
        __instance.StartCoroutine(ClearInvincibleAfterDelay(__instance, 4f));
    }

    private static IEnumerator ClearInvincibleAfterDelay(HeroController hero, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (hero != null)
            hero.cState.invulnerable = false;
    }

    // 拦截所有伤害，保护期内免疫
    [HarmonyPatch("TakeDamage", typeof(GameObject), typeof(CollisionSide), typeof(int), typeof(HazardType), typeof(DamagePropertyFlags))]
    [HarmonyPrefix]
    private static bool TakeDamagePrefix(HeroController __instance)
    {
        if (Time.time < _protectionEndTime)
        {
            // 保护期内，阻止所有伤害
            return false;
        }
        return true;
    }
}