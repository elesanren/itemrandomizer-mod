// Decompiled with JetBrains decompiler
// Type: SkillTriggerMod.SkillTrigger
// Assembly: SkillRandomizerMod, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 31ECD94A-A255-405A-B0F7-6544B29C2F91
// Assembly location: E:\a\HardItemRandomizer\plugins\SkillRandomizerMod.dll

using UnityEngine;

#nullable enable
namespace SkillTriggerMod;

public class SkillTrigger : MonoBehaviour
{
  private bool _triggered = false;
  private string _sceneName;

  public void SetSceneName(string name) => this._sceneName = name;

  private void OnTriggerEnter2D(Collider2D other)
  {
    if (this._triggered || !((Component) other).CompareTag("Hero") && !((Component) other).CompareTag("Player"))
      return;
    PlayerData instance = PlayerData.instance;
    if (instance == null)
      return;
    string str = $"SkillTriggered_{this._sceneName}_{((Component) this).transform.position.x:F2}_{((Component) this).transform.position.y:F2}";
    if (instance.GetBool(str))
    {
      Object.Destroy((Object) ((Component) this).gameObject);
    }
    else
    {
      this._triggered = true;
      if (this._sceneName == "Shellwood_10")
      {
        SkillRandomizer.GiveWallJump();
        instance.SetBool(str, true);
      }
      else
      {
        SkillRandomizer.GiveRandomSkill();
        instance.SetBool(str, true);
      }
      Object.Destroy((Object) ((Component) this).gameObject);
    }
  }
}
