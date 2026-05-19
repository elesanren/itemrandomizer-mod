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

    private Harmony _harmonyItem;

    public static bool PublicItemRandomEnabled
    {
        get => ItemRandomEnabled.Value;
        set
        {
            if (ItemRandomEnabled.Value == value) return;
            ItemRandomEnabled.Value = value;
            Instance.Config.Save();
            if (Instance != null)
            {
                if (value)
                    Instance.ApplyItemPatches();
                else
                    Instance._harmonyItem.UnpatchSelf();
            }
        }
    }

    private static string DestroyedPickupsFilePath
    {
        get
        {
            string dir = Path.Combine(Paths.ConfigPath, "SilksongItemRandomizer");
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
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void OverrideBenchwarpLanguage()
    {
        bool isChinese = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (!isChinese) return;

        string sourcePath = Path.Combine(Paths.PluginPath, "elesanren-Hard_Item_Randomizer", "en.json");
        string targetDir = Path.Combine(Paths.PluginPath, "homothety-Benchwarp", "languages");
        string targetPath = Path.Combine(targetDir, "en.json");

        if (!File.Exists(sourcePath))
        {
            Log.LogWarning($"[Benchwarp] 源文件不存在: {sourcePath}");
            return;
        }

        try
        {
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            string backupPath = targetPath + ".backup";
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
        GameObject menu = GameObject.Find("WarpMenu") ?? GameObject.Find("BenchwarpMenu");
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
        ItemRandomEnabled = Config.Bind("General", "ItemRandomEnabled", true, "Enable/disable item randomization (pickups, shops, crests, etc.)");
        _harmonyItem = new Harmony("SilksongItemRandomizer.ItemPatches");
        ApplyItemPatches();

        OverrideBenchwarpLanguage();

        TrapRandomizer.LoadState();

        _bgTex = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f));

        StartCoroutine(InitializeAfterLoad(RandomSeed.Value));
        SceneManager.sceneLoaded += OnSceneLoaded;
        LoadDestroyedKeys();

        StartCoroutine(InitTrapsAfterLoad());

        Harmony.CreateAndPatchAll(typeof(HeroRespawnReset));

        // ★ 关键：创建并持久化 HotkeyHandler 游戏对象，确保热键始终有效
        GameObject hotkeyGo = new GameObject("SilksongItemRandomizer_HotkeyHandler");
        DontDestroyOnLoad(hotkeyGo);
        hotkeyGo.AddComponent<HotkeyHandler>();
        Log.LogInfo("[Plugin] HotkeyHandler 已创建并持久化，F6 可传送至上次坐的椅子");

        Log.LogInfo("Plugin SilksongItemRandomizer loaded (seed-based)");
    }

    private void ApplyItemPatches()
    {
        if (!ItemRandomEnabled.Value) return;
        _harmonyItem.PatchAll(typeof(PickupPatch));
        _harmonyItem.PatchAll(typeof(CurrencyCollectPatch));
        _harmonyItem.PatchAll(typeof(TryGetPatch));
        _harmonyItem.PatchAll(typeof(ToolUnlockPatch));
        _harmonyItem.PatchAll(typeof(CrestRandomizePatch));
        CrestRandomizePatch.Initialize();
        _harmonyItem.PatchAll(typeof(ShopMenuStock_BuildItemList_Patch));
        _harmonyItem.PatchAll(typeof(ShopItemStats_Purchase_Patch));
        _harmonyItem.PatchAll(typeof(SilkSpearPityPatch));
        _harmonyItem.PatchAll(typeof(BenchRespawnPatch));
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
        string path = DestroyedPickupsFilePath;
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
        string path = DestroyedPickupsFilePath;
        try
        {
            string json = JsonConvert.SerializeObject(_destroyedPickupKeys.ToList(), Formatting.Indented);
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
        string path = DestroyedPickupsFilePath;
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
        ShopMenuStock_BuildItemList_Patch.ResetAllCounts();
        TrapRandomizer.ClearAllCache();
        Log.LogInfo("物品随机MOD所有静态数据已重置");
    }

    private IEnumerator InitializeAfterLoad(int seed)
    {
        yield return new WaitForSeconds(3f);
        ItemRandomizer.Initialize(seed);
        ToolUnlockPatch.Initialize();
        CrestRandomizer.Initialize();
        Log.LogInfo($"Randomizer initialized with seed: {seed}");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
            if (p.gameObject.scene == scene)
            {
                string key = $"{scene.name}_{p.transform.position.x:F1}_{p.transform.position.y:F1}_{p.transform.position.z:F1}";
                if (_destroyedPickupKeys.Contains(key))
                {
                    Destroy(p.gameObject);
                    Log.LogInfo("场景加载时销毁已标记点: " + key);
                }
            }
        }
    }

    private void Update()
    {
        // 自动隐藏最近物品 UI（必须保留）
        RecentItemsUI.UpdateAutoHide();
        // 所有热键处理已迁移至 HotkeyHandler，此处不再重复
    }

    private void OnGUI()
    {
        RecentItemsUI.Draw();
        if (_notificationMessage != null && Time.time <= _notificationEndTime)
        {
            if (_notificationStyle == null)
            {
                _notificationStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 40,
                    alignment = TextAnchor.MiddleCenter
                };
                _notificationStyle.normal.textColor = Color.white;
                _notificationStyle.normal.background = _bgTex;
            }
            float width = 600f;
            float height = 120f;
            float x = (Screen.width - width) / 2f;
            float y = Screen.height / 2 - 100;
            GUI.Box(new Rect(x, y, width, height), _notificationMessage, _notificationStyle);
        }
        else
        {
            _notificationMessage = null;
        }
    }

    public void DumpAllMappings()
    {
        int seed = RandomSeed.Value;
        Log.LogInfo($"===== 当前种子: {seed} =====");
        var crestList = CrestRandomizer.CrestList;
        if (crestList != null && crestList.Count > 0)
        {
            Log.LogInfo("--- 纹章映射 (来自外部存储) ---");
            foreach (var crest in crestList)
            {
                string mapped = CrestRandomizer.GetMappedCrestName(crest.name);
                Log.LogInfo($"  {crest.name} -> {mapped}");
            }
        }
        else
        {
            Log.LogInfo("--- 未找到纹章 ---");
        }
        Log.LogInfo("--- 当前场景拾取点映射 ---");
        var pickups = Resources.FindObjectsOfTypeAll<CollectableItemPickup>()
            .Where(p => p.gameObject.scene.isLoaded).ToList();
        if (pickups.Count == 0)
        {
            Log.LogInfo("当前场景无拾取点。");
        }
        else
        {
            foreach (var p in pickups)
            {
                SavedItem original = p.Item;
                if (original != null)
                {
                    var rng = new Random(seed + p.GetInstanceID());
                    SavedItem random = ItemRandomizer.PeekRandomItem(rng);
                    if (random != null)
                        Log.LogInfo($"  {original.name} (位置 {p.transform.position}) -> {random.name}");
                    else
                        Log.LogInfo($"  {original.name} -> 随机失败");
                }
            }
        }
        Log.LogInfo("===============================");
    }
}