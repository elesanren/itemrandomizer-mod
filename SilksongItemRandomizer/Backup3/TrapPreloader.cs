using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace SilksongItemRandomizer;

/// <summary>
/// 陷阱配置工具类：提供陷阱元数据字典与功能分类，供 TrapRandomizer 查询
/// </summary>
public static class TrapPreloader
{
    // ★ 陷阱元数据字典
    public static readonly Dictionary<string, TrapMeta> TrapMetaDict = new()
    {
        // ===== 一、直接放置即有伤害，无需任何额外配置 =====
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

        // ===== 二、需要简单额外配置的陷阱 =====
        ["wisp_flame_lantern"] = new TrapMeta(
            new() { ["breakable_on"] = "True" }
        ),
        ["falling_bell"] = new TrapMeta(
            new() { ["bell_reset"] = "1" }
        ),
        ["shellwood_thorns"] = new TrapMeta(
            new() { ["vines_hurt_player"] = "True" }
        ),
        ["brown_vines"] = new TrapMeta(
            new() { ["vines_hurt_player"] = "True" }
        ),
        ["white_thorns"] = new TrapMeta(
            new() { ["vines_hurt_player"] = "True" }
        ),
        ["junk_pipe"] = new TrapMeta(
            new() { ["junk_pipe_terrain"] = "True" }
        ),
        ["frost_marker"] = new TrapMeta(
            new() { ["frost_speed"] = "10" }
        ),
        ["jelly_egg"] = new TrapMeta(
            new() { ["egg_regen"] = "-1" }
        ),
        ["wp_trap_spikes"] = new TrapMeta(
            new()
            {
                ["wp_spikes_up"] = "True",
                ["wp_spikes_delay"] = "0",
                ["wp_spikes_speed"] = "1"
            }
        ),
        ["coral_lightning_orb"] = new TrapMeta(),

        // ===== 三、需要外部触发器激活的陷阱 =====
        ["pilgrim_trap_spike"] = new TrapMeta(needsActivator: true, activatorId: "pilgrim_trap_wire"),
        ["rubble_field"] = new TrapMeta(needsActivator: true, activatorId: "slab_pressure_plate"),
        ["bilewater_trap"] = new TrapMeta(needsActivator: true, activatorId: "slab_pressure_plate"),
        ["falling_spike_ball"] = new TrapMeta(needsActivator: true, activatorId: "slab_pressure_plate"),
        ["swing_trap_small"] = new TrapMeta(needsActivator: true, activatorId: "slab_pressure_plate"),
        ["swing_trap_spike"] = new TrapMeta(needsActivator: true, activatorId: "slab_pressure_plate"),
        ["hunter_landmine"] = new TrapMeta(needsActivator: true, activatorId: "hunter_trap_plate"),
        ["hunter_sickle_trap"] = new TrapMeta(needsActivator: true, activatorId: "hunter_trap_plate"),
        ["craw_chain"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
        ["coral_spike"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
        ["coral_spike_fall"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
        ["stomp_spire"] = new TrapMeta(needsActivator: true, activatorId: "trigger_zone"),
    };
    public static readonly Dictionary<string, List<string>> ConfirmedCategories = new()
    {
        ["暗雷"] = new List<string>
    {
        "hunter_landmine",
        "pilgrim_trap_spike",
        "dust_trap_spike_plate",
        "slab_trap",
        "slab_prob_blade",
        "abyss_tendrils",
        "hunter_sickle_trap"
    },
        ["跳跳乐"] = new List<string>
    {
        "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5",
        "cradle_spikes"
    },
        ["窄道"] = new List<string>
    {
        "falling_lava",
        "coral_lightning_orb",
        "coral_lightning_rock",
        "steam_vent",
        "junk_pipe",
        "hot_coal"
    },
        ["平台类"] = new List<string>
    {
        "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5",
        "fan_hazard"
    },
        ["墙壁"] = new List<string>
    {
        "shellwood_thorns",
        "brown_vines",
        "white_thorns"
    },
        ["天花板"] = new List<string>
    {
        "falling_bell",
        "bone_boulder",
        "dust_trap_spike_dropper"
    },
        ["障碍物"] = new List<string>
    {
        "coral_crust_s",
        "coral_crust_m",
        "coral_crust_l",
        "voltgrass",
        "mill_trap"
    },
        ["装饰物"] = new List<string>
    {
        "jelly_egg",
        "craw_chain",
        "frost_marker"
    }
    };
    // ★ 陷阱功能分类（用于按场景特征选择陷阱池）
    public static readonly Dictionary<string, List<string>> TrapCategories = new()
    {
        ["暗雷"] = new List<string>
        {
            "hunter_landmine",
            "pilgrim_trap_spike",
            "dust_trap_spike_plate",
            "slab_trap",
            "slab_prob_blade",
            "abyss_tendrils",
            "hunter_sickle_trap"
        },

        ["跳跳乐"] = new List<string>
        {
            "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5",
            "cradle_spikes"
        },

        ["窄道"] = new List<string>
        {
            "falling_lava",
            "coral_lightning_orb",
            "coral_lightning_rock",  // ★ 已从障碍物移入窄道（每隔1秒放电柱）
            "steam_vent",
            "junk_pipe",
            "hot_coal"
        },

        ["平台类"] = new List<string>
        {
            "spike_cog_1", "spike_cog_2", "spike_cog_3", "spike_cog_4", "spike_cog_5",
            "fan_hazard"
        },

        ["墙壁"] = new List<string>
        {
            "shellwood_thorns",
            "brown_vines",
            "white_thorns"
        },

        ["天花板"] = new List<string>
        {
            "falling_bell",
            "bone_boulder",
            "dust_trap_spike_dropper"
        },

        ["障碍物"] = new List<string>
        {
            "coral_crust_s",
            "coral_crust_m",
            "coral_crust_l",
            "voltgrass",
            "mill_trap"
        },

        ["装饰物"] = new List<string>
        {
            "jelly_egg",
            "craw_chain",
            "frost_marker"
        },

        ["触发型"] = new List<string>
        {
            "wp_trap_spikes",
            "swing_trap_small",
            "swing_trap_spike",
            "coral_spike",
            "coral_spike_fall",
            "stomp_spire",
            "organ_spikes",
            "falling_spike_ball",
            "rubble_field",
            "mite_trap",
            "bilewater_trap"
        },

        ["追逐型"] = new List<string>
        {
            "void_wave",
            "wisp_flame_lantern"
        }
    };
}

// ★ 陷阱元数据类
public class TrapMeta
{
    public Dictionary<string, string> Config { get; set; }
    public bool NeedsActivator { get; set; }
    public string ActivatorId { get; set; }

    public TrapMeta(
        Dictionary<string, string> config = null,
        bool needsActivator = false,
        string activatorId = null)
    {
        Config = config;
        NeedsActivator = needsActivator;
        ActivatorId = activatorId;
    }
}