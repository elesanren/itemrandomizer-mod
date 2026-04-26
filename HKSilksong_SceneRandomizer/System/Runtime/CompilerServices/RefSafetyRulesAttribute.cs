// Decompiled with JetBrains decompiler
// Type: System.Runtime.CompilerServices.RefSafetyRulesAttribute
// Assembly: HKSilksong_SceneRandomizer, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 5E09943E-2DCF-4A2F-97BD-502A92DA087D
// Assembly location: E:\a\Hard_Item_Randomizer_w_SceneRandom\plugins\HKSilksong_SceneRandomizer.dll

using Microsoft.CodeAnalysis;
using System.Runtime.InteropServices;

#nullable disable
namespace System.Runtime.CompilerServices;

[CompilerGenerated]
[Embedded]
[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
internal sealed class RefSafetyRulesAttribute : Attribute
{
  public readonly int Version;

  public RefSafetyRulesAttribute([In] int obj0) => this.Version = obj0;
}
