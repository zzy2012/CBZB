using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameLogic.Takeover;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UGUI 资源与数据层的静态校验（编辑器/批处理）。
/// </summary>
public static class TakeoverUiValidation
{
    private const string WindowPrefabPath = "Assets/AssetRaw/UI/TakeoverUIWindow.prefab";

    public static void RunChecks(ICollection<string> failures, Tables tables)
    {
        CheckWindowPrefab(failures);
        CheckSprites(failures);
        CheckCjkFont(failures);
        CheckSaveSchema(failures);
        CheckEdictRefund(tables, failures);
        CheckFactionMapping(failures);
    }

    private static void CheckWindowPrefab(ICollection<string> failures)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (prefab == null)
        {
            failures.Add($"UGUI 窗口 Prefab 缺失或无法导入: {WindowPrefabPath}");
            return;
        }

        Canvas canvas = prefab.GetComponent<Canvas>();
        if (canvas == null)
            failures.Add($"UGUI 窗口 Prefab 根节点缺少 Canvas: {WindowPrefabPath}");
        else if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            failures.Add($"UGUI 窗口 Canvas 必须使用 Screen Space - Overlay: {WindowPrefabPath}");

        if (prefab.GetComponent<CanvasScaler>() == null)
            failures.Add($"UGUI 窗口 Prefab 根节点缺少 CanvasScaler: {WindowPrefabPath}");
        if (prefab.GetComponent<GraphicRaycaster>() == null)
            failures.Add($"UGUI 窗口 Prefab 根节点缺少 GraphicRaycaster: {WindowPrefabPath}");
    }

    private static void CheckCjkFont(ICollection<string> failures)
    {
        const string fontPath = "Assets/Resources/UI/Fonts/TakeoverCjk.ttf";
        const string resourcePath = "UI/Fonts/TakeoverCjk";
        string ttcPath = "Assets/Resources/UI/Fonts/TakeoverCjk.ttc";
        if (!File.Exists(fontPath) && !File.Exists(ttcPath))
        {
            failures.Add("中文字体缺失：请执行菜单 TEngine → Takeover → 安装中文字体（从系统复制），" +
                         $"或手动放入 {fontPath}");
            return;
        }

        if (Resources.Load<Font>(resourcePath) == null)
            failures.Add($"中文字体无法从 Resources 加载：{resourcePath}");
    }

    private static void CheckSprites(ICollection<string> failures)
    {
        var spriteRoot = "Assets/AssetRaw/Actor/Takeover/Sprites";
        if (!File.Exists(Path.Combine(spriteRoot, "takeover_building_2.png")))
            failures.Add("UI 地图节点/标题图缺失: takeover_building_2.png");

        var iconRoot = "Assets/AssetRaw/UI/Icons";
        foreach (string icon in new[]
                 {
                     "takeover_status_frozen", "takeover_status_poison", "takeover_status_charge",
                     "takeover_status_shield", "takeover_status_fear",
                 })
        {
            if (!File.Exists(Path.Combine(iconRoot, icon + ".png")))
                failures.Add($"UI 状态图标缺失: {icon}.png");
        }

        if (!File.Exists("Assets/AssetRaw/Scenes/Takeover/Maps/takeover_map_1.png"))
            failures.Add("UI 战役地图背景缺失: takeover_map_1.png");
    }

    private static void CheckSaveSchema(ICollection<string> failures)
    {
        var save = CampaignSave.CreateDefault();
        save.SelectedFaction = Faction.Westaria;
        save.Xp = 3000;
        save.Medals = 3;
        save.Gold = 1234;
        save.Honor = 25;
        save.TutorialStep = 2;
        save.EdictPoints = 4;
        save.Unlocked = new List<int> { 1, 2, 3 };
        LevelRecord record = save.Record(2);
        record.BestStars = 3;
        record.BestSeconds = 120f;

        var restored = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(save));
        if (restored == null)
        {
            failures.Add("存档 v3 JSON 往返失败");
            return;
        }
        restored.Normalize();

        Check(restored.Version >= 3, "存档版本未升级到 3", failures);
        Check(restored.SelectedFaction == Faction.Westaria, "存档阵营字段丢失", failures);
        Check(restored.Xp == 3000, "存档 XP 字段丢失", failures);
        Check(restored.Medals == 3, "存档徽章字段丢失", failures);
        Check(restored.Gold == 1234, "存档金币字段丢失", failures);
        Check(restored.Honor == 25, "存档荣誉字段丢失", failures);
        Check(restored.TutorialStep == 2, "存档教程步数字段丢失", failures);
        Check(restored.EdictPoints == 4, "存档教令点字段丢失", failures);
        Check(restored.IsUnlocked(2) && restored.IsUnlocked(3), "存档解锁关卡丢失", failures);
        Check(restored.Record(2).BestStars == 3 &&
              Mathf.Approximately(restored.Record(2).BestSeconds, 120f),
            "存档关卡成绩丢失", failures);
        Check(Campaign.LevelForXp(3000) == Campaign.BasePlayerLevel + 2, "玩家等级公式错误", failures);

        var legacy = CampaignSave.CreateDefault();
        legacy.SelectedFaction = Faction.Bandits;
        legacy.Normalize();
        Check(legacy.SelectedFaction == Faction.Westaria,
            "非法阵营没有回退到西利亚", failures);
    }

    private static void CheckEdictRefund(Tables tables, ICollection<string> failures)
    {
        if (tables == null) return;
        Cfg.InstallValidationTables(tables);
        try
        {
            var tiers = new List<int>(new int[TakeoverEdicts.EdictCount]);
            int expected = 0;
            for (int edictId = 1; edictId <= TakeoverEdicts.EdictsPerFaction; edictId++)
            {
                tiers[edictId - 1] = 1;
                expected += TakeoverEdicts.CostOf(edictId);
            }
            int refund = Campaign.CalculateEdictRefund(tiers);
            Check(refund == expected,
                $"教令重选退款错误: got={refund} expected={expected}", failures);
        }
        finally
        {
            Cfg.ClearValidationTables();
        }
    }

    private static void CheckFactionMapping(ICollection<string> failures)
    {
        Check(TakeoverFactions.LevelsOf(Faction.Westaria).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
            "西利亚关卡映射错误", failures);
        Check(TakeoverFactions.LevelsOf(Faction.Khaganate).SequenceEqual(
                new[] { 11, 12, 13, 14, 15, 16, 17, 18 }),
            "卡若米森关卡映射错误", failures);
        Check(TakeoverFactions.LevelsOf(Faction.Cult).SequenceEqual(
                new[] { 21, 22, 23, 24, 25, 26, 27, 28 }),
            "艾斯得雷关卡映射错误", failures);
        Check(TakeoverFactions.IsPlayable(Faction.Westaria), "西利亚必须可玩", failures);
        Check(!TakeoverFactions.IsPlayable(Faction.Khaganate) &&
              !TakeoverFactions.IsPlayable(Faction.Cult),
            "第 2/3 章素材未导入时应保持锁定", failures);
    }

    private static void Check(bool condition, string message, ICollection<string> failures)
    {
        if (!condition) failures.Add(message);
    }
}
