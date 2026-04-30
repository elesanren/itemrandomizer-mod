using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HKSilksong_Randomizer;

[HarmonyPatch(typeof(GameManager), "BeginSceneTransition")]
public class TransitionPatch
{
    private static RoomRando roomRando;
    private static bool isRedirecting;

    private static bool Prefix(ref GameManager.SceneLoadInfo info)
    {
        try
        {
            if (isRedirecting)
            {
                isRedirecting = false;
                return true;
            }
            // 死亡重生时放行原生逻辑，不随机
            if (GameManager.instance != null && GameManager.instance.RespawningHero)
                return true;
            // ★ 新增：全局开关关闭时，直接放行原版过渡
            if (!RandomSceneLoader.EnableRandomization)
                return true;

            try
            {
                if (RandomSceneLoader.SuppressTransitionPatch)
                {
                    Debug.Log("TransitionPatch: suppression active, allowing original transition");
                    return true;
                }
            }
            catch
            {
            }

            if (roomRando == null)
            {
                GameObject gameObject = GameObject.Find("__RoomRando");
                if (gameObject != null)
                    roomRando = gameObject.GetComponent<RoomRando>();
            }

            if (roomRando == null)
            {
                Debug.LogWarning("TransitionPatch: RoomRando not found, allowing normal transition");
                return true;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            string currentScene = activeScene.name;

            if (!string.IsNullOrEmpty(info.EntryGateName))
            {
                Debug.Log($"TransitionPatch: intercepted transition from {currentScene} via exit to {info.SceneName} entry gate {info.EntryGateName}");
                string exitGateBeingUsed = FindExitGateBeingUsed(currentScene, info);

                if (!string.IsNullOrEmpty(exitGateBeingUsed))
                {
                    Debug.Log($"TransitionPatch: detected exit gate used: {exitGateBeingUsed}");
                    var connection = roomRando.GetConnection(currentScene, exitGateBeingUsed);

                    if (connection.HasValue)
                    {
                        Debug.Log($"TransitionPatch: redirecting to {connection.Value.targetScene} via {connection.Value.targetGate}");

                        GameManager.SceneLoadInfo sceneLoadInfo = new GameManager.SceneLoadInfo()
                        {
                            SceneName = connection.Value.targetScene,
                            EntryGateName = connection.Value.targetGate,
                            EntryDelay = 0.0f,
                            WaitForSceneTransitionCameraFade = true,
                            PreventCameraFadeOut = false,
                            Visualization = GameManager.SceneLoadVisualizations.Default,
                            AlwaysUnloadUnusedAssets = true
                        };

                        isRedirecting = true;
                        GameManager.instance.BeginSceneTransition(sceneLoadInfo);
                        return false;
                    }

                    Debug.Log($"TransitionPatch: no randomized connection for {currentScene}_{exitGateBeingUsed}");
                }
            }

            Debug.Log($"TransitionPatch: allowing normal transition from {currentScene} to {info.SceneName}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"TransitionPatch error: {ex}");
            return true;
        }
    }

    private static string FindExitGateBeingUsed(string currentScene, GameManager.SceneLoadInfo info)
    {
        try
        {
            List<string> validExitsForScene = roomRando?.GetValidExitsForScene(currentScene);
            if (validExitsForScene == null || validExitsForScene.Count == 0)
            {
                Debug.LogWarning($"FindExitGateBeingUsed: no valid exits found for {currentScene}");
                return null;
            }

            HeroController instance = HeroController.instance;
            if (instance == null)
                return null;

            Vector3 position = instance.transform.position;
            GameObject[] objectsByType = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            float num1 = float.MaxValue;
            string exitGateBeingUsed = null;

            foreach (GameObject gameObject in objectsByType)
            {
                string name = gameObject.name;
                if (validExitsForScene.Any(exit => string.Equals(exit, name, StringComparison.OrdinalIgnoreCase)))
                {
                    float num2 = Vector3.Distance(position, gameObject.transform.position);
                    if (num2 < 5.0 && num2 < num1)
                    {
                        num1 = num2;
                        exitGateBeingUsed = name;
                    }
                }
            }

            if (!string.IsNullOrEmpty(exitGateBeingUsed))
                Debug.Log($"FindExitGateBeingUsed: found valid exit '{exitGateBeingUsed}' near hero in {currentScene}");

            return exitGateBeingUsed;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"FindExitGateBeingUsed error: {ex.Message}");
            return null;
        }
    }
}