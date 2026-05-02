using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;
namespace HKSilksong_Randomizer;

[BepInPlugin("com.yourname.randomsceneloader", "Random Scene Loader", "0.1.1")]
public class RandomSceneLoader : BaseUnityPlugin
{
    internal ManualLogSource log;
    private Random rng;
    internal readonly Dictionary<string, SceneConfig> sceneConfigs = new Dictionary<string, SceneConfig>(StringComparer.OrdinalIgnoreCase)
    {
        { "Tut_01", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "top1" }) },
        { "Tut_01b", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Tut_02", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Tut_03", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Bonetown", new SceneConfig(new List<string> { "bot1", "bot2", "left1", "left2", "right1", "right2", "top1", "top2", "top3", "top4", "top5", "top6" }) },
        { "Weave_02", new SceneConfig(new List<string> { "left2", "left3", "left4", "right1", "right2", "right3" }) },
        { "Weave_03", new SceneConfig(new List<string> { "right1" }) },
        { "Weave_04", new SceneConfig(new List<string> { "left1", "right2" }) },
        { "Weave_05b", new SceneConfig(new List<string> { "left1" }) },
        { "Weave_07", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Weave_08", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Weave_10", new SceneConfig(new List<string> { "left1" }) },
        { "Weave_11", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Weave_12", new SceneConfig(new List<string> { "left1" }) },
        { "Weave_13", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Weave_14", new SceneConfig(new List<string> { "bot1" }) },
        { "Bone_01", new SceneConfig(new List<string> { "left2", "right1", "right2", "top2" }) },
        { "Bone_04", new SceneConfig(new List<string> { "bot2", "left1", "left2", "right1", "top1" }) },
        { "Bone_05", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_03", new SceneConfig(new List<string> { "bot1", "left1", "left2", "left4", "right1", "right3", "top1" }) },
        { "Bone_05b", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Bone_06", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_07", new SceneConfig(new List<string> { "left1", "right1", "right2", "top1" }) },
        { "Bone_08", new SceneConfig(new List<string> { "bot1", "left2", "left3", "right2", "right3" }) },
        { "Bone_09", new SceneConfig(new List<string> { "left1", "right1", "right2", "top1" }) },
        { "Bone_10", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_11", new SceneConfig(new List<string> { "bot1", "left1", "right1", "right2", "top1" }) },
        { "Bone_14", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bone_15", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Bone_16", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Bone_17", new SceneConfig(new List<string> { "right1" }) },
        { "Bone_18", new SceneConfig(new List<string> { "left1" }) },
        { "Bone_19", new SceneConfig(new List<string> { "bot1" }) },
        { "Bone_East_01", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Bone_East_02", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Bone_East_04", new SceneConfig(new List<string> { "bot1", "left1", "right1", "right2", "top2" }) },
        { "Bone_East_05", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bone_East_07", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2", "right3", "right4", "right5", "top1" }) },
        { "Bone_East_09", new SceneConfig(new List<string> { "left2", "left3", "right1", "right2", "top1" }) },
        { "Bone_East_10", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Bone_East_11", new SceneConfig(new List<string> { "bot1", "left1", "right1", "right2" }) },
        { "Bone_East_12", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_East_13", new SceneConfig(new List<string> { "left1" }) },
        { "Bone_East_14", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Bone_East_15", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_East_16", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Bone_East_17", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_East_18", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Bone_East_18b", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Bone_East_18c", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bone_East_20", new SceneConfig(new List<string> { "right1" }) },
        { "Bone_East_22", new SceneConfig(new List<string> { "left1" }) },
        { "Bone_East_24", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_East_25", new SceneConfig(new List<string> { "left1" }) },
        { "Bone_East_26", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Bone_East_27", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Bone_East_04c", new SceneConfig(new List<string> { "left1" }) },
        { "Bone_East_04b", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Bone_East_09b", new SceneConfig(new List<string> { "bot1", "left1", "top1" }) },
        { "Bone_East_14b", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Ant_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Ant_03", new SceneConfig(new List<string> { "left2", "right3" }) },
        { "Ant_04", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Ant_04_left", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Ant_04_mid", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Ant_05b", new SceneConfig(new List<string> { "bot1", "bot2", "right1" }) },
        { "Ant_05c", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Ant_09", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Ant_14", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "left5", "right2", "right3" }) },
        { "Ant_17", new SceneConfig(new List<string> { "right1" }) },
        { "Ant_19", new SceneConfig(new List<string> { "left1" }) },
        { "Ant_20", new SceneConfig(new List<string> { "left1" }) },
        { "Ant_21", new SceneConfig(new List<string> { "right1" }) },
        { "Ant_Merchant", new SceneConfig(new List<string> { "right1" }) },
        { "Ant_Queen", new SceneConfig(new List<string> { "left1" }) },
        { "Aspid_01", new SceneConfig(new List<string> { "bot1", "bot2", "bot3", "bot4", "bot5", "bot6", "bot7", "bot8", "left1", "left2", "right2", "right3", "right4", "top1", "top2", "top3", "top4", "top5", "top6", "top7" }) },
        { "Belltown_04", new SceneConfig(new List<string> { "bot1", "left1", "left2" }) },
        { "Belltown_06", new SceneConfig(new List<string> { "left1", "left3", "right1" }) },
        { "Belltown_Room_shellwood", new SceneConfig(new List<string> { "left1" }) },
        { "Belltown_Shrine", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Belltown_basement_03", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Bellshrine", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bellshrine_03", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bonegrave", new SceneConfig(new List<string> { "right1", "right2", "top1" }) },
        { "Bone_01c", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Bone_02", new SceneConfig(new List<string> { "left1", "right1", "top1", "top2" }) },
        { "Bellshrine_05", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bellway_03", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Greymoor_01", new SceneConfig(new List<string> { "bot1", "left1", "left2", "right1", "right2", "right3" }) },
        { "Bellshrine_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Greymoor_02", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3" }) },
        { "Greymoor_15", new SceneConfig(new List<string> { "left1", "left3", "right2", "right3" }) },
        { "Greymoor_15b", new SceneConfig(new List<string> { "left2", "left3", "right1", "top1" }) },
        { "Room_CrowCourt", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Room_CrowCourt_02", new SceneConfig(new List<string> { "top1" }) },
        { "Clover_01", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Greymoor_22", new SceneConfig(new List<string> { "bot1" }) },
        { "Greymoor_12", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Greymoor_03", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3", "right4", "right5" }) },
        { "Dust_01", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Dust_02", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3", "top1" }) },
        { "Dust_03", new SceneConfig(new List<string> { "bot1", "left1", "top1" }) },
        { "Dust_Chef", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Dust_04", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Dust_Shack", new SceneConfig(new List<string> { "left1" }) },
        { "Dust_10", new SceneConfig(new List<string> { "right1" }) },
        { "Dust_05", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Dust_06", new SceneConfig(new List<string> { "left1", "right1", "right2", "right3" }) },
        { "Dust_12", new SceneConfig(new List<string> { "left1" }) },
        { "Dust_11", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Greymoor_17", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Halfway_01", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Greymoor_04", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2" }) },
        { "Greymoor_11", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Greymoor_06", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3", "right4", "top1" }) },
        { "Greymoor_10", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Wisp_03", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Wisp_06", new SceneConfig(new List<string> { "bot1" }) },
        { "Greymoor_07", new SceneConfig(new List<string> { "bot1", "left1", "right1", "right2" }) },
        { "Greymoor_20b", new SceneConfig(new List<string> { "right1" }) },
        { "Greymoor_20c", new SceneConfig(new List<string> { "left1" }) },
        { "Greymoor_08", new SceneConfig(new List<string> { "left2", "right1", "top1" }) },
        { "Greymoor_16", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Bellway_04", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Greymoor_05", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Belltown", new SceneConfig(new List<string> { "left3", "right2" }) },
        { "Memory_Needolin", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Belltown_07", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shellwood_01", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Shellwood_02", new SceneConfig(new List<string> { "left2", "left3", "right1", "right2" }) },
        { "Shellwood_16", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shellwood_03", new SceneConfig(new List<string> { "bot1", "left1", "left3", "right1", "right2", "right3" }) },
        { "Shellwood_04b", new SceneConfig(new List<string> { "left1", "right1", "top1", "top2" }) },
        { "Shellwood_08c", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shellwood_04c", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Shellwood_08", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Coral_19", new SceneConfig(new List<string> { "bot1", "bot2", "bot3", "bot4", "bot5", "bot6", "bot7", "right1", "top1", "top2", "top3", "top4", "top5", "top6", "top7", "top8" }) },
        { "Shellwood_19", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shellwood_14", new SceneConfig(new List<string> { "left1" }) },
        { "Shellwood_10", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3" }) },
        { "Shellwood_11", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Shellwood_26", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Shellwood_18", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Shellwood_13", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Shellwood_01b", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Shellwood_20", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shellwood_Witch", new SceneConfig(new List<string> { "right1" }) },
        { "Dock_08", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Dock_01", new SceneConfig(new List<string> { "left1", "right1", "right2" }) },
        { "Bellway_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Dock_16", new SceneConfig(new List<string> { "right1" }) },
        { "Bone_East_03", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Room_Forge", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Dock_04", new SceneConfig(new List<string> { "left1", "right1", "right2", "right3" }) },
        { "Dock_06_Church", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Dock_10", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Dock_15", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Dock_13", new SceneConfig(new List<string> { "right1" }) },
        { "Dock_11", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Dock_12", new SceneConfig(new List<string> { "left1" }) },
        { "Dock_14", new SceneConfig(new List<string> { "left1" }) },
        { "Dock_09", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Dock_02", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Dock_02b", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2" }) },
        { "Dock_03c", new SceneConfig(new List<string> { "left2", "top1", "top2" }) },
        { "Dock_03", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Dock_03b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shellwood_15", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_19b", new SceneConfig(new List<string> { "bot1" }) },
        { "Coral_02", new SceneConfig(new List<string> { "bot2", "right1" }) },
        { "Coral_03", new SceneConfig(new List<string> { "bot1", "bot2", "bot3", "bot4", "bot5", "bot6", "left1", "left2", "left3", "right1", "right2", "right3" }) },
        { "Coral_12", new SceneConfig(new List<string> { "left2", "left3", "right1" }) },
        { "Coral_11", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_11b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_34", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Coral_25", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Coral_23", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Coral_39", new SceneConfig(new List<string> { "right1" }) },
        { "Coral_35b", new SceneConfig(new List<string> { "bot1", "left2", "left3", "left4", "left5", "right1", "right2" }) },
        { "Coral_40", new SceneConfig(new List<string> { "right1" }) },
        { "Coral_41", new SceneConfig(new List<string> { "right1" }) },
        { "Coral_35", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "top1" }) },
        { "Coral_24", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_26", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Coral_38", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Coral_44", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_29", new SceneConfig(new List<string> { "left1" }) },
        { "Coral_27", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_28", new SceneConfig(new List<string> { "right1" }) },
        { "Coral_Tower_01", new SceneConfig(new List<string> { "left1" }) },
        { "Coral_42", new SceneConfig(new List<string> { "right1" }) },
        { "Coral_43", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bellway_08", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_32", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Coral_Judge_Arena", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Coral_10", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Song_19_entrance", new SceneConfig(new List<string> { "left1", "right1", "right2" }) },
        { "Song_01c", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Song_01", new SceneConfig(new List<string> { "bot1", "right2", "top1" }) },
        { "Under_07b", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Song_01b", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Song_05", new SceneConfig(new List<string> { "left3", "left4", "left5", "right2", "right3", "right4" }) },
        { "Song_02", new SceneConfig(new List<string> { "left2", "right1" }) },
        { "Ward_01", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3" }) },
        { "Ward_02", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Ward_02b", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Ward_03", new SceneConfig(new List<string> { "bot1", "left1", "top1" }) },
        { "Ward_07", new SceneConfig(new List<string> { "bot1" }) },
        { "Ward_06", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Song_07", new SceneConfig(new List<string> { "right1" }) },
        { "Song_27", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Song_20", new SceneConfig(new List<string> { "left1", "left2", "right4", "right5", "right6", "top1" }) },
        { "Bellway_City", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Library_13", new SceneConfig(new List<string> { "left1", "right1", "right2" }) },
        { "Song_11", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2", "right3" }) },
        { "Song_15", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Song_17", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Hang_01", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Hang_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Hang_03", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "top1" }) },
        { "Hang_10", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Hang_03_top", new SceneConfig(new List<string> { "bot1" }) },
        { "Hang_13", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Hang_08", new SceneConfig(new List<string> { "bot1", "left1", "left2", "left3", "left4", "right1" }) },
        { "Hang_09", new SceneConfig(new List<string> { "right1" }) },
        { "Hang_16", new SceneConfig(new List<string> { "right1" }) },
        { "Hang_06", new SceneConfig(new List<string> { "bot1", "left1", "right1", "top1" }) },
        { "Hang_06b", new SceneConfig(new List<string> { "left1" }) },
        { "Hang_17b", new SceneConfig(new List<string> { "left1" }) },
        { "Hang_04", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Hang_12", new SceneConfig(new List<string> { "right1" }) },
        { "Hang_07", new SceneConfig(new List<string> { "bot1", "left1", "right1", "top1" }) },
        { "Song_09", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Song_09b", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Cog_05", new SceneConfig(new List<string> { "left1", "right2", "top1" }) },
        { "Cog_Dancers", new SceneConfig(new List<string> { "bot1", "bot2", "left1", "right1", "top1" }) },
        { "Cog_04", new SceneConfig(new List<string> { "left2", "right2", "right3", "top1", "top2" }) },
        { "Cog_Bench", new SceneConfig(new List<string> { "left1" }) },
        { "Song_25", new SceneConfig(new List<string> { "bot1", "left1", "right1", "top1", "top2" }) },
        { "Song_Enclave", new SceneConfig(new List<string> { "bot1", "left1", "left2", "top1" }) },
        { "Song_Enclave_Tube", new SceneConfig(new List<string> { "bot1" }) },
        { "Under_01", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1" }) },
        { "Under_27", new SceneConfig(new List<string> { "left1", "right1", "right2" }) },
        { "Shellwood_22", new SceneConfig(new List<string> { "right1" }) },
        { "Shellwood_11b", new SceneConfig(new List<string> { "right1" }) },
        { "Under_01b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Under_02", new SceneConfig(new List<string> { "left1", "left3", "right1", "right2", "right3", "right4" }) },
        { "Under_14", new SceneConfig(new List<string> { "left1" }) },
        { "Under_16", new SceneConfig(new List<string> { "right1" }) },
        { "Under_03b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Under_07", new SceneConfig(new List<string> { "left3", "right2" }) },
        { "Under_07c", new SceneConfig(new List<string> { "left2", "top1" }) },
        { "Under_06", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Under_08", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Under_05", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3" }) },
        { "Under_11", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Under_23", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Wisp_09", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Wisp_05", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Wisp_02", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Belltown_08", new SceneConfig(new List<string> { "right1" }) },
        { "Wisp_04", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Wisp_08", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Wisp_07", new SceneConfig(new List<string> { "left1" }) },
        { "Under_04", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Under_03c", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Under_03", new SceneConfig(new List<string> { "right1" }) },
        { "Under_10", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Under_12", new SceneConfig(new List<string> { "left1" }) },
        { "Under_13", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2", "right3" }) },
        { "Under_22", new SceneConfig(new List<string> { "right1" }) },
        { "Under_21", new SceneConfig(new List<string> { "right1" }) },
        { "Under_19", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Under_19c", new SceneConfig(new List<string> { "bot1", "left1", "left2" }) },
        { "Under_19b", new SceneConfig(new List<string> { "right1" }) },
        { "Under_18", new SceneConfig(new List<string> { "left1", "right1", "top1", "top2" }) },
        { "Under_17", new SceneConfig(new List<string> { "bot1", "bot2", "left1", "right1", "top1" }) },
        { "Library_11b", new SceneConfig(new List<string> { "left3", "right1" }) },
        { "Library_11", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2" }) },
        { "Library_13b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Library_04", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2", "right3", "right4", "right5", "right6", "top1" }) },
        { "Library_16", new SceneConfig(new List<string> { "right1" }) },
        { "Library_10", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Library_12b", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Library_12", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Library_05", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Library_06", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Library_07", new SceneConfig(new List<string> { "bot1", "bot2", "bot3", "left1", "left2", "top1" }) },
        { "Library_08", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Library_09", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Library_14", new SceneConfig(new List<string> { "left1" }) },
        { "Library_01", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2" }) },
        { "Library_15", new SceneConfig(new List<string> { "right1" }) },
        { "Library_03", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Song_24", new SceneConfig(new List<string> { "right1" }) },
        { "Song_20b", new SceneConfig(new List<string> { "bot1", "left2", "left4", "right2", "right3", "top1" }) },
        { "Library_02", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Song_29", new SceneConfig(new List<string> { "right1" }) },
        { "Arborium_01", new SceneConfig(new List<string> { "bot1", "left1", "left2", "left3", "right1", "right2", "right3", "right4", "right5" }) },
        { "Cog_10", new SceneConfig(new List<string> { "bot1" }) },
        { "Cog_07", new SceneConfig(new List<string> { "left1" }) },
        { "Cog_06", new SceneConfig(new List<string> { "left2", "right1" }) },
        { "Arborium_04", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Arborium_03", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2" }) },
        { "Arborium_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Arborium_05", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Arborium_06", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Arborium_09", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Arborium_Tube", new SceneConfig(new List<string> { "right1" }) },
        { "Arborium_07", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Arborium_08", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Song_13", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Song_18", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Song_03", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Song_04", new SceneConfig(new List<string> { "bot1", "left1", "right1", "right2" }) },
        { "Song_10", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Song_12", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2", "right3" }) },
        { "Song_14", new SceneConfig(new List<string> { "left1" }) },
        { "Song_26", new SceneConfig(new List<string> { "right1" }) },
        { "Song_28", new SceneConfig(new List<string> { "right1" }) },
        { "Song_08", new SceneConfig(new List<string> { "right1" }) },
        { "Slab_01", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Slab_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Slab_03", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "left5", "left6", "left7", "left8", "right1", "right2", "right3", "right4", "right5", "right7", "right8", "right9" }) },
        { "Slab_04", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Slab_23", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Slab_21", new SceneConfig(new List<string> { "left1", "left3", "top1" }) },
        { "Slab_22", new SceneConfig(new List<string> { "bot1", "bot2" }) },
        { "Slab_Cell_Creature", new SceneConfig(new List<string> { "left1" }) },
        { "Slab_20", new SceneConfig(new List<string> { "left1" }) },
        { "Slab_17", new SceneConfig(new List<string> { "left1" }) },
        { "Slab_18", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Slab_16", new SceneConfig(new List<string> { "bot1", "left1", "right1", "top1" }) },
        { "Slab_15", new SceneConfig(new List<string> { "bot1", "left1", "right1", "top1" }) },
        { "Peak_01", new SceneConfig(new List<string> { "left1", "left2", "left3", "left4", "right1", "right2", "right3", "right4", "top1", "top2", "top3", "top4" }) },
        { "Slab_14", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Slab_13", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Slab_05", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Slab_06", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Peak_07", new SceneConfig(new List<string> { "bot1", "bot2", "bot3", "bot4", "bot5", "top1", "top2" }) },
        { "Peak_08b", new SceneConfig(new List<string> { "bot4", "bot5", "bot6", "left1", "left2" }) },
        { "Peak_08", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Peak_05d", new SceneConfig(new List<string> { "bot1" }) },
        { "Peak_05", new SceneConfig(new List<string> { "bot1", "right3", "top2" }) },
        { "Peak_05c", new SceneConfig(new List<string> { "left2", "right1" }) },
        { "Peak_05e", new SceneConfig(new List<string> { "left1", "right1", "right2" }) },
        { "Peak_06b", new SceneConfig(new List<string> { "left1" }) },
        { "Peak_02", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3", "right4" }) },
        { "Peak_04d", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Peak_04", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Bellway_Peak", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "top1" }) },
        { "Bellway_Peak_02", new SceneConfig(new List<string> { "left1" }) },
        { "Peak_04c", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Peak_10", new SceneConfig(new List<string> { "right1" }) },
        { "Slab_08", new SceneConfig(new List<string> { "left1" }) },
        { "Slab_Cell_Quiet", new SceneConfig(new List<string> { "left1", "left2" }) },
        { "Slab_19b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Slab_07", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Slab_12", new SceneConfig(new List<string> { "left1" }) },
        { "Slab_10c", new SceneConfig(new List<string> { "left1" }) },
        { "Slab_10b", new SceneConfig(new List<string> { "left1" }) },
        { "Cog_10_Destroyed", new SceneConfig(new List<string> { "bot1", "left1" }) },
        { "Cog_09_Destroyed", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Song_Tower_Destroyed", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Arborium_10", new SceneConfig(new List<string> { "left1" }) },
        { "Arborium_11", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Aqueduct_01", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Aqueduct_02", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3" }) },
        { "Bellway_Aqueduct", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Aqueduct_07", new SceneConfig(new List<string> { "right1" }) },
        { "Aqueduct_04", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Aqueduct_03", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Aqueduct_06", new SceneConfig(new List<string> { "bot1", "left1", "left2" }) },
        { "Aqueduct_08", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Aqueduct_05", new SceneConfig(new List<string> { "left1" }) },
        { "Shadow_01", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1", "right2", "right3", "top1" }) },
        { "Shadow_18", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_12", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_19", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Shadow_24", new SceneConfig(new List<string> { "left1" }) },
        { "Shadow_10", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Shadow_25", new SceneConfig(new List<string> { "left1" }) },
        { "Shadow_08", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Shadow_27", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_26", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2" }) },
        { "Shadow_16", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_15", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Shadow_14", new SceneConfig(new List<string> { "right1", "right2" }) },
        { "Shadow_02", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Shadow_11", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_13", new SceneConfig(new List<string> { "left1" }) },
        { "Shadow_23", new SceneConfig(new List<string> { "left1" }) },
        { "Shadow_03", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Shadow_21", new SceneConfig(new List<string> { "bot1" }) },
        { "Shadow_09", new SceneConfig(new List<string> { "left1", "left2", "left3", "right1" }) },
        { "Shadow_Weavehome", new SceneConfig(new List<string> { "left1" }) },
        { "Shadow_05", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_04b", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Shadow_04", new SceneConfig(new List<string> { "left1", "right1", "right2", "top1" }) },
        { "Shadow_20", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Bellway_Shadow", new SceneConfig(new List<string> { "left1" }) },
        { "Tube_Hub", new SceneConfig(new List<string> { "left1", "left3", "left4" }) },
        { "Cradle_01", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Cradle_02", new SceneConfig(new List<string> { "left2", "right1", "right2" }) },
        { "Cradle_02b", new SceneConfig(new List<string> { "right1" }) },
        { "Cradle_03", new SceneConfig(new List<string> { "left2", "right2" }) },
        { "Crawl_04", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Crawl_02", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Crawl_06", new SceneConfig(new List<string> { "left1" }) },
        { "Crawl_01", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Crawl_03", new SceneConfig(new List<string> { "bot1", "left1", "right1", "top1" }) },
        { "Crawl_08", new SceneConfig(new List<string> { "bot1" }) },
        { "Crawl_05", new SceneConfig(new List<string> { "right1" }) },
        { "Crawl_03b", new SceneConfig(new List<string> { "bot1", "right1", "top1" }) },
        { "Crawl_07", new SceneConfig(new List<string> { "bot1", "left1", "top1" }) },
        { "Crawl_09", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Crawl_10", new SceneConfig(new List<string> { "right1" }) },
        { "Abyss_12", new SceneConfig(new List<string> { "left1", "right2" }) },
        { "Abyss_05", new SceneConfig(new List<string> { "left2", "right1" }) },
        { "Abyss_08", new SceneConfig(new List<string> { "left1" }) },
        { "Abyss_07", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Abyss_01", new SceneConfig(new List<string> { "left1", "right2", "right3", "right4" }) },
        { "Abyss_06", new SceneConfig(new List<string> { "right1" }) },
        { "Abyss_04", new SceneConfig(new List<string> { "left1" }) },
        { "Abyss_02b", new SceneConfig(new List<string> { "left2", "right1", "top1" }) },
        { "Abyss_02", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Abyss_03", new SceneConfig(new List<string> { "left1", "left2" }) },
        { "Abyss_13", new SceneConfig(new List<string> { "left1", "right1", "top1" }) },
        { "Abyss_11", new SceneConfig(new List<string> { "bot1", "right1" }) },
        { "Abyss_09", new SceneConfig(new List<string> { "bot1", "top1" }) },
        { "Greymoor_13", new SceneConfig(new List<string> { "bot1", "left1", "right1" }) },
        { "Clover_01b", new SceneConfig(new List<string> { "right1" }) },
        { "Clover_02c", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Clover_05c", new SceneConfig(new List<string> { "left1", "left2", "right1", "right2", "right3" }) },
        { "Clover_21", new SceneConfig(new List<string> { "right1" }) },
        { "Clover_16", new SceneConfig(new List<string> { "right1", "top1" }) },
        { "Clover_06", new SceneConfig(new List<string> { "bot1", "bot2" }) },
        { "Clover_19", new SceneConfig(new List<string> { "left1", "top1" }) },
        { "Clover_04b", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Clover_11", new SceneConfig(new List<string> { "right1" }) },
        { "Clover_03", new SceneConfig(new List<string> { "left1", "left2", "right1" }) },
        { "Clover_18", new SceneConfig(new List<string> { "left1" }) },
        { "Tut_04", new SceneConfig(new List<string> { "left1", "right1" }) },
        { "Cog_09", new SceneConfig(new List<string> { "bot1" }) },
        { "Cog_08", new SceneConfig(new List<string> { "bot1", "top1" }) }
    };

    private string lastEntryGateUsed = null;
    private string previousSceneName = null;
    private string currentSceneName = "(unknown)";
    private bool isTransitioning = false;
    private bool loadRequested = false;
    public static bool SuppressTransitionPatch;
    private bool autoDiscoveryMode = false;
    private HashSet<string> discoveredScenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string discoveryFilePath;
    private readonly List<string> preferredSceneSubstrings = new List<string>();
    private WaitForSeconds waitHalfSecond;
    private WaitForSeconds waitThirdSecond;
    private WaitForSeconds waitTenthSecond;
    private WaitForSeconds waitFifthSecond;
    private GUIStyle sceneLabelStyle;
    private string cachedSceneLabel = "";
    private string lastRenderedScene = "";
    private ConfigEntry<bool> cfgShowSceneLabel;
    private ConfigEntry<string> cfgTeleportScene;
    private ConfigEntry<bool> cfgTeleportConfirm;
    private HashSet<string> visitedScenesF6 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public static bool EnableRandomization = false;
    private ConfigEntry<bool> cfgEnableRandomization;

    private void Awake()
    {
        log = Logger;
        rng = new Random();
        Scene activeScene = SceneManager.GetActiveScene();
        currentSceneName = activeScene.name;
        waitHalfSecond = new WaitForSeconds(0.5f);
        waitThirdSecond = new WaitForSeconds(0.3f);
        waitTenthSecond = new WaitForSeconds(0.1f);
        waitFifthSecond = new WaitForSeconds(0.2f);
        sceneLabelStyle = new GUIStyle();

        try { cfgShowSceneLabel = Config.Bind<bool>("UI", "ShowSceneLabel", true, "Show small scene label in upper-left (toggle)"); } catch { }
        try { cfgTeleportScene = Config.Bind<string>("Teleport", "TeleportScene", "", "Scene name to teleport to (type exact scene name)"); } catch { }
        try { cfgTeleportConfirm = Config.Bind<bool>("Teleport", "TeleportConfirm", false, "Set to true to teleport to scene in TeleportScene (resets automatically)"); } catch { }

        // ★ 开关绑定
        try
        {
            cfgEnableRandomization = Config.Bind<bool>("General", "EnableRandomization", false, "Enable/disable scene transition randomization");
            EnableRandomization = cfgEnableRandomization.Value;
        }
        catch { }

        discoveryFilePath = Path.Combine(Paths.PluginPath, "discovered_exits.txt");
        log.LogInfo($"RandomSceneLoader loaded. Current scene: {currentSceneName}. F5=random, F7=discover");

        try
        {
            if (sceneConfigs != null)
            {
                discoveredScenes.UnionWith(sceneConfigs.Keys);
                log.LogInfo($"Preloaded {discoveredScenes.Count} known scenes into discovered set");
            }
        }
        catch { }

        if (autoDiscoveryMode)
        {
            log.LogInfo($"Auto-discovery mode enabled. RoomRando disabled. Results saved to: {discoveryFilePath}");
            return;
        }

        try
        {
            new Harmony("com.yourname.randomsceneloader").PatchAll();
            log.LogInfo("Harmony patches applied successfully");
        }
        catch (Exception ex) { log.LogError($"Failed to apply Harmony patches: {ex.Message}"); }

        try
        {
            if (GameObject.Find("__RoomRando") == null)
            {
                GameObject go = new GameObject("__RoomRando");
                RoomRando rando = go.AddComponent<RoomRando>();
                rando.Initialize(this);
                DontDestroyOnLoad(go);
            }
        }
        catch (Exception ex) { log.LogError($"Failed to create RoomRando: {ex.Message}"); }

        try
        {
            if (GameObject.Find("__SeedManager") == null)
            {
                GameObject go = new GameObject("__SeedManager");
                SeedManager seed = go.AddComponent<SeedManager>();
                seed.Initialize(this);
                DontDestroyOnLoad(go);
            }
        }
        catch (Exception ex) { log.LogError($"Failed to create SeedManager: {ex.Message}"); }
    }

    private void LoadRandomUniqueScene()
    {
        if (isTransitioning)
        {
            log.LogInfo("LoadRandomUniqueScene skipped: transition already in progress");
            return;
        }

        List<string> candidates = sceneConfigs.Keys
            .Where(k => !string.Equals(k, currentSceneName, StringComparison.OrdinalIgnoreCase) && !visitedScenesF6.Contains(k))
            .ToList();

        if (candidates.Count == 0)
        {
            log.LogInfo("F6: All scenes have been visited already (or no candidates available). Clearing visited list.");
            visitedScenesF6.Clear();
            candidates = sceneConfigs.Keys
                .Where(k => !string.Equals(k, currentSceneName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0) return;
        }

        List<string> preferred = null;
        try
        {
            List<string> lowered = preferredSceneSubstrings
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.ToLower())
                .ToList();
            preferred = candidates.Where(c => lowered.Any(sub => c.ToLower().Contains(sub))).ToList();
        }
        catch { }

        List<string> finalPool = preferred != null && preferred.Count > 0 ? preferred : candidates;
        string sceneName = finalPool[rng.Next(finalPool.Count)];
        SceneConfig config = sceneConfigs.ContainsKey(sceneName) ? sceneConfigs[sceneName] : null;
        string entryGate = null;
        if (config != null && config.Exits != null && config.Exits.Count > 0)
            entryGate = config.Exits[rng.Next(config.Exits.Count)];

        visitedScenesF6.Add(sceneName);
        lastEntryGateUsed = entryGate;
        log.LogInfo($"[F6] Selected unique random scene: {sceneName} (enter via gate '{entryGate ?? "(null)"}')");
        SuppressTransitionPatch = true;
        StartCoroutine(LoadSceneCoroutine(sceneName, entryGate));
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
            LoadRandomScene();
        if (Input.GetKeyDown(KeyCode.F6))
            LoadRandomUniqueScene();
        if (Input.GetKeyDown(KeyCode.F7))
            DiscoverGatesInCurrentScene();

        try
        {
            if (cfgTeleportConfirm != null && cfgTeleportConfirm.Value)
            {
                string sceneName = cfgTeleportScene != null ? cfgTeleportScene.Value : string.Empty;
                if (!string.IsNullOrEmpty(sceneName))
                    TeleportToScene(sceneName);
                else
                    log.LogWarning("TeleportScene is empty in config.");
                cfgTeleportConfirm.Value = false;
            }
        }
        catch { }
    }

    private void TeleportToScene(string sceneName)
    {
        try
        {
            if (isTransitioning)
            {
                log.LogInfo("Teleport skipped: transition already in progress");
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                log.LogWarning("TeleportToScene called with empty scene name");
                return;
            }

            // ---- 分离场景名和入口名 ----
            string desiredEntryGate = null;
            string scenePart = sceneName.Trim();

            // 查找最后一个空格，之前为场景，之后为入口
            int lastSpace = scenePart.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                desiredEntryGate = scenePart.Substring(lastSpace + 1).Trim();
                scenePart = scenePart.Substring(0, lastSpace).Trim();
                // 如果入口名为空字符串，则视为未指定
                if (string.IsNullOrEmpty(desiredEntryGate))
                    desiredEntryGate = null;
            }

            // ---- 模糊匹配场景 ----
            string entryGate = null;
            SceneConfig config = null;
            string matchedScene = null;

            // 1. 精确匹配（忽略大小写）
            if (sceneConfigs != null && sceneConfigs.TryGetValue(scenePart, out config))
            {
                matchedScene = scenePart;
            }
            else if (sceneConfigs != null)
            {
                var allScenes = sceneConfigs.Keys.ToList();
                string inputLetters = new string(scenePart.Where(char.IsLetter).ToArray());
                string inputDigits = new string(scenePart.Where(char.IsDigit).ToArray());

                // 2a. 再次尝试精确匹配（和 TryGetValue 相同，但可能有大小写差异）
                matchedScene = allScenes.FirstOrDefault(s =>
                    string.Equals(s, scenePart, StringComparison.OrdinalIgnoreCase));

                // 2b. 字母+数字匹配（针对有数字后缀的场景）
                if (matchedScene == null
                    && !string.IsNullOrEmpty(inputLetters)
                    && !string.IsNullOrEmpty(inputDigits))
                {
                    matchedScene = allScenes.FirstOrDefault(s =>
                    {
                        string sceneDigits = new string(s.Where(char.IsDigit).ToArray());
                        string sceneLetters = new string(s.Where(char.IsLetter).ToArray());
                        return !string.IsNullOrEmpty(sceneDigits)
                            && sceneLetters.IndexOf(inputLetters, StringComparison.OrdinalIgnoreCase) >= 0
                            && sceneDigits.IndexOf(inputDigits, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
                }

                // 2c. 纯字母匹配（针对无数字的场景）
                if (matchedScene == null
                    && !string.IsNullOrEmpty(inputLetters)
                    && string.IsNullOrEmpty(inputDigits))
                {
                    matchedScene = allScenes.FirstOrDefault(s =>
                    {
                        string sceneDigits = new string(s.Where(char.IsDigit).ToArray());
                        return string.IsNullOrEmpty(sceneDigits)
                            && s.IndexOf(inputLetters, StringComparison.OrdinalIgnoreCase) >= 0;
                    });
                }

                // 2d. 简单包含匹配（兜底）
                if (matchedScene == null)
                {
                    matchedScene = allScenes.FirstOrDefault(s =>
                        s.IndexOf(scenePart, StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            // ---- 确定入口门 ----
            if (matchedScene != null && sceneConfigs.TryGetValue(matchedScene, out config))
            {
                // 如果用户指定了入口，且该入口在场景的出口列表中，则使用；否则随机
                if (desiredEntryGate != null)
                {
                    if (config.Exits != null && config.Exits.Contains(desiredEntryGate, StringComparer.OrdinalIgnoreCase))
                    {
                        entryGate = desiredEntryGate;
                    }
                    else
                    {
                        log.LogWarning($"TeleportToScene: entry '{desiredEntryGate}' not valid for scene '{matchedScene}', will pick random.");
                        // 仍随机选一个
                        if (config.Exits != null && config.Exits.Count > 0)
                            entryGate = config.Exits[rng.Next(config.Exits.Count)];
                    }
                }
                else
                {
                    if (config.Exits != null && config.Exits.Count > 0)
                        entryGate = config.Exits[rng.Next(config.Exits.Count)];
                }
                log.LogInfo($"Teleport: {scenePart} -> {matchedScene} (entry '{entryGate ?? "(null)"}')");
            }
            else
            {
                log.LogWarning($"TeleportToScene: scene '{scenePart}' not found in sceneConfigs. Attempting direct load.");
                matchedScene = scenePart;
                if (desiredEntryGate != null)
                    entryGate = desiredEntryGate;
            }

            SuppressTransitionPatch = true;
            StartCoroutine(LoadSceneCoroutine(matchedScene, entryGate));
        }
        catch (Exception ex)
        {
            log.LogError($"TeleportToScene failed: {ex}");
        }
    }
    private void DiscoverGatesInCurrentScene()
    {
        log.LogInfo("=== DISCOVERING GATES ===");
        log.LogInfo($"Scene: {currentSceneName}");
        try
        {
            List<GameObject> gates = FindObjectsByType<GameObject>(FindObjectsSortMode.None)
                .Where(obj =>
                {
                    string lower = obj.name.ToLower();
                    return (lower.StartsWith("left") || lower.StartsWith("right") || lower.StartsWith("top") || lower.StartsWith("bot"))
                           && lower.Length <= 6 && char.IsDigit(lower[lower.Length - 1]);
                }).ToList();

            log.LogInfo($"Found {gates.Count} gates:");
            var grouped = gates.GroupBy(g => g.name).OrderBy(g => g.Key);
            foreach (var grp in grouped)
                log.LogInfo($"  - {grp.Key} (count: {grp.Count()}, pos: {grp.First().transform.position})");

            List<string> exitNames = grouped.Select(g => g.Key).ToList();
            if (exitNames.Count > 0)
            {
                string configLine = $"{{ \"{currentSceneName}\", new SceneConfig(new List<string> {{ {string.Join(", ", exitNames.Select(e => $"\"{e}\""))} }}) }},";
                log.LogInfo($"Suggested: {configLine}");
                if (autoDiscoveryMode)
                    SaveDiscoveryToFile(currentSceneName, exitNames, configLine);
            }
        }
        catch (Exception ex) { log.LogError($"DiscoverGates failed: {ex.Message}"); }
    }

    private void SaveDiscoveryToFile(string sceneName, List<string> exitNames, string configLine)
    {
        try
        {
            if (discoveredScenes.Contains(sceneName)) return;
            if (sceneConfigs != null && sceneConfigs.ContainsKey(sceneName))
            {
                log.LogInfo($"Discovery skipped for {sceneName}: already exists in sceneConfigs.");
                discoveredScenes.Add(sceneName);
                return;
            }
            discoveredScenes.Add(sceneName);
            File.AppendAllText(discoveryFilePath, $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {sceneName}\n  Gates found: {string.Join(", ", exitNames)}\n  Config: {configLine}\n");
            log.LogInfo($"Saved discovery for {sceneName} to file ({exitNames.Count} exits)");
        }
        catch (Exception ex) { log.LogError($"Failed to save discovery to file: {ex.Message}"); }
    }

    private void LoadRandomScene()
    {
        if (isTransitioning)
        {
            log.LogInfo("LoadRandomScene skipped: transition already in progress");
            return;
        }
        loadRequested = true;
        List<string> candidates = sceneConfigs.Keys.Where(k => !string.Equals(k, currentSceneName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0)
        {
            log.LogWarning("No candidate scenes found in sceneConfigs.");
            return;
        }
        List<string> preferred = null;
        try
        {
            List<string> lowered = preferredSceneSubstrings.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.ToLower()).ToList();
            preferred = candidates.Where(c => lowered.Any(sub => c.ToLower().Contains(sub))).ToList();
        }
        catch { }
        List<string> finalPool = preferred != null && preferred.Count > 0 ? preferred : candidates;
        string sceneName = finalPool[rng.Next(finalPool.Count)];
        SceneConfig config = sceneConfigs.ContainsKey(sceneName) ? sceneConfigs[sceneName] : null;
        string entryGate = null;
        if (config != null && config.Exits != null && config.Exits.Count > 0)
            entryGate = config.Exits[rng.Next(config.Exits.Count)];
        lastEntryGateUsed = entryGate;
        log.LogInfo($"Selected random scene: {sceneName} (enter via gate '{entryGate ?? "(null)"}')");
        SuppressTransitionPatch = true;
        StartCoroutine(LoadSceneCoroutine(sceneName, entryGate));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName, string entryGate)
    {
        if (!isTransitioning)
        {
            loadRequested = false;
            isTransitioning = true;
            lastEntryGateUsed = entryGate;
            float timeout = 8f;
            float startTime = Time.time;
            if (!string.IsNullOrEmpty(sceneName) && GameManager.instance != null)
            {
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
                    log.LogWarning($"BeginSceneTransition failed: {ex.Message}");
                    isTransitioning = false;
                    SuppressTransitionPatch = false;
                    yield break;
                }
                while (SceneManager.GetActiveScene().name != sceneName)
                {
                    if (Time.time - startTime > timeout)
                    {
                        log.LogWarning($"LoadSceneCoroutine timeout waiting for scene {sceneName}");
                        isTransitioning = false;
                        SuppressTransitionPatch = false;
                        yield break;
                    }
                    yield return waitTenthSecond;
                }
                yield return waitFifthSecond;
                isTransitioning = false;
            }
            else
            {
                try { SceneManager.LoadScene(sceneName); }
                catch (Exception ex) { log.LogWarning($"SceneManager.LoadScene failed: {ex.Message}"); }
                finally
                {
                    isTransitioning = false;
                }
            }
        }
        SuppressTransitionPatch = false;
    }

    public void SetLastEntryGate(string gateName) => lastEntryGateUsed = gateName;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        cachedSceneLabel = "Scene: " + currentSceneName;
        lastRenderedScene = currentSceneName;
        log.LogInfo($"Scene loaded: {currentSceneName}");
        if (autoDiscoveryMode)
            StartCoroutine(AutoDiscoverAfterLoad());
        else
            StartCoroutine(DetectEntryGateAfterLoad());
    }

    private IEnumerator AutoDiscoverAfterLoad()
    {
        yield return waitHalfSecond;
        DiscoverGatesInCurrentScene();
    }

    private IEnumerator DetectEntryGateAfterLoad()
    {
        yield return waitThirdSecond;
        try
        {
            HeroController hero = HeroController.instance;
            if (hero != null)
            {
                Vector3 heroPos = hero.transform.position;
                GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    string name = obj.name.ToLower();
                    if ((name.StartsWith("left") || name.StartsWith("right") || name.StartsWith("top") || name.StartsWith("bot"))
                        && name.Length <= 6 && char.IsDigit(name[name.Length - 1])
                        && Vector3.Distance(heroPos, obj.transform.position) < 10.0)
                    {
                        if (string.IsNullOrEmpty(lastEntryGateUsed))
                        {
                            lastEntryGateUsed = obj.name;
                            log.LogInfo($"Auto-detected entry gate: {obj.name}");
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex) { log.LogWarning($"DetectEntryGate failed: {ex.Message}"); }
    }

    private void OnActiveSceneChanged(Scene prev, Scene next)
    {
        // ★ 退出流程保护：如果新场景是菜单，不执行任何随机过渡逻辑
        if (next.name == "Menu_Title" || next.name == "Quit_To_Menu")
        {
            lastEntryGateUsed = null;
            return;
        }

        previousSceneName = currentSceneName;
        currentSceneName = next.name;
        try
        {
            RoomRando roomRando = GameObject.Find("__RoomRando")?.GetComponent<RoomRando>();
            if (roomRando != null && !string.IsNullOrEmpty(previousSceneName) && !string.IsNullOrEmpty(lastEntryGateUsed))
                roomRando.OnExitUsed(previousSceneName, lastEntryGateUsed);
        }
        catch (Exception ex) { log.LogWarning($"RoomRando notify failed: {ex.Message}"); }
        lastEntryGateUsed = null;
    }

    private void OnGUI()
    {
        try
        {
            if (sceneLabelStyle.normal.textColor != Color.white || sceneLabelStyle.fontSize != 14)
                sceneLabelStyle = new GUIStyle(GUI.skin.label) { normal = { textColor = Color.white }, fontSize = 14 };
            if (cfgShowSceneLabel != null && !cfgShowSceneLabel.Value) return;
            if (currentSceneName != lastRenderedScene)
            {
                cachedSceneLabel = "Scene: " + currentSceneName;
                lastRenderedScene = currentSceneName;
            }
            GUI.Label(new Rect(8f, 8f, 300f, 20f), cachedSceneLabel, sceneLabelStyle);
        }
        catch { }
    }

    internal class SceneConfig
    {
        public List<string> Exits;
        public SceneConfig(List<string> exits) => Exits = exits ?? new List<string>();
    }
}