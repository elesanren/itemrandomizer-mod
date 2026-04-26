using BepInEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Random = System.Random;

namespace HKSilksong_Randomizer;

public class RoomRando : MonoBehaviour
{
    private Dictionary<string, (string targetScene, string targetGate)> connections = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
    private List<string> availableScenes = new List<string>();
    private bool enableRandomization = true;
    private RandomSceneLoader sceneLoader;
    private string saveFilePath;
    private Random rng;
    private int generationSeed = 0;
    private bool enableMapGeneration = false;
    private List<(string beforeScene, string afterScene)> precedencePairs = new List<(string, string)>();
    private bool forceExactSeed = false;

    // ★ 排除门黑名单：这些出口无法正常使用，随机连接时必须跳过
    private static readonly HashSet<string> BlacklistedExits = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---- 已确认 ----
        "Bone_11_top1",             // Bone_11 的上
        "Bellshrine_right1",        // Bellshrine 的右
        "Bellshrine_05_right1",     // Bellshrine_05 右
        "Dock_06_Church_right1",    // Dock_06_Church 右
        "Bone_East_09_top1",        // Bone_East_09 上
        "Bellshrine_02_left1",      // Bellshrine_02 左
        "Dust_04_right1",           // Dust_04 右下
        "Dust_05_right1",           // Dust_05 右
        "Dust_06_left1",            // Dust_06 左中
        "Shadow_20_bot1",           // Shadow_20 下

