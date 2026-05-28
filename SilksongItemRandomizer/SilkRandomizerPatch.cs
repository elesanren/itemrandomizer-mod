using HarmonyLib;
using System;
using UnityEngine;

namespace SilksongItemRandomizer;

[HarmonyPatch(typeof(PlayerData))]
public static class SilkRandomizerPatch
{
    private static System.Random _rng;

    private static readonly int[] CostWeights = new int[9] { 1, 1, 1, 1, 2, 2, 2, 2, 2 };

    private static void EnsureRng()
    {
        if (_rng != null) return;
        var seed = Plugin.RandomSeed.Value;
        _rng = new System.Random(seed ^ 0x5A5A5A5A);
    }

    private static int GetRandomGainAmount()
    {
        if (_rng.NextDouble() < 0.9)
            return 1;
        else
            return _rng.Next(2, 10);
    }

    private static int GetRandomCostAmount()
    {
        int total = 0;
        foreach (int w in CostWeights) total += w;
        int roll = _rng.Next(total);
        int cumulative = 0;
        for (int i = 0; i < CostWeights.Length; i++)
        {
            cumulative += CostWeights[i];
            if (roll < cumulative) return i + 1;
        }
        return 5;
    }

    [HarmonyPatch(nameof(PlayerData.AddSilk))]
    [HarmonyPrefix]
    private static bool PrefixAddSilk(PlayerData __instance, ref int amount)
    {
        if (!Plugin.ItemRandomEnabled.Value) return true;
        EnsureRng();
        amount = GetRandomGainAmount();
        return true;
    }

    [HarmonyPatch(nameof(PlayerData.TakeSilk))]
    [HarmonyPrefix]
    private static bool PrefixTakeSilk(PlayerData __instance, ref int amount)
    {
        if (!Plugin.ItemRandomEnabled.Value) return true;
        EnsureRng();
        amount = GetRandomCostAmount();
        return true;
    }
}