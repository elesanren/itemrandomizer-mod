using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Random = System.Random;

namespace StartingAbilityPicker;

public static class SkillRandomizer
{
    private static readonly Dictionary<string, string> SkillDisplayNames = new Dictionary<string, string>()
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
        { "hasWallJump", "爬墙" }
    };

    // 新增：技能分类列表
    public static readonly List<string> VerticalSkills = new List<string>()
    {
        "hasSuperJump", "hasDoubleJump", "hasWallJump", "hasBrolly", "hasHarpoonDash"
    };
    public static readonly List<string> HorizontalSkills = new List<string>()
    {
        "hasDash", "hasBrolly", "hasHarpoonDash", "hasSilkCharge"
    };
    public static readonly List<string> SpecialSkills = new List<string>()
    {
        "hasNeedolin", "hasChargeSlash", "hasBrolly", "hasHarpoonDash"
    };
    public static readonly List<string> AttackSkills = new List<string>()
    {
        "hasSilkBomb", "hasSilkBossNeedle", "hasThreadSphere", "hasSilkSpear", "hasSilkCharge"
    };

    private static readonly List<string> AllSkillFields = SkillDisplayNames.Keys.ToList();
    private static Random _rng;
    private static int _currentSeed;

    public static void SetSeed(int seed)
    {
        _currentSeed = seed;
        _rng = seed == 0 ? new Random() : new Random(seed);
        Plugin.Log.LogInfo($"SkillRandomizer seed set to: {seed}");
    }

    public static void ResetSeed()
    {
        SetSeed(_currentSeed);
        Plugin.Log.LogInfo("SkillRandomizer seed reset.");
    }

    private static void EnsureRng()
    {
        if (_rng == null)
        {
            _rng = new Random();
            Plugin.Log.LogWarning("SkillRandomizer RNG was not initialized, using default seed.");
        }
    }

    public static void GiveRandomSkill()
    {
        EnsureRng();
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null)
            {
                Plugin.Log.LogError("PlayerData.instance 为空");
                return;
            }

            List<string> missing = AllSkillFields.Where(fieldName =>
            {
                FieldInfo field = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                return field != null && field.FieldType == typeof(bool) && !(bool)field.GetValue(pd);
            }).ToList();

            if (missing.Count == 0)
            {
                Plugin.Log.LogInfo("所有技能已解锁，给予默认技能 Wall Jump");
                GiveWallJump();
                return;
            }

            string chosen = missing[_rng.Next(missing.Count)];
            FieldInfo chosenField = typeof(PlayerData).GetField(chosen, BindingFlags.Instance | BindingFlags.Public);
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

    // 新增：分类随机
    public static void GiveRandomSkillFromCategory(List<string> categoryFields)
    {
        EnsureRng();
        try
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;

            List<string> missing = categoryFields.Where(fieldName =>
            {
                FieldInfo field = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                return field != null && field.FieldType == typeof(bool) && !(bool)field.GetValue(pd);
            }).ToList();

            if (missing.Count == 0) return;

            string chosen = missing[_rng.Next(missing.Count)];
            FieldInfo chosenField = typeof(PlayerData).GetField(chosen, BindingFlags.Instance | BindingFlags.Public);
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
            foreach (string fieldName in candidates)
            {
                FieldInfo field = typeof(PlayerData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
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