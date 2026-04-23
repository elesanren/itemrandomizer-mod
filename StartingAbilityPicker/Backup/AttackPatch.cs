// Decompiled with JetBrains decompiler
// Type: StartingAbilityPicker.AttackPatch
// Assembly: StartingAbilityPicker, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4695D065-A369-4338-8DBD-5D0C146838A7
// Assembly location: E:\a\HardItemRandomizer\plugins\StartingAbilityPicker.dll

using HarmonyLib;
using System;
using System.Reflection;

#nullable enable
namespace StartingAbilityPicker;

[HarmonyPatch(typeof (HeroController), "DoAttack")]
public class AttackPatch
{
  private static void Prefix(HeroController __instance, ref bool __runOriginal)
  {
    try
    {
      if (!__runOriginal)
        return;
      FieldInfo field1 = typeof (HeroController).GetField("inputHandler", BindingFlags.Instance | BindingFlags.NonPublic);
      if (field1 == (FieldInfo) null)
        return;
      object obj1 = field1.GetValue((object) __instance);
      if (obj1 == null)
        return;
      FieldInfo field2 = obj1.GetType().GetField("inputActions", BindingFlags.Instance | BindingFlags.Public);
      if (field2 == (FieldInfo) null)
        return;
      object obj2 = field2.GetValue(obj1);
      if (obj2 == null)
        return;
      FieldInfo field3 = obj2.GetType().GetField("Up");
      FieldInfo field4 = obj2.GetType().GetField("Down");
      if (field3 == (FieldInfo) null || field4 == (FieldInfo) null)
        return;
      object obj3 = field3.GetValue(obj2);
      object obj4 = field4.GetValue(obj2);
      PropertyInfo property = obj3?.GetType().GetProperty("IsPressed");
      if (property == (PropertyInfo) null)
        return;
      bool flag1 = (bool) property.GetValue(obj3);
      bool flag2 = (bool) property.GetValue(obj4);
      bool flag3 = true;
      try
      {
        FieldInfo field5 = typeof (HeroController).GetField("facingRight", BindingFlags.Instance | BindingFlags.NonPublic);
        flag3 = !(field5 != (FieldInfo) null) ? (double) __instance.transform.localScale.x > 0.0 : (bool) field5.GetValue((object) __instance);
      }
      catch
      {
      }
      bool flag4 = false;
      bool flag5 = false;
      bool flag6 = false;
      try
      {
        PlayerData instance = PlayerData.instance;
        if (instance != null)
        {
          flag4 = instance.GetBool("AllowUpwardAttack");
          flag5 = instance.GetBool("AllowLeftAttack");
          flag6 = instance.GetBool("AllowRightAttack");
        }
      }
      catch
      {
      }
      if (!flag1 ? flag2 && (__instance.allowAttackCancellingDownspikeRecovery || !__instance.cState.onGround) || (!flag3 ? flag6 : flag5) : flag4)
        return;
      __runOriginal = false;
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"Exception in AttackPatch: {ex}");
      __runOriginal = true;
    }
  }
}
