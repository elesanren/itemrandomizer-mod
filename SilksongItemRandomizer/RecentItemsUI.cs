using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer;

public static class RecentItemsUI
{
    private static readonly Queue<SavedItem> RecentItems = new();
    private const int MaxItems = 5;
    private static Rect _windowRect = new(20f, 300f, 700f, 600f);
    private static bool _showWindow;
    private static float _hideTime;

    public static bool IsVisible => _showWindow;

    public static void AddItem(SavedItem item)
    {
        if (item == null) return;
        if (RecentItems.Any(i => i.name == item.name)) return;

        RecentItems.Enqueue(item);
        while (RecentItems.Count > MaxItems)
            RecentItems.Dequeue();

        _showWindow = true;
        _hideTime = Time.time + 10f;
    }

    public static void Toggle()
    {
        _showWindow = !_showWindow;
        if (_showWindow) _hideTime = float.MaxValue;
    }

    public static void UpdateAutoHide()
    {
        if (_showWindow && Time.time >= _hideTime)
            _showWindow = false;
    }

    public static void Draw()
    {
        if (!_showWindow) return;
        _windowRect = GUILayout.Window(999, _windowRect, DrawWindow, "最近获得物品");
    }

    private static void DrawWindow(int id)
    {
        var originalFontSize = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 32;

        GUILayout.BeginVertical();
        foreach (var item in RecentItems)
        {
            GUILayout.BeginHorizontal();

            Sprite sprite = null;
            try { sprite = item?.GetPopupIcon(); } catch { }

            var iconWidth = 96f;
            var iconHeight = 96f;
            var texCoords = new Rect(0, 0, 1, 1);

            if (sprite != null && sprite.texture != null)
            {
                var texRect = sprite.textureRect;
                var texW = sprite.texture.width;
                var texH = sprite.texture.height;
                texCoords = new Rect(texRect.x / texW, texRect.y / texH, texRect.width / texW, texRect.height / texH);
                if (texRect.width > 0 && texRect.height > 0)
                {
                    iconHeight = iconWidth * (texRect.height / texRect.width);
                    if (iconHeight > 180f) iconHeight = 180f;
                }
            }

            var iconRect = GUILayoutUtility.GetRect(iconWidth, iconHeight, GUILayout.Width(iconWidth), GUILayout.Height(iconHeight));

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

            var displayName = GetDisplayName(item);
            GUILayout.Label("• " + displayName, GUILayout.Height(iconHeight));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        GUI.DragWindow();
        GUI.skin.label.fontSize = originalFontSize;
    }

    private static string GetDisplayName(SavedItem item)
    {
        if (item == null) return "未知物品";
        try
        {
            var method = item.GetType().GetMethod("GetCollectionName", BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
                return method.Invoke(item, null) as string ?? item.name;
            var popupName = item.GetPopupName();
            return string.IsNullOrEmpty(popupName) ? item.name : popupName;
        }
        catch
        {
            return item.name ?? "未知物品";
        }
    }
}