using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SilksongItemRandomizer
{
    public static class ToolEffectRandomizer
    {
        private static readonly string FilePath = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer", "crest_effects.json");
        private static Dictionary<string, Dictionary<string, float>> _crestMultipliers = new();
        private static bool _initialized = false;
        private static System.Random _rng;
        private static Dictionary<string, float> _originalHeroValues = new();
        private static bool _enabled = false;

        // 需要修改的英雄属性列表
        private static readonly List<string> HeroFields = new List<string>
        {
            "attack_cooldown", "throwToolCooldown", "NAIL_CHARGE_TIME", "NAIL_CHARGE_TIME_QUICK",
            "RUN_SPEED", "WALK_SPEED", "DASH_SPEED", "DASH_TIME", "AIR_DASH_TIME",
            "MAX_FALL_VELOCITY"
        };

        // 温和版倍率范围：冷却类负面 1.2~2.0，速度/下落类负面 0.7~0.9
        private static readonly Dictionary<string, (float positive, float negativeMin, float negativeMax)> FieldRanges = new()
        {
            ["attack_cooldown"] = (0.25f, 1.2f, 2.0f),
            ["throwToolCooldown"] = (0.25f, 1.2f, 2.0f),
            ["NAIL_CHARGE_TIME"] = (0.25f, 1.2f, 2.0f),
            ["NAIL_CHARGE_TIME_QUICK"] = (0.25f, 1.2f, 2.0f),
            ["RUN_SPEED"] = (1.5f, 0.7f, 0.9f),
            ["WALK_SPEED"] = (1.5f, 0.7f, 0.9f),
            ["DASH_SPEED"] = (1.5f, 0.7f, 0.9f),
            ["DASH_TIME"] = (1.5f, 0.7f, 0.9f),
            ["AIR_DASH_TIME"] = (1.5f, 0.7f, 0.9f),
            ["MAX_FALL_VELOCITY"] = (1.5f, 0.7f, 0.9f)
        };

        // 缓存 HeroController 字段
        private static Dictionary<string, FieldInfo> _heroFieldCache;

        static ToolEffectRandomizer()
        {
            _heroFieldCache = new Dictionary<string, FieldInfo>();
            var heroType = typeof(HeroController);
            foreach (var fieldName in HeroFields)
            {
                var field = heroType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    _heroFieldCache[fieldName] = field;
            }
        }

        public static void Initialize(int seed)
        {
            _rng = new System.Random(seed ^ 0x7E57C0E);
            LoadEffects();
            _initialized = true;

            // 开局强制保存原始值并重置所有英雄属性为1倍（确保无诅咒残留）
            SaveOriginalHeroValues();
            ResetHeroToOriginal();
        }

        private static void LoadEffects()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _crestMultipliers = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, float>>>(json) ?? new();
                }
                else
                {
                    _crestMultipliers.Clear();
                }
            }
            catch { _crestMultipliers.Clear(); }
        }

        private static void SaveEffects()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(_crestMultipliers, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        private static void GenerateAllCrestMultipliers()
        {
            var allCrests = Resources.FindObjectsOfTypeAll<ToolCrest>()
                .Where(c => c != null && !string.IsNullOrEmpty(c.name))
                .Select(c => c.name)
                .Distinct()
                .ToList();
            if (allCrests.Count == 0) return;

            foreach (var crestId in allCrests)
            {
                if (_crestMultipliers.ContainsKey(crestId)) continue;

                var shuffled = HeroFields.OrderBy(x => _rng.Next()).ToList();
                var positiveFields = shuffled.Take(3).ToList();

                var mults = new Dictionary<string, float>();
                foreach (var field in HeroFields)
                {
                    var range = FieldRanges[field];
                    double r = _rng.NextDouble();
                    float mult = (float)(range.negativeMin + r * (range.negativeMax - range.negativeMin));
                    mults[field] = mult;
                }
                foreach (var field in positiveFields)
                {
                    if (FieldRanges.TryGetValue(field, out var range))
                        mults[field] = range.positive;
                }
                _crestMultipliers[crestId] = mults;
            }
            SaveEffects();
        }

        private static void SaveOriginalHeroValues()
        {
            if (_originalHeroValues.Count > 0) return;
            HeroController hero = HeroController.instance;
            if (hero == null) return;

            foreach (var kv in _heroFieldCache)
            {
                try
                {
                    float val = (float)kv.Value.GetValue(hero);
                    _originalHeroValues[kv.Key] = val;
                }
                catch { }
            }
        }

        private static void ResetHeroToOriginal()
        {
            if (_originalHeroValues.Count == 0) return;
            HeroController hero = HeroController.instance;
            if (hero == null) return;

            foreach (var kv in _originalHeroValues)
            {
                if (_heroFieldCache.TryGetValue(kv.Key, out var field))
                    field.SetValue(hero, kv.Value);
            }
        }

        public static void ApplyCrestEffects(string crestId)
        {
            if (!_enabled) return;
            if (!_initialized || !Plugin.ItemRandomEnabled.Value) return;
            if (string.IsNullOrEmpty(crestId)) return;

            HeroController hero = HeroController.instance;
            if (hero == null) return;

            SaveOriginalHeroValues();

            if (_crestMultipliers.Count == 0)
                GenerateAllCrestMultipliers();

            if (!_crestMultipliers.TryGetValue(crestId, out var mults)) return;

            foreach (var kv in _heroFieldCache)
            {
                string fieldName = kv.Key;
                var field = kv.Value;
                if (!_originalHeroValues.TryGetValue(fieldName, out float original)) continue;
                float mult = mults[fieldName];
                float newValue = original * mult;
                if (fieldName.Contains("Cooldown") || fieldName.Contains("Time"))
                    newValue = Mathf.Max(0.05f, newValue);
                if (fieldName.Contains("SPEED"))
                    newValue = Mathf.Max(0.3f, newValue);
                field.SetValue(hero, newValue);
            }
        }

        public static void RemoveCrestEffects()
        {
            if (_originalHeroValues.Count == 0) return;
            HeroController hero = HeroController.instance;
            if (hero == null) return;

            foreach (var kv in _originalHeroValues)
            {
                if (_heroFieldCache.TryGetValue(kv.Key, out var field))
                    field.SetValue(hero, kv.Value);
            }
        }

        public static void ApplyCurrentCrest()
        {
            if (!_enabled) return;
            var pd = PlayerData.instance;
            if (pd != null && !string.IsNullOrEmpty(pd.CurrentCrestID))
                ApplyCrestEffects(pd.CurrentCrestID);
        }

        public static void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;

            if (!_enabled)
            {
                RemoveCrestEffects();
                ResetHeroToOriginal();
            }
            else
            {
                ApplyCurrentCrest();
            }
        }

        public static void ResetAllEffects()
        {
            _crestMultipliers.Clear();
            if (File.Exists(FilePath)) File.Delete(FilePath);
            _originalHeroValues.Clear();
        }
    }
}