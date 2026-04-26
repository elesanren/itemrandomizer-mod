using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HKSilksong_Randomizer;

public static class RoomMapGenerator
{
    private static readonly bool[,] letters = new bool[128, 25];

    static RoomMapGenerator()
    {
        string[] lettersAtoZ = new string[26]
        {
            "01110100011000111111000110001",
            "11110100011111010001100011111",
            "01110100011000010000100000111",
            "11110100011000110001100011110",
            "11111100001111010000100011111",
            "11111100001111010000100010000",
            "01110100011000010111100001111",
            "10001100011111110001100011000",
            "01110001000010000100001000111",
            "00111000100001000100011001100",
            "10001100101110010010100011000",
            "10000100001000010000100001111",
            "10001110111010110001100011000",
            "10001110011010110011100011000",
            "01110100011000110001100010111",
            "11110100011111010000100010000",
            "01110100011000110001101010101",
            "11110100011111010010100011001",
            "01111100000111000001100011110",
            "11111001000010000100001000010",
            "10001100011000110001100001110",
            "10001100011000101010001000100",
            "10001100011000110101101011010",
            "10001010100010001010100010001",
            "10001010100010000100001000010",
            "11111000100010001000100011111"
        };
        for (int i = 0; i < 26; i++)
        {
            string code = lettersAtoZ[i];
            for (int j = 0; j < 25; j++)
                letters[65 + i, j] = code[j] == '1';
        }

        string[] digits = new string[10]
        {
            "01110100011000110001100010111",
            "00100011000010000100001000111",
            "01110100010001000100010001111",
            "11110000111100000011100011110",
            "10001100111110000010000100010",
            "11111100001111000001100011111",
            "01110100001111010001100010111",
            "11111000010001000100001000010",
            "01110100011000101110100010111",
            "01110100011000101111000011110"
        };
        for (int i = 0; i < 10; i++)
        {
            string code = digits[i];
            for (int j = 0; j < 25; j++)
                letters[48 + i, j] = code[j] == '1';
        }

        string underscore = "00000000000000000000011111";
        for (int i = 0; i < 25; i++)
            letters[95, i] = underscore[i] == '1';
    }

    public static bool GenerateFromFile(string connectionsFilePath, string outputImagePath)
    {
        if (!File.Exists(connectionsFilePath))
        {
            Debug.LogWarning("File not found: " + connectionsFilePath);
            return false;
        }

        string[] lines = File.ReadAllLines(connectionsFilePath);
        Dictionary<string, HashSet<string>> graph = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (string line in lines)
        {
            string trimmed = line?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            string[] parts = trimmed.Split('|');
            if (parts.Length == 4)
            {
                string source = parts[0];
                string target = parts[2];
                if (!string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(target))
                {
                    if (!graph.ContainsKey(source))
                        graph[source] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!graph.ContainsKey(target))
                        graph[target] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    graph[source].Add(target);
                    graph[target].Add(source);
                }
            }
        }

        if (graph.Count == 0)
        {
            Debug.LogWarning("No connections found.");
            return false;
        }

        GenerateMapTexture(graph, outputImagePath);
        Debug.Log("Map saved to " + outputImagePath);
        return true;
    }

    private static void GenerateMapTexture(Dictionary<string, HashSet<string>> graph, string outputPath)
    {
        int nodeWidth = 250;
        int nodeHeight = 120;
        int marginX = 100;
        int marginY = 80;

        List<string> nodes = graph.Keys.ToList();
        int cols = (int)Math.Ceiling(Math.Sqrt(nodes.Count));
        int rows = (int)Math.Ceiling((double)nodes.Count / cols);

        int texWidth = cols * (nodeWidth + marginX) + marginX;
        int texHeight = rows * (nodeHeight + marginY) + marginY;

        Texture2D tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[texWidth * texHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.black;
        tex.SetPixels(pixels);

        Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        int xIndex = 0;
        int yIndex = 0;
        foreach (string node in nodes)
        {
            positions[node] = new Vector2(marginX + xIndex * (nodeWidth + marginX), marginY + yIndex * (nodeHeight + marginY));
            xIndex++;
            if (xIndex >= cols)
            {
                xIndex = 0;
                yIndex++;
            }
        }

        // draw edges
        foreach (var kvp in graph)
        {
            if (positions.TryGetValue(kvp.Key, out Vector2 pos1))
            {
                foreach (string neighbor in kvp.Value)
                {
                    if (positions.TryGetValue(neighbor, out Vector2 pos2))
                    {
                        DrawLine(tex,
                            (int)(pos1.x + nodeWidth / 2) + 2, (int)(pos1.y + nodeHeight / 2) + 2,
                            (int)(pos2.x + nodeWidth / 2) + 2, (int)(pos2.y + nodeHeight / 2) + 2,
                            Color.gray, 3);
                    }
                }
            }
        }

        // draw nodes
        foreach (var kvp in positions)
        {
            DrawEmptyRectangle(tex, (int)kvp.Value.x, (int)kvp.Value.y, nodeWidth, nodeHeight, Color.gray);
            DrawTextBitmap(tex, (int)kvp.Value.x + 10, (int)kvp.Value.y + 35, kvp.Key, Color.white);
        }

        tex.Apply();
        string dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(outputPath, ImageConversion.EncodeToPNG(tex));
        UnityEngine.Object.DestroyImmediate(tex);
    }

    private static void DrawEmptyRectangle(Texture2D tex, int x, int y, int w, int h, Color color)
    {
        for (int i = x; i < x + w; i++)
        {
            if (i >= 0 && i < tex.width)
            {
                if (y >= 0 && y < tex.height)
                    tex.SetPixel(i, y, color);
                if (y + h - 1 >= 0 && y + h - 1 < tex.height)
                    tex.SetPixel(i, y + h - 1, color);
            }
        }
        for (int j = y; j < y + h; j++)
        {
            if (j >= 0 && j < tex.height)
            {
                if (x >= 0 && x < tex.width)
                    tex.SetPixel(x, j, color);
                if (x + w - 1 >= 0 && x + w - 1 < tex.width)
                    tex.SetPixel(x + w - 1, j, color);
            }
        }
    }

    private static void DrawTextBitmap(Texture2D tex, int x, int y, string text, Color color)
    {
        int curX = x;
        int scale = 4;
        int underscoreShift = 1;

        foreach (char c in text.ToUpper())
        {
            if ((c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '_')
            {
                curX += 5 * scale + scale;
                continue;
            }

            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    if (letters[(int)c, row * 5 + col])
                    {
                        int pixelY = c != '_' ? 4 - row : 4 - row - underscoreShift;
                        for (int sx = 0; sx < scale; sx++)
                        {
                            for (int sy = 0; sy < scale; sy++)
                            {
                                int px = curX + col * scale + sx;
                                int py = y + pixelY * scale + sy;
                                if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                                    tex.SetPixel(px, py, color);
                            }
                        }
                    }
                }
            }
            curX += 5 * scale + scale;
        }
    }

    private static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color color, int thickness = 2)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            for (int i = -thickness / 2; i <= thickness / 2; i++)
            {
                for (int j = -thickness / 2; j <= thickness / 2; j++)
                {
                    int px = x0 + i;
                    int py = y0 + j;
                    if (px >= 0 && px < tex.width && py >= 0 && py < tex.height)
                        tex.SetPixel(px, py, color);
                }
            }
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}