// TrapPreloader.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilksongItemRandomizer;

public static class TrapPreloader
{
    public enum TrapDifficulty { Beginner, Focused, Overflow }

    // 完整陷阱池
    public static readonly List<string> TrapPool = new()
    {
        "fan_hazard", "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5",
        "hot_coal", "lava_area", "falling_lava", "bone_boulder",
        "hunter_landmine", "pilgrim_trap_spike", "wisp_flame_lantern",
        "falling_bell", "shellwood_thorns",
        "coral_lightning_rock", "coral_lightning_orb", "voltgrass",
        "coral_crust_s", "coral_crust_m", "coral_crust_l",
        "coral_spike", "coral_spike_fall", "stomp_spire",
        "rubble_field", "steam_vent", "junk_pipe",
        "slab_trap", "slab_spike_ball", "slab_prob_blade", "hunter_sickle_trap",
        "bilewater_trap", "falling_spike_ball", "swing_trap_small", "swing_trap_spike",
        "dust_trap_spike_plate", "dust_trap_spike_dropper", "mite_trap",
        "organ_spikes", "cradle_spikes",
        "brown_vines", "abyss_tendrils", "void_wave",
        "mill_trap", "craw_chain",
        "frost_marker", "white_thorns", "jelly_egg", "wp_trap_spikes"
    };

