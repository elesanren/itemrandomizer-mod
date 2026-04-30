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
        { "hasNeedleThrow", "飞针" },
        { "hasThreadSphere", "丝线球" },
        { "hasHarpoonDash", "鱼叉冲刺" },
        { "hasSilkCharge", "丝线冲刺" },
        { "hasSilkBomb", "丝线炸弹" },
        { "hasSilkBossNeedle", "丝线针" },
        { "hasNeedolin", "针线" },
        { "hasDash", "冲刺" },
        { "hasBrolly", "伞" },
        { "hasDoubleJump", "二段跳" },
        { "hasChargeSlash", "蓄力斩" },
        { "hasSuperJump", "超级跳" },
        { "hasWallJump", "爬墙" },
        { "hasSilkSpear", "丝矛" },
        { "hasParry", "弹反" },
    };


    public static readonly List<string> VerticalSkills = new() { "hasSuperJump", "hasDoubleJump", "hasWallJump", "hasBrolly", "hasHarpoonDash" };
    public static readonly List<string> HorizontalSkills = new() { "hasDash", "hasBrolly", "hasHarpoonDash", "hasSilkCharge" };
    public static readonly List<string> SpecialSkills = new() { "hasNeedolin", "hasChargeSlash", "hasBrolly", "hasHarpoonDash" };
    public static readonly List<string> AttackSkills = new() { "hasSilkBomb", "hasSilkBossNeedle", "hasThreadSphere", "hasSilkSpear", "hasSilkCharge", "hasParry", "hasNeedleThrow" };

    private static readonly List<string> AllSkillFields = SkillDisplayNames.Keys.ToList();
    private static Random _rng;
    private static int _currentSeed;

    public static void SetSeed(int seed)
    {
        _currentSeed = seed;
        _rng = seed == 0 ? new Random() : new Random(seed);
    }

    private static void EnsureRng()
    {
        _rng ??= new Random();
    }

    public static void GiveRandomSkill()
    {
        EnsureRng();
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;

            List<string> missing = AllSkillFields.Where(f =>
            {
                var field = typeof(PlayerData).GetField(f, BindingFlags.Instance | BindingFlags.Public);
                return field != null && field.FieldType == typeof(bool) && !(bool)field.GetValue(pd);
            }).ToList();

            if (missing.Count == 0) { GiveWallJump(); return; }

            string chosen = missing[_rng.Next(missing.Count)];
            var chosenField = typeof(PlayerData).GetField(chosen, BindingFlags.Instance | BindingFlags.Public);
            if (chosenField == null) return;
            chosenField.SetValue(pd, true);

            string display = SkillDisplayNames.TryGetValue(chosen, out string name) ? name : chosen.Replace("has", "");
            Plugin.ShowNotification("获得技能: " + display);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"SkillRandomizer 出错: {ex}");
        }
    }

    public static void GiveRandomSkillFromCategory(List<string> categoryFields)
    {
        EnsureRng();
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;

            List<string> missing = categoryFields.Where(f =>
            {
                var field = typeof(PlayerData).GetField(f, BindingFlags.Instance | BindingFlags.Public);
                return field != null && field.FieldType == typeof(bool) && !(bool)field.GetValue(pd);
            }).ToList();

            if (missing.Count == 0) return;

            string chosen = missing[_rng.Next(missing.Count)];
            var chosenField = typeof(PlayerData).GetField(chosen, BindingFlags.Instance | BindingFlags.Public);
            if (chosenField == null) return;
            chosenField.SetValue(pd, true);

            string display = SkillDisplayNames.TryGetValue(chosen, out string name) ? name : chosen.Replace("has", "");
            Plugin.ShowNotification("获得技能: " + display);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"SkillRandomizer 出错: {ex}");
        }
    }

    public static void GiveWallJump()
    {
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;

            string[] candidates = { "hasWallJump", "hasWalljump", "hasWallJumpUnlocked" };
            foreach (string f in candidates)
            {
                var field = typeof(PlayerData).GetField(f, BindingFlags.Instance | BindingFlags.Public);
                if (field != null && field.FieldType == typeof(bool))
                {
                    if (!(bool)field.GetValue(pd))
                    {
                        field.SetValue(pd, true);
                        Plugin.ShowNotification("获得技能: 爬墙");
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"GiveWallJump 出错: {ex}");
        }
    }
}