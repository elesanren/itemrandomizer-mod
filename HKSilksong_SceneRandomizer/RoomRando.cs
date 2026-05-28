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
        "Bone_11_top1", "Bellshrine_right1", "Bellshrine_05_right1", "Dock_06_Church_right1",
        "Bone_East_09_top1", "Bellshrine_02_left1", "Dust_04_right1", "Dust_05_right1",
        "Dust_06_left1", "Shadow_20_bot1", "Bellshrine_03_right1", "Shellwood_19_left1",
        "Shellwood_01b_right2", "Bellshrine_02_right1", "Dust_06_right1", "Mosstown_03_top1",
        "Coral_10_left1", "Song_27_left1", "Dock_01_right1", "Dock_01_right2", "Bone_East_14_right1",
        "Bone_East_14_right2", "Greymoor_01_right1", "Greymoor_01_right2", "Dust_02_right1",
        "Dust_02_right2", "Dust_04_left1", "Dust_04_left2", "Song_25_top1", "Song_25_top2",
        "Arborium_05_top1", "Hang_07_top1", "Hang_07_left1", "Slab_02_left1", "Slab_05_right1",
        "Cog_06_right1", "Library_11b_right1", "Dock_03_bot1", "Dock_03c_top1", "Dock_03c_left2",
        "Dock_02_left1", "Shellwood_02_right2", "Shellwood_01_left2", "Dust_Chef_left1",
        "Song_11_left4", "Slab_03_right1", "Slab_03_right2", "Slab_03_right5", "Slab_03_right7",
        "Slab_03_left6", "Under_05_left2", "Under_02_right4", "Under_02_left1", "Bone_East_12_right1",
        "Shadow_27_right1", "Halfway_01_right1", "Wisp_03_top1", "Arborium_01_right1",
        "Arborium_04_left1", "Library_13_left1", "Hang_03_right2", "Aqueduct_02_right3",
        "Song_19_entrance_left1", "Weave_04_right2", "Slab_16_right1", "Bone_East_04_left1","Bone_East_10_right1",
        "Bone_East_18c_left1","Greymoor_05_left1","Hang_08_left1","Arborium_08_left1"
    };

    private static readonly HashSet<string> SkipExits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Bone_01_bot1", "Bone_01_left4", "Bone_01_right3", "Aspid_01_bot8", "Aspid_01_left2",
        "Aspid_01_right4", "Crawl_03b_right1", "Shellwood_10_left3", "Shellwood_10_right3",
        "Dust_02_left2", "Dust_02_right2", "Coral_24_left1", "Coral_24_right1", "Coral_35b_bot1",
        "Coral_35b_left5", "Coral_35b_right2", "Peak_07_bot5", "Peak_07_top2", "Peak_01_left4",
        "Peak_01_right4", "Peak_01_top4", "Peak_02_left3", "Peak_02_right4", "Peak_04_left1",
        "Peak_04_right1", "Peak_05_bot1", "Peak_05_right3", "Peak_05_top2", "Peak_05c_left2",
        "Peak_05c_right1", "Peak_05e_left1", "Peak_05e_right2", "Song_05_left5", "Song_05_right4",
        "Song_11_right3","Song_01c"
    };

    private static readonly Dictionary<string, int> SingleExitPairedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // ★ 高房间列表
    private static readonly HashSet<string> HighPriorityRooms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Bonegrave", "Shellwood_04c", "Coral_02", "Coral_35", "Coral_32", "Coral_24", "Coral_23", "Coral_25",
        "Dust_03", "Dust_06", "Shadow_05", "Shadow_08", "Shadow_09", "Shadow_16", "Shadow_10", "Shadow_19", "Shadow_24",
        "Shadow_01", "Shadow_14", "Aqueduct_04", "Aqueduct_03", "Aqueduct_01", "Arborium_11", "Arborium_09", "Arborium_05",
        "Song_Enclave", "Song_09b", "Song_11", "Song_01", "Song_19_entrance", "Under_02", "Under_07c", "Under_27",
        "Under_17", "Under_10", "Under_19c", "Library_11", "Library_12", "Library_12b", "Library_02", "Song_20b", "Song_20", "Library_03",
        "Slab_06", "Slab_05", "Slab_19b", "Slab_15", "Coral_27", "Coral_44", "Abyss_13", "Abyss_01", "Cog_08", "Cog_09","Ward_02","Wisp_05",
        "Wisp_08","Slab_07","Weave_08","Song_03","Abyss_05", "Cradle_02","Hang_02", "Hang_03", "Hang_13","Hang_10","Hang_16", "Bone_East_15","Cog_04"
        , "Arborium_08", "Under_23","Bellway_03", "Dock_15", "Dust_04"
    };

    public void Initialize(RandomSceneLoader loader)
    {
        sceneLoader = loader;
        if (sceneLoader != null)
        {
            foreach (var scene in sceneLoader.sceneConfigs.Keys)
            {
                if (scene.StartsWith("Peak_", StringComparison.OrdinalIgnoreCase))
                    HighPriorityRooms.Add(scene);
                if (scene.StartsWith("Ward_", StringComparison.OrdinalIgnoreCase))
                    HighPriorityRooms.Add(scene);
                if (scene.StartsWith("Clover_", StringComparison.OrdinalIgnoreCase))
                    HighPriorityRooms.Add(scene);
            }
        }
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

        if (TryLoadConnectionsFromFile())
        {
            bool hasConflict = false;
            foreach (var kv in connections)
            {
                int keyPos = kv.Key.LastIndexOf('_');
                if (keyPos > 0)
                {
                    string sceneA = kv.Key.Substring(0, keyPos);
                    string exitA = kv.Key.Substring(keyPos + 1);
                    string sceneB = kv.Value.targetScene;
                    string exitB = kv.Value.targetGate;
                    if (!IsConnectionAllowed(sceneA, exitA, sceneB, exitB))
                    {
                        hasConflict = true;
                        break;
                    }
                }
            }
            if (hasConflict)
            {
                Debug.LogWarning("RoomRando: Loaded connections conflict with current rules. Forcing regeneration...");
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

    // ★ 两步随机法主函数（已加入单出口房间排除）
    private void GenerateAllConnectionsAtStart()
    {
        int num1 = generationSeed == 0 ? Environment.TickCount : generationSeed;
        int finalConnectionCount = 0;
        List<string> allScenesWithExits = availableScenes.Where(s =>
        {
            RandomSceneLoader.SceneConfig sceneConfig;
            return sceneLoader != null && sceneLoader.sceneConfigs.TryGetValue(s, out sceneConfig) && sceneConfig.Exits != null && sceneConfig.Exits.Count > 0;
        }).ToList();

        // 分类：有效出口数 > 1 才参与随机；有效出口数 == 1 的不参与
        List<string> normalScenes = new List<string>();
        List<string> highScenes = new List<string>();
        List<string> singleExitScenes = new List<string>();

        foreach (string scene in allScenesWithExits)
        {
            int actualExits = GetActualExitCount(scene);
            if (actualExits == 1)
            {
                singleExitScenes.Add(scene);
                continue;
            }
            if (HighPriorityRooms.Contains(scene))
                highScenes.Add(scene);
            else
                normalScenes.Add(scene);
        }

        Debug.Log($"RoomRando: 普通房间(>1出口)数量: {normalScenes.Count}, 高房间(>1出口)数量: {highScenes.Count}, 单出口房间数量: {singleExitScenes.Count}");

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

            // ========== 第一阶段：普通房间 ==========
            Dictionary<string, List<string>> freeExitsNormal = BuildFreeExits(normalScenes);
            SingleExitPairedCounts.Clear();

            // 预定义连接移除已用出口
            foreach (var kv in connections.ToList())
            {
                int pos = kv.Key.LastIndexOf('_');
                if (pos > 0)
                {
                    string scene = kv.Key.Substring(0, pos);
                    string exit = kv.Key.Substring(pos + 1);
                    if (freeExitsNormal.ContainsKey(scene))
                        freeExitsNormal[scene].Remove(exit);
                }
            }

            // 优先连接
            try
            {
                foreach (var (a, b) in precedencePairs)
                {
                    if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && freeExitsNormal.ContainsKey(a) && freeExitsNormal.ContainsKey(b) && freeExitsNormal[a].Count > 0 && freeExitsNormal[b].Count > 0)
                        ConnectScenesWithFreeExits(a, b, freeExitsNormal);
                }
            }
            catch (Exception ex) { Debug.LogWarning($"precedencePairs error: {ex.Message}"); }

            // 顺序连接相邻配对
            List<string> shuffledNormal = normalScenes.OrderBy(_ => rng.Next()).ToList();
            for (int i = 1; i < shuffledNormal.Count; i++)
            {
                string a = null, b = null;
                for (int j = i - 1; j >= 0; j--)
                    if (freeExitsNormal[shuffledNormal[j]].Count > 0) { a = shuffledNormal[j]; break; }
                for (int j = i; j < shuffledNormal.Count; j++)
                    if (freeExitsNormal[shuffledNormal[j]].Count > 0) { b = shuffledNormal[j]; break; }
                if (a != null && b != null && !string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    ConnectScenesWithFreeExits(a, b, freeExitsNormal);
            }

            // 构建邻接图
            Dictionary<string, HashSet<string>> neighbors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in connections)
            {
                int pos = kv.Key.LastIndexOf('_');
                if (pos > 0)
                    AddNeighborEdge(neighbors, kv.Key.Substring(0, pos), kv.Value.targetScene);
            }

            // 智能配对剩余出口
            List<(string, string)> remainingNormal = freeExitsNormal.SelectMany(kv => kv.Value.Select(e => (kv.Key, e))).ToList();
            bool normalPairingSuccess = false;
            int pairingAttempts = 6;
            for (int inner = 0; inner < pairingAttempts && !normalPairingSuccess; inner++)
            {
                List<(string, string)> shuffledPairs = remainingNormal.OrderBy(_ => rng.Next()).ToList();
                HashSet<int> used = new HashSet<int>();
                List<((string, string), (string, string))> pairs = new List<((string, string), (string, string))>();
                bool conflict = false;

                for (int i = 0; i < shuffledPairs.Count; i++)
                {
                    if (used.Contains(i)) continue;
                    var p1 = shuffledPairs[i];
                    int bestIdx = -1;
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
                        int leafCount1 = n1?.Count(n => IsLeaf(neighbors, n)) ?? 0;
                        int leafCount2 = n2?.Count(n => IsLeaf(neighbors, n)) ?? 0;
                        bool p2Leaf = IsLeaf(neighbors, p2.Item1);
                        bool p1Leaf = IsLeaf(neighbors, p1.Item1);

                        if (leafCount1 + (p2Leaf ? 1 : 0) > 1) score -= 50;
                        if (leafCount2 + (p1Leaf ? 1 : 0) > 1) score -= 50;

                        if (score > bestScore) { bestScore = score; bestIdx = j; }
                    }

                    if (bestIdx < 0) { conflict = true; break; }
                    used.Add(i); used.Add(bestIdx);
                    pairs.Add((shuffledPairs[i], shuffledPairs[bestIdx]));
                    AddNeighborEdge(neighbors, shuffledPairs[i].Item1, shuffledPairs[bestIdx].Item1);
                }

                if (conflict)
                {
                    neighbors.Clear();
                    foreach (var kv in connections)
                    {
                        int pos = kv.Key.LastIndexOf('_');
                        if (pos > 0) AddNeighborEdge(neighbors, kv.Key.Substring(0, pos), kv.Value.targetScene);
                    }
                    continue;
                }

                bool invalid = false;
                foreach (var (a, b) in pairs)
                    if (IsOriginalSingle(a.Item1) && IsOriginalSingle(b.Item1)) { invalid = true; break; }

                Dictionary<string, int> leafCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in neighbors)
                    leafCounts[kv.Key] = kv.Value.Count(n => IsLeaf(neighbors, n));
                if (leafCounts.Values.Any(c => c > 1)) invalid = true;

                if (invalid)
                {
                    neighbors.Clear();
                    foreach (var kv in connections)
                    {
                        int pos = kv.Key.LastIndexOf('_');
                        if (pos > 0) AddNeighborEdge(neighbors, kv.Key.Substring(0, pos), kv.Value.targetScene);
                    }
                    continue;
                }

                foreach (var (p1, p2) in pairs)
                {
                    string key1 = MakeKey(p1.Item1, p1.Item2);
                    string key2 = MakeKey(p2.Item1, p2.Item2);
                    if (!connections.ContainsKey(key1) && !connections.ContainsKey(key2))
                    {
                        connections[key1] = (p2.Item1, p2.Item2);
                        connections[key2] = (p1.Item1, p1.Item2);
                        freeExitsNormal[p1.Item1].Remove(p1.Item2);
                        freeExitsNormal[p2.Item1].Remove(p2.Item2);
                        finalConnectionCount++;
                    }
                }
                normalPairingSuccess = true;
            }

            if (!normalPairingSuccess)
            {
                Debug.LogWarning("第一阶段智能配对失败，使用贪心配对");
                List<(string, string)> greedy = remainingNormal.OrderBy(_ => rng.Next()).ToList();
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
                            freeExitsNormal[a.Item1].Remove(a.Item2);
                            freeExitsNormal[b.Item1].Remove(b.Item2);
                        }
                    }
                }
            }

            // 收集第一阶段未配对的出口
            List<(string scene, string exit)> leftoverNormal = freeExitsNormal.SelectMany(kv => kv.Value.Select(e => (kv.Key, e))).ToList();
            if (leftoverNormal.Count > 0)
                Debug.LogWarning($"第一阶段剩余 {leftoverNormal.Count} 个未配对出口");

            // ========== 第二阶段：混合配对 ==========
            Dictionary<string, List<string>> freeExitsHigh = BuildFreeExits(highScenes);
            List<(string scene, string exit)> phase2Pool = new List<(string, string)>();
            phase2Pool.AddRange(leftoverNormal);
            foreach (var kv in freeExitsHigh)
                foreach (var ex in kv.Value)
                    phase2Pool.Add((kv.Key, ex));

            if (phase2Pool.Count > 0)
            {
                Debug.Log($"第二阶段开始，共 {phase2Pool.Count} 个出口");
                PairExitsSimple(phase2Pool);
            }

            // 验证 hub 叶子节点数量
            neighbors.Clear();
            foreach (var kv in connections)
            {
                int pos = kv.Key.LastIndexOf('_');
                if (pos > 0)
                    AddNeighborEdge(neighbors, kv.Key.Substring(0, pos), kv.Value.targetScene);
            }

            bool hubIssue = false;
            foreach (var kv in neighbors)
                if (kv.Value.Count(n => IsLeaf(neighbors, n)) > 3) { hubIssue = true; break; }

            if (!hubIssue)
            {
                success = true;
                generationSeed = seed;
                Debug.Log($"成功，种子={seed}");
            }
            else
                Debug.LogWarning($"尝试 {attempt + 1} 失败：叶子节点过多");

            // 局部辅助函数
            void ConnectScenesWithFreeExits(string scA, string scB, Dictionary<string, List<string>> freeExits)
            {
                if (!freeExits.ContainsKey(scA) || !freeExits.ContainsKey(scB) || freeExits[scA].Count == 0 || freeExits[scB].Count == 0)
                    return;
                string exitA = freeExits[scA][rng.Next(freeExits[scA].Count)];
                string exitB = freeExits[scB][rng.Next(freeExits[scB].Count)];
                if (!IsConnectionAllowed(scA, exitA, scB, exitB)) return;
                string keyA = MakeKey(scA, exitA);
                string keyB = MakeKey(scB, exitB);
                if (connections.ContainsKey(keyA) || connections.ContainsKey(keyB)) return;
                connections[keyA] = (scB, exitB);
                connections[keyB] = (scA, exitA);
                freeExits[scA].Remove(exitA);
                freeExits[scB].Remove(exitB);
                finalConnectionCount++;
                UpdateSingleExitCount(scA, scB);
            }

            void AddNeighborEdge(Dictionary<string, HashSet<string>> graph, string s1, string s2)
            {
                if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return;
                if (!graph.ContainsKey(s1)) graph[s1] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!graph.ContainsKey(s2)) graph[s2] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                graph[s1].Add(s2);
                graph[s2].Add(s1);
            }

            bool IsLeaf(Dictionary<string, HashSet<string>> graph, string scene)
            {
                return graph.TryGetValue(scene, out var set) && set.Count == 1;
            }

            bool IsOriginalSingle(string scene)
            {
                return sceneLoader.sceneConfigs.TryGetValue(scene, out var cfg) && cfg.Exits != null && cfg.Exits.Count == 1;
            }
        }

        if (!success)
            Debug.LogWarning($"所有尝试失败，使用最后一次结果，种子={generationSeed}");
        Debug.Log($"生成了 {finalConnectionCount} 对双向连接 ({connections.Count} 条单向)");
    }

    // ★ 构建 freeExits 字典
    private Dictionary<string, List<string>> BuildFreeExits(List<string> scenes)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string scene in scenes)
        {
            var exits = new List<string>(sceneLoader.sceneConfigs[scene].Exits);
            foreach (string entry in BlacklistedExits)
            {
                int sep = entry.LastIndexOf('_');
                if (sep > 0)
                {
                    string blackScene = entry.Substring(0, sep);
                    string blackExit = entry.Substring(sep + 1);
                    if (string.Equals(blackScene, scene, StringComparison.OrdinalIgnoreCase))
                        exits.Remove(blackExit);
                }
            }
            dict[scene] = exits;
        }
        return dict;
    }

    // ★ 第二阶段简单配对（无图约束）
    private void PairExitsSimple(List<(string scene, string exit)> exitPool)
    {
        var shuffled = exitPool.OrderBy(_ => rng.Next()).ToList();
        HashSet<int> used = new HashSet<int>();

        for (int idxA = 0; idxA < shuffled.Count; idxA++)
        {
            if (used.Contains(idxA)) continue;
            var exitA = shuffled[idxA];
            int matchIdx = -1;
            for (int idxB = idxA + 1; idxB < shuffled.Count; idxB++)
            {
                if (used.Contains(idxB)) continue;
                var exitB2 = shuffled[idxB];
                if (string.Equals(exitA.scene, exitB2.scene, StringComparison.OrdinalIgnoreCase))
                    continue;
                if ((exitA.scene == "Tut_01" && HighPriorityRooms.Contains(exitB2.scene)) ||
                    (exitB2.scene == "Tut_01" && HighPriorityRooms.Contains(exitA.scene)))
                    continue;
                matchIdx = idxB;
                break;
            }
            if (matchIdx < 0) continue;

            used.Add(idxA);
            used.Add(matchIdx);
            var pairedExit = shuffled[matchIdx];

            string key1 = MakeKey(exitA.scene, exitA.exit);
            string key2 = MakeKey(pairedExit.scene, pairedExit.exit);
            if (!connections.ContainsKey(key1) && !connections.ContainsKey(key2))
            {
                connections[key1] = (pairedExit.scene, pairedExit.exit);
                connections[key2] = (exitA.scene, exitA.exit);
            }
        }

        if ((shuffled.Count - used.Count) > 0)
            Debug.LogWarning($"第二阶段配对后剩余 {shuffled.Count - used.Count} 个未配对出口");
    }

    // ★ 计算有效出口数（排除黑名单后）
    private int GetActualExitCount(string scene)
    {
        var cfg = sceneLoader.sceneConfigs[scene];
        int blacklisted = cfg.Exits.Count(e => BlacklistedExits.Contains(MakeKey(scene, e)));
        return cfg.Exits.Count - blacklisted;
    }

    // ★ IsConnectionAllowed 增加 Tut_01 禁止连接高房间
    private bool IsConnectionAllowed(string sceneA, string exitA, string sceneB, string exitB)
    {
        // 禁止自己连自己
        if (string.Equals(sceneA, sceneB, StringComparison.OrdinalIgnoreCase))
            return false;

        // Tut_01 禁止连接到高房间
        if ((sceneA == "Tut_01" && HighPriorityRooms.Contains(sceneB)) ||
            (sceneB == "Tut_01" && HighPriorityRooms.Contains(sceneA)))
            return false;

        string keyA = MakeKey(sceneA, exitA);
        string keyB = MakeKey(sceneB, exitB);
        if (BlacklistedExits.Contains(keyA) || BlacklistedExits.Contains(keyB))
            return false;

        int origA = sceneLoader.sceneConfigs[sceneA].Exits.Count;
        int origB = sceneLoader.sceneConfigs[sceneB].Exits.Count;

        int blacklistedA = sceneLoader.sceneConfigs[sceneA].Exits.Count(e => BlacklistedExits.Contains(MakeKey(sceneA, e)));
        int blacklistedB = sceneLoader.sceneConfigs[sceneB].Exits.Count(e => BlacklistedExits.Contains(MakeKey(sceneB, e)));
        int actualA = origA - blacklistedA;
        int actualB = origB - blacklistedB;

        if (actualA == 1 && actualB < 3) return false;
        if (actualB == 1 && actualA < 3) return false;

        if (actualA == 1 && actualB >= 3)
        {
            int used = SingleExitPairedCounts.TryGetValue(sceneB, out int c) ? c : 0;
            if (used >= actualB - 2) return false;
        }
        if (actualB == 1 && actualA >= 3)
        {
            int used = SingleExitPairedCounts.TryGetValue(sceneA, out int c) ? c : 0;
            if (used >= actualA - 2) return false;
        }

        if (keyA == "Tut_01_left3" || keyB == "Tut_01_left3")
        {
            string otherScene = keyA == "Tut_01_left3" ? sceneB : sceneA;
            string otherExit = keyA == "Tut_01_left3" ? exitB : exitA;
            string otherKey = keyA == "Tut_01_left3" ? keyB : keyA;

            int otherActual = keyA == "Tut_01_left3" ? actualB : actualA;
            if (otherActual == 1)
                return false;

            HashSet<string> tutRestricted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { };
            if (tutRestricted.Contains(otherKey))
                return false;
        }
        return true;
    }

    private void UpdateSingleExitCount(string sceneA, string sceneB)
    {
        int exitsA = sceneLoader.sceneConfigs[sceneA].Exits.Count;
        int exitsB = sceneLoader.sceneConfigs[sceneB].Exits.Count;

        int blacklistedA = sceneLoader.sceneConfigs[sceneA].Exits.Count(e => BlacklistedExits.Contains(MakeKey(sceneA, e)));
        int blacklistedB = sceneLoader.sceneConfigs[sceneB].Exits.Count(e => BlacklistedExits.Contains(MakeKey(sceneB, e)));
        int actualA = exitsA - blacklistedA;
        int actualB = exitsB - blacklistedB;

        if (actualA == 1 && actualB >= 3)
        {
            if (!SingleExitPairedCounts.ContainsKey(sceneB))
                SingleExitPairedCounts[sceneB] = 1;
            else
                SingleExitPairedCounts[sceneB]++;
        }
        if (actualB == 1 && actualA >= 3)
        {
            if (!SingleExitPairedCounts.ContainsKey(sceneA))
                SingleExitPairedCounts[sceneA] = 1;
            else
                SingleExitPairedCounts[sceneA]++;
        }
    }

    private bool TryLoadConnectionsFromFile()
    {
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