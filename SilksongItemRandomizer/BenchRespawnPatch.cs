using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(HeroController), "SetBenchRespawn", new Type[] { typeof(RespawnMarker), typeof(string), typeof(int) })]
public static class BenchRespawnPatch
{
    private static Coroutine _activeCoroutine = null;
    private static float _lastExecutionTime = 0f;
    private const float MinInterval = 10f;

    private static void Postfix(HeroController __instance)
    {
        try
        {
            if (__instance == null || PlayerData.instance == null) return;
            if (_activeCoroutine != null) return;
            if (Time.time - _lastExecutionTime < MinInterval) return;

            string originalCrestId = PlayerData.instance.CurrentCrestID;
            if (string.IsNullOrEmpty(originalCrestId)) return;

            Plugin.Log.LogInfo($"重生触发: 延迟7秒处理纹章 {originalCrestId}");
            // 使用 Plugin.Instance 启动协程，确保能够运行
            _activeCoroutine = Plugin.Instance.StartCoroutine(DelayedReplace(originalCrestId, __instance));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"BenchRespawnPatch异常: {ex}");
        }
    }

    private static IEnumerator DelayedReplace(string originalCrestId, HeroController hero)
    {
        yield return new WaitForSeconds(7f);
        _activeCoroutine = null;

        string targetCrest = CrestRandomizer.LastUnlockedCrest;
        if (string.IsNullOrEmpty(targetCrest))
        {
            Plugin.Log.LogInfo("没有最后解锁的纹章记录，跳过重生替换");
            yield break;
        }

        Plugin.Log.LogInfo($"重生强制替换: {originalCrestId} -> {targetCrest}");
        typeof(ToolItemManager).GetMethod("SetEquippedCrest", new[] { typeof(string) })
            ?.Invoke(null, new object[] { targetCrest });
        hero?.ResetAllCrestState();

        yield return null;
        ToolItemManager.SendEquippedChangedEvent(true);

        _lastExecutionTime = Time.time;
        Plugin.Log.LogInfo($"替换完成，当前装备: {targetCrest}");
    }

    public static void ResetCooldown()
    {
        if (_activeCoroutine != null)
            _activeCoroutine = null;
        _lastExecutionTime = 0f;
    }
}