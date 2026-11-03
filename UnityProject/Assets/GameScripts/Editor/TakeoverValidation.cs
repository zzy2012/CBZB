using System;
using System.IO;
using System.Linq;
using GameConfig;
using GameLogic.Takeover;
using Luban;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TakeoverValidation
{
    private const string ByteRoot = "Assets/AssetRaw/Configs/bytes";
    private const string PlaySmokeSessionKey = "TakeoverValidation.PlaySmoke.Active";
    private const string PlaySmokeDeadlineKey = "TakeoverValidation.PlaySmoke.Deadline";

    [InitializeOnLoadMethod]
    private static void HookPlaySmoke()
    {
        if (!SessionState.GetBool(PlaySmokeSessionKey, false)) return;
        EditorApplication.update -= PollPlaySmoke;
        EditorApplication.update += PollPlaySmoke;
    }

    [MenuItem("TEngine/Takeover/运行首章规则校验")]
    public static void RunFromMenu()
    {
        var failures = RunChecks();
        if (failures.Length == 0)
            Debug.Log("[TakeoverValidation] PASS");
        else
            Debug.LogError("[TakeoverValidation] FAIL\n" + string.Join("\n", failures.Select(x => "- " + x)));
    }

    /// <summary>Entry point for Unity batch mode:
    /// -executeMethod TakeoverValidation.RunBatch -quit.
    /// </summary>
    public static void RunBatch()
    {
        var failures = RunChecks();
        if (failures.Length == 0)
        {
            Debug.Log("[TakeoverValidation] PASS");
            if (Application.isBatchMode) EditorApplication.Exit(0);
            return;
        }

        foreach (var failure in failures) Debug.LogError("[TakeoverValidation] " + failure);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }

    [MenuItem("TEngine/Takeover/运行首章八关压力测试")]
    public static void RunScenarioSoakFromMenu()
    {
        var results = RunScenarioSoak();
        foreach (var result in results)
        {
            Debug.Log($"[TakeoverSoak] L{result.LevelId} over={result.IsOver} win={result.HumanVictory} " +
                      $"time={result.ElapsedSeconds:F1}s player={result.HumanSquads} enemy={result.EnemySquads} " +
                      $"playerPos={result.HumanPosition} enemyPos={result.EnemyPosition} " +
                      $"playerHp={result.HumanHp:F0} enemyHp={result.EnemyHp:F0} " +
                      $"moving={result.HumanMoving} inBounds={result.UnitsInBounds}");
        }
    }

    public static void RunScenarioSoakBatch()
    {
        try
        {
            var results = RunScenarioSoak();
            bool pass = results.Count == 8 && results.All(r => r.ElapsedSeconds > 0f && r.UnitsInBounds);
            foreach (var result in results)
            {
                Debug.Log($"[TakeoverSoak] L{result.LevelId} over={result.IsOver} win={result.HumanVictory} " +
                          $"time={result.ElapsedSeconds:F1}s player={result.HumanSquads} enemy={result.EnemySquads} " +
                          $"playerPos={result.HumanPosition} enemyPos={result.EnemyPosition} " +
                          $"playerHp={result.HumanHp:F0} enemyHp={result.EnemyHp:F0} " +
                          $"moving={result.HumanMoving} inBounds={result.UnitsInBounds}");
            }
            Debug.Log(pass ? "[TakeoverValidation] SOAK COMPLETE" : "[TakeoverValidation] SOAK FAIL");
            if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogError("[TakeoverValidation] SOAK FAIL: " + exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    public static void RunMovementDiagnosticBatch()
    {
        try
        {
            var tables = new Tables(Load);
            Debug.Log("[TakeoverMovement]\n" + TakeoverSelfCheck.DebugMovement(tables, 1, 30f));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("[TakeoverMovement] FAIL: " + exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    /// <summary>Noninteractive Unity entry point used by CI:
    /// -executeMethod TakeoverValidation.RunPlaySmoke (without -quit).</summary>
    public static void RunPlaySmoke()
    {
        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/main.unity");
        if (scene == null)
        {
            Debug.LogError("[TakeoverValidation] main.unity not found");
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene("Assets/Scenes/main.unity");
        SessionState.SetBool(PlaySmokeSessionKey, true);
        SessionState.SetFloat(PlaySmokeDeadlineKey,
            (float)EditorApplication.timeSinceStartup + 45f);
        EditorApplication.update -= PollPlaySmoke;
        EditorApplication.update += PollPlaySmoke;
        EditorApplication.isPlaying = true;
    }

    private static void PollPlaySmoke()
    {
        if (!SessionState.GetBool(PlaySmokeSessionKey, false)) return;
        float deadline = SessionState.GetFloat(PlaySmokeDeadlineKey, 0f);
        if (EditorApplication.timeSinceStartup > deadline)
        {
            FinishPlaySmoke(false, "45 秒内未完成 Play Mode 冒烟");
            return;
        }

        if (!EditorApplication.isPlaying) return;
        var runner = UnityEngine.Object.FindObjectOfType<TakeoverRunner>();
        if (runner == null) return;

        if (runner.Simulation == null)
        {
            if (!Campaign.IsUnlocked(1))
                Campaign.Reset();
            runner.StartLevel(1);
            SessionState.SetFloat("TakeoverValidation.PlaySmoke.Started",
                (float)EditorApplication.timeSinceStartup);
            return;
        }

        float started = SessionState.GetFloat("TakeoverValidation.PlaySmoke.Started", 0f);
        if (started <= 0f || EditorApplication.timeSinceStartup - started < 3f) return;

        bool uiReady = GameModule.UI.HasWindow<TakeoverBattleUI>();
        bool simulated = runner.Simulation.Time > 0.5f;
        const string music = "takeover_sfx_2910_musicGameplayClass_musicGameplayClass";
        Debug.Log($"[TakeoverValidation] audio candidates: " +
                  $"plain={GameModule.Resource.CheckLocationValid(music)} " +
                  $"ext={GameModule.Resource.CheckLocationValid(music + ".wav")}");
        Debug.Log($"[TakeoverValidation] original font valid=" +
                  $"{GameModule.Resource.CheckLocationValid("takeover_font_328_Chaparral_Pro")}");
        FinishPlaySmoke(uiReady && simulated,
            uiReady ? (simulated ? string.Empty : "模拟没有推进")
                : "TakeoverBattleUI 未创建");
    }

    private static void FinishPlaySmoke(bool success, string message)
    {
        SessionState.EraseBool(PlaySmokeSessionKey);
        EditorApplication.update -= PollPlaySmoke;
        if (success)
            Debug.Log("[TakeoverValidation] PLAY PASS");
        else
            Debug.LogError("[TakeoverValidation] PLAY FAIL: " + message);
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
    }

    private static string[] RunChecks()
    {
        if (!Directory.Exists(ByteRoot))
            return new[] { $"配置目录不存在: {ByteRoot}" };

        try
        {
            var tables = new Tables(Load);
            var failures = TakeoverSelfCheck.Run(tables).ToList();
            CheckAssetCounts(failures);
            CheckUiTextPlaceholders(failures);
            CheckUiPrefabStructure(failures);
            return failures.ToArray();
        }
        catch (Exception exception)
        {
            return new[] { $"配置加载失败: {exception.GetType().Name}: {exception.Message}" };
        }
    }

    private static void CheckAssetCounts(System.Collections.Generic.ICollection<string> failures)
    {
        var spriteRoot = "Assets/AssetRaw/Actor/Takeover/Sprites";
        var mapRoot = "Assets/AssetRaw/Scenes/Takeover/Maps";
        var audioRoot = "Assets/AssetRaw/Audios/Takeover";
        var fontRoot = "Assets/AssetRaw/Fonts/Takeover";
        CheckCount(spriteRoot, "takeover_unit_*.png", 45, "兵种精灵", failures);
        CheckCount(spriteRoot, "takeover_unit_*_f*.png", 135, "兵种动画帧", failures);
        CheckCount(spriteRoot, "takeover_building_*.png", 35, "建筑精灵", failures);
        CheckCount(mapRoot, "takeover_map_*.png", 8, "地图底图", failures);
        CheckCount(audioRoot, "*.wav", 70, "原版音频", failures);
        CheckCount(fontRoot, "*.ttf", 10, "原版字体", failures);
        CheckCount("Assets/AssetRaw/UI/Icons", "takeover_status_*.png", 5, "状态图标", failures);

        foreach (int buildingId in new[] { 1, 2, 3, 4, 5, 6, 7 })
        {
            foreach (string color in new[] { "green", "red", "blue", "purple" })
            {
                var path = Path.Combine(spriteRoot,
                    $"takeover_building_{buildingId}_{color}.png");
                if (!File.Exists(path)) failures.Add($"缺少建筑资源: {path}");
            }
        }
    }

    private static readonly string[] UiWindowPrefabs =
    {
        "Assets/AssetRaw/UI/TakeoverLevelSelectUI.prefab",
        "Assets/AssetRaw/UI/TakeoverBattleUI.prefab",
        "Assets/AssetRaw/UI/TakeoverResultUI.prefab",
        "Assets/AssetRaw/UI/TakeoverToastUI.prefab",
    };

    private static readonly string[] UiPrefabs =
    {
        "Assets/AssetRaw/UI/TakeoverLevelSelectUI.prefab",
        "Assets/AssetRaw/UI/TakeoverBattleUI.prefab",
        "Assets/AssetRaw/UI/TakeoverResultUI.prefab",
        "Assets/AssetRaw/UI/TakeoverToastUI.prefab",
        "Assets/AssetRaw/UI/TakeoverUIActionButton.prefab",
    };

    private static readonly string[] LevelSelectPaths =
    {
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_text_Title",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_text_Hint",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_btn_ResetSave",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_btn_ResetSave/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_btn_Edicts",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_text_EdictTitle",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_text_EdictPoints",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_rect_EdictList",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_text_EdictHint",
        "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_btn_EdictClose",
    };

    private static readonly string[] BattleHudPaths =
    {
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_HpFill",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_Ring",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_Portrait",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_StatusFrozen",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_StatusPoison",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_StatusCharge",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_StatusShield",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/m_img_StatusFear",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype/m_img_Ring",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype/m_img_Inside",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype/m_img_HpBack",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype/m_img_HpFill",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype/m_text_Level",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_rect_Top/m_text_Resources",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_rect_Top/m_text_Clock",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_btn_Settings",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_btn_AiToggle",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_btn_AiToggle/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_Controls/m_btn_Pause",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_Controls/m_btn_Restart",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_Controls/m_btn_Levels",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SpeedControls/m_btn_Speed1/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SpeedControls/m_btn_Speed4/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SpeedControls/m_btn_Speed16/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SpeedControls/m_btn_Speed64/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SpeedControls/m_btn_Speed256/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_Inspector/m_text_InspectorTitle",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_Inspector/m_text_InspectorBody",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_Inspector/m_rect_Actions",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_SpellBar",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_btn_GmToggle/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmGold",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmMana",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmWin",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmLose",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_text_GmTitle",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_text_GmHint",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmSpeed1/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmSpeed2/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmSpeed4/m_text_Label",
        "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmSpeed8/m_text_Label",
    };

    private static readonly string[] ResultPaths =
    {
        "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_text_Title",
        "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_text_Body",
        "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Replay",
        "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Next",
        "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Menu",
    };

    private static readonly string[] ToastPaths =
    {
        "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel",
        "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel/m_text_ToastText",
    };

    /// <summary>窗口 Canvas、Widget 节点和交互控件均由预制体提供。</summary>
    private static void CheckUiPrefabStructure(System.Collections.Generic.ICollection<string> failures)
    {
        var prefabs = new System.Collections.Generic.Dictionary<string, GameObject>();
        foreach (string path in UiWindowPrefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add($"UI 预制体缺失: {path}");
                continue;
            }

            prefabs[path] = prefab;
            if (prefab.GetComponent<Canvas>() == null)
                failures.Add($"UI 窗口缺少 Canvas: {path}");
            var rootRect = prefab.GetComponent<RectTransform>();
            if (rootRect == null)
                failures.Add($"UI 窗口缺少根 RectTransform: {path}");
            else if (rootRect.localScale != Vector3.one ||
                     rootRect.anchorMin != Vector2.zero ||
                     rootRect.anchorMax != Vector2.one ||
                     rootRect.pivot != new Vector2(0.5f, 0.5f))
                failures.Add($"UI 窗口根 RectTransform 未铺满 Canvas: {path}");
            var scaler = prefab.GetComponent<CanvasScaler>();
            if (scaler == null)
                failures.Add($"UI 窗口缺少 CanvasScaler: {path}");
            else if (scaler.referenceResolution != new Vector2(800f, 600f) ||
                     scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight ||
                     !Mathf.Approximately(scaler.matchWidthOrHeight, 0.5f))
                failures.Add($"UI 窗口 CanvasScaler 设置不符合 800x600 舞台: {path}");
            if (prefab.GetComponent<GraphicRaycaster>() == null)
                failures.Add($"UI 窗口缺少 GraphicRaycaster: {path}");
        }

        GameObject actionButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabs[4]);
        if (actionButtonPrefab == null)
            failures.Add($"UI 预制体缺失: {UiPrefabs[4]}");
        else
            prefabs[UiPrefabs[4]] = actionButtonPrefab;

        CheckChildPaths(prefabs, UiPrefabs[0], LevelSelectPaths, failures);
        CheckChildPaths(prefabs, UiPrefabs[1], BattleHudPaths, failures);
        CheckChildPaths(prefabs, UiPrefabs[2], ResultPaths, failures);
        CheckChildPaths(prefabs, UiPrefabs[3], ToastPaths, failures);

        if (prefabs.TryGetValue(UiPrefabs[0], out GameObject levelSelect))
        {
            CheckComponent<RectTransform>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect", UiPrefabs[0], failures);
            CheckComponent<Text>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_text_Title",
                UiPrefabs[0], failures);
            CheckComponent<Text>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_text_Hint",
                UiPrefabs[0], failures);
            CheckComponent<Button>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_btn_ResetSave",
                UiPrefabs[0], failures);
            CheckComponent<Text>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_btn_ResetSave/m_text_Label",
                UiPrefabs[0], failures);
            CheckComponent<Button>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_btn_Edicts",
                UiPrefabs[0], failures);
            CheckComponent<RectTransform>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel",
                UiPrefabs[0], failures);
            CheckComponent<Text>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_text_EdictPoints",
                UiPrefabs[0], failures);
            CheckComponent<RectTransform>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_rect_EdictList",
                UiPrefabs[0], failures);
            CheckComponent<Button>(levelSelect,
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel/m_btn_EdictClose",
                UiPrefabs[0], failures);
            for (int levelId = 1; levelId <= 8; levelId++)
            {
                string path = $"m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_ChapterPanel/m_item_LevelButton{levelId}";
                CheckComponent<Button>(levelSelect, path, UiPrefabs[0], failures);
                CheckComponent<Text>(levelSelect, path + "/m_text_Label", UiPrefabs[0], failures);
            }
        }

        if (prefabs.TryGetValue(UiPrefabs[1], out GameObject battle))
        {
            CheckComponent<RectTransform>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud", UiPrefabs[1], failures);
            Transform badgePrototype = FindChild(battle.transform,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype");
            if (badgePrototype != null && badgePrototype.gameObject.activeSelf)
                failures.Add($"UI 徽标原型必须默认隐藏: {UiPrefabs[1]}");
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_rect_Top/m_text_Resources",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_rect_Top/m_text_Clock",
                UiPrefabs[1], failures);
            CheckComponent<Button>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_Controls/m_btn_Pause",
                UiPrefabs[1], failures);
            CheckComponent<Button>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_Controls/m_btn_Restart",
                UiPrefabs[1], failures);
            CheckComponent<Button>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_Controls/m_btn_Levels",
                UiPrefabs[1], failures);
            CheckComponent<Button>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_btn_Settings",
                UiPrefabs[1], failures);
            CheckComponent<Button>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_btn_AiToggle",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_BottomBar/m_btn_AiToggle/m_text_Label",
                UiPrefabs[1], failures);
            CheckComponent<RectTransform>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_Inspector",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_Inspector/m_text_InspectorTitle",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_Inspector/m_text_InspectorBody",
                UiPrefabs[1], failures);
            CheckComponent<RectTransform>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_SpellBar",
                UiPrefabs[1], failures);
            CheckComponent<RectTransform>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel",
                UiPrefabs[1], failures);
            const string gmContentPath =
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent";
            CheckComponent<Image>(battle, gmContentPath, UiPrefabs[1], failures);
            Transform gmContent = FindChild(battle.transform, gmContentPath);
            if (gmContent != null)
            {
                if (gmContent.gameObject.activeSelf)
                    failures.Add($"GM 面板内容必须默认折叠: {UiPrefabs[1]}");
                Image gmBackground = gmContent.GetComponent<Image>();
                if (gmBackground != null &&
                    (gmBackground.raycastTarget || gmBackground.color.a > 0.001f))
                    failures.Add($"GM 面板背景必须透明且不拦截射线: {UiPrefabs[1]}");
            }
            string badge = "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype/";
            foreach (string part in new[] { "m_img_Ring", "m_img_HpFill", "m_img_Portrait",
                         "m_img_StatusFrozen", "m_img_StatusPoison", "m_img_StatusCharge",
                         "m_img_StatusShield", "m_img_StatusFear" })
                CheckComponent<Image>(battle, badge + part, UiPrefabs[1], failures);
            CheckComponent<Button>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_btn_GmToggle",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_btn_GmToggle/m_text_Label",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_text_GmTitle",
                UiPrefabs[1], failures);
            CheckComponent<Text>(battle,
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_text_GmHint",
                UiPrefabs[1], failures);
            foreach (string button in new[] { "m_btn_GmGold", "m_btn_GmMana", "m_btn_GmWin", "m_btn_GmLose" })
                CheckComponent<Button>(battle,
                    $"m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/{button}",
                    UiPrefabs[1], failures);
            foreach (float speed in new[] { 1f, 2f, 4f, 8f, 16f, 32f, 64f })
            {
                CheckComponent<Button>(battle,
                    $"m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmSpeed{speed:0}",
                    UiPrefabs[1], failures);
                CheckComponent<Text>(battle,
                    $"m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_item_GmPanel/m_rect_GmContent/m_btn_GmSpeed{speed:0}/m_text_Label",
                    UiPrefabs[1], failures);
            }
            foreach (float speed in new[] { 1f, 4f, 16f, 64f, 256f })
            {
                string path =
                    $"m_rect_TakeoverRuntimeUI/m_item_BattleHud/m_rect_SpeedControls/m_btn_Speed{speed:0}";
                CheckComponent<Button>(battle, path, UiPrefabs[1], failures);
                CheckComponent<Text>(battle, path + "/m_text_Label", UiPrefabs[1], failures);
            }
        }

        if (prefabs.TryGetValue(UiPrefabs[2], out GameObject result))
        {
            CheckComponent<Text>(result,
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_text_Title",
                UiPrefabs[2], failures);
            CheckComponent<Text>(result,
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_text_Body",
                UiPrefabs[2], failures);
            CheckComponent<Button>(result,
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Replay",
                UiPrefabs[2], failures);
            CheckComponent<Button>(result,
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Next",
                UiPrefabs[2], failures);
            CheckComponent<Button>(result,
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Menu",
                UiPrefabs[2], failures);
        }

        if (prefabs.TryGetValue(UiPrefabs[3], out GameObject toast))
        {
            CheckComponent<RectTransform>(toast,
                "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel", UiPrefabs[3], failures);
            CheckComponent<Text>(toast,
                "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel/m_text_ToastText",
                UiPrefabs[3], failures);
        }

        if (prefabs.TryGetValue(UiPrefabs[4], out GameObject actionButton))
        {
            CheckComponent<Button>(actionButton, string.Empty, UiPrefabs[4], failures);
            CheckComponent<Text>(actionButton, "m_text_Label", UiPrefabs[4], failures);
        }
    }

    private static void CheckChildPaths(
        System.Collections.Generic.Dictionary<string, GameObject> prefabs,
        string prefabPath, string[] paths,
        System.Collections.Generic.ICollection<string> failures)
    {
        if (!prefabs.TryGetValue(prefabPath, out GameObject prefab)) return;
        foreach (string path in paths)
        {
            if (FindChild(prefab.transform, path) == null)
                failures.Add($"UI 预制体缺少节点: {prefabPath} -> {path}");
        }
    }

    private static void CheckComponent<T>(
        GameObject prefab, string path, string prefabPath,
        System.Collections.Generic.ICollection<string> failures) where T : Component
    {
        Transform node = string.IsNullOrEmpty(path)
            ? prefab.transform
            : FindChild(prefab.transform, path);
        if (node == null)
        {
            failures.Add($"UI 预制体缺少节点: {prefabPath} -> {path}");
            return;
        }
        if (node.GetComponent<T>() == null)
            failures.Add($"UI 预制体节点缺少 {typeof(T).Name}: {prefabPath} -> {path}");
    }

    private static Transform FindChild(Transform root, string path)
    {
        if (root == null) return null;
        Transform current = root;
        foreach (string segment in path.Split('/'))
        {
            current = current.Find(segment);
            if (current == null) return null;
        }
        return current;
    }

    /// <summary>预制体里的中文文案一旦被非 UTF-8 工具改写就会退化成问号占位符，
    /// 这里做静态校验，避免再次出现整屏 "?" 的界面。</summary>
    private static void CheckUiTextPlaceholders(System.Collections.Generic.ICollection<string> failures)
    {
        foreach (string path in UiPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add($"UI 预制体缺失: {path}");
                continue;
            }

            foreach (var text in prefab.GetComponentsInChildren<Text>(true))
            {
                if (text.text.IndexOf('?') < 0) continue;
                failures.Add($"UI 文案仍是问号占位符: {path} -> {text.name} = \"{text.text}\"");
            }
        }
    }

    private static void CheckCount(string directory, string pattern, int minimum,
        string label, System.Collections.Generic.ICollection<string> failures)
    {
        if (!Directory.Exists(directory))
        {
            failures.Add($"资源目录不存在: {directory}");
            return;
        }
        int count = Directory.GetFiles(directory, pattern).Length;
        if (count < minimum) failures.Add($"{label}不足: {count}/{minimum}");
    }

    private static System.Collections.Generic.IReadOnlyList<TakeoverSelfCheck.ChapterScenarioResult> RunScenarioSoak()
    {
        var tables = new Tables(Load);
        return TakeoverSelfCheck.RunChapterSoak(tables);
    }

    private static ByteBuf Load(string name)
    {
        var path = Path.Combine(ByteRoot, name + ".bytes");
        if (!File.Exists(path)) throw new FileNotFoundException("Luban bytes not found", path);
        return new ByteBuf(File.ReadAllBytes(path));
    }
}
