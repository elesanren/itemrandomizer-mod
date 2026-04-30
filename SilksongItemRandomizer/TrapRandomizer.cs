using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public static class TrapRandomizer
{
    // ===== 完整陷阱池 =====
    private static readonly List<string> TrapPool = new()
    {
        "fan_hazard", "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5",
        "hot_coal", "lava_area", "falling_lava", "bone_boulder",
        "hunter_sickle_trap", "hunter_landmine",
        "pilgrim_trap_spike", "wisp_flame_lantern",
        "falling_bell", "shellwood_thorns",
        "coral_lightning_rock", "coral_lightning_orb", "voltgrass",
        "coral_crust_s", "coral_crust_m", "coral_crust_l",
        "coral_spike", "coral_spike_fall", "stomp_spire",
        "rubble_field", "steam_vent", "junk_pipe",
        "slab_trap", "slab_spike_ball", "slab_prob_blade",
        "bilewater_trap", "falling_spike_ball", "swing_trap_small", "swing_trap_spike",
        "dust_trap_spike_plate", "dust_trap_spike_dropper", "mite_trap",
        "organ_spikes", "cradle_spikes",
        "brown_vines", "abyss_tendrils", "void_wave",
        "mill_trap", "craw_chain",
        "frost_marker", "white_thorns", "jelly_egg", "wp_trap_spikes"
    };

    // 大陷阱名单
    private static readonly HashSet<string> LargeTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "fan_hazard",
        "steam_vent",
        "coral_lightning_rock",
        "mill_trap"
    };
    private const int LargeTrapMinClearance = 8;
    private const float RayMaxDist = 30f;

    private static readonly List<GameObject> ActiveTraps = new();
    private static List<Vector3> _surfacePoints = new();
    private static string _lastScene = "";
    public static bool Enabled = true;
    private static int _masterSeed;
    private const float MinDistance = 6f;

    private static readonly Dictionary<string, List<(Vector3 pos, string trapId)>> _cachedTraps = new();

    public static void Initialize(int seed)
    {
        _masterSeed = seed == 0 ? Environment.TickCount : seed;
        ForceEnableLoadAllAssets();
    }

    private static void ForceEnableLoadAllAssets()
    {
        try
        {
            string configFile = Path.Combine(BepInEx.Paths.ConfigPath, "com.cometcake575.architect.cfg");
            if (!File.Exists(configFile))
            {
                Plugin.Log?.LogWarning("TrapRandomizer: 未找到 Architect 配置文件，跳过自动设置");
                return;
            }
            string content = File.ReadAllText(configFile);
            string newContent = Regex.Replace(content, @"LoadAllAssets\s*=\s*false", "LoadAllAssets = true", RegexOptions.IgnoreCase);
            if (newContent != content)
            {
                File.WriteAllText(configFile, newContent);
                Plugin.Log?.LogInfo("TrapRandomizer: 已自动开启 Architect 全量素材加载");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"TrapRandomizer: 自动修改配置文件失败: {ex.Message}");
        }
    }

    private static void ScanSceneSurfaces()
    {
        _surfacePoints.Clear();
        HeroController hero = HeroController.instance;
        Vector2 center = hero ? (Vector2)hero.transform.position : Vector2.zero;
        Vector2 areaMin = center + new Vector2(-60f, -60f);
        Vector2 areaMax = center + new Vector2(60f, 60f);

        Collider2D[] allColliders = Physics2D.OverlapAreaAll(areaMin, areaMax, LayerMask.GetMask("Terrain"));
        foreach (var col in allColliders)
        {
            Bounds b = col.bounds;
            if (b.size.y > b.size.x * 2f) continue;
            if (b.min.y > center.y + 120f) continue;

            float step = 2f;
            for (float x = b.min.x + 1f; x <= b.max.x - 1f; x += step)
            {
                Vector2 samplePoint = new Vector2(x, b.max.y + 0.5f);
                RaycastHit2D hitUp = Physics2D.Raycast(samplePoint, Vector2.up, 1.5f, LayerMask.GetMask("Terrain"));
                if (hitUp.collider != null) continue;

                RaycastHit2D hitDown = Physics2D.Raycast(samplePoint, Vector2.down, 0.5f, LayerMask.GetMask("Terrain"));
                if (hitDown.collider != col) continue;

                _surfacePoints.Add(new Vector3(x, b.max.y + 0.1f, 0));
            }
        }

        _lastScene = GameManager.instance?.sceneName ?? "";
        Plugin.Log?.LogInfo($"TrapRandomizer: 扫描到 {_surfacePoints.Count} 个平台候选点 on {_lastScene}");
    }

    // 检测某个方向上到障碍物的距离（忽略脚下地板）
    private static float Clearance(Vector2 origin, Vector2 direction, Collider2D ignoreCollider)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, RayMaxDist, LayerMask.GetMask("Terrain"));
        foreach (var hit in hits)
        {
            if (hit.collider != ignoreCollider)
                return hit.distance;
        }
        return float.MaxValue;
    }

    // 大陷阱空间检测：上下不能都<8，左右不能都<8
    private static bool CanPlaceLargeTrap(Vector2 origin)
    {
        // 先获取脚下地板 collider
        RaycastHit2D floorHit = Physics2D.Raycast(origin, Vector2.down, 0.5f, LayerMask.GetMask("Terrain"));
        Collider2D floor = floorHit.collider;

        float up = Clearance(origin, Vector2.up, floor);
        float down = Clearance(origin, Vector2.down, floor);
        float left = Clearance(origin, Vector2.left, floor);
        float right = Clearance(origin, Vector2.right, floor);

        bool verticalOk = (up >= LargeTrapMinClearance) || (down >= LargeTrapMinClearance);
        bool horizontalOk = (left >= LargeTrapMinClearance) || (right >= LargeTrapMinClearance);

        return verticalOk && horizontalOk;
    }

    public static void SpawnTraps()
    {
        if (!Enabled) return;
        HeroController hero = HeroController.instance;
        if (hero == null) return;

        string currentScene = GameManager.instance?.sceneName ?? "";
        if (_surfacePoints.Count == 0 || _lastScene != currentScene)
            ScanSceneSurfaces();

        if (_cachedTraps.TryGetValue(currentScene, out var cached))
        {
            foreach (var (pos, id) in cached)
                ArchitectSpawn(id, pos);
            Plugin.Log?.LogInfo($"TrapRandomizer: 使用缓存重新生成 {currentScene} 的 {cached.Count} 个陷阱");
            return;
        }

        if (_surfacePoints.Count == 0)
        {
            Plugin.Log?.LogWarning("TrapRandomizer: 未找到任何平台表面");
            return;
        }

        int pointCount = _surfacePoints.Count;
        int trapsToSpawn = 4;
        if (pointCount >= 100) trapsToSpawn = 7;
        else if (pointCount >= 50) trapsToSpawn = 6;
        else if (pointCount >= 20) trapsToSpawn = 5;

        Random rng = new Random(_masterSeed ^ currentScene.GetHashCode());

        Vector2 roomCenter = Vector2.zero;
        foreach (var pt in _surfacePoints)
            roomCenter += new Vector2(pt.x, pt.y);
        if (_surfacePoints.Count > 0)
            roomCenter /= _surfacePoints.Count;

        List<Vector3> usedPositions = new();
        List<(Vector3 pos, string trapId)> newCache = new();

        for (int i = 0; i < trapsToSpawn; i++)
        {
            List<Vector3> validPoints = new();
            foreach (var pt in _surfacePoints)
            {
                float distToCenter = Vector2.Distance(new Vector2(pt.x, pt.y), roomCenter);
                if (distToCenter < 5f || distToCenter > 40f) continue;

                bool tooClose = false;
                foreach (var used in usedPositions)
                {
                    if (Vector2.Distance(new Vector2(pt.x, pt.y), new Vector2(used.x, used.y)) < MinDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;
                validPoints.Add(pt);
            }

            if (validPoints.Count == 0) continue;

            bool placed = false;
            for (int attempt = 0; attempt < 20 && !placed; attempt++)
            {
                int index = rng.Next(validPoints.Count);
                Vector3 chosen = validPoints[index];
                string trapId = TrapPool[rng.Next(TrapPool.Count)];

                if (LargeTraps.Contains(trapId) && !CanPlaceLargeTrap(chosen))
                    continue;

                ArchitectSpawn(trapId, chosen);
                usedPositions.Add(chosen);
                newCache.Add((chosen, trapId));
                placed = true;
            }
        }

        _cachedTraps[currentScene] = newCache;
        Plugin.Log?.LogInfo($"TrapRandomizer: {currentScene} 生成 {newCache.Count} 个陷阱 (候选点 {pointCount})");
    }

    public static void ClearAllCache()
    {
        _cachedTraps.Clear();
        Plugin.Log?.LogInfo("TrapRandomizer: 已清除所有场景陷阱缓存");
    }

    public static void ClearAndRescan()
    {
        ClearAll();
        _surfacePoints.Clear();
        _lastScene = "";
        Plugin.Log?.LogInfo("TrapRandomizer: 已清除所有陷阱并准备重新扫描");
    }

    public static void ClearAll()
    {
        foreach (var trap in ActiveTraps)
            if (trap != null) UnityEngine.Object.Destroy(trap);
        ActiveTraps.Clear();
    }

    private static void ArchitectSpawn(string id, Vector3 pos)
    {
        try
        {
            var registeredType = Type.GetType("Architect.Objects.Placeable.PlaceableObject, Architect");
            var field = registeredType?.GetField("RegisteredObjects", BindingFlags.Public | BindingFlags.Static);
            var registeredDict = field?.GetValue(null) as System.Collections.IDictionary;

            if (registeredDict == null || !registeredDict.Contains(id))
            {
                Plugin.Log?.LogWarning($"TrapRandomizer: 找不到陷阱ID '{id}'");
                return;
            }

            object placeableObj = registeredDict[id];
            var placementType = Type.GetType("Architect.Placements.ObjectPlacement, Architect");
            var configValueType = Type.GetType("Architect.Config.Types.ConfigValue, Architect");
            var emptyConfigs = configValueType != null ? Array.CreateInstance(configValueType, 0) : Array.Empty<object>();

            object placement = Activator.CreateInstance(placementType, new object[] {
                placeableObj, pos, Guid.NewGuid().ToString().Substring(0, 8),
                false, 0f, 1f, false,
                new (string,string)[0], new (string,string,int)[0], emptyConfigs
            });

            var spawnMethod = placementType.GetMethod("SpawnObject");
            GameObject obj = spawnMethod?.Invoke(placement, new object[] { Vector3.zero, null, 0f, 1f }) as GameObject;

            if (obj != null)
            {
                var pmType = Type.GetType("Architect.Placements.PlacementManager, Architect");
                var objectsField = pmType?.GetField("Objects", BindingFlags.Public | BindingFlags.Static);
                var objects = objectsField?.GetValue(null) as System.Collections.IDictionary;
                string pid = placementType.GetProperty("ID")?.GetValue(placement) as string;
                if (objects != null && pid != null) objects[pid] = obj;

                ActiveTraps.Add(obj);
                Plugin.Log?.LogInfo($"TrapRandomizer: {id} @ ({pos.x:F0}, {pos.y:F0})");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"TrapRandomizer 生成异常: {ex.Message}");
        }
    }
}