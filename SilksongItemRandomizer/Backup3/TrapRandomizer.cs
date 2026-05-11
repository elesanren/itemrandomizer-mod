using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        "slab_trap", "slab_spike_ball", "slab_prob_blade","hunter_sickle_trap",
        "bilewater_trap", "falling_spike_ball", "swing_trap_small", "swing_trap_spike",
        "dust_trap_spike_plate", "dust_trap_spike_dropper", "mite_trap",
        "organ_spikes", "cradle_spikes",
        "brown_vines", "abyss_tendrils", "void_wave",
        "mill_trap", "craw_chain",
        "frost_marker", "white_thorns", "jelly_egg", "wp_trap_spikes"
    };

    // ★ 排除场景
    private static readonly HashSet<string> ExcludedScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Belltown_04"
    };

    // ★ 禁止岩浆的场景
    private static readonly HashSet<string> NoLavaScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hang_01",
        "Hang_02",
        "Hang_10"
    };

    // ★ 大房间（Coral_35b 已加入）
    private static readonly HashSet<string> LargeRooms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Song_20",
        "Arborium_01",
        "Cog_04",
        "Song_11",
        "Song_05",
        "Song_01",
        "Coral_35b"
    };

    // ★ 大型陷阱（已移除 coral_lightning_rock）
    private static readonly HashSet<string> LargeTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "fan_hazard", "steam_vent", "mill_trap",
        "spike_cog_2", "spike_cog_3", "spike_cog_1", "spike_cog_4", "spike_cog_5", "voltgrass",
        "junk_pipe"
    };
    private const int LargeTrapRadius = 7;

    private static readonly HashSet<string> ThornTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "brown_vines", "shellwood_thorns", "white_thorns"
    };
    private const int ThornTrapMinWidth = 7;

    private const string LavaTrapId = "lava_area";

    // ★ 需要下移的尖刺陷阱
    private static readonly HashSet<string> LoweredSpikeTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "pilgrim_trap_spike",
        "organ_spikes",
        "cradle_spikes"
    };
    private const float SpikeYOffset = 1.3f;

    // ★ 刺锤陷阱
    private static readonly HashSet<string> HammerTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "slab_spike_ball"
    };
    private const int HammerTrapMinHeight = 7;

    // ★ 陷阱难度
    public enum TrapDifficulty { Beginner, Focused, Overflow }
    public static TrapDifficulty CurrentDifficulty = TrapDifficulty.Beginner;

    private static readonly List<GameObject> ActiveTraps = new();
    private static List<Vector3> _surfacePoints = new();
    private static List<Vector3> _ceilingPoints = new();
    private static string _lastScene = "";

    private static List<(Vector3 position, Bounds? bounds)> _pickupData = new();

    private static string EnabledFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "trap_enabled.txt");

    private static void SaveState()
    {
        try
        {
            string dir = Path.GetDirectoryName(EnabledFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(EnabledFilePath, $"{(_enabled ? "true" : "false")}|{CurrentDifficulty}");
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"保存陷阱状态失败: {ex}");
        }
    }

    public static void LoadState()
    {
        try
        {
            if (File.Exists(EnabledFilePath))
            {
                string[] parts = File.ReadAllText(EnabledFilePath).Trim().Split('|');
                if (parts.Length >= 1) _enabled = parts[0] == "true";
                if (parts.Length >= 2 && Enum.TryParse<TrapDifficulty>(parts[1], out var diff))
                    CurrentDifficulty = diff;
            }
        }
        catch
        {
            _enabled = false;
            CurrentDifficulty = TrapDifficulty.Beginner;
        }
    }

    private static bool _enabled = false;
    public static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            SaveState();
        }
    }

    public static void SetDifficulty(TrapDifficulty difficulty)
    {
        if (CurrentDifficulty == difficulty) return;
        CurrentDifficulty = difficulty;
        SaveState();
        ClearAllCache();
        if (Enabled)
            RespawnTraps();
    }

    private static int _masterSeed;
    private const float MinDistance = 6f;
    private const float PickupSafeRadius = 7f;

    private static List<(Vector3 center, float minX, float maxX, float waterY)> _waterRegions = new();
    private static readonly Dictionary<string, List<(Vector3 pos, string trapId)>> _cachedTraps = new();

    private static List<Vector2> _doorPositions = new();

    public static void Initialize(int seed)
    {
        _masterSeed = seed == 0 ? Environment.TickCount : seed;
        LoadState();
    }

    private static void ScanPickups()
    {
        _pickupData.Clear();
        var pickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>();
        foreach (var pickup in pickups)
        {
            if (pickup == null || !pickup.gameObject.scene.isLoaded) continue;
            Vector3 pos = pickup.transform.position;
            Bounds? bounds = null;
            var col = pickup.GetComponent<Collider2D>();
            if (col != null)
                bounds = col.bounds;
            _pickupData.Add((pos, bounds));
        }
    }

    private static void ScanSceneSurfaces()
    {
        _surfacePoints.Clear();
        _ceilingPoints.Clear();

        HeroController hero = HeroController.instance;
        Vector2 center = hero ? (Vector2)hero.transform.position : Vector2.zero;
        string currentScene = GameManager.instance?.sceneName ?? "";
        bool isLargeRoom = LargeRooms.Contains(currentScene);
        float scanWidth = isLargeRoom ? 60f : 120f;
        float scanHeight = isLargeRoom ? 120f : 80f;
        Vector2 areaMin = center + new Vector2(-scanWidth, -scanHeight);
        Vector2 areaMax = center + new Vector2(scanWidth, scanHeight);

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

                Vector2 ceilingSample = new Vector2(x, b.min.y - 0.5f);
                RaycastHit2D hitUp2 = Physics2D.Raycast(ceilingSample, Vector2.up, 0.5f, LayerMask.GetMask("Terrain"));
                if (hitUp2.collider != col) continue;
                RaycastHit2D hitDown2 = Physics2D.Raycast(ceilingSample, Vector2.down, 1.5f, LayerMask.GetMask("Terrain"));
                if (hitDown2.collider != null) continue;
                _ceilingPoints.Add(new Vector3(x, b.min.y - 0.5f, 0));
            }
        }

        _doorPositions.Clear();
        var transitionPoints = UnityEngine.Object.FindObjectsOfType<TransitionPoint>();
        foreach (var tp in transitionPoints)
            _doorPositions.Add(tp.transform.position);

        ScanPickups();
        _lastScene = GameManager.instance?.sceneName ?? "";
        Plugin.Log?.LogInfo($"TrapRandomizer: 扫描到 {_surfacePoints.Count} 个平台候选点, {_ceilingPoints.Count} 个天花板点, {_pickupData.Count} 个拾取点 on {_lastScene}");
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

    private static bool IsTooCloseToPickup(Vector3 point)
    {
        foreach (var (pos, _) in _pickupData)
        {
            if (Vector2.Distance(new Vector2(point.x, point.y), new Vector2(pos.x, pos.y)) < PickupSafeRadius)
                return true;
        }
        return false;
    }

    private static bool IsInsidePickupBounds(Vector3 point)
    {
        foreach (var (_, bounds) in _pickupData)
        {
            if (bounds.HasValue && bounds.Value.Contains(point))
                return true;
        }
        return false;
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

    // ==================== 主要生成方法 ====================
    public static void SpawnTraps()
    {
        if (!Enabled) return;
        HeroController hero = HeroController.instance;
        if (hero == null) return;

        string currentScene = GameManager.instance?.sceneName ?? "";

        if (ExcludedScenes.Contains(currentScene))
        {
            _cachedTraps.Remove(currentScene);
            Plugin.Log?.LogInfo($"TrapRandomizer: 场景 {currentScene} 已排除，跳过陷阱生成");
            return;
        }

        if (_surfacePoints.Count == 0 && _ceilingPoints.Count == 0 || _lastScene != currentScene)
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

        if (_surfacePoints.Count == 0 && _ceilingPoints.Count == 0 && _waterRegions.Count == 0)
        {
            Plugin.Log?.LogWarning("TrapRandomizer: 未找到任何平台表面或水体");
            return;
        }

        List<(Vector3 pt, bool isCeiling)> allPoints = new();
        foreach (var pt in _surfacePoints) allPoints.Add((pt, false));
        foreach (var pt in _ceilingPoints) allPoints.Add((pt, true));

        // ★ 按类型独立配额
        var categoryQuotas = new Dictionary<string, int>();
        switch (CurrentDifficulty)
        {
            case TrapDifficulty.Beginner:
                categoryQuotas["暗雷"] = 2;
                categoryQuotas["跳跳乐"] = 2;
                categoryQuotas["窄道"] = 2;
                categoryQuotas["平台类"] = 2;
                categoryQuotas["墙壁"] = 2;
                categoryQuotas["天花板"] = 2;
                categoryQuotas["障碍物"] = 3;
                categoryQuotas["装饰物"] = 3;
                categoryQuotas["触发型"] = 2;
                categoryQuotas["追逐型"] = 1;
                break;
            case TrapDifficulty.Focused:
                categoryQuotas["暗雷"] = 4;
                categoryQuotas["跳跳乐"] = 4;
                categoryQuotas["窄道"] = 4;
                categoryQuotas["平台类"] = 4;
                categoryQuotas["墙壁"] = 4;
                categoryQuotas["天花板"] = 4;
                categoryQuotas["障碍物"] = 5;
                categoryQuotas["装饰物"] = 5;
                categoryQuotas["触发型"] = 4;
                categoryQuotas["追逐型"] = 3;
                break;
            case TrapDifficulty.Overflow:
            default:
                categoryQuotas["暗雷"] = 6;
                categoryQuotas["跳跳乐"] = 6;
                categoryQuotas["窄道"] = 6;
                categoryQuotas["平台类"] = 6;
                categoryQuotas["墙壁"] = 6;
                categoryQuotas["天花板"] = 6;
                categoryQuotas["障碍物"] = 7;
                categoryQuotas["装饰物"] = 7;
                categoryQuotas["触发型"] = 6;
                categoryQuotas["追逐型"] = 5;
                break;
        }

        Random rng = new Random(_masterSeed ^ currentScene.GetHashCode());
        Vector2 roomCenter = Vector2.zero;
        foreach (var pt in _surfacePoints)
            roomCenter += new Vector2(pt.x, pt.y);
        if (_surfacePoints.Count > 0)
            roomCenter /= _surfacePoints.Count;

        float minY = float.MaxValue;
        foreach (var pt in _surfacePoints)
            if (pt.y < minY) minY = pt.y;
        if (minY == float.MaxValue) minY = 0;

        List<Vector3> usedPositions = new();
        List<(Vector3 pos, string trapId)> newCache = new();

        string[] categoryOrder = { "暗雷", "跳跳乐", "窄道", "平台类", "墙壁", "天花板", "障碍物", "装饰物", "触发型", "追逐型" };

        foreach (string category in categoryOrder)
        {
            if (!TrapPreloader.TrapCategories.TryGetValue(category, out var trapList) || trapList.Count == 0)
                continue;

            int quota = categoryQuotas.TryGetValue(category, out int q) ? q : 2;

            for (int i = 0; i < quota; i++)
            {
                List<(Vector3 pt, bool isCeiling)> validPoints = new();
                foreach (var (pt, isCeiling) in allPoints)
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

                    bool tooCloseToDoor = false;
                    foreach (var doorPos in _doorPositions)
                    {
                        if (Vector2.Distance(new Vector2(pt.x, pt.y), doorPos) < 7f)
                        { tooCloseToDoor = true; break; }
                    }
                    if (tooCloseToDoor) continue;

                    if (!isCeiling && !LargeRooms.Contains(currentScene) && pt.y - minY > 70f)
                        continue;

                    if (IsTooCloseToPickup(pt)) continue;

                    if (category == "天花板" && !isCeiling) continue;
                    if (category != "天花板" && isCeiling) continue;

                    validPoints.Add((pt, isCeiling));
                }

                if (validPoints.Count == 0)
                    continue;

                bool placed = false;
                for (int attempt = 0; attempt < 20 && !placed; attempt++)
                {
                    int index = rng.Next(validPoints.Count);
                    var (chosen, isCeiling) = validPoints[index];

                    string trapId;
                    if (rng.NextDouble() < 0.7)
                    {
                        trapId = trapList[rng.Next(trapList.Count)];
                    }
                    else
                    {
                        var broadPool = TrapPool.Where(t => t != LavaTrapId).ToList();
                        trapId = broadPool[rng.Next(broadPool.Count)];
                    }

                    if (trapId == LavaTrapId) continue;
                    if (NoLavaScenes.Contains(currentScene) && trapId == LavaTrapId) continue;

                    if (LargeTraps.Contains(trapId) && !CanPlaceLargeTrap(chosen))
                        continue;

                    if (HammerTraps.Contains(trapId) && (chosen.y - minY) < HammerTrapMinHeight)
                        continue;

                    if (ThornTraps.Contains(trapId))
                    {
                        float width = GetPlatformWidth(chosen);
                        if (width < ThornTrapMinWidth || !CanPlaceLargeTrap(chosen))
                            continue;
                        if (IsInsidePickupBounds(chosen))
                            continue;
                    }

                    ArchitectSpawn(trapId, chosen);
                    usedPositions.Add(chosen);
                    newCache.Add((chosen, trapId));
                    placed = true;
                }
            }
        }

        if (_waterRegions.Count > 0 && rng.NextDouble() < 0.3 && !NoLavaScenes.Contains(currentScene))
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
            if (!tooClose && !IsTooCloseToPickup(lavaPos))
            {
                ArchitectSpawn(LavaTrapId, lavaPos);
                usedPositions.Add(lavaPos);
                newCache.Add((lavaPos, LavaTrapId));
            }
        }

        if (newCache.Count > 0)
            _cachedTraps[currentScene] = newCache;
        else
            Plugin.Log?.LogWarning($"TrapRandomizer: {currentScene} 无法生成任何陷阱，跳过缓存");

        Plugin.Log?.LogInfo($"TrapRandomizer: {currentScene} 生成 {newCache.Count} 个陷阱，难度：{CurrentDifficulty}");
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
        _pickupData.Clear();
        _ceilingPoints.Clear();
        Plugin.Log?.LogInfo("TrapRandomizer: 已清除所有陷阱并准备重新扫描");
    }

    public static void ClearAll()
    {
        foreach (var trap in ActiveTraps)
            if (trap != null) UnityEngine.Object.Destroy(trap);
        ActiveTraps.Clear();
    }

    public static void RespawnTraps()
    {
        if (!Enabled) return;
        ClearAndRescan();
        SpawnTraps();
    }

    // ★★★ 最终修复：回到旧版架构，只改 receivers 和 broadcasters 内容 ★★★
    private static void ArchitectSpawn(string id, Vector3 pos)
    {
        try
        {
            var meta = TrapPreloader.TrapMetaDict.TryGetValue(id, out var m) ? m : new TrapMeta();

            // 位置调整
            if (LoweredSpikeTraps.Contains(id)) { pos.y -= SpikeYOffset; }
            if (id == "wp_trap_spikes") { pos.y += 5f; }
            if (LargeTraps.Contains(id)) { pos.y -= 2.3f; }

            var configValueType = Type.GetType("Architect.Config.Types.ConfigValue, Architect");
            Array configs = null;
            if (meta.Config != null && meta.Config.Count > 0)
            {
                var configList = new List<object>();
                foreach (var kvp in meta.Config)
                {
                    var cfg = Activator.CreateInstance(configValueType);
                    configValueType.GetProperty("Key")?.SetValue(cfg, kvp.Key);
                    configValueType.GetProperty("Value")?.SetValue(cfg, kvp.Value);
                    configList.Add(cfg);
                }
                configs = Array.CreateInstance(configValueType, configList.Count);
                for (int i = 0; i < configList.Count; i++)
                    configs.SetValue(configList[i], i);
            }
            else
            {
                configs = configValueType != null ? Array.CreateInstance(configValueType, 0) : Array.Empty<object>();
            }

            // 需要激活器的陷阱：生成配套触发器，并用唯一事件名配对
            string uniqueEvent = null;
            if (meta.NeedsActivator && !string.IsNullOrEmpty(meta.ActivatorId))
            {
                Vector3 activatorPos = pos + new Vector3(1.5f, 0, 0);
                string pairPid = Guid.NewGuid().ToString().Substring(0, 8);
                uniqueEvent = "Activate_" + pairPid;
                SpawnActivatorWithEvent(meta.ActivatorId, activatorPos, uniqueEvent);
            }

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

            // 构建 receivers（触发器关联）—— 唯一改动：用 uniqueEvent 和 "Activatable"
            var receivers = new (string, string, int)[0];
            if (uniqueEvent != null)
            {
                receivers = new (string, string, int)[] { (uniqueEvent, "Activatable", 0) };
            }

            // 核心：旧版 Activator.CreateInstance 调用 — 1:1 保留原始参数
            object placement = Activator.CreateInstance(placementType, new object[] {
                placeableObj, pos, Guid.NewGuid().ToString().Substring(0, 8),
                false, 0f, 1f, false,
                new (string,string)[0], receivers, configs
            });

            var spawnMethod = placementType.GetMethod("SpawnObject");
            GameObject obj = spawnMethod?.Invoke(placement, new object[] { Vector3.zero, null, 0f, 1f }) as GameObject;
            if (obj != null)
            {
                if (ThornTraps.Contains(id))
                    obj.transform.rotation = Quaternion.Euler(0, 0, 90);

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

    // ★★★ 旧版触发器生成，只加上 broadcasters ★★★
    private static void SpawnActivatorWithEvent(string activatorId, Vector3 pos, string eventName)
    {
        try
        {
            var registeredType = Type.GetType("Architect.Objects.Placeable.PlaceableObject, Architect");
            var field = registeredType?.GetField("RegisteredObjects", BindingFlags.Public | BindingFlags.Static);
            var registeredDict = field?.GetValue(null) as System.Collections.IDictionary;
            if (registeredDict == null || !registeredDict.Contains(activatorId))
            {
                Plugin.Log?.LogWarning($"TrapRandomizer: 找不到触发器ID '{activatorId}'");
                return;
            }
            object placeableObj = registeredDict[activatorId];
            var placementType = Type.GetType("Architect.Placements.ObjectPlacement, Architect");
            var configValueType = Type.GetType("Architect.Config.Types.ConfigValue, Architect");
            var emptyConfigs = configValueType != null ? Array.CreateInstance(configValueType, 0) : Array.Empty<object>();

            // ★ 唯一改动：构建 broadcasters
            var broadcasters = new (string, string)[] { (eventName, "Activatable") };

            object placement = Activator.CreateInstance(placementType, new object[] {
                placeableObj, pos, Guid.NewGuid().ToString().Substring(0, 8),
                false, 0f, 1f, false,
                broadcasters, new (string,string,int)[0], emptyConfigs
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
                Plugin.Log?.LogInfo($"TrapRandomizer: 触发器 {activatorId} @ ({pos.x:F0}, {pos.y:F0}) 事件: {eventName}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"TrapRandomizer 触发器生成异常: {ex.Message}");
        }
    }
}