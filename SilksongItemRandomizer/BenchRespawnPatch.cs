using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(HeroController), "SetBenchRespawn", new Type[] { typeof(RespawnMarker), typeof(string), typeof(int) })]
public static class BenchRespawnPatch
{
    private static void Postfix(HeroController __instance)
    {
        try
        {
            if (__instance == null || PlayerData.instance == null) return;
            __instance.StartCoroutine(DelayedCrestRefresh(__instance));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"BenchRespawnPatch异常: {ex}");
        }
    }

    private static IEnumerator DelayedCrestRefresh(HeroController hero)
    {
        yield return new WaitForSeconds(15f);

        try
        {
            string currentCrest = PlayerData.instance.CurrentCrestID;
            if (string.IsNullOrEmpty(currentCrest)) yield break;

            // ★ 只检查当前纹章是不是某个“源”——如果是，就换到它的目标
            string sourceMapped = CrestRandomizer.GetMappedCrestName(currentCrest);
            if (sourceMapped == currentCrest)
            {
                // 当前纹章没有映射记录，说明它不是源，什么都不做
                yield break;
            }

            // 当前纹章是“源”，需要换成目标
            Plugin.Log.LogInfo($"长椅刷新: {currentCrest} -> {sourceMapped}");

            MethodInfo setEquippedMethod = typeof(ToolItemManager).GetMethod("SetEquippedCrest", new[] { typeof(string) });
            setEquippedMethod?.Invoke(null, new object[] { sourceMapped });

            if (hero != null)
                hero.ResetAllCrestState();

            Plugin.Log.LogInfo($"长椅刷新完成，当前装备: {sourceMapped}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"DelayedCrestRefresh异常: {ex}");
        }
    }

    public static void ResetCooldown()
    {
        // 保持接口不变
    }
}