        // ---- 待确认（去掉前缀 "//" 即可激活）----
        //"Bone_08_left2",
        //"Bone_08_left3",
        //"Dock_01_right1",
        //"Dock_01_right2",
        //"Dock_03_bot1",
        //"Dock_03c_left2",
        //"Dock_03c_top1",
        //"Dock_03c_top2",
        //"Dock_02_left1",
        //"Dock_02_left2",
        //"Bone_East_14_right1",
        //"Bone_East_14_right2",
        //"Greymoor_01_right1",
        //"Greymoor_01_right2",
        //"Dust_02_right1",
        //"Dust_02_right2",
        //"Dust_04_left1",
        //"Dust_04_left2",
        //"Dust_Chef_left1",
        //"Dust_Chef_bot1",
    };

    public void Initialize(RandomSceneLoader loader)
    {
        sceneLoader = loader;
        InitializeRoomRando();
    }

    private void InitializeRoomRando()
    {
        try
        {
            availableScenes = sceneLoader.sceneConfigs.Keys.ToList();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RoomRando: failed to populate availableScenes from sceneLoader: {ex.Message}");
        }
        InitializePredefinedConnections();
        try
        {
            saveFilePath = Path.Combine(Paths.PluginPath, "roomrando_connections.txt");
        }
        catch
        {
            saveFilePath = "roomrando_connections.txt";
        }
        Debug.Log($"RoomRando: saveFilePath set to '{saveFilePath}' (exists={File.Exists(saveFilePath)})");
        if (!enableRandomization)
            return;

        // [核心逻辑] 尝试加载，如果存档与新规则冲突，则强制重新生成
        if (TryLoadConnectionsFromFile())
        {
            // 构建当前应该存在的合法出口集合，用于验证存档有效性
            var validExitsSnapshot = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var scene in availableScenes)
            {
                if (sceneLoader.sceneConfigs.TryGetValue(scene, out var cfg) && cfg.Exits != null)
                {
                    foreach (var exit in cfg.Exits)
                    {
                        validExitsSnapshot.Add(MakeKey(scene, exit));
                    }
                }
            }
            validExitsSnapshot.ExceptWith(BlacklistedExits); // 从合法集合中移除黑名单

            // 检查加载的 connections 中是否有任何键不存在于过滤后的合法集合中
            if (connections.Keys.Any(key => !validExitsSnapshot.Contains(key)))
            {
                Debug.LogWarning("RoomRando: Loaded connections conflict with current blacklist rules. Forcing regeneration...");
                connections.Clear();
                GenerateAllConnectionsAtStart();
                SaveConnectionsToFile();
            }
            else
            {
                Debug.Log($"RoomRando: loaded randomized connections from {saveFilePath}");
            }
        }
        else
        {
            GenerateAllConnectionsAtStart();
            SaveConnectionsToFile();
        }
        if (enableMapGeneration)
            TryGenerateMap();
    }

    private void GenerateAllConnectionsAtStart()
    {
        int num1 = generationSeed == 0 ? Environment.TickCount : generationSeed;
        int finalConnectionCount = 0;
        List<string> scenesWithExits = availableScenes.Where(s =>
        {
            RandomSceneLoader.SceneConfig sceneConfig;
            return sceneLoader != null && sceneLoader.sceneConfigs.TryGetValue(s, out sceneConfig) && sceneConfig.Exits != null && sceneConfig.Exits.Count > 0;
        }).ToList();
        Debug.Log($"RoomRando: {scenesWithExits.Count} scenes have defined exits");

        bool success = false;
        int attempts = forceExactSeed ? 1 : 8;
        for (int attempt = 0; attempt < attempts && !success; attempt++)
        {
            int seed = forceExactSeed ? num1 : num1 + attempt;
            rng = new Random(seed);
            connections.Clear();
            finalConnectionCount = 0;
            Debug.Log($"RoomRando: generation attempt {attempt + 1}/{attempts} using seed={seed}");

            InitializePredefinedConnections();

            Dictionary<string, List<string>> freeExits = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in scenesWithExits)
                freeExits[key] = new List<string>(sceneLoader.sceneConfigs[key].Exits);

            // ★ 生成时排除黑名单 (源头过滤)
            foreach (string entry in BlacklistedExits)
            {
                int sep = entry.LastIndexOf('_');
                if (sep > 0)
                {
                    string scene = entry.Substring(0, sep);
                    string exit = entry.Substring(sep + 1);
                    if (freeExits.ContainsKey(scene))
                        freeExits[scene].Remove(exit);
                }
            }

            foreach (var kv in connections.ToList())
            {
                string key = kv.Key;
                int pos = key.LastIndexOf('_');
                if (pos > 0)
                {
                    string scene = key.Substring(0, pos);
                    string exit = key.Substring(pos + 1);
                    if (freeExits.ContainsKey(scene))
                        freeExits[scene].Remove(exit);
                }
            }

            try
            {
                foreach (var (a, b) in precedencePairs)
                {
                    if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && freeExits.ContainsKey(a) && freeExits.ContainsKey(b) && freeExits[a].Count > 0 && freeExits[b].Count > 0)
                        ConnectScenes(a, b);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"RoomRando: failed applying precedence pairs: {ex.Message}");
            }

            List<string> shuffled = scenesWithExits.OrderBy(_ => rng.Next()).ToList();
            for (int i = 1; i < shuffled.Count; i++)
            {
                string a = null;
                string b = null;
                for (int j = i - 1; j >= 0; j--)
                    if (freeExits[shuffled[j]].Count > 0) { a = shuffled[j]; break; }
                for (int j = i; j < shuffled.Count; j++)
                    if (freeExits[shuffled[j]].Count > 0) { b = shuffled[j]; break; }
                if (a != null && b != null && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    ConnectScenes(a, b);
            }

            Dictionary<string, HashSet<string>> neighbors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in connections)
            {
                int pos = kv.Key.LastIndexOf('_');
                if (pos > 0)
                    AddNeighborEdge(kv.Key.Substring(0, pos), kv.Value.targetScene);
            }

            List<(string, string)> remaining = freeExits.SelectMany(kv => kv.Value.Select(e => (kv.Key, e))).ToList();
            bool pairingSuccess = false;
            int pairingAttempts = 6;
            for (int innerAttempt = 0; innerAttempt < pairingAttempts && !pairingSuccess; innerAttempt++)
            {
                List<(string, string)> shuffledPairs = remaining.OrderBy(_ => rng.Next()).ToList();
                HashSet<int> used = new HashSet<int>();
                List<((string, string), (string, string))> pairs = new List<((string, string), (string, string))>();
                bool conflict = false;

                for (int i = 0; i < shuffledPairs.Count; i++)
                {
                    if (used.Contains(i)) continue;
                    var p1 = shuffledPairs[i];
                    int bestIndex = -1;
                    int bestScore = int.MinValue;

                    for (int j = i + 1; j < shuffledPairs.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        var p2 = shuffledPairs[j];
                        if (string.Equals(p1.Item1, p2.Item1, StringComparison.OrdinalIgnoreCase)) continue;

                        int score = 0;
                        if (!IsOriginalSingle(p2.Item1)) score += 20;
                        if (!IsOriginalSingle(p1.Item1)) score += 10;
                        if (IsOriginalSingle(p1.Item1) && IsOriginalSingle(p2.Item1)) score -= 1000;

                        neighbors.TryGetValue(p1.Item1, out var n1);
                        neighbors.TryGetValue(p2.Item1, out var n2);
                        int leafCount1 = n1?.Count(n => IsLeaf(n)) ?? 0;
                        int leafCount2 = n2?.Count(n => IsLeaf(n)) ?? 0;
                        bool p2Leaf = IsLeaf(p2.Item1);
                        bool p1Leaf = IsLeaf(p1.Item1);

                        if (leafCount1 + (p2Leaf ? 1 : 0) > 1) score -= 50;
                        if (leafCount2 + (p1Leaf ? 1 : 0) > 1) score -= 50;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestIndex = j;
                        }
                    }

                    if (bestIndex < 0)
                    {
                        conflict = true;
                        break;
                    }
                    used.Add(i);
                    used.Add(bestIndex);
                    pairs.Add((shuffledPairs[i], shuffledPairs[bestIndex]));
                    AddNeighborEdge(shuffledPairs[i].Item1, shuffledPairs[bestIndex].Item1);
                }

                if (conflict)
                {
                    neighbors.Clear();
                    foreach (var kv in connections)
                    {
                        int pos = kv.Key.LastIndexOf('_');
                        if (pos > 0) AddNeighborEdge(kv.Key.Substring(0, pos), kv.Value.targetScene);
                    }
                }
                else
                {
                    bool invalid = false;
                    foreach (var (a, b) in pairs)
                        if (IsOriginalSingle(a.Item1) && IsOriginalSingle(b.Item1)) { invalid = true; break; }

                    Dictionary<string, int> leafCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in neighbors)
                        leafCounts[kv.Key] = kv.Value.Count(n => IsLeaf(n));
                    foreach (var count in leafCounts.Values)
                        if (count > 1) { invalid = true; break; }

                    if (invalid)
                    {
                        neighbors.Clear();
                        foreach (var kv in connections)
                        {
                            int pos = kv.Key.LastIndexOf('_');
                            if (pos > 0) AddNeighborEdge(kv.Key.Substring(0, pos), kv.Value.targetScene);
                        }
                    }
                    else
                    {
                        foreach (var (p1, p2) in pairs)
                        {
                            string key1 = MakeKey(p1.Item1, p1.Item2);
                            string key2 = MakeKey(p2.Item1, p2.Item2);
                            if (!connections.ContainsKey(key1) && !connections.ContainsKey(key2))
                            {
                                connections[key1] = (p2.Item1, p2.Item2);
                                connections[key2] = (p1.Item1, p1.Item2);
                                freeExits[p1.Item1].Remove(p1.Item2);
                                freeExits[p2.Item1].Remove(p2.Item2);
                                finalConnectionCount++;
                            }
                        }
                        pairingSuccess = true;
                    }
                }
            }

            if (!pairingSuccess)
            {
                Debug.LogWarning("RoomRando: intelligent pairing failed to satisfy constraints; falling back to greedy pairing");
                List<(string, string)> greedy = remaining.OrderBy(_ => rng.Next()).ToList();
                for (int i = 0; i + 1 < greedy.Count; i += 2)
                {
                    var a = greedy[i];
                    var b = greedy[i + 1];
                    if (!string.Equals(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase))
                    {
                        string keyA = MakeKey(a.Item1, a.Item2);
                        string keyB = MakeKey(b.Item1, b.Item2);
                        if (!connections.ContainsKey(keyA) && !connections.ContainsKey(keyB))
                        {
                            connections[keyA] = (b.Item1, b.Item2);
                            connections[keyB] = (a.Item1, a.Item2);
                            finalConnectionCount++;
                        }
                    }
                }
            }

            neighbors.Clear();
            foreach (var kv in connections)
            {
                int pos = kv.Key.LastIndexOf('_');
                if (pos > 0) AddNeighborEdge(kv.Key.Substring(0, pos), kv.Value.targetScene);
            }

            bool hubIssue = false;
            foreach (var kv in neighbors)
            {
                if (kv.Value.Count(n => IsLeaf(n)) > 3)
                {
                    hubIssue = true;
                    break;
                }
            }

            if (!hubIssue)
            {
                success = true;
                generationSeed = seed;
                Debug.Log($"RoomRando: generation succeeded with seed={seed}");
            }
            else
                Debug.LogWarning($"RoomRando: generation attempt {attempt + 1} produced hubs with too many leaf neighbors; retrying");

            void ConnectScenes(string a, string b)
            {
                if (!freeExits.ContainsKey(a) || !freeExits.ContainsKey(b) || freeExits[a].Count == 0 || freeExits[b].Count == 0)
                    return;
                string exit1 = freeExits[a][rng.Next(freeExits[a].Count)];
                string exit2 = freeExits[b][rng.Next(freeExits[b].Count)];
                string key1 = MakeKey(a, exit1);
                string key2 = MakeKey(b, exit2);
                if (connections.ContainsKey(key1) || connections.ContainsKey(key2))
                    return;
                connections[key1] = (b, exit2);
                connections[key2] = (a, exit1);
                freeExits[a].Remove(exit1);
                freeExits[b].Remove(exit2);
                finalConnectionCount++;
            }

            void AddNeighborEdge(string s1, string s2)
            {
                if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase))
                    return;
                if (!neighbors.ContainsKey(s1)) neighbors[s1] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!neighbors.ContainsKey(s2)) neighbors[s2] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                neighbors[s1].Add(s2);
                neighbors[s2].Add(s1);
            }

            bool IsLeaf(string scene)
            {
                HashSet<string> set;
                return neighbors.TryGetValue(scene, out set) && set.Count == 1;
            }

            bool IsOriginalSingle(string scene)
            {
                RandomSceneLoader.SceneConfig cfg;
                if (sceneLoader.sceneConfigs.TryGetValue(scene, out cfg))
                    return cfg.Exits != null && cfg.Exits.Count == 1;
                return false;
            }
        }

        if (!success)
            Debug.LogWarning($"RoomRando: all {attempts} generation attempts failed to fully satisfy constraints; using last attempt's map and seed={generationSeed}");

        Debug.Log($"RoomRando: Generated {finalConnectionCount} bidirectional connection pairs ({connections.Count} total directional connections)");
    }

    private bool TryLoadConnectionsFromFile()
    {
        // 代码逻辑与之前完全一致，包含严密的源头过滤
        try
        {
            if (string.IsNullOrWhiteSpace(saveFilePath))
            {
                Debug.LogWarning("RoomRando: saveFilePath is empty or null");
                return false;
            }
            if (!File.Exists(saveFilePath))
            {
                Debug.Log($"RoomRando: connections file not found at {saveFilePath}");
                return false;
            }
            string[] lines = File.ReadAllLines(saveFilePath);
            Debug.Log($"RoomRando: read {lines.Length} lines from {saveFilePath}");

            int dataStart = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i]?.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    dataStart = i + 1;
                    continue;
                }
                if (line.StartsWith("#seed|", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    int result;
                    if (parts.Length >= 2 && int.TryParse(parts[1], out result))
                    {
                        generationSeed = result;
                        rng = new Random(generationSeed);
                        Debug.Log($"RoomRando: loaded generation seed {generationSeed} from {saveFilePath}");
                    }
                    dataStart = i + 1;
                    break;
                }
                dataStart = i;
                break;
            }

            int loaded = 0;
            int skipped = 0;
            for (int i = dataStart; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                {
                    string[] cols = line.Split('|');
                    if (cols.Length != 4)
                    {
                        skipped++;
                    }
                    else
                    {
                        string sourceKey = $"{cols[0]}_{cols[1]}";
                        string targetKey = $"{cols[2]}_{cols[3]}";

                        if (BlacklistedExits.Contains(sourceKey) || BlacklistedExits.Contains(targetKey))
                        {
                            skipped++;
                            continue;
                        }

                        connections[sourceKey] = (cols[2], cols[3]);
                        loaded++;
                    }
                }
            }

            Debug.Log($"RoomRando: parsed {loaded} connection lines, skipped {skipped} invalid lines, total connections now {connections.Count}");
            if (loaded != 0 || generationSeed == 0)
                return connections.Count > 0;
            Debug.Log($"RoomRando: file contained seed {generationSeed} but no explicit connections; will regenerate deterministically using that seed");
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RoomRando: failed to load connections file: {ex.Message}");
            return false;
        }
    }

    private void SaveConnectionsToFile()
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(saveFilePath, false))
            {
                if (generationSeed == 0)
                    generationSeed = Environment.TickCount;
                writer.WriteLine($"#seed|{generationSeed}");
                foreach (var kv in connections)
                {
                    string key = kv.Key;
                    int pos = key.LastIndexOf('_');
                    if (pos > 0)
                    {
                        string scene = key.Substring(0, pos);
                        string exit = key.Substring(pos + 1);
                        writer.WriteLine($"{scene}|{exit}|{kv.Value.targetScene}|{kv.Value.targetGate}");
                    }
                }
            }
            Debug.Log($"RoomRando: saved {connections.Count} connections to {saveFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RoomRando: failed to save connections: {ex.Message}");
        }
    }

    private void TryGenerateMap()
    {
        if (!enableMapGeneration)
        {
            Debug.Log("RoomRando: map generation is disabled");
            return;
        }
        try
        {
            string outputImagePath = Path.Combine(Paths.PluginPath, "RoomRandoMap.png");
            if (RoomMapGenerator.GenerateFromFile(saveFilePath, outputImagePath))
                Debug.Log($"RoomRando: generated map image at {outputImagePath}");
            else
                Debug.LogWarning("RoomRando: map generation reported failure or no data.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RoomRando: map generation failed: {ex.Message}");
        }
    }

    private void InitializePredefinedConnections()
    {
        List<(string, string, string, string)> predefined = new List<(string, string, string, string)>();
        foreach (var (scene1, exit1, scene2, exit2) in predefined)
        {
            string key1 = MakeKey(scene1, exit1);
            string key2 = MakeKey(scene2, exit2);
            connections[key1] = (scene2, exit2);
            connections[key2] = (scene1, exit1);
            Debug.Log($"RoomRando: predefined {key1} <-> {key2}");
        }
        Debug.Log($"RoomRando: initialized with {predefined.Count} predefined pairs");
    }

    private string MakeKey(string scene, string exit) => $"{scene}_{exit}";

    public (string targetScene, string targetGate)? GetConnection(string currentScene, string exitName)
    {
        if (string.IsNullOrEmpty(exitName))
            return null;
        (string, string) value;
        return connections.TryGetValue(MakeKey(currentScene, exitName), out value) ? value : ((string, string)?)null;
    }

    public List<string> GetValidExitsForScene(string sceneName)
    {
        RandomSceneLoader.SceneConfig cfg;
        return sceneLoader != null && sceneLoader.sceneConfigs.TryGetValue(sceneName, out cfg) ? cfg.Exits ?? new List<string>() : new List<string>();
    }

    public void OnExitUsed(string currentScene, string exitName)
    {
        if (string.IsNullOrEmpty(exitName))
        {
            Debug.LogWarning($"RoomRando: OnExitUsed called with null/empty exitName from {currentScene}");
            return;
        }
        string key = MakeKey(currentScene, exitName);
        (string targetScene, string targetGate) tuple;
        if (connections.TryGetValue(key, out tuple))
        {
            Debug.Log($"RoomRando: using connection {key} -> {tuple.targetScene} via {tuple.targetGate}");
            LoadScene(tuple.targetScene, tuple.targetGate);
        }
        else
            Debug.LogWarning($"RoomRando: no connection found for {key}");
    }

    private void LoadScene(string sceneName, string entryGate)
    {
        if (GameManager.instance == null)
        {
            Debug.LogError("RoomRando: GameManager not available");
            return;
        }
        try
        {
            RandomSceneLoader loader = FindFirstObjectByType<RandomSceneLoader>();
            if (loader != null)
                loader.SetLastEntryGate(entryGate);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"RoomRando: failed to notify RandomSceneLoader: {ex.Message}");
        }
        try
        {
            GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo()
            {
                SceneName = sceneName,
                EntryGateName = entryGate,
                PreventCameraFadeOut = false,
                WaitForSceneTransitionCameraFade = true,
                Visualization = GameManager.SceneLoadVisualizations.Default
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"RoomRando: BeginSceneTransition failed: {ex}");
        }
    }

    public void LogAllConnections()
    {
        Debug.Log($"RoomRando: {connections.Count} connections:");
        foreach (var kv in connections)
            Debug.Log($"  {kv.Key} -> {kv.Value.targetScene} (via {kv.Value.targetGate})");
    }

    public int GetGenerationSeed() => generationSeed;

    public void RegenerateWithSeed(int seed)
    {
        generationSeed = seed;
        rng = new Random(generationSeed);
        connections.Clear();
        forceExactSeed = true;
        Debug.Log($"RoomRando: regenerating with exact seed {seed}");
        GenerateAllConnectionsAtStart();
        forceExactSeed = false;
        SaveConnectionsToFile();
        Debug.Log($"RoomRando: regeneration complete with seed {generationSeed}, saved to {saveFilePath}");
    }

}