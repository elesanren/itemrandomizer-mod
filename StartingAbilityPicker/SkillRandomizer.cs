using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Random = System.Random;

namespace StartingAbilityPicker;

public static class SkillRandomizer
{
    private static readonly Dictionary<string, string> SkillDisplayNames = new()
    {
        { "hasNeedleThrow", "飞针" }, { "hasThreadSphere", "丝线球" }, { "hasHarpoonDash", "鱼叉冲刺" },
        { "hasSilkCharge", "丝线冲刺" }, { "hasSilkBomb", "丝线炸弹" }, { "hasSilkBossNeedle", "丝线针" },
        { "hasNeedolin", "针线" }, { "hasDash", "冲刺" }, { "hasBrolly", "伞" }, { "hasDoubleJump", "二段跳" },
        { "hasChargeSlash", "蓄力斩" }, { "hasSuperJump", "超级跳" }, { "hasWallJump", "爬墙" },
        { "hasSilkSpear", "丝矛" }, { "hasParry", "弹反" }
    };

    public static readonly List<string> VerticalSkills = new() { "hasSuperJump", "hasDoubleJump", "hasWallJump", "hasBrolly", "hasHarpoonDash" };
    public static readonly List<string> HorizontalSkills = new() { "hasDash", "hasBrolly", "hasHarpoonDash", "hasSilkCharge" };
    public static readonly List<string> SpecialSkills = new() { "hasNeedolin", "hasChargeSlash", "hasBrolly", "hasHarpoonDash" };
    public static readonly List<string> AttackSkills = new() { "hasSilkBomb", "hasSilkBossNeedle", "hasThreadSphere", "hasSilkSpear", "hasSilkCharge", "hasParry", "hasNeedleThrow" };

    private static readonly List<string> AllSkillFields = SkillDisplayNames.Keys.ToList();
    private static Random _rng;

    public static void SetSeed(int seed) => _rng = seed == 0 ? new Random() : new Random(seed);
    private static Random GetRng() => _rng ??= new Random();

    private static bool GetBoolField(PlayerData pd, string fieldName)
    {
        var field = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(pd);
    }

    private static void SetBoolField(PlayerData pd, string fieldName, bool value)
    {
        var field = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null && field.FieldType == typeof(bool))
            field.SetValue(pd, value);
    }

    public static void GiveRandomSkill()
    {
        var pd = PlayerData.instance;
        if (pd == null) return;

        var missing = new List<string>();
        foreach (var f in AllSkillFields)
            if (!GetBoolField(pd, f)) missing.Add(f);

        if (missing.Count == 0)
        {
            GiveWallJump();
            return;
        }

        string chosen = missing[GetRng().Next(missing.Count)];
        SetBoolField(pd, chosen, true);
        string display = SkillDisplayNames.TryGetValue(chosen, out string name) ? name : chosen.Replace("has", "");
        Plugin.ShowNotification("获得技能: " + display);
    }

    public static void GiveRandomSkillFromCategory(List<string> categoryFields)
    {
        var pd = PlayerData.instance;
        if (pd == null) return;

        var missing = new List<string>();
        foreach (var f in categoryFields)
            if (!GetBoolField(pd, f)) missing.Add(f);

        if (missing.Count == 0) return;

        string chosen = missing[GetRng().Next(missing.Count)];
        SetBoolField(pd, chosen, true);
        string display = SkillDisplayNames.TryGetValue(chosen, out string name) ? name : chosen.Replace("has", "");
        Plugin.ShowNotification("获得技能: " + display);
    }

    public static void GiveWallJump()
    {
        var pd = PlayerData.instance;
        if (pd == null) return;

        string[] candidates = { "hasWallJump", "hasWalljump", "hasWallJumpUnlocked" };
        foreach (string f in candidates)
        {
            var field = typeof(PlayerData).GetField(f, BindingFlags.Instance | BindingFlags.Public);
            if (field != null && field.FieldType == typeof(bool) && !(bool)field.GetValue(pd))
            {
                field.SetValue(pd, true);
                Plugin.ShowNotification("获得技能: 爬墙");
                break;
            }
        }
    }
}