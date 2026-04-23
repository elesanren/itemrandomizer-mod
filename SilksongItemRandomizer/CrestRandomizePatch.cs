using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(ToolCrest), "Unlock")]
public static class CrestRandomizePatch
{
    private static List<ToolCrest> _allCrests;
    private static HashSet<int> _processedInstanceIds = new HashSet<int>();

    public static List<ToolCrest> CrestList => _allCrests;

    public static void Initialize()
    {
        _allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>().ToList();
    }

    public static void ResetProcessedIds()
    {
        _processedInstanceIds.Clear();
    }

    [HarmonyPrefix]
    private static bool Prefix(ToolCrest __instance)
    {
        try
        {
            if (_processedInstanceIds.Contains(__instance.GetInstanceID()))
                return true;

            _processedInstanceIds.Add(__instance.GetInstanceID());
            string name = __instance.name;
            string mappedCrestName = CrestRandomizer.GetMappedCrestName(name);

            if (string.IsNullOrEmpty(mappedCrestName) || mappedCrestName == name)
            {
                Plugin.Log.LogInfo($"纹章 {name} 映射到自身，放行原解锁");
                return true;
            }

            if (_allCrests == null || _allCrests.Count == 0)
                Initialize();

            ToolCrest targetCrest = _allCrests?.FirstOrDefault(c => c.name == mappedCrestName);
            if (targetCrest == null)
            {
                Plugin.Log.LogWarning($"无法找到目标纹章 {mappedCrestName}，放行原解锁");
                return true;
            }

            targetCrest.Unlock();

            PlayerData instance = PlayerData.instance;
            if (instance != null && instance.CurrentCrestID == name)
            {
                typeof(ToolItemManager)
                    .GetMethod("SetEquippedCrest", new Type[] { typeof(string) })
                    ?.Invoke(null, new object[] { mappedCrestName });
            }

            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"CrestRandomizePatch异常: {ex}");
            return true;
        }
    }
}