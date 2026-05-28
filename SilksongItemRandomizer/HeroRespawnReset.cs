using GlobalEnums;
using HarmonyLib;
using System.Collections;
using System.Reflection;
using UnityEngine;

[HarmonyPatch(typeof(HeroController))]
public static class HeroRespawnReset
{
    private const float ProtectionDuration = 4f;
    private static float _protectionEndTime;

    [HarmonyPatch("FinishedEnteringScene", typeof(bool), typeof(bool))]
    [HarmonyPostfix]
    private static void FinishedEnteringScenePostfix(HeroController __instance)
    {
        __instance.controlReqlinquished = false;
        __instance.cState.recoiling = false;
        __instance.cState.recoilingLeft = false;
        __instance.cState.recoilingRight = false;
        __instance.cState.floating = false;

        var acceptingField = typeof(HeroController).GetField("acceptingInput", BindingFlags.Instance | BindingFlags.NonPublic);
        acceptingField?.SetValue(__instance, true);

        var inputHandlerField = typeof(HeroController).GetField("inputHandler", BindingFlags.Instance | BindingFlags.NonPublic);
        var inputHandler = inputHandlerField?.GetValue(__instance);
        if (inputHandler != null)
        {
            var inputActionsProp = inputHandler.GetType().GetProperty("inputActions");
            var inputActions = inputActionsProp?.GetValue(inputHandler);
            (inputActions?.GetType().GetMethod("ClearInputState"))?.Invoke(inputActions, null);
        }

        __instance.damageMode = 0;
        __instance.AffectedByGravity(true);

        _protectionEndTime = Time.time + ProtectionDuration;
        __instance.cState.invulnerable = true;
        __instance.StartCoroutine(ClearInvincibleAfterDelay(__instance, ProtectionDuration));
    }

    private static IEnumerator ClearInvincibleAfterDelay(HeroController hero, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (hero != null) // 空引用保护
            hero.cState.invulnerable = false;
    }

    [HarmonyPatch("TakeDamage", typeof(GameObject), typeof(CollisionSide), typeof(int), typeof(HazardType), typeof(DamagePropertyFlags))]
    [HarmonyPrefix]
    private static bool TakeDamagePrefix(HeroController __instance)
    {
        return Time.time >= _protectionEndTime;
    }
}