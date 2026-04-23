using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(HeroController), "SetBenchRespawn", new Type[] { typeof(RespawnMarker), typeof(string), typeof(int) })]
public static class BenchRespawnPatch
{
    private static Dictionary<string, float> _lastRefreshTime = new Dictionary<string, float>();
    private const float COOLDOWN = 30f;

    private static void Postfix(HeroController __instance)
    {
        try
        {
            if (__instance == null || PlayerData.instance == null)
                return;
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
            object currentCrestObj = PlayerData.instance.CurrentCrestID;
            if (currentCrestObj != null)
            {
                string currentCrest = currentCrestObj.ToString();
                if (!string.IsNullOrEmpty(currentCrest))
                {
                    if (_lastRefreshTime.TryGetValue(currentCrest, out float last) && Time.time - last < COOLDOWN)
                    {
                        Plugin.Log.LogInfo($"纹章 {currentCrest} 在冷却期内，跳过刷新");
                    }
                    else
                    {
                        string targetCrest = CrestRandomizer.GetMappedCrestName(currentCrest);
                        if (!string.IsNullOrEmpty(targetCrest))
                        {
                            if (currentCrest != targetCrest)
                            {
                                Plugin.Log.LogInfo($"延迟15秒后检测到纹章不一致: 当前={currentCrest}, 目标={targetCrest}，开始刷新");
                                MethodInfo setEquippedMethod = typeof(ToolItemManager).GetMethod("SetEquippedCrest", new Type[] { typeof(string) });
                                if (setEquippedMethod != null)
                                    setEquippedMethod.Invoke(null, new object[] { targetCrest });
                                if (hero != null)
                                    hero.ResetAllCrestState();
                                _lastRefreshTime[currentCrest] = Time.time;
                                Plugin.Log.LogInfo("延迟刷新完成，当前装备: " + targetCrest);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"DelayedCrestRefresh异常: {ex}");
        }
    }

    public static void ResetCooldown()
    {
        _lastRefreshTime.Clear();
        Plugin.Log.LogInfo("纹章刷新冷却已清空");
    }
}