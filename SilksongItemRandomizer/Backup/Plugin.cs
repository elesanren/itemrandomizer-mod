// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.Plugin
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

#nullable enable
namespace SilksongItemRandomizer;

[BepInPlugin("YourName.SilksongItemRandomizer", "Silksong Item Randomizer", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
  internal static ManualLogSource Log;
  private static HashSet<string> _destroyedPickupKeys = new HashSet<string>();
  private static string _notificationMessage = (string) null;
  private static float _notificationEndTime = 0.0f;
  private static GUIStyle _notificationStyle;

  public static ConfigEntry<int> RandomSeed { get; private set; }

  public static Plugin Instance { get; private set; }

  private static string DestroyedPickupsFilePath
  {
    get
    {
      string str = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer");
      if (!Directory.Exists(str))
        Directory.CreateDirectory(str);
      return Path.Combine(str, "destroyed_pickups.json");
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
    Plugin.Instance = this;
    Plugin.Log = this.Logger;
    Plugin.RandomSeed = this.Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
    Harmony.CreateAndPatchAll(typeof (PickupPatch), (string) null);
    Harmony.CreateAndPatchAll(typeof (CurrencyCollectPatch), (string) null);
    Harmony.CreateAndPatchAll(typeof (TryGetPatch), (string) null);
    Harmony.CreateAndPatchAll(typeof (ToolUnlockPatch), (string) null);
    Harmony.CreateAndPatchAll(typeof (CrestRandomizePatch), (string) null);
    Harmony.CreateAndPatchAll(typeof (ShopMenuStock_BuildItemList_Patch), (string) null);
    Harmony.CreateAndPatchAll(typeof (ShopItemStats_Purchase_Patch), (string) null);
    Harmony.CreateAndPatchAll(typeof (SilkSpearPityPatch), (string) null);
    Harmony.CreateAndPatchAll(typeof (BenchRespawnPatch), (string) null);
    ((MonoBehaviour) this).StartCoroutine(this.InitializeAfterLoad(Plugin.RandomSeed.Value));
    // ISSUE: method pointer
    SceneManager.sceneLoaded += new UnityAction<Scene, LoadSceneMode>((object) this, __methodptr(OnSceneLoaded));
    this.LoadDestroyedKeys();
    Plugin.Log.LogInfo((object) "Plugin SilksongItemRandomizer loaded (seed-based)");
  }

  private void LoadDestroyedKeys()
  {
    string destroyedPickupsFilePath = Plugin.DestroyedPickupsFilePath;
    if (File.Exists(destroyedPickupsFilePath))
    {
      try
      {
        List<string> collection = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(destroyedPickupsFilePath));
        Plugin._destroyedPickupKeys = collection != null ? new HashSet<string>((IEnumerable<string>) collection) : new HashSet<string>();
      }
      catch (Exception ex)
      {
        Plugin.Log.LogError((object) $"加载特殊点消失记录失败: {ex}");
        Plugin._destroyedPickupKeys = new HashSet<string>();
      }
    }
    else
      Plugin._destroyedPickupKeys = new HashSet<string>();
  }

  private void SaveDestroyedKeys()
  {
    string destroyedPickupsFilePath = Plugin.DestroyedPickupsFilePath;
    try
    {
      string contents = JsonConvert.SerializeObject((object) Plugin._destroyedPickupKeys.ToList<string>(), Formatting.Indented);
      File.WriteAllText(destroyedPickupsFilePath, contents);
    }
    catch (Exception ex)
    {
      Plugin.Log.LogError((object) $"保存特殊点消失记录失败: {ex}");
    }
  }

  public static void AddDestroyedPickupKey(string key)
  {
    Plugin._destroyedPickupKeys.Add(key);
    Plugin.Instance?.SaveDestroyedKeys();
  }

  public static void ResetDestroyedPickupKeys()
  {
    Plugin._destroyedPickupKeys.Clear();
    string destroyedPickupsFilePath = Plugin.DestroyedPickupsFilePath;
    if (!File.Exists(destroyedPickupsFilePath))
      return;
    File.Delete(destroyedPickupsFilePath);
  }

  public static void ResetAllStaticData()
  {
    CrestRandomizer.ResetMappings();
    CrestRandomizePatch.ResetProcessedIds();
    CurrencyCollectPatch.ResetCounters();
    Plugin.ResetDestroyedPickupKeys();
    ShopRandomizer.ResetCache();
    ShopMenuStock_BuildItemList_Patch.ResetAllCounts();
    Plugin.Log.LogInfo((object) "物品随机MOD所有静态数据已重置");
  }

  private void OnDestroy()
  {
    // ISSUE: method pointer
    SceneManager.sceneLoaded -= new UnityAction<Scene, LoadSceneMode>((object) this, __methodptr(OnSceneLoaded));
  }

  private IEnumerator InitializeAfterLoad(int seed)
  {
    yield return (object) new WaitForSeconds(3f);
    ItemRandomizer.Initialize(seed);
    ToolUnlockPatch.Initialize();
    CrestRandomizer.Initialize();
    Plugin.Log.LogInfo((object) $"Randomizer initialized with seed: {seed}");
  }

  private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
  {
    ((MonoBehaviour) this).StartCoroutine(this.DestroyMarkedPickups(scene));
  }

  private IEnumerator DestroyMarkedPickups(Scene scene)
  {
    yield return (object) null;
    CollectableItemPickup[] collectableItemPickupArray = Resources.FindObjectsOfTypeAll<CollectableItemPickup>();
    for (int index = 0; index < collectableItemPickupArray.Length; ++index)
    {
      CollectableItemPickup p = collectableItemPickupArray[index];
      if (!Scene.op_Inequality(((Component) p).gameObject.scene, scene))
      {
        string key = $"{((Scene) ref scene).name}_{((Component) p).transform.position.x:F2}_{((Component) p).transform.position.y:F2}_{((Component) p).transform.position.z:F2}";
        if (Plugin._destroyedPickupKeys.Contains(key))
        {
          Object.Destroy((Object) ((Component) p).gameObject);
          Plugin.Log.LogInfo((object) ("场景加载时销毁已标记点: " + key));
        }
        key = (string) null;
        p = (CollectableItemPickup) null;
      }
    }
    collectableItemPickupArray = (CollectableItemPickup[]) null;
  }

  private void Update()
  {
    RecentItemsUI.UpdateAutoHide();
    if (Input.GetKeyDown((KeyCode) 286))
    {
      RecentItemsUI.Toggle();
      Plugin.Log.LogInfo((object) ("最近获得物品UI " + (RecentItemsUI.IsVisible ? "显示" : "隐藏")));
    }
    if (!Input.GetKeyDown((KeyCode) 289))
      return;
    this.DumpAllMappings();
  }

  private void OnGUI()
  {
    RecentItemsUI.Draw();
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

  private void DumpAllMappings()
  {
    int num = Plugin.RandomSeed.Value;
    Plugin.Log.LogInfo((object) $"===== 当前种子: {num} =====");
    List<ToolCrest> crestList = CrestRandomizer.CrestList;
    if (crestList != null && crestList.Count > 0)
    {
      Plugin.Log.LogInfo((object) "--- 纹章映射 (来自外部存储) ---");
      foreach (ToolCrest toolCrest in crestList)
      {
        string name = toolCrest.name;
        string mappedCrestName = CrestRandomizer.GetMappedCrestName(name);
        Plugin.Log.LogInfo((object) $"  {name} -> {mappedCrestName}");
      }
    }
    else
      Plugin.Log.LogInfo((object) "--- 未找到纹章 ---");
    Plugin.Log.LogInfo((object) "--- 当前场景拾取点映射 ---");
    List<CollectableItemPickup> list = ((IEnumerable<CollectableItemPickup>) Resources.FindObjectsOfTypeAll<CollectableItemPickup>()).Where<CollectableItemPickup>((Func<CollectableItemPickup, bool>) (p =>
    {
      Scene scene1 = ((Component) p).gameObject.scene;
      Scene scene2 = ((Component) p).gameObject.scene;
      return ((Scene) ref scene2).isLoaded;
    })).ToList<CollectableItemPickup>();
    if (list.Count == 0)
    {
      Plugin.Log.LogInfo((object) "当前场景无拾取点。");
    }
    else
    {
      foreach (CollectableItemPickup collectableItemPickup in list)
      {
        SavedItem savedItem1 = collectableItemPickup.Item;
        if (!Object.op_Equality((Object) savedItem1, (Object) null))
        {
          SavedItem savedItem2 = ItemRandomizer.PeekRandomItem(new Random(num + ((Object) collectableItemPickup).GetInstanceID()));
          if (Object.op_Inequality((Object) savedItem2, (Object) null))
            Plugin.Log.LogInfo((object) $"  {((Object) savedItem1).name} (位置 {((Component) collectableItemPickup).transform.position}) -> {((Object) savedItem2).name}");
          else
            Plugin.Log.LogInfo((object) $"  {((Object) savedItem1).name} -> 随机失败");
        }
      }
    }
    Plugin.Log.LogInfo((object) "===============================");
  }
}
