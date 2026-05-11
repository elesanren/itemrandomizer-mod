using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = System.Random;

namespace SkillTriggerMod;

public static class SkillRandomizer
{
    // 字段名 → 显示名
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        { "hasNeedleThrow",  "丝之矛" },
        { "hasThreadSphere", "灵丝风暴" },
        { "hasHarpoonDash",  "飞针" },
        { "hasSilkCharge",   "丝刃标" },
        { "hasSilkBomb",     "符文之怒" },
        { "hasSilkBossNeedle","苍白之爪" },
        { "hasNeedolin",     "丝忆弦针" },
        { "hasDash",         "疾风步" },
        { "hasBrolly",       "流浪者披风" },
        { "hasDoubleJump",   "雪绒披风" },
        { "hasChargeSlash",  "蓄力斩" },
        { "hasSuperJump",    "灵丝升腾" },
        { "hasWallJump",     "蛛攀术" }
    };

    // ★ 手动修正的最佳图标名（精确匹配）
    private static readonly Dictionary<string, string> BestPickNames = new()
    {
        { "hasNeedleThrow",     "Silk Spear" },
        { "hasThreadSphere",    "Thread Sphere" },
        { "hasHarpoonDash",     "prompt_hornet_silk_dash" },
        { "hasSilkCharge",      "Silk Charge" },
        { "hasSilkBomb",        "Silk Bomb" },
        { "hasSilkBossNeedle",  "Silk Boss Needle" },
        { "hasNeedolin",        "Needolin_Prompt" },
        { "hasDash",            "prompt_swiftstep" },
        { "hasBrolly",          "prompt_hornet_umbrella" },
        { "hasDoubleJump",      "Hornet_Double_Jump_Prompt" },
        { "hasChargeSlash",     "charge_dash_slash" },
        { "hasSuperJump",       "prompt_super_jump" },
        { "hasWallJump",        "Wall_Jump_Prompt" }
    };

    // 后备关键词
    private static readonly Dictionary<string, string[]> FallbackKeywords = new()
    {
        { "hasNeedleThrow",     new[] { "Silk Spear", "SilkSpear", "NeedleThrow", "needle", "spear" } },
        { "hasThreadSphere",    new[] { "Thread Storm", "ThreadStorm", "ThreadSphere", "thread", "storm", "sphere" } },
        { "hasHarpoonDash",     new[] { "Clawline", "Harpoon Dash", "HarpoonDash", "harpoon", "dash" } },
        { "hasSilkCharge",      new[] { "Sharpdart", "Silk Charge", "SilkCharge", "sharpdart", "silk charge" } },
        { "hasSilkBomb",        new[] { "Rune Rage", "RuneRage", "Silk Bomb", "SilkBomb", "rune", "rage", "silk bomb" } },
        { "hasSilkBossNeedle",  new[] { "Pale Nails", "PaleNails", "Silk Boss Needle", "SilkBossNeedle", "pale", "nail", "needle", "boss" } },
        { "hasNeedolin",        new[] { "Needolin", "needolin" } },
        { "hasDash",            new[] { "Swift Step", "SwiftStep", "Dash", "dash", "swift", "step" } },
        { "hasBrolly",          new[] { "Drifter's Cloak", "DriftersCloak", "Drifter", "Brolly", "Umbrella", "brolly", "drifter", "cloak" } },
        { "hasDoubleJump",      new[] { "Faydown Cloak", "FaydownCloak", "Double Jump", "DoubleJump", "faydown", "double", "jump" } },
        { "hasChargeSlash",     new[] { "Needle Strike", "NeedleStrike", "Charge Slash", "ChargeSlash", "needle strike", "charge", "slash" } },
        { "hasSuperJump",       new[] { "Silk Soar", "SilkSoar", "Super Jump", "SuperJump", "silk soar", "super", "jump" } },
        { "hasWallJump",        new[] { "Cling Grip", "ClingGrip", "Wall Jump", "WallJump", "cling", "grip", "wall", "jump" } }
    };

    private static readonly List<string> AllFields = DisplayNames.Keys.ToList();
    private static Random _rng;
    private static int _seed;
    private static Dictionary<string, Sprite> _icons = new();
    private static Sprite _fallback;
    private static bool _cacheBuilt;

    public static void SetSeed(int seed)
    {
        _seed = seed;
        _rng = seed == 0 ? new Random() : new Random(seed);
        _cacheBuilt = false;
    }

    private static void EnsureRng() { _rng ??= new Random(); }

    private static void BuildIconCache()
    {
        if (_cacheBuilt) return;
        _cacheBuilt = true;
        _icons = new Dictionary<string, Sprite>();

        var allItems = Resources.FindObjectsOfTypeAll<SavedItem>();
        var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();

        foreach (var field in AllFields)
        {
            bool found = false;

            // 1. 精确匹配
            if (BestPickNames.TryGetValue(field, out string bestName))
            {
                // SavedItem
                var exactItem = allItems.FirstOrDefault(i => i && string.Equals(i.name, bestName, StringComparison.OrdinalIgnoreCase));
                if (exactItem != null)
                {
                    try { var icon = exactItem.GetPopupIcon(); if (icon) { _icons[field] = icon; Plugin.Log.LogInfo($"✓ {field} -> [SavedItem] {exactItem.name}"); found = true; } }
                    catch { }
                }
                // Sprite
                if (!found)
                {
                    var exactSprite = allSprites.FirstOrDefault(s => s && string.Equals(s.name, bestName, StringComparison.OrdinalIgnoreCase));
                    if (exactSprite != null) { _icons[field] = exactSprite; Plugin.Log.LogInfo($"✓ {field} -> [Sprite] {exactSprite.name}"); found = true; }
                }
            }

            // 2. 模糊兜底
            if (!found && FallbackKeywords.TryGetValue(field, out string[] keywords))
            {
                // SavedItem
                var itemMatch = allItems.FirstOrDefault(i => i && !string.IsNullOrEmpty(i.name) && keywords.Any(k => i.name.ToLower().Contains(k.ToLower())));
                if (itemMatch != null)
                {
                    try { var icon = itemMatch.GetPopupIcon(); if (icon) { _icons[field] = icon; Plugin.Log.LogInfo($"~ {field} -> [SavedItem] {itemMatch.name}"); found = true; } }
                    catch { }
                }
                // Sprite
                if (!found)
                {
                    var spriteMatch = allSprites.FirstOrDefault(s => s && !string.IsNullOrEmpty(s.name) && keywords.Any(k => s.name.ToLower().Contains(k.ToLower())));
                    if (spriteMatch != null) { _icons[field] = spriteMatch; Plugin.Log.LogInfo($"~ {field} -> [Sprite] {spriteMatch.name}"); found = true; }
                }
            }

            if (!found) Plugin.Log.LogWarning($"✗ {field} 未找到图标");
        }

        // 后备图标
        if (_fallback == null)
        {
            var rosary = allItems.FirstOrDefault(i => i && i.name.Contains("Rosary"));
            if (rosary) try { _fallback = rosary.GetPopupIcon(); } catch { }
            if (_fallback == null) _fallback = allSprites.FirstOrDefault(s => s && s.name.Contains("Rosary"));
        }
    }

    private static Sprite GetIcon(string field)
    {
        BuildIconCache();
        return _icons.TryGetValue(field, out Sprite icon) ? icon : _fallback;
    }

    // ========== 给予逻辑 ==========
    public static void GiveRandomSkill()
    {
        EnsureRng();
        try
        {
            var pd = PlayerData.instance;
            if (pd == null) return;

            List<string> missing = new();
            foreach (var fn in AllFields)
            {
                var fi = typeof(PlayerData).GetField(fn, BindingFlags.Instance | BindingFlags.Public);
                if (fi != null && fi.FieldType == typeof(bool) && !(bool)fi.GetValue(pd))
                    missing.Add(fn);
            }
            if (missing.Count == 0) { GiveWallJump(); return; }

            string chosen = missing[_rng.Next(missing.Count)];
            string display = DisplayNames.TryGetValue(chosen, out var n) ? n : chosen;
            typeof(PlayerData).GetField(chosen, BindingFlags.Instance | BindingFlags.Public)?.SetValue(pd, true);
            Plugin.ShowSkillPopup(GetIcon(chosen), display);
        }
        catch (Exception ex) { Plugin.Log.LogError($"GiveRandomSkill: {ex}"); }
    }

    public static void GiveWallJump()
    {
        try
        {
            var pd = PlayerData.instance;
            foreach (var fn in new[] { "hasWallJump", "hasWalljump", "hasWallJumpUnlocked" })
            {
                var fi = typeof(PlayerData).GetField(fn, BindingFlags.Instance | BindingFlags.Public);
                if (fi != null && fi.FieldType == typeof(bool) && !(bool)fi.GetValue(pd))
                { fi.SetValue(pd, true); break; }
            }
            Plugin.ShowSkillPopup(GetIcon("hasWallJump"), "蛛攀术");
        }
        catch (Exception ex) { Plugin.Log.LogError($"GiveWallJump: {ex}"); }
    }

    public static void GiveHarpoonDash()
    {
        try
        {
            var pd = PlayerData.instance;
            typeof(PlayerData).GetField("hasHarpoonDash", BindingFlags.Instance | BindingFlags.Public)?.SetValue(pd, true);
            Plugin.ShowSkillPopup(GetIcon("hasHarpoonDash"), "飞针");
        }
        catch (Exception ex) { Plugin.Log.LogError($"GiveHarpoonDash: {ex}"); }
    }
}