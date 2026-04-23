// Decompiled with JetBrains decompiler
// Type: SkillTriggerMod.Plugin
// Assembly: SkillRandomizerMod, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: 31ECD94A-A255-405A-B0F7-6544B29C2F91
// Assembly location: E:\a\HardItemRandomizer\plugins\SkillRandomizerMod.dll

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

#nullable enable
namespace SkillTriggerMod;

[BepInPlugin("YourName.SkillTriggerMod", "Skill Trigger Mod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
  internal static ManualLogSource Log;
  private static string _notificationMessage;
  private static float _notificationEndTime;
  private static GUIStyle _notificationStyle;
  private readonly List<(string scene, float x, float y, float z)> targetPositions = new List<(string, float, float, float)>()
  {
    ("Mosstown_02", 86.922f, 52.568f, 0.004f),
    ("Crawl_05", 23.032f, 16.568f, 0.004f),
    ("Shellwood_10", 40.643f, 79.57f, 0.004f),
    ("Greymoor_22", 39.783f, 36.826f, 0.004f),
    ("Bone_East_05", 100.062f, 13.568f, 0.004f)
  };
  private readonly string[] shrineKeywords = new string[5]
  {
    "bind orb",
    "shrine weaver ability",
    "weaver_shrine",
    "bellshrine",
    "dash shrine"
  };

  public static ConfigEntry<int> RandomSeed { get; private set; }

  public static void ShowNotification(string message, float duration = 3f)
  {
    Plugin._notificationMessage = message;
    Plugin._notificationEndTime = Time.time + duration;
  }

  private void OnGUI()
  {
    if (Plugin._notificationMessage == null || (double) Time.time > (double) Plugin._notificationEndTime)
    {
      Plugin._notificationMessage = (string) null;
    }
    else
    {
      if (Plugin._notificationStyle == null)
      {
        Plugin._notificationStyle = new GUIStyle(GUI.skin.box);
        Plugin._notificationStyle.fontSize = 40;
        Plugin._notificationStyle.alignment = (TextAnchor) 4;
        Plugin._notificationStyle.normal.textColor = Color.white;
        Plugin._notificationStyle.normal.background = this.MakeTexture(2, 2, new Color(0.0f, 0.0f, 0.0f, 0.8f));
        Plugin._notificationStyle.border = new RectOffset(20, 20, 10, 10);
      }
      float num1 = 600f;
      float num2 = 120f;
      GUI.Box(new Rect((float) (((double) Screen.width - (double) num1) / 2.0), (float) (Screen.height / 2 - 100), num1, num2), Plugin._notificationMessage, Plugin._notificationStyle);
    }
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
    Plugin.RandomSeed = this.Config.Bind<int>("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
    Plugin.Log.LogInfo((object) "SkillTriggerMod 加载成功！");
    // ISSUE: method pointer
    SceneManager.sceneLoaded += new UnityAction<Scene, LoadSceneMode>((object) this, __methodptr(OnSceneLoaded));
    ((MonoBehaviour) this).StartCoroutine(this.DisableShrinesAfterStart());
    ((MonoBehaviour) this).StartCoroutine(this.InitializeRandomizer());
  }

  private IEnumerator InitializeRandomizer()
  {
    yield return (object) new WaitForSeconds(3f);
    SkillRandomizer.SetSeed(Plugin.RandomSeed.Value);
  }

  private void OnDestroy()
  {
    // ISSUE: method pointer
    SceneManager.sceneLoaded -= new UnityAction<Scene, LoadSceneMode>((object) this, __methodptr(OnSceneLoaded));
  }

  private IEnumerator DisableShrinesAfterStart()
  {
    yield return (object) new WaitForSeconds(5f);
    for (int i = 0; i < SceneManager.sceneCount; ++i)
    {
      Scene scene = SceneManager.GetSceneAt(i);
      if (((Scene) ref scene).isLoaded)
        this.DisableShrinesInScene(scene);
    }
  }

  private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
  {
    this.DisableShrinesInScene(scene);
    foreach ((string scene, float x, float y, float z) targetPosition in this.targetPositions)
    {
      if (targetPosition.scene == ((Scene) ref scene).name)
      {
        ((MonoBehaviour) this).StartCoroutine(this.CreateTriggerDelayed(targetPosition));
        break;
      }
    }
  }

  private void DisableShrinesInScene(Scene scene)
  {
    foreach (GameObject rootGameObject in ((Scene) ref scene).GetRootGameObjects())
    {
      foreach (Component componentsInChild in rootGameObject.GetComponentsInChildren<Transform>(true))
      {
        GameObject gameObject = componentsInChild.gameObject;
        string lower = ((Object) gameObject).name.ToLower();
        foreach (string shrineKeyword in this.shrineKeywords)
        {
          if (lower.Contains(shrineKeyword))
          {
            foreach (Behaviour component in gameObject.GetComponents<Collider2D>())
              component.enabled = false;
            foreach (Collider component in gameObject.GetComponents<Collider>())
              component.enabled = false;
            foreach (Behaviour component in gameObject.GetComponents<PlayMakerFSM>())
              component.enabled = false;
            break;
          }
        }
      }
    }
  }

  private IEnumerator CreateTriggerDelayed((string scene, float x, float y, float z) target)
  {
    yield return (object) null;
    Vector3 pos = new Vector3(target.x, target.y, target.z);
    this.CreateTriggerAt(pos, target.scene);
  }

  private void CreateTriggerAt(Vector3 position, string sceneName)
  {
    GameObject gameObject = new GameObject("SkillTrigger_" + sceneName);
    gameObject.transform.position = position;
    BoxCollider2D boxCollider2D = gameObject.AddComponent<BoxCollider2D>();
    ((Collider2D) boxCollider2D).isTrigger = true;
    boxCollider2D.size = new Vector2(8f, 8f);
    gameObject.AddComponent<SkillTrigger>().SetSceneName(sceneName);
    Scene sceneByName = SceneManager.GetSceneByName(sceneName);
    if (((Scene) ref sceneByName).IsValid())
    {
      SceneManager.MoveGameObjectToScene(gameObject, sceneByName);
      Plugin.Log.LogInfo((object) ("触发器创建: " + sceneName));
    }
    else
    {
      Plugin.Log.LogError((object) ("触发器创建失败: " + sceneName));
      Object.Destroy((Object) gameObject);
    }
  }
}
