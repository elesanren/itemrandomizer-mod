// Decompiled with JetBrains decompiler
// Type: System.Runtime.CompilerServices.RefSafetyRulesAttribute
// Assembly: StartingAbilityPicker, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4695D065-A369-4338-8DBD-5D0C146838A7
// Assembly location: E:\a\HardItemRandomizer\plugins\StartingAbilityPicker.dll

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
