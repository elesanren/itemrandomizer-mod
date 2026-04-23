// Decompiled with JetBrains decompiler
// Type: StartingAbilityPicker.Plugin
// Assembly: StartingAbilityPicker, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 4695D065-A369-4338-8DBD-5D0C146838A7
// Assembly location: E:\a\HardItemRandomizer\plugins\StartingAbilityPicker.dll

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

#nullable enable
namespace StartingAbilityPicker;

[BepInPlugin("YourName.StartingAbilityPicker", "Starting Ability Picker", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
  internal static ManualLogSource Log;
  public static bool AllowLeftAttack = false;
  public static bool AllowRightAttack = false;
  public static bool AllowUpwardAttack = false;
  private bool _lastSceneWasMenu = true;
  private bool showUI = false;
  private Rect uiWindowRect;
  private bool allowUpward = false;
  private bool allowLeft = false;
  private bool allowRight = false;
  private int skillCount = 0;
  private int itemCount = 0;
  private bool resetPickups = false;
  private string seedInput = "";
  private static HashSet<int> chosenProfileSet = new HashSet<int>();
  private static string _notificationMessage = (string) null;
  private static float _notificationEndTime = 0.0f;
  private static GUIStyle _notificationStyle;

  public static ConfigEntry<string> ChosenProfiles { get; private set; }

  public static ConfigEntry<int> StartingSkillCount { get; private set; }

  public static ConfigEntry<int> StartingItemCount { get; private set; }

  public static ConfigEntry<int> RandomSeed { get; private set; }

  private int currentProfileID
  {
    get
    {
      return !Object.op_Inequality((Object) GameManager.instance, (Object) null) ? -1 : GameManager.instance.profileID;
    }
  }

  public static void ShowNotification(string message, float duration = 3f)
  {
    Plugin._notificationMessage = message;
    Plugin._notificationEndTime = Time.time + duration;
  }

  private Texture2D MakeTexture(int width, int height, Color col)
  {
    Color[] colorArray = new Color[width * height];
    for (int index = 0; index < colorArray.Length; ++index)
      colorArray[index] = col;
    Texture2D texture2D = new Texture2D(width, height);
    texture2D.SetPixels(colorArray);
    texture2D.Apply();
    return texture2D;
  }

  private void Awake()
  {
    Plugin.Log = this.Logger;
    Plugin.Log.LogInfo((object) "StartingAbilityPicker loaded! Press F7 to open starting options.");
    Plugin.ChosenProfiles = this.Config.Bind<string>("General", "ChosenProfiles", "", "已选择过开局选项的存档ID列表");
    Plugin.StartingSkillCount = this.Config.Bind<int>("General", "StartingSkillCount", 0, "开局随机技能数量 (0-5)");
    Plugin.StartingItemCount = this.Config.Bind<int>("General", "StartingItemCount", 0, "开局随机物品数量 (0-5)");
    Plugin.RandomSeed = this.Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
    this.LoadChosenProfiles();
    this.EnsureRandomSeed();
    // ISSUE: method pointer
    SceneManager.sceneLoaded += new UnityAction<Scene, LoadSceneMode>((object) this, __methodptr(OnSceneLoaded));
    Harmony.CreateAndPatchAll(typeof (AttackPatch), (string) null);
  }

  private void EnsureRandomSeed()
  {
    if (Plugin.RandomSeed.Value != 0)
      return;
    int num = new Random().Next(1, int.MaxValue);
    Plugin.RandomSeed.Value = num;
    this.Config.Save();
    Plugin.Log.LogInfo((object) $"已自动生成真实种子: {num}");
  }

  private void OnDestroy()
  {
    // ISSUE: method pointer
    SceneManager.sceneLoaded -= new UnityAction<Scene, LoadSceneMode>((object) this, __methodptr(OnSceneLoaded));
  }

  private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
  {
    if (((Scene) ref scene).name != "Menu_Title" && ((Scene) ref scene).name != "Menu" && ((Scene) ref scene).name != "Loading" && Object.op_Inequality((Object) HeroController.instance, (Object) null))
    {
      if (this._lastSceneWasMenu)
      {
        ((MonoBehaviour) this).StartCoroutine(this.ShowUIAuto());
        this._lastSceneWasMenu = false;
      }
      try
      {
        PlayerData instance = PlayerData.instance;
        if (instance == null)
          return;
        Plugin.AllowUpwardAttack = instance.GetBool("AllowUpwardAttack");
        Plugin.AllowLeftAttack = instance.GetBool("AllowLeftAttack");
        Plugin.AllowRightAttack = instance.GetBool("AllowRightAttack");
        Plugin.Log.LogInfo((object) $"从存档加载劈方向: 上={Plugin.AllowUpwardAttack}, 左={Plugin.AllowLeftAttack}, 右={Plugin.AllowRightAttack}");
      }
      catch (Exception ex)
      {
        Plugin.Log.LogError((object) $"加载劈方向设置失败: {ex}");
      }
    }
    else
    {
      if (!(((Scene) ref scene).name == "Menu_Title") && !(((Scene) ref scene).name == "Menu"))
        return;
      this._lastSceneWasMenu = true;
    }
  }

  private IEnumerator ShowUIAuto()
  {
    yield return (object) null;
    if (this.currentProfileID != -1)
    {
      this.skillCount = Plugin.StartingSkillCount.Value;
      this.itemCount = Plugin.StartingItemCount.Value;
      this.allowUpward = false;
      this.allowLeft = false;
      this.allowRight = false;
      this.resetPickups = false;
      this.seedInput = Plugin.RandomSeed.Value.ToString();
      this.showUI = true;
      Plugin.Log.LogInfo((object) "加载存档，自动打开开局选项界面");
    }
  }

  private void LoadChosenProfiles()
  {
    Plugin.chosenProfileSet.Clear();
    foreach (string str in Plugin.ChosenProfiles.Value.Split(','))
    {
      int result;
      if (int.TryParse(str.Trim(), out result))
        Plugin.chosenProfileSet.Add(result);
    }
  }

  private void SaveChosenProfiles()
  {
    Plugin.ChosenProfiles.Value = string.Join<int>(",", (IEnumerable<int>) Plugin.chosenProfileSet);
    this.Config.Save();
  }

  private void Update()
  {
    if (!Input.GetKeyDown((KeyCode) 288) || this.currentProfileID == -1)
      return;
    this.showUI = !this.showUI;
    if (this.showUI)
    {
      this.allowUpward = Plugin.AllowUpwardAttack;
      this.allowLeft = Plugin.AllowLeftAttack;
      this.allowRight = Plugin.AllowRightAttack;
      this.skillCount = Plugin.StartingSkillCount.Value;
      this.itemCount = Plugin.StartingItemCount.Value;
      this.resetPickups = false;
      this.seedInput = Plugin.RandomSeed.Value.ToString();
    }
  }

  private void OnGUI()
  {
    if (this.showUI)
    {
      GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.7f);
      GUI.DrawTexture(new Rect(0.0f, 0.0f, (float) Screen.width, (float) Screen.height), (Texture) Texture2D.whiteTexture);
      GUI.color = Color.white;
      this.uiWindowRect = new Rect((float) (Screen.width / 2 - 250 - 280), (float) (Screen.height / 2 - 400), 500f, 750f);
      // ISSUE: method pointer
      this.uiWindowRect = GUILayout.Window(100, this.uiWindowRect, new GUI.WindowFunction((object) this, __methodptr(DrawUIWindow)), "开局选项", Array.Empty<GUILayoutOption>());
    }
    if (Plugin._notificationMessage != null && (double) Time.time <= (double) Plugin._notificationEndTime)
    {
      if (Plugin._notificationStyle == null)
      {
        Plugin._notificationStyle = new GUIStyle(GUI.skin.box);
        Plugin._notificationStyle.fontSize = 40;
        Plugin._notificationStyle.alignment = (TextAnchor) 4;
        Plugin._notificationStyle.normal.textColor = Color.white;
        Plugin._notificationStyle.normal.background = this.MakeTexture(2, 2, new Color(0.0f, 0.0f, 0.0f, 0.7f));
      }
      float num1 = 600f;
      float num2 = 120f;
      GUI.Box(new Rect((float) (((double) Screen.width - (double) num1) / 2.0), (float) (Screen.height / 2 - 100), num1, num2), Plugin._notificationMessage, Plugin._notificationStyle);
    }
    else
      Plugin._notificationMessage = (string) null;
  }

  private void DrawUIWindow(int windowID)
  {
    GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
    GUI.skin.label.fontSize = 20;
    GUI.skin.toggle.fontSize = 20;
    GUI.skin.button.fontSize = 24;
    GUI.skin.horizontalSlider.fontSize = 18;
    GUI.skin.textField.fontSize = 18;
    GUILayout.Label("当前存档开局设置：", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    });
    GUILayout.Space(15f);
    bool flag1 = Plugin.chosenProfileSet.Contains(this.currentProfileID);
    if (flag1)
    {
      GUILayout.Label("（此存档已设置过，只能查看）", new GUILayoutOption[1]
      {
        GUILayout.Height(35f)
      });
      GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f);
      if (GUILayout.Button("重置本存档设置", new GUILayoutOption[1]
      {
        GUILayout.Height(45f)
      }))
      {
        Plugin.chosenProfileSet.Remove(this.currentProfileID);
        this.SaveChosenProfiles();
        this.allowUpward = false;
        this.allowLeft = false;
        this.allowRight = false;
        this.skillCount = 0;
        this.itemCount = 0;
        this.resetPickups = false;
        return;
      }
      GUI.backgroundColor = Color.white;
    }
    GUILayout.Space(20f);
    GUILayout.Label("攻击方向选择：", new GUILayoutOption[1]
    {
      GUILayout.Height(35f)
    });
    GUILayout.Space(5f);
    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
    GUI.color = Color.gray;
    GUILayout.Label("下劈 (默认)", new GUILayoutOption[2]
    {
      GUILayout.Width(200f),
      GUILayout.Height(40f)
    });
    GUI.enabled = false;
    GUILayout.Toggle(true, "", new GUILayoutOption[2]
    {
      GUILayout.Width(40f),
      GUILayout.Height(40f)
    });
    GUI.enabled = !flag1;
    GUI.color = Color.white;
    GUILayout.EndHorizontal();
    GUI.color = this.allowUpward ? Color.green : Color.white;
    bool flag2 = GUILayout.Toggle((this.allowUpward ? 1 : 0) != 0, "上劈", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    });
    if (flag2 != this.allowUpward && !flag1)
      this.allowUpward = flag2;
    GUI.color = this.allowLeft ? Color.green : Color.white;
    bool flag3 = GUILayout.Toggle((this.allowLeft ? 1 : 0) != 0, "左劈", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    });
    if (flag3 != this.allowLeft && !flag1)
      this.allowLeft = flag3;
    GUI.color = this.allowRight ? Color.green : Color.white;
    bool flag4 = GUILayout.Toggle((this.allowRight ? 1 : 0) != 0, "右劈", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    });
    if (flag4 != this.allowRight && !flag1)
      this.allowRight = flag4;
    GUI.color = Color.white;
    GUILayout.Space(20f);
    GUILayout.Label("开局随机技能数量：", new GUILayoutOption[1]
    {
      GUILayout.Height(35f)
    });
    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
    GUILayout.Label(this.skillCount.ToString(), new GUILayoutOption[2]
    {
      GUILayout.Width(35f),
      GUILayout.Height(40f)
    });
    int num1 = (int) GUILayout.HorizontalSlider((float) this.skillCount, 0.0f, 5f, new GUILayoutOption[1]
    {
      GUILayout.Width(220f)
    });
    if (num1 != this.skillCount && !flag1)
      this.skillCount = num1;
    GUILayout.EndHorizontal();
    GUILayout.Space(15f);
    GUILayout.Label("开局随机物品数量：", new GUILayoutOption[1]
    {
      GUILayout.Height(35f)
    });
    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
    GUILayout.Label(this.itemCount.ToString(), new GUILayoutOption[2]
    {
      GUILayout.Width(35f),
      GUILayout.Height(40f)
    });
    int num2 = (int) GUILayout.HorizontalSlider((float) this.itemCount, 0.0f, 5f, new GUILayoutOption[1]
    {
      GUILayout.Width(220f)
    });
    if (num2 != this.itemCount && !flag1)
      this.itemCount = num2;
    GUILayout.EndHorizontal();
    GUILayout.Space(25f);
    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
    GUILayout.Label("种子", new GUILayoutOption[2]
    {
      GUILayout.Width(60f),
      GUILayout.Height(40f)
    });
    this.seedInput = GUILayout.TextField(this.seedInput, new GUILayoutOption[2]
    {
      GUILayout.Width(160f),
      GUILayout.Height(40f)
    });
    GUILayout.EndHorizontal();
    GUILayout.Space(5f);
    GUI.color = this.resetPickups ? Color.red : Color.white;
    bool flag5 = GUILayout.Toggle((this.resetPickups ? 1 : 0) != 0, "重置种子世界（含技能触发器）", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    });
    if (flag5 != this.resetPickups && !flag1)
      this.resetPickups = flag5;
    GUI.color = Color.white;
    if (this.resetPickups)
    {
      GUI.color = Color.yellow;
      GUILayout.Label("警告：重置后当前种子世界将重新生成，所有拾取点会重生，技能触发器也会重置。", new GUILayoutOption[1]
      {
        GUILayout.Height(70f)
      });
      GUI.color = Color.white;
    }
    GUILayout.Space(35f);
    GUI.enabled = !flag1;
    GUI.backgroundColor = Color.green;
    if (GUILayout.Button("确认", new GUILayoutOption[1]
    {
      GUILayout.Height(50f)
    }) && !flag1)
    {
      Plugin.AllowUpwardAttack = this.allowUpward;
      Plugin.AllowLeftAttack = this.allowLeft;
      Plugin.AllowRightAttack = this.allowRight;
      try
      {
        PlayerData instance = PlayerData.instance;
        if (instance != null)
        {
          instance.SetBool("AllowUpwardAttack", this.allowUpward);
          instance.SetBool("AllowLeftAttack", this.allowLeft);
          instance.SetBool("AllowRightAttack", this.allowRight);
          Plugin.Log.LogInfo((object) $"劈方向已保存到存档: 上={this.allowUpward}, 左={this.allowLeft}, 右={this.allowRight}");
        }
      }
      catch (Exception ex)
      {
        Plugin.Log.LogError((object) $"保存劈方向失败: {ex}");
      }
      Plugin.StartingSkillCount.Value = this.skillCount;
      Plugin.StartingItemCount.Value = this.itemCount;
      this.Config.Save();
      Plugin.chosenProfileSet.Add(this.currentProfileID);
      this.SaveChosenProfiles();
      if (this.resetPickups)
        this.ResetSeedWorld();
      for (int index = 0; index < this.skillCount; ++index)
        SkillRandomizer.GiveRandomSkill();
      for (int index = 0; index < this.itemCount; ++index)
      {
        SavedItem randomItem = ItemRandomizer.GetRandomItem();
        if (Object.op_Inequality((Object) randomItem, (Object) null))
          randomItem.TryGet(false, true);
      }
      this.showUI = false;
      Plugin.Log.LogInfo((object) $"开局设置已保存: 上劈={this.allowUpward}, 左劈={this.allowLeft}, 右劈={this.allowRight}, 技能={this.skillCount}, 物品={this.itemCount}, 重置种子={this.resetPickups}");
    }
    GUI.backgroundColor = Color.white;
    GUI.enabled = true;
    GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
    if (GUILayout.Button("关闭", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    }))
      this.showUI = false;
    GUI.backgroundColor = Color.white;
    GUILayout.Space(15f);
    int fontSize = GUI.skin.label.fontSize;
    GUI.skin.label.fontSize = 26;
    GUI.color = Color.yellow;
    GUILayout.Label("提示: 按 F7 呼出此窗口", new GUILayoutOption[1]
    {
      GUILayout.Height(40f)
    });
    GUI.color = Color.white;
    GUI.skin.label.fontSize = fontSize;
    GUILayout.EndVertical();
    GUI.DragWindow();
  }

  private void ResetSeedWorld()
  {
    int result;
    int num1;
    if (int.TryParse(this.seedInput, out result))
    {
      if (result != Plugin.RandomSeed.Value)
      {
        num1 = result;
      }
      else
      {
        num1 = new Random().Next(1, int.MaxValue);
        Plugin.Log.LogInfo((object) $"输入种子与原种子相同，自动生成新种子: {num1}");
      }
    }
    else
    {
      num1 = new Random().Next(1, int.MaxValue);
      Plugin.Log.LogInfo((object) $"输入无效，自动生成新种子: {num1}");
    }
    this.seedInput = num1.ToString();
    Plugin.RandomSeed.Value = num1;
    this.Config.Save();
    try
    {
      Type type1 = Type.GetType("SilksongItemRandomizer.Plugin, SilksongItemRandomizer");
      if (type1 != (Type) null)
      {
        PropertyInfo property = type1.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Static);
        if (property != (PropertyInfo) null)
          (property.GetValue((object) null) as ConfigEntry<int>).Value = num1;
        Type type2 = Type.GetType("SilksongItemRandomizer.ItemRandomizer, SilksongItemRandomizer");
        if (type2 != (Type) null)
        {
          type2.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, new object[1]
          {
            (object) num1
          });
          Plugin.Log.LogInfo((object) $"物品随机MOD重新初始化，种子 {num1}");
        }
        type1.GetMethod("ResetAllStaticData", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, (object[]) null);
        Plugin.Log.LogInfo((object) "已重置物品随机MOD所有静态数据");
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"重置物品随机MOD失败: {ex}");
    }
    try
    {
      Type type3 = Type.GetType("SkillTriggerMod.Plugin, SkillTriggerMod");
      if (type3 != (Type) null)
      {
        PropertyInfo property = type3.GetProperty("RandomSeed", BindingFlags.Public | BindingFlags.Static);
        if (property != (PropertyInfo) null)
          (property.GetValue((object) null) as ConfigEntry<int>).Value = num1;
        Type type4 = Type.GetType("SkillTriggerMod.SkillRandomizer, SkillTriggerMod");
        if (type4 != (Type) null)
        {
          type4.GetMethod("SetSeed", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, new object[1]
          {
            (object) num1
          });
          Plugin.Log.LogInfo((object) $"技能随机MOD重新初始化，种子 {num1}");
        }
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"更新技能随机MOD失败: {ex}");
    }
    try
    {
      PlayerData instance = PlayerData.instance;
      if (instance != null)
      {
        FieldInfo[] fields = typeof (PlayerData).GetFields(BindingFlags.Instance | BindingFlags.Public);
        int num2 = 0;
        foreach (FieldInfo fieldInfo in fields)
        {
          if (fieldInfo.Name.StartsWith("SkillTriggered_"))
          {
            fieldInfo.SetValue((object) instance, (object) false);
            ++num2;
          }
        }
        Plugin.Log.LogInfo((object) $"已重置 {num2} 个技能触发器记录");
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"重置技能触发器失败: {ex}");
    }
    try
    {
      Type type5 = Type.GetType("SilksongItemRandomizer.CrestRandomizer, SilksongItemRandomizer");
      if (type5 != (Type) null)
      {
        type5.GetMethod("ResetMappings", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, (object[]) null);
        Plugin.Log.LogInfo((object) "已重置纹章映射");
      }
      if (type5 != (Type) null)
      {
        type5.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, (object[]) null);
        Plugin.Log.LogInfo((object) "已重新生成纹章映射");
      }
      Type type6 = Type.GetType("SilksongItemRandomizer.CrestRandomizePatch, SilksongItemRandomizer");
      if (type6 != (Type) null)
      {
        type6.GetMethod("ResetProcessedIds", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, (object[]) null);
        Plugin.Log.LogInfo((object) "已清空纹章解锁记录");
      }
      Type type7 = Type.GetType("SilksongItemRandomizer.BenchRespawnPatch, SilksongItemRandomizer");
      if (type7 != (Type) null)
      {
        type7.GetMethod("ResetCooldown", BindingFlags.Public | BindingFlags.Static)?.Invoke((object) null, (object[]) null);
        Plugin.Log.LogInfo((object) "已清空纹章刷新冷却");
      }
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"清空纹章相关标记失败: {ex}");
    }
    Plugin.Log.LogInfo((object) "请重载当前场景以使所有随机世界重置。");
  }
}
