// Decompiled with JetBrains decompiler
// Type: SkillTriggerMod.SkillRandomizer
// Assembly: SkillRandomizerMod, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 31ECD94A-A255-405A-B0F7-6544B29C2F91
// Assembly location: E:\a\HardItemRandomizer\plugins\SkillRandomizerMod.dll

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#nullable enable
namespace SkillTriggerMod;

public static class SkillRandomizer
{
  private static readonly Dictionary<string, string> SkillDisplayNames = new Dictionary<string, string>()
  {
    {
      "hasNeedleThrow",
      "飞针"
    },
    {
      "hasThreadSphere",
      "丝线球"
    },
    {
      "hasHarpoonDash",
      "鱼叉冲刺"
    },
    {
      "hasSilkCharge",
      "丝线冲刺"
    },
    {
      "hasSilkBomb",
      "丝线炸弹"
    },
    {
      "hasSilkBossNeedle",
      "丝线针"
    },
    {
      "hasNeedolin",
      "针线"
    },
    {
      "hasDash",
      "冲刺"
    },
    {
      "hasBrolly",
      "伞"
    },
    {
      "hasDoubleJump",
      "二段跳"
    },
    {
      "hasChargeSlash",
      "蓄力斩"
    },
    {
      "hasSuperJump",
      "超级跳"
    },
    {
      "hasWallJump",
      "爬墙"
    }
  };
  private static readonly List<string> AllSkillFields = SkillRandomizer.SkillDisplayNames.Keys.ToList<string>();
  private static Random? _rng;
  private static int _currentSeed;

  public static void SetSeed(int seed)
  {
    SkillRandomizer._currentSeed = seed;
    SkillRandomizer._rng = seed == 0 ? new Random() : new Random(seed);
  }

  private static void EnsureRng()
  {
    if (SkillRandomizer._rng != null)
      return;
    SkillRandomizer._rng = new Random();
  }

  public static void GiveRandomSkill()
  {
    SkillRandomizer.EnsureRng();
    try
    {
      PlayerData instance = PlayerData.instance;
      if (instance == null)
        return;
      List<string> stringList = new List<string>();
      foreach (string allSkillField in SkillRandomizer.AllSkillFields)
      {
        FieldInfo field = typeof (PlayerData).GetField(allSkillField, BindingFlags.Instance | BindingFlags.Public);
        if (field != (FieldInfo) null && field.FieldType == typeof (bool) && !(bool) field.GetValue((object) instance))
          stringList.Add(allSkillField);
      }
      if (stringList.Count == 0)
      {
        SkillRandomizer.GiveWallJump();
      }
      else
      {
        string str1 = stringList[SkillRandomizer._rng.Next(stringList.Count)];
        FieldInfo field = typeof (PlayerData).GetField(str1, BindingFlags.Instance | BindingFlags.Public);
        if (!(field != (FieldInfo) null))
          return;
        field.SetValue((object) instance, (object) true);
        string str2;
        Plugin.ShowNotification("获得技能: " + (SkillRandomizer.SkillDisplayNames.TryGetValue(str1, out str2) ? str2 : str1.Replace("has", "")));
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"SkillRandomizer 出错: {ex}");
    }
  }

  public static void GiveWallJump()
  {
    try
    {
      PlayerData instance = PlayerData.instance;
      if (instance == null)
        return;
      string[] strArray = new string[3]
      {
        "hasWallJump",
        "hasWalljump",
        "hasWallJumpUnlocked"
      };
      foreach (string name in strArray)
      {
        FieldInfo field = typeof (PlayerData).GetField(name, BindingFlags.Instance | BindingFlags.Public);
        if (field != (FieldInfo) null && field.FieldType == typeof (bool))
        {
          if ((bool) field.GetValue((object) instance))
            break;
          field.SetValue((object) instance, (object) true);
          Plugin.ShowNotification("获得技能: 爬墙");
          break;
        }
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"GiveWallJump 出错: {ex}");
    }
  }
}
