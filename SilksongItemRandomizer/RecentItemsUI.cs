using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

public static class RecentItemsUI
{
    private static Queue<SavedItem> recentItems = new();
    private const int MaxItems = 5;
    private static Rect windowRect = new Rect(20f, 300f, 700f, 600f);
    private static bool _showWindow;
    private static float hideTime;

    public static bool IsVisible => _showWindow;

    public static void AddItem(SavedItem item)
    {
        if (item == null) return;

        if (recentItems.Any(i => i.name == item.name))
            return;

        recentItems.Enqueue(item);
        while (recentItems.Count > MaxItems)
            recentItems.Dequeue();
        _showWindow = true;
        hideTime = Time.time + 10f;
    }

    public static void Toggle()
    {
        _showWindow = !_showWindow;
        if (_showWindow)
            hideTime = float.MaxValue;
    }

    public static void UpdateAutoHide()
    {
        if (_showWindow && Time.time >= hideTime)
            _showWindow = false;
    }

    public static void Draw()
    {
        if (!_showWindow) return;
        windowRect = GUILayout.Window(999, windowRect, DoWindow, "最近获得物品");
    }

    private static void DoWindow(int id)
    {
        int originalFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 32;

        GUILayout.BeginVertical();
        foreach (SavedItem item in recentItems.Reverse())
        {
            GUILayout.BeginHorizontal();

            Sprite sprite = null;
            try { sprite = item?.GetPopupIcon(); } catch { }

            float iconWidth = 96f;
            float iconHeight = 96f;
            Rect texCoords = new Rect(0, 0, 1, 1);

            if (sprite != null && sprite.texture != null)
            {
                Rect texRect = sprite.textureRect;
                float texW = sprite.texture.width;
                float texH = sprite.texture.height;
                texCoords = new Rect(texRect.x / texW, texRect.y / texH, texRect.width / texW, texRect.height / texH);
                if (texRect.width > 0 && texRect.height > 0)
                {
                    iconHeight = iconWidth * (texRect.height / texRect.width);
                    if (iconHeight > 180f) iconHeight = 180f;
                }
            }

            Rect iconRect = GUILayoutUtility.GetRect(iconWidth, iconHeight, GUILayout.Width(iconWidth), GUILayout.Height(iconHeight));

            if (sprite != null && sprite.texture != null)
            {
                GUI.DrawTextureWithTexCoords(iconRect, sprite.texture, texCoords);
            }
            else
            {
                GUI.Box(iconRect, "");
                GUI.Label(iconRect, "?", new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 36
                });
            }

            string displayName = "未知物品";
            try
            {
                MethodInfo method = item?.GetType().GetMethod("GetCollectionName", BindingFlags.Instance | BindingFlags.Public);
                if (method != null)
                    displayName = method.Invoke(item, null) as string;
                else
                    displayName = item?.GetPopupName();

                if (string.IsNullOrEmpty(displayName))
                    displayName = item?.name ?? "未知物品";
            }
            catch
            {
                displayName = item?.name ?? "未知物品";
            }

            GUILayout.Label("• " + displayName, GUILayout.Height(iconHeight));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        GUI.DragWindow();
        GUI.skin.label.fontSize = originalFontSize;
    }
}