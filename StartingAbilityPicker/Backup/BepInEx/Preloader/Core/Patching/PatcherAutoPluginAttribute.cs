// Decompiled with JetBrains decompiler
// Type: BepInEx.Preloader.Core.Patching.PatcherAutoPluginAttribute
// Assembly: StartingAbilityPicker, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4695D065-A369-4338-8DBD-5D0C146838A7
// Assembly location: E:\a\HardItemRandomizer\plugins\StartingAbilityPicker.dll

using System;
using System.Diagnostics;

#nullable enable
namespace BepInEx.Preloader.Core.Patching;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
[Conditional("CodeGeneration")]
internal sealed class PatcherAutoPluginAttribute : Attribute
{
  public PatcherAutoPluginAttribute(string? id = null, string? name = null, string? version = null)
  {
  }
}
