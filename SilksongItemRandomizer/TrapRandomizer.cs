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
        "hunter_landmine",
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

    private static readonly HashSet<string> LargeTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "fan_hazard", "steam_vent", "coral_lightning_rock", "mill_trap",
        "spike_cog_2", "spike_cog_3", "spike_cog_1", "spike_cog_4", "spike_cog_5", "voltgrass"
    };
    private const int LargeTrapRadius = 7;

    private static readonly HashSet<string> ThornTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "brown_vines", "shellwood_thorns", "white_thorns"
    };
    private const int ThornTrapMinWidth = 7;

    private const string LavaTrapId = "lava_area";

    private static readonly List<GameObject> ActiveTraps = new();
    private static List<Vector3> _surfacePoints = new();
    private static string _lastScene = "";
    public static bool Enabled = false;
    private static int _masterSeed;
    private const float MinDistance = 6f;

    private static List<(Vector3 center, float minX, float maxX, float waterY)> _waterRegions = new();
    private static readonly Dictionary<string, List<(Vector3 pos, string trapId)>> _cachedTraps = new();

    // ★ 新增：门位置缓存，用于防止陷阱生成在门口
    private static List<Vector2> _doorPositions = new();

    public static void Initialize(int seed)
    {
        _masterSeed = seed == 0 ? Environment.TickCount : seed;
        
    }

    

    // ★ 原始物理扫描 + 收集门位置
    private static void ScanSceneSurfaces()
    {
        _surfacePoints.Clear();
        HeroController hero = HeroController.instance;
        Vector2 center = hero ? (Vector2)hero.transform.position : Vector2.zero;
        Vector2 areaMin = center + new Vector2(-120f, -80f);
        Vector2 areaMax = center + new Vector2(120f, 80f);

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
                _surfacePoints.Add(new Vector3(x, b.max.y + 0.5f, 0));
            }
        }

        // ★ 收集所有门的位置
        _doorPositions.Clear();
        var transitionPoints = UnityEngine.Object.FindObjectsOfType<TransitionPoint>();
        foreach (var tp in transitionPoints)
            _doorPositions.Add(tp.transform.position);

        _lastScene = GameManager.instance?.sceneName ?? "";
        Plugin.Log?.LogInfo($"TrapRandomizer: 扫描到 {_surfacePoints.Count} 个平台候选点 on {_lastScene}");
    }

    private static void ScanWaterRegions()
    {
        _waterRegions.Clear();
        var waters = UnityEngine.Object.FindObjectsOfType<SurfaceWaterRegion>();
        foreach (var water in waters)
        {
            BoxCollider2D col = water.GetComponent<BoxCollider2D>();
            if (col == null) continue;
            Bounds bounds = col.bounds;
            float minX = bounds.min.x;
            float maxX = bounds.max.x;
            float waterY = water.transform.position.y + 0.4f;
            _waterRegions.Add((new Vector3((minX + maxX) / 2, waterY, 0), minX, maxX, waterY));
        }
        Plugin.Log?.LogInfo($"TrapRandomizer: 扫描到 {_waterRegions.Count} 个水体区域 on {_lastScene}");
    }

    private static float GetPlatformWidth(Vector2 point)
    {
        float checkY = point.y - 0.2f;
        float left = point.x;
        for (int i = 0; i < 20; i++)
        {
            Vector2 checkPos = new Vector2(left - 0.5f, checkY);
            RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, 1f, LayerMask.GetMask("Terrain"));
            if (hit.collider != null) left -= 0.5f;
            else break;
        }
        float right = point.x;
        for (int i = 0; i < 20; i++)
        {
            Vector2 checkPos = new Vector2(right + 0.5f, checkY);
            RaycastHit2D hit = Physics2D.Raycast(checkPos, Vector2.down, 1f, LayerMask.GetMask("Terrain"));
            if (hit.collider != null) right += 0.5f;
            else break;
        }
        return right - left;
    }

    private static bool CanPlaceLargeTrap(Vector2 origin)
    {
        RaycastHit2D floorHit = Physics2D.Raycast(origin, Vector2.down, 2f, LayerMask.GetMask("Terrain"));
        Collider2D floor = floorHit.collider;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, LargeTrapRadius, LayerMask.GetMask("Terrain"));
        bool hasUpper = false, hasLower = false;
        foreach (var col in hits)
        {
            if (col == floor) continue;
            Bounds b = col.bounds;
            if (b.max.y > origin.y) hasUpper = true;
            if (b.min.y < origin.y) hasLower = true;
            if (hasUpper && hasLower) return false;
        }
        return !(hasUpper && hasLower);
    }

    public static void SpawnTraps()
    {
        if (!Enabled) return;
        HeroController hero = HeroController.instance;
        if (hero == null) return;

        string currentScene = GameManager.instance?.sceneName ?? "";
        if (_surfacePoints.Count == 0 || _lastScene != currentScene)
        {
            ScanSceneSurfaces();
            ScanWaterRegions();
        }

        if (_cachedTraps.TryGetValue(currentScene, out var cached))
        {
            foreach (var (pos, id) in cached)
                ArchitectSpawn(id, pos);
            Plugin.Log?.LogInfo($"TrapRandomizer: 使用缓存重新生成 {currentScene} 的 {cached.Count} 个陷阱");
            return;
        }

        if (_surfacePoints.Count == 0 && _waterRegions.Count == 0)
        {
            Plugin.Log?.LogWarning("TrapRandomizer: 未找到任何平台表面或水体");
            return;
        }

        int pointCount = _surfacePoints.Count;
        int trapsToSpawn = 4;
        if (pointCount >= 100) trapsToSpawn = 8;
        else if (pointCount >= 50) trapsToSpawn = 7;
        else if (pointCount >= 20) trapsToSpawn = 5;

        Random rng = new Random(_masterSeed ^ currentScene.GetHashCode());
        Vector2 roomCenter = Vector2.zero;
        foreach (var pt in _surfacePoints)
            roomCenter += new Vector2(pt.x, pt.y);
        if (_surfacePoints.Count > 0)
            roomCenter /= _surfacePoints.Count;

        // ★ 计算所有候选点的最低 Y 坐标（用于排除过高的天花板平台）
        float minY = float.MaxValue;
        foreach (var pt in _surfacePoints)
            if (pt.y < minY) minY = pt.y;

        List<Vector3> usedPositions = new();
        List<(Vector3 pos, string trapId)> newCache = new();

        for (int i = 0; i < trapsToSpawn; i++)
        {
            if (_waterRegions.Count > 0 && rng.NextDouble() < 0.3)
            {
                var water = _waterRegions[rng.Next(_waterRegions.Count)];
                float x = (float)(rng.NextDouble() * (water.maxX - water.minX) + water.minX);
                Vector3 lavaPos = new Vector3(x, water.waterY - 3f, 0);
                bool tooClose = false;
                foreach (var used in usedPositions)
                {
                    if (Vector2.Distance(new Vector2(lavaPos.x, lavaPos.y), new Vector2(used.x, used.y)) < MinDistance)
                    { tooClose = true; break; }
                }
                if (!tooClose)
                {
                    ArchitectSpawn(LavaTrapId, lavaPos);
                    usedPositions.Add(lavaPos);
                    newCache.Add((lavaPos, LavaTrapId));
                    continue;
                }
            }

            List<Vector3> validPoints = new();
            foreach (var pt in _surfacePoints)
            {
                float distToCenter = Vector2.Distance(new Vector2(pt.x, pt.y), roomCenter);
                if (distToCenter < 1.5f) continue;
                bool tooClose = false;
                foreach (var used in usedPositions)
                {
                    if (Vector2.Distance(new Vector2(pt.x, pt.y), new Vector2(used.x, used.y)) < MinDistance)
                    { tooClose = true; break; }
                }
                if (tooClose) continue;

                // 排除离门7格内的点
                bool tooCloseToDoor = false;
                foreach (var doorPos in _doorPositions)
                {
                    if (Vector2.Distance(new Vector2(pt.x, pt.y), doorPos) < 7f)
                    {
                        tooCloseToDoor = true;
                        break;
                    }
                }
                if (tooCloseToDoor) continue;

                // ★ 排除纵坐标过高的点（距离最低点超过70格）
                if (pt.y - minY > 70f) continue;

                validPoints.Add(pt);
            }

            if (validPoints.Count == 0) continue;

            bool placed = false;
            for (int attempt = 0; attempt < 20 && !placed; attempt++)
            {
                int index = rng.Next(validPoints.Count);
                Vector3 chosen = validPoints[index];
                string trapId = TrapPool[rng.Next(TrapPool.Count)];
                if (trapId == LavaTrapId) continue;
                if (LargeTraps.Contains(trapId) && !CanPlaceLargeTrap(chosen)) continue;
                if (ThornTraps.Contains(trapId))
                {
                    float platformWidth = GetPlatformWidth(chosen);
                    if (platformWidth < ThornTrapMinWidth || !CanPlaceLargeTrap(chosen)) continue;
                }
                ArchitectSpawn(trapId, chosen);
                usedPositions.Add(chosen);
                newCache.Add((chosen, trapId));
                placed = true;
            }
        }

        if (newCache.Count > 0)
            _cachedTraps[currentScene] = newCache;
        else
            Plugin.Log?.LogWarning($"TrapRandomizer: {currentScene} 无法生成任何陷阱，跳过缓存");

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
        _waterRegions.Clear();
        _doorPositions.Clear();
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