    // 场景黑名单
    public static readonly HashSet<string> ExcludedScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Belltown_04",
        "Bellshrine", "Bellshrine_02", "Bellshrine_03", "Bellshrine_05",
        "Bone_East_Umbrella",
        "Room_Pinstress",
    };

    public static readonly HashSet<string> NoLavaScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hang_01", "Hang_02", "Hang_10"
    };

    public static readonly HashSet<string> LargeRooms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Song_20", "Arborium_01", "Cog_04", "Song_11", "Song_05", "Song_01", "Coral_35b"
    };

    // 特殊陷阱集
    public static readonly HashSet<string> LargeTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "fan_hazard", "steam_vent", "mill_trap",
        "spike_cog_2", "spike_cog_3", "spike_cog_1", "spike_cog_4", "spike_cog_5", "voltgrass",
        "junk_pipe"
    };
    public static List<string> TrapPoolNoLava => TrapPool.Where(t => t != LavaTrapId).ToList();

    public static readonly HashSet<string> ThornTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "brown_vines", "shellwood_thorns", "white_thorns"
    };

    public static readonly HashSet<string> LoweredSpikeTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "pilgrim_trap_spike", "organ_spikes", "cradle_spikes"
    };

    public static readonly HashSet<string> HammerTraps = new(StringComparer.OrdinalIgnoreCase)
    {
        "slab_spike_ball"
    };

    // 常量
    public const string LavaTrapId = "lava_area";
    public const string FallingLavaId = "falling_lava";
    public const float MinDistance = 8f;
    public const float PickupSafeRadius = 7f;
    public const int LargeTrapRadius = 7;
    public const int ThornTrapMinWidth = 7;
    public const float SpikeYOffset = 1.8f;
    public const int HammerTrapMinHeight = 7;

    // 陷阱元数据（已添加位置偏移和旋转）
    public static readonly Dictionary<string, TrapMeta> TrapMetaDict = new()
    {
        ["fan_hazard"] = new TrapMeta(),
        ["spike_cog_1"] = new TrapMeta(),
        ["spike_cog_2"] = new TrapMeta(),
        ["spike_cog_3"] = new TrapMeta(),
        ["spike_cog_4"] = new TrapMeta(),
        ["spike_cog_5"] = new TrapMeta(),
        ["hot_coal"] = new TrapMeta(),
        ["lava_area"] = new TrapMeta(),
        ["falling_lava"] = new TrapMeta(),
        ["voltgrass"] = new TrapMeta(),
        ["steam_vent"] = new TrapMeta(),
        ["slab_trap"] = new TrapMeta(),
        ["slab_prob_blade"] = new TrapMeta(),
        ["slab_spike_ball"] = new TrapMeta(),
        ["dust_trap_spike_plate"] = new TrapMeta(),
        ["dust_trap_spike_dropper"] = new TrapMeta(),
        ["mite_trap"] = new TrapMeta(),
        ["organ_spikes"] = new TrapMeta(),
        ["cradle_spikes"] = new TrapMeta(),
        ["mill_trap"] = new TrapMeta(),
        ["coral_lightning_rock"] = new TrapMeta(),
        ["coral_crust_s"] = new TrapMeta(),
        ["coral_crust_m"] = new TrapMeta(),
        ["coral_crust_l"] = new TrapMeta(),
        ["abyss_tendrils"] = new TrapMeta(),
        ["bone_boulder"] = new TrapMeta(),
        ["void_wave"] = new TrapMeta(),

        ["wisp_flame_lantern"] = new TrapMeta(new() { ["breakable_on"] = "True" }),
        ["falling_bell"] = new TrapMeta(new() { ["bell_reset"] = "1" }),
        ["shellwood_thorns"] = new TrapMeta(new() { ["vines_hurt_player"] = "True" }),
        ["brown_vines"] = new TrapMeta(new() { ["vines_hurt_player"] = "True" }),
        ["white_thorns"] = new TrapMeta(new() { ["vines_hurt_player"] = "True" }),
        ["junk_pipe"] = new TrapMeta(new() { ["junk_pipe_terrain"] = "True" }),
        ["frost_marker"] = new TrapMeta(new() { ["frost_speed"] = "10" }),
        ["jelly_egg"] = new TrapMeta(new() { ["egg_regen"] = "-1" }),
        // wp_trap_spikes 上移 5 格（固定规则）
        ["wp_trap_spikes"] = new TrapMeta(
            new() { ["wp_spikes_up"] = "True", ["wp_spikes_delay"] = "0", ["wp_spikes_speed"] = "1" },
            positionOffset: new Vector3(0f, 5f, 0f)
        ),
        ["coral_lightning_orb"] = new TrapMeta(),

        ["pilgrim_trap_spike"] = new TrapMeta(needsActivator: true, activatorId: "pilgrim_trap_wire"),
        // 碎石区上移 0.5
        ["rubble_field"] = new TrapMeta(
            needsActivator: true, activatorId: "slab_pressure_plate",
            positionOffset: new Vector3(0f, 0.5f, 0f)
        ),
        // 腐汁陷阱向右平移 10 格
        ["bilewater_trap"] = new TrapMeta(
            needsActivator: true, activatorId: "slab_pressure_plate",
            positionOffset: new Vector3(10f, 0f, 0f)
        ),
        ["falling_spike_ball"] = new TrapMeta(needsActivator: true, activatorId: "slab_pressure_plate"),
        // 小摇摆陷阱：激活器正上方 8 格
        ["swing_trap_small"] = new TrapMeta(
            needsActivator: true, activatorId: "slab_pressure_plate",
            positionOffset: new Vector3(0f, 8f, 0f)
        ),
        // 大摇摆陷阱（天花板类）：激活器正上方 15 格
        ["swing_trap_spike"] = new TrapMeta(
            needsActivator: true, activatorId: "slab_pressure_plate",
            positionOffset: new Vector3(0f, 15f, 0f)
        ),
        // 猎人地雷：逆时针旋转 180 度，同时在激活器位置下方 1 格
        ["hunter_landmine"] = new TrapMeta(
            needsActivator: true, activatorId: "hunter_trap_plate",
            positionOffset: new Vector3(0f, -1f, 0f),
            positionRotate: new Vector3(0f, 0f, 180f)
        ),
        // 猎人镰刀：逆时针旋转 180 度，同时在激活器位置下方 1 格
        ["hunter_sickle_trap"] = new TrapMeta(
    needsActivator: true, activatorId: "hunter_trap_plate",
    positionOffset: new Vector3(0f, 6f, 0f)
        ),
        ["craw_chain"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
        ["coral_spike"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
        ["coral_spike_fall"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
        ["stomp_spire"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
    };

    // 功能分类（未变动）
    public static readonly Dictionary<string, List<string>> TrapCategories = new()
    {
        ["暗雷"] = new() { "hunter_landmine", "pilgrim_trap_spike", "dust_trap_spike_plate", "slab_trap", "slab_prob_blade", "abyss_tendrils", "hunter_sickle_trap" },
        ["跳跳乐"] = new() { "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5", "cradle_spikes" },
        ["窄道"] = new() { "falling_lava", "coral_lightning_orb", "coral_lightning_rock", "steam_vent", "junk_pipe", "hot_coal" },
        ["平台类"] = new() { "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5", "fan_hazard" },
        ["墙壁"] = new() { "shellwood_thorns", "brown_vines", "white_thorns" },
        ["天花板"] = new() { "falling_bell", "bone_boulder", "dust_trap_spike_dropper", "swing_trap_spike" },
        ["障碍物"] = new() { "coral_crust_s", "coral_crust_m", "coral_crust_l", "voltgrass", "mill_trap" },
        ["装饰物"] = new() { "jelly_egg", "craw_chain" },
        ["触发型"] = new() { "wp_trap_spikes", "swing_trap_small", "coral_spike", "coral_spike_fall", "stomp_spire", "organ_spikes", "falling_spike_ball", "rubble_field", "mite_trap", "bilewater_trap" },
        ["追逐型"] = new() { "void_wave", "wisp_flame_lantern" },
        ["场景伤害"] = new() { },
        ["跳跳乐1"] = new()
        {
            // 纯弹跳果实/植物
            "march_pogo", "bounce_bloom", "wisp_bounce_pod", "sprintmaster_pod",
            "swap_bounce_pod", "clover_pod", "abyss_pod", "celeste_bumper",
            "lilypad", "cradle_nut",
            // 珊瑚平台
            "coral_plat_float", "small_grey_coral_plat", "small_red_coral_plat",
            "mid_red_coral_plat", "large_red_coral_plat",
            // 雕像
            "karaka_statue", "judge_statue", "clover_statue",
            "shard_statue_1", "shard_statue_2", "shard_statue_3",
            "shard_statue_4", "shard_statue_5", "flick_statue",
            // 雪山
            "fayforn_npc", "snow_chunk", "float_crystal",
            // 固定生物
            "white_palace_fly", "pond_skipper_body", "winged_lifeseed",
            "life_pustule", "bounce_flea", "dodge_flea",
            "hornet_cocoon", "bellbeast_child",
            // 铃铛
            "bell_s", "bell_l", "bell_lock",
            // 布气球
            "greymoor_balloon_small", "greymoor_balloon_mid", "greymoor_balloon_large",
            // 深渊深巢
            "abyss_plat_mid", "abyss_plat_wide",
            "deepnest_platform_01", "deepnest_platform_02",
            "deepnest_platform_03", "deepnest_platform_04", "deepnest_platform_05",
            "hive_pod", "silk_pod",
            "hive_platform_01", "hive_platform_02", "hive_platform_03",
            // 贝壳
            "shell_small", "shell_mid", "shell_large",
            // 动态生物
            "swamp_mosquito", "swamp_mosquito_skinny", "mothleaf",
            "imoba", "garpid",
            "gloomsac", "gargant_gloom",
            "crystal_drifter", "crystal_drifter_giant",
        }
    };

    // 难度配额（未变动）
    public static Dictionary<string, int> GetCategoryQuotas(TrapDifficulty difficulty)
    {
        switch (difficulty)
        {
            case TrapDifficulty.Beginner:
                return new()
                {
                    ["暗雷"] = 1,
                    ["跳跳乐"] = 1,
                    ["窄道"] = 1,
                    ["平台类"] = 1,
                    ["墙壁"] = 1,
                    ["天花板"] = 3,
                    ["障碍物"] = 2,
                    ["装饰物"] = 2,
                    ["触发型"] = 3,
                    ["追逐型"] = 0,
                    ["场景伤害"] = 0
                };
            case TrapDifficulty.Focused:
                return new()
                {
                    ["暗雷"] = 3,
                    ["跳跳乐"] = 3,
                    ["窄道"] = 3,
                    ["平台类"] = 3,
                    ["墙壁"] = 3,
                    ["天花板"] = 3,
                    ["障碍物"] = 4,
                    ["装饰物"] = 3,
                    ["触发型"] = 5,
                    ["追逐型"] = 1,
                    ["场景伤害"] = 0
                };
            case TrapDifficulty.Overflow:
                return new()
                {
                    ["暗雷"] = 5,
                    ["跳跳乐"] = 5,
                    ["窄道"] = 5,
                    ["平台类"] = 5,
                    ["墙壁"] = 5,
                    ["天花板"] = 5,
                    ["障碍物"] = 5,
                    ["装饰物"] = 5,
                    ["触发型"] = 7,
                    ["追逐型"] = 2,
                    ["场景伤害"] = 0
                };
            default:
                return new()
                {
                    ["暗雷"] = 1,
                    ["跳跳乐"] = 1,
                    ["窄道"] = 1,
                    ["平台类"] = 1,
                    ["墙壁"] = 1,
                    ["天花板"] = 3,
                    ["障碍物"] = 2,
                    ["装饰物"] = 2,
                    ["触发型"] = 3,
                    ["追逐型"] = 0,
                    ["场景伤害"] = 0
                };
        }
    }

    public static readonly string[] CategoryOrder = { "暗雷", "跳跳乐", "窄道", "平台类", "墙壁", "天花板", "障碍物", "装饰物", "触发型", "追逐型", "场景伤害" };

    public static double GetFrostProbability(TrapDifficulty difficulty) => difficulty switch
    {
        TrapDifficulty.Focused => 0.10,
        TrapDifficulty.Overflow => 0.20,
        _ => 0.0,
    };
}

// 元数据类（增加了 PositionOffset 和 Rotation）
public class TrapMeta
{
    public Dictionary<string, string> Config { get; set; }
    public bool NeedsActivator { get; set; }
    public string ActivatorId { get; set; }
    public Vector3 PositionOffset { get; set; }   // 陷阱本体相对于激活器的偏移
    public Vector3 PositionRotate { get; set; }   // 陷阱本体的额外旋转（欧拉角）

    public TrapMeta(Dictionary<string, string> config = null,
        bool needsActivator = false,
        string activatorId = null,
        Vector3? positionOffset = null,
        Vector3? positionRotate = null)
    {
        Config = config;
        NeedsActivator = needsActivator;
        ActivatorId = activatorId;
        PositionOffset = positionOffset ?? Vector3.zero;
        PositionRotate = positionRotate ?? Vector3.zero;
    }
}