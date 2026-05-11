using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using UnityEngine;
using Random = System.Random;

namespace SilksongItemRandomizer;

public static class TrapRandomizer
{
    public static TrapPreloader.TrapDifficulty CurrentDifficulty = TrapPreloader.TrapDifficulty.Beginner;

    private static readonly List<GameObject> ActiveTraps = new();
    private static List<Vector3> _surfacePoints = new();
    private static List<Vector3> _ceilingPoints = new();
    private static string _lastScene = "";
    private static List<(Vector3 pos, Bounds? bounds)> _pickupData = new();
    private static List<(Vector3 center, float minX, float maxX, float waterY)> _waterRegions = new();
    private static readonly Dictionary<string, List<(Vector3, string)>> _cachedTraps = new();
    private static List<Vector2> _doorPositions = new();
    private static readonly HashSet<string> FrostBannedScenes = new(StringComparer.OrdinalIgnoreCase);
    private static int _masterSeed;
    private static bool _enabled;
    private static bool _movementEnabled = true;
    private static bool _lastSceneHadFrost = false; // ★ 冰冻相邻限制

    private static string EnabledFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "trap_enabled.txt");
    private static string MovementEnabledFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "trap_movement_enabled.txt");
    private static string FrostBannedFilePath => Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "frost_banned.txt");

    public static bool Enabled
    {
        get => _enabled;
        set { if (_enabled == value) return; _enabled = value; SaveState(); }
    }

    public static bool MovementEnabled
    {
        get => _movementEnabled;
        set { if (_movementEnabled == value) return; _movementEnabled = value; SaveMovementState(); }
    }

    public static void Initialize(int seed)
    {
        _masterSeed = (seed == 0) ? Environment.TickCount : seed;
        LoadState();
        LoadMovementState();
        LoadFrostBannedScenes();
    }

    public static void SetDifficulty(TrapPreloader.TrapDifficulty d)
    {
        if (CurrentDifficulty == d) return;
        CurrentDifficulty = d;
        SaveState();
        ClearAllCache();
        if (Enabled) RespawnTraps();
    }

    private static void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(EnabledFilePath)!);
            File.WriteAllText(EnabledFilePath, $"{_enabled}|{CurrentDifficulty}");
        }
        catch (Exception e) { Plugin.Log?.LogError($"save trap state err: {e.Message}"); }
    }

    public static void LoadState()
    {
        try
        {
            if (File.Exists(EnabledFilePath))
            {
                var parts = File.ReadAllText(EnabledFilePath).Trim().Split('|');
                if (parts.Length >= 1) _enabled = parts[0] == "true";
                if (parts.Length >= 2 && Enum.TryParse<TrapPreloader.TrapDifficulty>(parts[1], out var diff)) CurrentDifficulty = diff;
            }
        }
        catch { _enabled = false; }
    }

    private static void LoadMovementState()
    {
        try
        {
            if (File.Exists(MovementEnabledFilePath))
                _movementEnabled = File.ReadAllText(MovementEnabledFilePath).Trim() == "true";
        }
        catch { _movementEnabled = true; }
    }

    private static void SaveMovementState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MovementEnabledFilePath)!);
            File.WriteAllText(MovementEnabledFilePath, _movementEnabled.ToString());
        }
        catch (Exception e) { Plugin.Log?.LogError($"save movement state err: {e.Message}"); }
    }

    private static void LoadFrostBannedScenes()
    {
        FrostBannedScenes.Clear();
        if (!File.Exists(FrostBannedFilePath)) return;
        foreach (var line in File.ReadAllLines(FrostBannedFilePath))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed)) FrostBannedScenes.Add(trimmed);
        }
    }

    private static void SaveFrostBannedScenes()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FrostBannedFilePath)!);
        File.WriteAllLines(FrostBannedFilePath, FrostBannedScenes);
    }

    public static void ResetFrostBannedScenes()
    {
        FrostBannedScenes.Clear();
        if (File.Exists(FrostBannedFilePath)) File.Delete(FrostBannedFilePath);
    }

    private static void ScanPickups()
    {
        _pickupData.Clear();
        foreach (var p in Resources.FindObjectsOfTypeAll<CollectableItemPickup>())
        {
            if (!p || !p.gameObject.scene.isLoaded) continue;
            var c = p.GetComponent<Collider2D>();
            _pickupData.Add((p.transform.position, c ? c.bounds : (Bounds?)null));
        }
    }

    private static void ScanSceneSurfaces()
    {
        _surfacePoints.Clear();
        _ceilingPoints.Clear();
        var hero = HeroController.instance;
        var center = hero ? (Vector2)hero.transform.position : Vector2.zero;
        var scene = GameManager.instance?.sceneName ?? "";
        bool isLarge = TrapPreloader.LargeRooms.Contains(scene);
        float sw = isLarge ? 60f : 120f, sh = isLarge ? 120f : 80f;
        var min = center + new Vector2(-sw, -sh);
        var max = center + new Vector2(sw, sh);
        var cols = Physics2D.OverlapAreaAll(min, max, LayerMask.GetMask("Terrain"));
        foreach (var col in cols)
        {
            var b = col.bounds;
            if (b.size.y > b.size.x * 2f || b.min.y > center.y + 120f) continue;
            for (float x = b.min.x + 1f; x <= b.max.x - 1f; x += 2f)
            {
                var sPt = new Vector2(x, b.max.y + 0.5f);
                if (Physics2D.Raycast(sPt, Vector2.up, 1.5f, LayerMask.GetMask("Terrain")).collider) continue;
                if (Physics2D.Raycast(sPt, Vector2.down, 0.5f, LayerMask.GetMask("Terrain")).collider != col) continue;
                _surfacePoints.Add(new Vector3(x, b.max.y + 0.5f, 0));

                var cPt = new Vector2(x, b.min.y - 0.5f);
                if (Physics2D.Raycast(cPt, Vector2.up, 0.5f, LayerMask.GetMask("Terrain")).collider != col) continue;
                if (Physics2D.Raycast(cPt, Vector2.down, 1.5f, LayerMask.GetMask("Terrain")).collider) continue;
                _ceilingPoints.Add(new Vector3(x, b.min.y - 0.5f, 0));
            }
        }
        _doorPositions.Clear();
        foreach (var tp in UnityEngine.Object.FindObjectsOfType<TransitionPoint>()) _doorPositions.Add(tp.transform.position);
        ScanPickups();
        _lastScene = scene;
    }

    private static void ScanWaterRegions()
    {
        _waterRegions.Clear();
        foreach (var w in UnityEngine.Object.FindObjectsOfType<SurfaceWaterRegion>())
        {
            var col = w.GetComponent<BoxCollider2D>();
            if (!col) continue;
            var b = col.bounds;
            _waterRegions.Add((new Vector3((b.min.x + b.max.x) / 2, w.transform.position.y + 0.4f, 0), b.min.x, b.max.x, w.transform.position.y + 0.4f));
        }
    }

    private static float GetPlatformWidth(Vector2 pt)
    {
        float checkY = pt.y - 0.2f, left = pt.x;
        for (int i = 0; i < 20; i++)
        {
            var hit = Physics2D.Raycast(new Vector2(left - 0.5f, checkY), Vector2.down, 1f, LayerMask.GetMask("Terrain"));
            if (hit.collider) left -= 0.5f; else break;
        }
        float right = pt.x;
        for (int i = 0; i < 20; i++)
        {
            var hit = Physics2D.Raycast(new Vector2(right + 0.5f, checkY), Vector2.down, 1f, LayerMask.GetMask("Terrain"));
            if (hit.collider) right += 0.5f; else break;
        }
        return right - left;
    }

    private static bool IsTooCloseToPickup(Vector3 p) => _pickupData.Any(x => Vector2.Distance(p, x.pos) < TrapPreloader.PickupSafeRadius);
    private static bool IsInsidePickupBounds(Vector3 p) => _pickupData.Any(x => x.bounds.HasValue && x.bounds.Value.Contains(p));
    private static bool CanPlaceLargeTrap(Vector2 origin)
    {
        var floor = Physics2D.Raycast(origin, Vector2.down, 2f, LayerMask.GetMask("Terrain")).collider;
        var hits = Physics2D.OverlapCircleAll(origin, TrapPreloader.LargeTrapRadius, LayerMask.GetMask("Terrain"));
        bool upper = false, lower = false;
        foreach (var c in hits)
        {
            if (c == floor) continue;
            var b = c.bounds;
            if (b.max.y > origin.y) upper = true;
            if (b.min.y < origin.y) lower = true;
            if (upper && lower) return false;
        }
        return !(upper && lower);
    }

    private static bool IsTrapAllowed(string id, Vector3 pos, string scene, float minY, bool isCeiling, string category)
    {
        if (id == TrapPreloader.LavaTrapId && TrapPreloader.NoLavaScenes.Contains(scene)) return false;
        if (id == TrapPreloader.LavaTrapId)
        {
            bool onWater = _waterRegions.Any(w =>
                pos.x >= w.minX && pos.x <= w.maxX &&
                Mathf.Abs(pos.y - (w.waterY - 3f)) < 0.5f);
            if (!onWater) return false;
        }
        if (id == TrapPreloader.FallingLavaId && _doorPositions.Any(d => Mathf.Abs(pos.x - d.x) < 9f)) return false;
        if (id == "frost_marker" && FrostBannedScenes.Contains(scene)) return false;
        if (TrapPreloader.LargeTraps.Contains(id) && !CanPlaceLargeTrap(pos)) return false;
        if (TrapPreloader.HammerTraps.Contains(id) && (pos.y - minY) < TrapPreloader.HammerTrapMinHeight) return false;
        if (TrapPreloader.ThornTraps.Contains(id))
        {
            if (GetPlatformWidth(pos) < TrapPreloader.ThornTrapMinWidth || !CanPlaceLargeTrap(pos) || IsInsidePickupBounds(pos)) return false;
        }
        if (id == "cradle_spikes" && GetPlatformWidth(pos) < 9f) return false;
        if (category == "天花板" && !isCeiling) return false;
        if (category != "天花板" && isCeiling) return false;
        return true;
    }

    public static void SpawnTraps()
    {
        if (!Enabled) return;
        var hero = HeroController.instance;
        if (!hero) return;
        var scene = GameManager.instance?.sceneName ?? "";
        if (TrapPreloader.ExcludedScenes.Contains(scene)) { _cachedTraps.Remove(scene); return; }

        // ★ 冰冻相邻限制：上一场景有冰冻则清除当前场景的冰冻
        if (_lastSceneHadFrost)
        {
            for (int i = ActiveTraps.Count - 1; i >= 0; i--)
            {
                var t = ActiveTraps[i];
                if (t != null && t.name.Contains("frost_marker"))
                {
                    UnityEngine.Object.Destroy(t);
                    ActiveTraps.RemoveAt(i);
                }
            }
            if (_cachedTraps.TryGetValue(scene, out var cache))
            {
                cache.RemoveAll(c => c.Item2 == "frost_marker");
                if (cache.Count == 0) _cachedTraps.Remove(scene);
            }
        }

        if ((_surfacePoints.Count == 0 && _ceilingPoints.Count == 0) || _lastScene != scene) { ScanSceneSurfaces(); ScanWaterRegions(); }

        var rng = new Random(_masterSeed ^ scene.GetHashCode());
        float minY = _surfacePoints.Count > 0 ? _surfacePoints.Min(p => p.y) : 0f;
        double frostProb = TrapPreloader.GetFrostProbability(CurrentDifficulty);

        var allPoints = new List<(Vector3 pt, bool isCeiling)>();
        foreach (var p in _surfacePoints) allPoints.Add((p, false));
        foreach (var p in _ceilingPoints) allPoints.Add((p, true));

        var quotas = TrapPreloader.GetCategoryQuotas(CurrentDifficulty);

        // ---------- 处理缓存 ----------
        if (_cachedTraps.TryGetValue(scene, out var cacheFromDict))
        {
            if (FrostBannedScenes.Contains(scene))
                cacheFromDict = cacheFromDict.Where(c => c.Item2 != "frost_marker").ToList();

            var validated = new List<(Vector3 pos, string trapId)>();
            var tempUsed = new List<Vector3>();
            foreach (var (pos, trapId) in cacheFromDict)
            {
                bool isCeiling = _ceilingPoints.Any(p => Vector3.Distance(p, pos) < 0.5f);
                if (tempUsed.Any(u => Vector3.Distance(pos, u) < TrapPreloader.MinDistance))
                    continue;
                string cat = "";
                foreach (var c in TrapPreloader.CategoryOrder)
                    if (TrapPreloader.TrapCategories.TryGetValue(c, out var pool) && pool.Contains(trapId))
                    { cat = c; break; }
                if (!IsTrapAllowed(trapId, pos, scene, minY, isCeiling, cat))
                    continue;
                validated.Add((pos, trapId));
                tempUsed.Add(pos);
            }

            int totalQuota = 0;
            foreach (var cat in TrapPreloader.CategoryOrder)
            {
                if (cat == "场景伤害") continue;
                totalQuota += quotas.TryGetValue(cat, out var q) ? q : 0;
            }

            if (validated.Count >= totalQuota * 0.8f)
            {
                // ★ 缓存打乱：避免同类陷阱并列排列
                for (int i = validated.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var tmp = validated[i];
                    validated[i] = validated[j];
                    validated[j] = tmp;
                }

                var validatedCache = new List<(Vector3, string)>();
                foreach (var (pos, trapId) in validated)
                {
                    ArchitectSpawn(trapId, pos);
                    validatedCache.Add((pos, trapId));
                }
                _cachedTraps[scene] = validatedCache;
                _lastSceneHadFrost = validatedCache.Any(c => c.Item2 == "frost_marker");
                return;
            }
            else
            {
                _cachedTraps.Remove(scene);
            }
        }

        // ---------- 全新生成 ----------
        var selected = new List<(string trapId, string category)>();
        foreach (var cat in TrapPreloader.CategoryOrder)
        {
            if (!TrapPreloader.TrapCategories.TryGetValue(cat, out var pool) || pool.Count == 0) continue;
            int quota = quotas.TryGetValue(cat, out var q) ? q : 0;
            for (int i = 0; i < quota; i++)
            {
                if (cat == "场景伤害" && _waterRegions.Count == 0) continue;
                string trapId;
                if (cat == "场景伤害")
                    trapId = pool[rng.Next(pool.Count)];
                else if (rng.NextDouble() < 0.7)
                    trapId = pool[rng.Next(pool.Count)];
                else
                    trapId = TrapPreloader.TrapPoolNoLava[rng.Next(TrapPreloader.TrapPoolNoLava.Count)];

                if (trapId == "frost_marker")
                {
                    if (_lastSceneHadFrost) continue;
                    if (FrostBannedScenes.Contains(scene)) continue;
                    if (frostProb <= 0.0 || rng.NextDouble() >= frostProb) continue;
                }
                selected.Add((trapId, cat));
            }
        }

        if (_waterRegions.Count > 0 && rng.NextDouble() < 0.3 && !TrapPreloader.NoLavaScenes.Contains(scene))
            selected.Add((TrapPreloader.LavaTrapId, "场景伤害"));

        if (frostProb > 0 && rng.NextDouble() < frostProb && !FrostBannedScenes.Contains(scene) && !_lastSceneHadFrost)
            selected.Add(("frost_marker", "场景伤害"));

        for (int i = selected.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = selected[i];
            selected[i] = selected[j];
            selected[j] = tmp;
        }

        var usedPositions = new List<Vector3>();
        var newCache = new List<(Vector3, string)>();
        bool roomHasFrost = false;

        foreach (var (trapId, category) in selected)
        {
            bool isCeilingTrap = (category == "天花板");
            var validPoints = new List<Vector3>();
            foreach (var (pt, isCeil) in allPoints)
            {
                if (isCeilingTrap && !isCeil) continue;
                if (!isCeilingTrap && isCeil) continue;
                if (usedPositions.Any(u => Vector2.Distance(pt, u) < TrapPreloader.MinDistance)) continue;
                if (_doorPositions.Any(d => Vector2.Distance(pt, d) < 7f)) continue;
                if (IsTooCloseToPickup(pt)) continue;
                if (!IsTrapAllowed(trapId, pt, scene, minY, isCeil, category)) continue;
                validPoints.Add(pt);
            }
            if (validPoints.Count == 0) continue;
            var chosen = validPoints[rng.Next(validPoints.Count)];

            ArchitectSpawn(trapId, chosen);
            usedPositions.Add(chosen);
            newCache.Add((chosen, trapId));
            if (trapId == "frost_marker") roomHasFrost = true;
        }

        _lastSceneHadFrost = roomHasFrost;

        if (newCache.Count > 0) _cachedTraps[scene] = newCache;
    }

    private static HashSet<string> GetConnectedScenes(string scene)
    {
        var conn = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var file = Path.Combine(Paths.PluginPath, "roomrando_connections.txt");
        if (!File.Exists(file)) return conn;
        foreach (var line in File.ReadAllLines(file))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var parts = line.Split('|');
            if (parts.Length == 4)
            {
                if (parts[0].Equals(scene, StringComparison.OrdinalIgnoreCase)) conn.Add(parts[2]);
                else if (parts[2].Equals(scene, StringComparison.OrdinalIgnoreCase)) conn.Add(parts[0]);
            }
        }
        return conn;
    }

    public static void ClearAllCache() => _cachedTraps.Clear();
    public static void ClearAndRescan()
    {
        ClearAll();
        _surfacePoints.Clear();
        _ceilingPoints.Clear();
        _waterRegions.Clear();
        _doorPositions.Clear();
        _pickupData.Clear();
        _lastScene = "";
    }

    public static void ClearAll()
    {
        foreach (var t in ActiveTraps) if (t) UnityEngine.Object.Destroy(t);
        ActiveTraps.Clear();
    }

    public static void RespawnTraps()
    {
        if (!Enabled) return;
        ClearAndRescan();
        SpawnTraps();
    }

    private static void ArchitectSpawn(string id, Vector3 pos)
    {
        try
        {
            var meta = TrapPreloader.TrapMetaDict.TryGetValue(id, out var m) ? m : new TrapMeta();

            Vector3 trapPos = pos;
            if (!meta.NeedsActivator)
            {
                if (TrapPreloader.LoweredSpikeTraps.Contains(id)) trapPos.y -= TrapPreloader.SpikeYOffset;
                if (id == "wp_trap_spikes") trapPos.y += 5f;
                if (TrapPreloader.LargeTraps.Contains(id)) trapPos.y -= 2.3f;
                if (TrapPreloader.ThornTraps.Contains(id)) trapPos.y -= 2.5f;
            }

            string uniqueEvent = null;
            Vector3 activatorPos = Vector3.zero;
            bool hasActivator = meta.NeedsActivator && !string.IsNullOrEmpty(meta.ActivatorId);

            if (hasActivator)
            {
                if (id == "swing_trap_spike")
                {
                    var s = _surfacePoints.OrderBy(p => Mathf.Abs(p.x - pos.x)).FirstOrDefault();
                    activatorPos = s != null ? new Vector3(pos.x, s.y, 0) : pos + new Vector3(1.5f, 0, 0);
                }
                else
                {
                    activatorPos = pos + new Vector3(1.5f, 0, 0);
                }

                uniqueEvent = "Activate_" + Guid.NewGuid().ToString().Substring(0, 8);
                SpawnActivatorWithEvent(meta.ActivatorId, activatorPos, uniqueEvent);

                if (meta.PositionOffset != Vector3.zero)
                    trapPos = activatorPos + meta.PositionOffset;
                else
                    trapPos = pos;
            }

            Array configs = null;
            var configValueType = Type.GetType("Architect.Config.Types.ConfigValue, Architect");
            if (meta.Config?.Count > 0 && configValueType != null)
            {
                var deser = Type.GetType("Architect.Config.ConfigurationManager, Architect")?.GetMethod("DeserializeConfigValue", BindingFlags.Public | BindingFlags.Static);
                if (deser != null)
                {
                    var list = new List<object>();
                    foreach (var kv in meta.Config) { var c = deser.Invoke(null, new object[] { kv.Key, kv.Value }); if (c != null) list.Add(c); }
                    if (list.Count > 0)
                    {
                        configs = Array.CreateInstance(configValueType, list.Count);
                        for (int i = 0; i < list.Count; i++) configs.SetValue(list[i], i);
                    }
                }
            }
            if (configs == null) configs = configValueType != null ? Array.CreateInstance(configValueType, 0) : Array.Empty<object>();

            var regType = Type.GetType("Architect.Objects.Placeable.PlaceableObject, Architect");
            var dict = regType?.GetField("RegisteredObjects", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as System.Collections.IDictionary;
            if (dict == null || !dict.Contains(id)) return;
            var placementType = Type.GetType("Architect.Placements.ObjectPlacement, Architect");
            var receivers = uniqueEvent != null ? new (string, string, int)[] { (uniqueEvent, "activate_trap", 0) } : new (string, string, int)[0];

            var placement = Activator.CreateInstance(placementType, new object[] {
                dict[id], trapPos, Guid.NewGuid().ToString().Substring(0, 8),
                false, 0f, 1f, false, 0,
                new (string,string)[0], receivers, configs
            });
            var spawn = placementType.GetMethod("SpawnObject");
            var obj = spawn?.Invoke(placement, new object[] { Vector3.zero, null, 0f, 1f, false }) as GameObject;

            if (obj)
            {
                if (TrapPreloader.ThornTraps.Contains(id)) obj.transform.rotation = Quaternion.Euler(0, 0, 90);
                if (meta.PositionRotate != Vector3.zero)
                    obj.transform.rotation *= Quaternion.Euler(meta.PositionRotate);

                var pmType = Type.GetType("Architect.Placements.PlacementManager, Architect");
                var objDict = pmType?.GetField("Objects", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as System.Collections.IDictionary;
                var pid = placementType.GetProperty("ID")?.GetValue(placement) as string;
                if (objDict != null && pid != null) objDict[pid] = obj;
                ActiveTraps.Add(obj);

                if (obj != null && MovementEnabled)
                {
                    var moveRng = new Random(_masterSeed ^ id.GetHashCode() ^ pos.GetHashCode());
                    TrapMovement.ApplyTrapMovement(obj, id, moveRng);
                }
            }
        }
        catch (Exception e) { Plugin.Log?.LogError($"ArchitectSpawn err: {e.Message}"); }
    }

    private static void SpawnActivatorWithEvent(string activatorId, Vector3 pos, string eventName)
    {
        try
        {
            var regType = Type.GetType("Architect.Objects.Placeable.PlaceableObject, Architect");
            var dict = regType?.GetField("RegisteredObjects", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as System.Collections.IDictionary;
            if (dict == null || !dict.Contains(activatorId)) return;
            var placementType = Type.GetType("Architect.Placements.ObjectPlacement, Architect");
            var configValueType = Type.GetType("Architect.Config.Types.ConfigValue, Architect");
            var emptyConfigs = configValueType != null ? Array.CreateInstance(configValueType, 0) : Array.Empty<object>();
            string triggerName = activatorId == "trigger_zone" ? "ZoneEnter" : "OnActivate";

            var placement = Activator.CreateInstance(placementType, new object[] {
                dict[activatorId], pos, Guid.NewGuid().ToString().Substring(0, 8),
                false, 0f, 1f, false, 0,
                new (string, string)[] { (triggerName, eventName) },
                new (string,string,int)[0], emptyConfigs
            });
            var spawn = placementType.GetMethod("SpawnObject");
            var obj = spawn?.Invoke(placement, new object[] { Vector3.zero, null, 0f, 1f, false }) as GameObject;
            if (obj)
            {
                var pmType = Type.GetType("Architect.Placements.PlacementManager, Architect");
                var objDict = pmType?.GetField("Objects", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as System.Collections.IDictionary;
                var pid = placementType.GetProperty("ID")?.GetValue(placement) as string;
                if (objDict != null && pid != null) objDict[pid] = obj;
            }
        }
        catch (Exception e) { Plugin.Log?.LogError($"SpawnActivator err: {e.Message}"); }
    }
}