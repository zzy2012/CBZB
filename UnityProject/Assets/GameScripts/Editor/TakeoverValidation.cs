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

        bool uiReady = GameModule.UI.HasWindow<TakeoverUIWindow>();
        bool simulated = runner.Simulation.Time > 0.5f;
        const string music = "takeover_sfx_2910_musicGameplayClass_musicGameplayClass";
        Debug.Log($"[TakeoverValidation] audio candidates: " +
                  $"plain={GameModule.Resource.CheckLocationValid(music)} " +
                  $"ext={GameModule.Resource.CheckLocationValid(music + ".wav")}");
        Debug.Log($"[TakeoverValidation] original font valid=" +
                  $"{GameModule.Resource.CheckLocationValid("takeover_font_328_Chaparral_Pro")}");
        FinishPlaySmoke(uiReady && simulated,
            uiReady ? (simulated ? string.Empty : "模拟没有推进")
                : "TakeoverUIWindow 未创建");
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

    private static readonly string[] UiPrefabs =
    {
        "Assets/AssetRaw/UI/TakeoverUIWindow.prefab",
        "Assets/AssetRaw/UI/TakeoverUIActionButton.prefab",
    };

    private static readonly string[] BattleHudPaths =
    {
        "BattleHUD/SquadBadgeLayer",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/HpFill",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/Ring",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/Portrait",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/StatusFrozen",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/StatusPoison",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/StatusCharge",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/StatusShield",
        "BattleHUD/SquadBadgeLayer/SquadBadgePrototype/StatusFear",
        "BattleHUD/BottomBar",
        "BattleHUD/BottomBar/PortraitFrame",
        "BattleHUD/BottomBar/Top/Resources",
        "BattleHUD/BottomBar/Top/Clock",
        "BattleHUD/BottomBar/Settings",
        "BattleHUD/SpeedControls",
        "BattleHUD/SpeedControls/Speed1/Label",
        "BattleHUD/SpeedControls/Speed2/Label",
        "BattleHUD/SpeedControls/Speed4/Label",
    };

    /// <summary>战斗 HUD 与头顶徽标全部来自预制体；节点被误删时这里要报出来。</summary>
    private static void CheckUiPrefabStructure(System.Collections.Generic.ICollection<string> failures)
    {
        var window = AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabs[0]);
        if (window == null)
        {
            failures.Add($"UI 预制体缺失: {UiPrefabs[0]}");
            return;
        }
        foreach (string path in BattleHudPaths)
        {
            if (FindChild(window.transform, path) == null)
                failures.Add($"UI 预制体缺少节点: {UiPrefabs[0]} -> {path}");
        }
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
