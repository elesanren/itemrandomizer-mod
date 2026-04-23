using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

public static class RecentItemsUI
{
    private static Queue<SavedItem> recentItems = new Queue<SavedItem>();
    private const int MaxItems = 5;
    private static Rect windowRect = new Rect(20f, 20f, 500f, 450f);
    private static bool _showWindow = false;
    private static float hideTime = 0.0f;

    public static bool IsVisible => _showWindow;

    public static void AddItem(SavedItem item)
    {
        if (item == null) return;
        recentItems.Enqueue(item);
        while (recentItems.Count > MaxItems)
            recentItems.Dequeue();
        _showWindow = true;
        hideTime = Time.time + 5f;
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
        GUILayout.BeginVertical();

        foreach (SavedItem item in recentItems.Reverse())
        {
            GUILayout.BeginHorizontal();

            // 获取物品图标
            Sprite sprite = null;
            try { sprite = item?.GetPopupIcon(); } catch { }

            float iconWidth = 64f;
            float iconHeight = 64f;
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
                    if (iconHeight > 120f) iconHeight = 120f;
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
                    fontSize = 24
                });
            }

            // 获取物品名称
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
    }
}