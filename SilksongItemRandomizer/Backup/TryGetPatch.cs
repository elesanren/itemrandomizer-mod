// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.TryGetPatch
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

[HarmonyPatch(typeof (SavedItem), "TryGet")]
public class TryGetPatch
{
  private static bool _isProcessing;

  private static void Postfix(SavedItem __instance, bool __result)
  {
    try
    {
      if (TryGetPatch._isProcessing || !__result)
        return;
      if (((Object) __instance).name != "Rosary_Set_Frayed")
        RecentItemsUI.AddItem(__instance);
      TryGetPatch._isProcessing = true;
      try
      {
        TryGetPatch.UnlockRandomAttack();
      }
      catch (Exception ex)
      {
        Plugin.Log.LogError((object) $"Error unlocking attack: {ex}");
      }
      finally
      {
        TryGetPatch._isProcessing = false;
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"Unhandled exception in TryGetPatch.Postfix: {ex}");
    }
  }

  private static void UnlockRandomAttack()
  {
    Type type = Type.GetType("StartingAbilityPicker.Plugin, StartingAbilityPicker");
    if (type == (Type) null)
      return;
    FieldInfo field1 = type.GetField("AllowUpwardAttack", BindingFlags.Public | BindingFlags.Static);
    FieldInfo field2 = type.GetField("AllowLeftAttack", BindingFlags.Public | BindingFlags.Static);
    FieldInfo field3 = type.GetField("AllowRightAttack", BindingFlags.Public | BindingFlags.Static);
    if (field1 == (FieldInfo) null || field2 == (FieldInfo) null || field3 == (FieldInfo) null)
      return;
    bool flag1 = (bool) field1.GetValue((object) null);
    bool flag2 = (bool) field2.GetValue((object) null);
    bool flag3 = (bool) field3.GetValue((object) null);
    List<string> stringList = new List<string>();
    if (!flag1)
      stringList.Add("upward");
    if (!flag2)
      stringList.Add("left");
    if (!flag3)
      stringList.Add("right");
    if (stringList.Count == 0 || ItemRandomizer.Rng == null || ItemRandomizer.Rng.NextDouble() > 0.05)
      return;
    string str = stringList[ItemRandomizer.Rng.Next(stringList.Count)];
    PlayerData instance = PlayerData.instance;
    if (instance == null)
      return;
    switch (str)
    {
      case "upward":
        field1.SetValue((object) null, (object) true);
        instance.SetBool("AllowUpwardAttack", true);
        Plugin.Log.LogInfo((object) "Attack direction unlocked via item: upward (saved to PlayerData)");
        break;
      case "left":
        field2.SetValue((object) null, (object) true);
        instance.SetBool("AllowLeftAttack", true);
        Plugin.Log.LogInfo((object) "Attack direction unlocked via item: left (saved to PlayerData)");
        break;
      case "right":
        field3.SetValue((object) null, (object) true);
        instance.SetBool("AllowRightAttack", true);
        Plugin.Log.LogInfo((object) "Attack direction unlocked via item: right (saved to PlayerData)");
        break;
    }
  }
}
