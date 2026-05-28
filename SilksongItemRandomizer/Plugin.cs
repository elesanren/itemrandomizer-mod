using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random;
using Input = UnityEngine.Input;

namespace SilksongItemRandomizer;

[BepInPlugin("YourName.SilksongItemRandomizer", "Silksong Item Randomizer", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    private static HashSet<string> _destroyedPickupKeys = new();
    private static string _notificationMessage;
    private static float _notificationEndTime;
    private static GUIStyle _notificationStyle;
    private static Texture2D _bgTex;

    public static ConfigEntry<int> RandomSeed { get; private set; }
    public static ConfigEntry<bool> ItemRandomEnabled { get; private set; }
    public static Plugin Instance { get; private set; }

    public static ConfigEntry<bool> SilkRandomizerEnabled { get; private set; }
    public static ConfigEntry<int> SilkRandomMin { get; private set; }
    public static ConfigEntry<int> SilkRandomMax { get; private set; }

    private Harmony _harmonyItem;

    public static bool PublicItemRandomEnabled
    {
        get => ItemRandomEnabled.Value;
        set
        {
            if (ItemRandomEnabled.Value == value) return;
            ItemRandomEnabled.Value = value;
            Instance.Config.Save();

            if (Instance == null) return;

            if (value)
            {
                Instance.ApplyItemPatches();
                PickupPatch.EnableRandomizer();
            }
            else
            {
                Instance._harmonyItem.UnpatchSelf();
                PickupPatch.DisableRandomizer();
            }
        }
    }

    private static string DestroyedPickupsFilePath
    {
        get
        {
            var dir = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "destroyed_pickups.json");
        }
    }

    public static void ShowNotification(string message, float duration = 3f)
    {
        _notificationMessage = message;
        _notificationEndTime = Time.time + duration;
    }

    private Texture2D MakeTexture(int width, int height, Color col)
    {
        var pixels = new Color[width * height];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = col;
        var tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void OverrideBenchwarpLanguage()
    {
        var isChinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (!isChinese) return;

        var sourcePath = Path.Combine(Paths.PluginPath, "elesanren-Hard_Item_Randomizer", "en.json");
        var targetDir = Path.Combine(Paths.PluginPath, "homothety-Benchwarp", "languages");
        var targetPath = Path.Combine(targetDir, "en.json");

        if (!File.Exists(sourcePath))
        {
            Log.LogWarning($"[Benchwarp] 源文件不存在: {sourcePath}");
            return;
        }

        try
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            var backupPath = targetPath + ".backup";
            if (File.Exists(targetPath) && !File.Exists(backupPath))
                File.Copy(targetPath, backupPath, true);
            File.Copy(sourcePath, targetPath, true);
            Log.LogInfo($"[Benchwarp] 已覆盖语言文件: {sourcePath} -> {targetPath}");
        }
        catch (Exception ex)
        {
            Log.LogError($"[Benchwarp] 覆盖失败: {ex.Message}");
        }
    }

    public void RefreshBenchwarpUI()
    {
        var menu = GameObject.Find("WarpMenu") ?? GameObject.Find("BenchwarpMenu");
        if (menu != null && menu.activeInHierarchy)
        {
            menu.SetActive(false);
            menu.SetActive(true);
            Log.LogInfo("[Benchwarp] UI 已刷新");
        }
    }

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        RandomSeed = Config.Bind("General", "RandomSeed", 0, "随机种子 (0 表示随机)");
        ItemRandomEnabled = Config.Bind("General", "ItemRandomEnabled", true, "Enable/disable item randomization");

        SilkRandomizerEnabled = Config.Bind("Silk Randomizer", "Enabled", true, "启用灵丝获得/消耗随机化");
        SilkRandomMin = Config.Bind("Silk Randomizer", "MinAmount", 1, "随机获得/消耗的最小灵丝数量（1-9）");
        SilkRandomMax = Config.Bind("Silk Randomizer", "MaxAmount", 9, "随机获得/消耗的最大灵丝数量（1-9）");

        PickupPatch.Initialize();

        _harmonyItem = new Harmony("SilksongItemRandomizer.ItemPatches");
        if (ItemRandomEnabled.Value)
        {
            ApplyItemPatches();
            PickupPatch.EnableRandomizer();
        }

        OverrideBenchwarpLanguage();
        TrapRandomizer.LoadState();

        _bgTex = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f));

        Extracurrencypickup.RegisterAll();

        StartCoroutine(InitializeAfterLoad(RandomSeed.Value));
        SceneManager.sceneLoaded += OnSceneLoaded;
        LoadDestroyedKeys();
        StartCoroutine(InitTrapsAfterLoad());

        Harmony.CreateAndPatchAll(typeof(HeroRespawnReset));

        var hotkeyGo = new GameObject("SilksongItemRandomizer_HotkeyHandler");
        DontDestroyOnLoad(hotkeyGo);
        hotkeyGo.AddComponent<HotkeyHandler>();

        Log.LogInfo("Plugin SilksongItemRandomizer loaded (seed-based)");
    }

    private void ApplyItemPatches()
    {
        if (!ItemRandomEnabled.Value) return;
        _harmonyItem.PatchAll(typeof(PickupPatch));
        _harmonyItem.PatchAll(typeof(CurrencyCollectPatch));
        _harmonyItem.PatchAll(typeof(TryGetPatch));
        //_harmonyItem.PatchAll(typeof(ToolUnlockPatch));
        _harmonyItem.PatchAll(typeof(CrestRandomizePatch));
        CrestRandomizePatch.Initialize();
        _harmonyItem.PatchAll(typeof(ShopMenuStock_BuildItemList_Patch));
        _harmonyItem.PatchAll(typeof(ShopItemStats_Purchase_Patch));
        _harmonyItem.PatchAll(typeof(SilkSpearPityPatch));
        _harmonyItem.PatchAll(typeof(BenchRespawnPatch));
        _harmonyItem.PatchAll(typeof(SilkRandomizerPatch));
    }

    private IEnumerator InitTrapsAfterLoad()
    {
        yield return new WaitForSeconds(5f);
        TrapRandomizer.Initialize(RandomSeed.Value);
        Log.LogInfo("陷阱随机系统已初始化");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void LoadDestroyedKeys()
    {
        var path = DestroyedPickupsFilePath;
        if (File.Exists(path))
        {
            try
            {
                var list = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path));
                _destroyedPickupKeys = list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
            catch (Exception ex)
            {
                Log.LogError($"加载特殊点消失记录失败: {ex}");
                _destroyedPickupKeys = new HashSet<string>();
            }
        }
        else
        {
            _destroyedPickupKeys = new HashSet<string>();
        }
    }

    private void SaveDestroyedKeys()
    {
        var path = DestroyedPickupsFilePath;
        try
        {
            var json = JsonConvert.SerializeObject(_destroyedPickupKeys.ToList(), Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Log.LogError($"保存特殊点消失记录失败: {ex}");
        }
    }

    public static void AddDestroyedPickupKey(string key)
    {
        _destroyedPickupKeys.Add(key);
        Instance?.SaveDestroyedKeys();
    }

    public static void ResetDestroyedPickupKeys()
    {
        _destroyedPickupKeys.Clear();
        var path = DestroyedPickupsFilePath;
        if (File.Exists(path)) File.Delete(path);
    }

    public static void ResetAllStaticData()
    {
        CrestRandomizer.ResetMappings();
        CrestRandomizer.Initialize();
        CrestRandomizePatch.ResetProcessedIds();
        CurrencyCollectPatch.ResetCounters();
        CurrencyCollectPatch.ResetKeyState();
        SilkSpearPityPatch.ResetSilkSpearState();
        ResetDestroyedPickupKeys();
        ShopRandomizer.ResetCache();
        Extracurrencypickup.ResetAll();
        ShopMenuStock_BuildItemList_Patch.ResetAllCounts();
        TrapRandomizer.ClearAllCache();
        PickupPatch.ResetAll();
        Log.LogInfo("物品随机MOD所有静态数据已重置");
    }

    private IEnumerator InitializeAfterLoad(int seed)
    {
        yield return new WaitForSeconds(3f);
        ItemRandomizer.Initialize(seed);
        //ToolUnlockPatch.Initialize();
        CrestRandomizer.Initialize();
        Log.LogInfo($"Randomizer initialized with seed: {seed}");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ItemRandomEnabled.Value)
        {
            PickupPatch.ApplyStateToSceneWithDelay(scene, 0.2f);
        }

        // 修正：使用传入的 scene 参数
        Extracurrencypickup.SpawnPickupsForScene(scene);

        StartCoroutine(DestroyMarkedPickups(scene));
        if (TrapRandomizer.Enabled && scene.name != "Menu_Title" && scene.name != "Menu" && scene.name != "Loading")
        {
            TrapRandomizer.ClearAndRescan();
            StartCoroutine(SpawnTrapsAfterSceneLoad());
        }
    }

    private IEnumerator SpawnTrapsAfterSceneLoad()
    {
        yield return new WaitForSeconds(0.5f);
        TrapRandomizer.SpawnTraps();
    }

    private IEnumerator DestroyMarkedPickups(Scene scene)
    {
        yield return null;
        var pickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>();
        foreach (var p in pickups)
        {
            if (p.gameObject.scene != scene) continue;
            var key = $"{scene.name}_{p.transform.position.x:F1}_{p.transform.position.y:F1}_{p.transform.position.z:F1}";
            if (_destroyedPickupKeys.Contains(key))
            {
                Destroy(p.gameObject);
                Log.LogInfo("场景加载时销毁已标记点: " + key);
            }
        }
    }

    private void Update()
    {
        RecentItemsUI.UpdateAutoHide();
    }

    private void OnGUI()
    {
        RecentItemsUI.Draw();
        if (_notificationMessage != null && Time.time <= _notificationEndTime)
        {
            _notificationStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 40,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _bgTex }
            };
            var x = (Screen.width - 600f) / 2f;
            var y = Screen.height / 2 - 100;
            GUI.Box(new Rect(x, y, 600f, 120f), _notificationMessage, _notificationStyle);
        }
        else
        {
            _notificationMessage = null;
        }
    }

    public void DumpAllMappings()
    {
        var seed = RandomSeed.Value;
        Log.LogInfo($"===== 当前种子: {seed} =====");
        var crestList = CrestRandomizer.CrestList;
        if (crestList != null && crestList.Count > 0)
        {
            Log.LogInfo("--- 纹章映射 (来自外部存储) ---");
            foreach (var crest in crestList)
                Log.LogInfo($"  {crest.name} -> {CrestRandomizer.GetMappedCrestName(crest.name)}");
        }
        else
        {
            Log.LogInfo("--- 未找到纹章 ---");
        }

        Log.LogInfo("--- 当前场景拾取点映射 ---");
        var pickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>().Where(p => p.gameObject.scene.isLoaded).ToList();
        if (pickups.Count == 0)
        {
            Log.LogInfo("当前场景无拾取点。");
        }
        else
        {
            foreach (var p in pickups)
            {
                var original = p.Item;
                if (original == null) continue;
                var rng = new Random(seed + p.GetInstanceID());
                var random = ItemRandomizer.PeekRandomItem(rng);
                if (random != null)
                    Log.LogInfo($"  {original.name} (位置 {p.transform.position}) -> {random.name}");
                else
                    Log.LogInfo($"  {original.name} -> 随机失败");
            }
        }
        Log.LogInfo("===============================");
    }
}