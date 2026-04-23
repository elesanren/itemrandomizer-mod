// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.BenchRespawnPatch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (HeroController), "SetBenchRespawn", new Type[] {typeof (RespawnMarker), typeof (string), typeof (int)})]
public static class BenchRespawnPatch
{
  private static Dictionary<string, float> _lastRefreshTime = new Dictionary<string, float>();
  private const float COOLDOWN = 30f;

  private static void Postfix(HeroController __instance)
  {
    try
    {
      if (Object.op_Equality((Object) __instance, (Object) null) || PlayerData.instance == null)
        return;
      ((MonoBehaviour) __instance).StartCoroutine(BenchRespawnPatch.DelayedCrestRefresh(__instance));
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"BenchRespawnPatch异常: {ex}");
    }
  }

  private static IEnumerator DelayedCrestRefresh(HeroController hero)
  {
    yield return (object) new WaitForSeconds(15f);
    try
    {
      object currentCrestObj = (object) PlayerData.instance.CurrentCrestID;
      if (currentCrestObj != null)
      {
        string currentCrest = currentCrestObj.ToString();
        if (!string.IsNullOrEmpty(currentCrest))
        {
          float last;
          if (BenchRespawnPatch._lastRefreshTime.TryGetValue(currentCrest, out last) && (double) Time.time - (double) last < 30.0)
          {
            Plugin.Log.LogInfo((object) $"纹章 {currentCrest} 在冷却期内，跳过刷新");
          }
          else
          {
            string targetCrest = CrestRandomizer.GetMappedCrestName(currentCrest);
            if (!string.IsNullOrEmpty(targetCrest))
            {
              if (currentCrest != targetCrest)
              {
                Plugin.Log.LogInfo((object) $"延迟15秒后检测到纹章不一致: 当前={currentCrest}, 目标={targetCrest}，开始刷新");
                MethodInfo setEquippedMethod = typeof (ToolItemManager).GetMethod("SetEquippedCrest", new Type[1]
                {
                  typeof (string)
                });
                if (setEquippedMethod != (MethodInfo) null)
                  setEquippedMethod.Invoke((object) null, new object[1]
                  {
                    (object) targetCrest
                  });
                if (Object.op_Inequality((Object) hero, (Object) null))
                  hero.ResetAllCrestState();
                BenchRespawnPatch._lastRefreshTime[currentCrest] = Time.time;
                Plugin.Log.LogInfo((object) ("延迟刷新完成，当前装备: " + targetCrest));
                setEquippedMethod = (MethodInfo) null;
              }
              currentCrestObj = (object) null;
              currentCrest = (string) null;
              targetCrest = (string) null;
            }
          }
        }
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"DelayedCrestRefresh异常: {ex}");
    }
  }

  public static void ResetCooldown()
  {
    BenchRespawnPatch._lastRefreshTime.Clear();
    Plugin.Log.LogInfo((object) "纹章刷新冷却已清空");
  }
}
