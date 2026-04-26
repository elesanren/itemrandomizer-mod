using BepInEx.Configuration;
using System;
using UnityEngine;

namespace HKSilksong_Randomizer;

public class SeedManager : MonoBehaviour
{
    private RoomRando roomRando;
    private RandomSceneLoader sceneLoader;
    private ConfigEntry<int> cfgNewSeed;
    private ConfigEntry<bool> cfgRegenerateTrigger;
    private ConfigEntry<string> cfgCurrentSeed;
    private ConfigEntry<bool> cfgShowSeedOnScreen;
    private GUIStyle seedLabelStyle;
    private string cachedSeedLabel = "";
    private int lastRenderedSeed = -1;
    private float configPollTimer = 0.0f;
    private const float CONFIG_POLL_INTERVAL = 0.1f;

    // 新增初始化方法，替代原来的 Start
    public void Initialize(RandomSceneLoader loader)
    {
        sceneLoader = loader;
        roomRando = FindAnyObjectByType<RoomRando>();
        if (roomRando == null)
        {
            Debug.LogWarning("SeedManager: Could not find RoomRando component!");
            return;
        }

        try
        {
            ConfigFile config = sceneLoader.Config;
            cfgShowSeedOnScreen = config.Bind<bool>("Seed Manager", "ShowSeedOnScreen", true, "Show current seed text on screen (toggle to hide)");
            cfgCurrentSeed = config.Bind<string>("Seed Manager", "CurrentSeed", roomRando.GetGenerationSeed().ToString(), "Current generation seed (read-only display)");
            cfgNewSeed = config.Bind<int>("Seed Manager", "NewSeed", 0, "Enter a seed value to regenerate the map");
            cfgRegenerateTrigger = config.Bind<bool>("Seed Manager", "RegenerateNow", false, "Set to true to regenerate map with NewSeed (resets automatically)");

            UpdateCurrentSeedDisplay();
            seedLabelStyle = new GUIStyle();
        }
        catch (Exception ex)
        {
            Debug.LogError($"SeedManager: Failed to bind config entries: {ex.Message}");
        }
    }

    private void Update()
    {
        if (roomRando == null || cfgRegenerateTrigger == null || cfgNewSeed == null)
            return;

        configPollTimer += Time.deltaTime;
        if (configPollTimer < CONFIG_POLL_INTERVAL)
            return;
        configPollTimer = 0.0f;

        try
        {
            if (!cfgRegenerateTrigger.Value)
                return;

            int seed = cfgNewSeed.Value;
            if (seed != 0)
            {
                Debug.Log($"SeedManager: Regenerating connections with seed {seed}");
                roomRando.RegenerateWithSeed(seed);
                UpdateCurrentSeedDisplay();
            }
            else
                Debug.LogWarning("SeedManager: NewSeed is 0, please enter a valid seed value");

            cfgRegenerateTrigger.Value = false;
        }
        catch (Exception ex)
        {
            Debug.LogError($"SeedManager: Error during regeneration: {ex.Message}");
            cfgRegenerateTrigger.Value = false;
        }
    }

    private void UpdateCurrentSeedDisplay()
    {
        if (roomRando == null || cfgCurrentSeed == null)
            return;

        int generationSeed = roomRando.GetGenerationSeed();
        cfgCurrentSeed.Value = generationSeed.ToString();
        cachedSeedLabel = "Current Seed: " + generationSeed.ToString();
        lastRenderedSeed = generationSeed;
    }

    private void OnGUI()
    {
        if (cfgShowSeedOnScreen == null || !cfgShowSeedOnScreen.Value || roomRando == null)
            return;

        try
        {
            if (seedLabelStyle.normal.textColor != Color.white || seedLabelStyle.fontSize != 16)
            {
                seedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    normal = { textColor = Color.white },
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };
            }

            int generationSeed = roomRando.GetGenerationSeed();
            if (generationSeed != lastRenderedSeed)
            {
                cachedSeedLabel = "Seed: " + generationSeed.ToString();
                lastRenderedSeed = generationSeed;
            }

            GUI.Label(new Rect(10f, 40f, 400f, 25f), cachedSeedLabel, seedLabelStyle);
        }
        catch
        {
        }
    }
}