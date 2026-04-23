// Decompiled with JetBrains decompiler
// Type: SilksongItemRandomizer.RecentItemsUI
// Assembly: SilksongItemRandomizer, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B70CDFC5-FCC7-47DC-924E-17AA884F2058
// Assembly location: E:\a\HardItemRandomizer\plugins\SilksongItemRandomizer.dll

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#nullable enable
namespace SilksongItemRandomizer;

public static class RecentItemsUI
{
  private static Queue<SavedItem> recentItems = new Queue<SavedItem>();
  private const int MaxItems = 5;
  private static Rect windowRect = new Rect(20f, 20f, 500f, 450f);
  private static bool _showWindow = false;
  private static float hideTime = 0.0f;

  public static bool IsVisible => RecentItemsUI._showWindow;

  public static void AddItem(SavedItem item)
  {
    if (Object.op_Equality((Object) item, (Object) null))
      return;
    RecentItemsUI.recentItems.Enqueue(item);
    while (RecentItemsUI.recentItems.Count > 5)
      RecentItemsUI.recentItems.Dequeue();
    RecentItemsUI._showWindow = true;
    RecentItemsUI.hideTime = Time.time + 5f;
  }

  public static void Toggle()
  {
    RecentItemsUI._showWindow = !RecentItemsUI._showWindow;
    if (!RecentItemsUI._showWindow)
      return;
    RecentItemsUI.hideTime = float.MaxValue;
  }

  public static void UpdateAutoHide()
  {
    if (!RecentItemsUI._showWindow || (double) Time.time < (double) RecentItemsUI.hideTime)
      return;
    RecentItemsUI._showWindow = false;
  }

  public static void Draw()
  {
    if (!RecentItemsUI._showWindow)
      return;
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    // ISSUE: method pointer
    RecentItemsUI.windowRect = GUILayout.Window(999, RecentItemsUI.windowRect, RecentItemsUI.\u003C\u003EO.\u003C0\u003E__DoWindow ?? (RecentItemsUI.\u003C\u003EO.\u003C0\u003E__DoWindow = new GUI.WindowFunction((object) null, __methodptr(DoWindow))), "最近获得物品", Array.Empty<GUILayoutOption>());
  }

  private static void DoWindow(int id)
  {
    GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
    foreach (SavedItem savedItem in RecentItemsUI.recentItems.ToArray())
    {
      GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
      Sprite sprite = (Sprite) null;
      try
      {
        sprite = savedItem?.GetPopupIcon();
      }
      catch
      {
      }
      float num1 = 64f;
      float num2 = 64f;
      Rect rect1;
      // ISSUE: explicit constructor call
      ((Rect) ref rect1).\u002Ector(0.0f, 0.0f, 1f, 1f);
      if (Object.op_Inequality((Object) sprite, (Object) null) && Object.op_Inequality((Object) sprite.texture, (Object) null))
      {
        Rect textureRect = sprite.textureRect;
        float width = (float) ((Texture) sprite.texture).width;
        float height = (float) ((Texture) sprite.texture).height;
        // ISSUE: explicit constructor call
        ((Rect) ref rect1).\u002Ector(((Rect) ref textureRect).x / width, ((Rect) ref textureRect).y / height, ((Rect) ref textureRect).width / width, ((Rect) ref textureRect).height / height);
        if ((double) ((Rect) ref textureRect).width > 0.0 && (double) ((Rect) ref textureRect).height > 0.0)
        {
          num2 = num1 * (((Rect) ref textureRect).height / ((Rect) ref textureRect).width);
          if ((double) num2 > 120.0)
            num2 = 120f;
        }
      }
      Rect rect2 = GUILayoutUtility.GetRect(num1, num2, new GUILayoutOption[2]
      {
        GUILayout.Width(num1),
        GUILayout.Height(num2)
      });
      if (Object.op_Inequality((Object) sprite, (Object) null) && Object.op_Inequality((Object) sprite.texture, (Object) null))
      {
        GUI.DrawTextureWithTexCoords(rect2, (Texture) sprite.texture, rect1);
      }
      else
      {
        GUI.Box(rect2, "");
        GUI.Label(rect2, "?", new GUIStyle(GUI.skin.label)
        {
          alignment = (TextAnchor) 4,
          fontSize = 24
        });
      }
      string str;
      try
      {
        MethodInfo method = savedItem?.GetType().GetMethod("GetCollectionName", BindingFlags.Instance | BindingFlags.Public);
        str = !(method != (MethodInfo) null) ? savedItem?.GetPopupName() : method.Invoke((object) savedItem, (object[]) null) as string;
        if (string.IsNullOrEmpty(str))
          str = ((Object) savedItem)?.name ?? "未知物品";
      }
      catch
      {
        str = ((Object) savedItem)?.name ?? "未知物品";
      }
      GUILayout.Label("• " + str, new GUILayoutOption[1]
      {
        GUILayout.Height(num2)
      });
      GUILayout.EndHorizontal();
    }
    GUILayout.EndVertical();
    GUI.DragWindow();
  }
}
