using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using GameLogic.Takeover;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 资源与数据层的静态校验（编辑器/批处理）。
/// UXML 只检查导入与必需节点；运行时面板在 PlaySmoke 中验证。
/// </summary>
public static class TakeoverUiValidation
{
    private const string UxmlRoot = "Assets/AssetRaw/UI/Toolkit";
    private const string ThemePath = "Assets/Resources/UI/TakeoverRuntimeTheme.tss";

    private static readonly Dictionary<string, string[]> RequiredNodes =
        new Dictionary<string, string[]>
        {
            ["TakeoverTitleView"] = new[]
            {
                "title-screen", "title-label", "beta-label", "start-button",
                "version-label", "studio-label",
            },
            ["TakeoverSaveSlotsView"] = new[] { "save-modal", "slot-list", "close-button" },
            ["TakeoverFactionView"] = new[]
            {
                "faction-screen", "ribbon", "faction-grid", "exit-button", "continue-button",
            },
            ["TakeoverCampaignMapView"] = new[]
            {
                "map-screen", "map-background", "faction-heading", "mission-nodes",
                "exit-button", "skills-button", "medal-label", "level-label",
                "xp-label", "xp-fill", "gold-label",
            },
            ["TakeoverMissionDetailView"] = new[]
            {
                "mission-detail", "mission-title", "mission-reward", "mission-description",
                "mission-enemy", "mission-stars", "start-battle-button", "close-button",
            },
            ["TakeoverSkillTreeView"] = new[]
            {
                "skill-screen", "skill-grid", "points-label", "reset-button", "ok-button",
            },
            ["TakeoverBattleHudView"] = new[]
            {
                "battle-hud", "battle-title", "settings-button", "pause-button", "speed-button",
                "badge-layer", "inspector", "inspector-title", "inspector-body",
                "spell-list", "menu-host", "gm-host", "tutorial-text", "tutorial-avatar",
                "tutorial-tag", "next-tutorial-button", "commander-name",
                "gold-value", "mana-value", "supply-value", "army-value", "honor-value",
                "enemy-value", "clock-label", "tutorial-panel",
            },
            ["TakeoverBattleMenuView"] = new[] { "battle-menu", "tab-host", "option-list" },
            ["TakeoverGmPanelView"] = new[]
            {
                "gm-panel", "gm-toggle", "gm-content", "gm-title", "gm-hint",
                "gm-gold", "gm-mana", "gm-win", "gm-lose", "gm-ai", "gm-ai-label",
                "gm-speed-1", "gm-speed-1-label", "gm-speed-2", "gm-speed-2-label",
                "gm-speed-4", "gm-speed-4-label", "gm-speed-8", "gm-speed-8-label",
                "gm-speed-16", "gm-speed-16-label", "gm-speed-32", "gm-speed-32-label",
                "gm-speed-64", "gm-speed-64-label",
            },
            ["TakeoverMessageOverlayView"] = new[] { "overlay-root", "title", "body", "actions" },
            ["TakeoverToastView"] = new[] { "toast-layer", "toast-panel", "toast-label" },
            ["TakeoverAudioControlsView"] = new[] { "sound-button", "music-button" },
        };

    private static readonly string[] RequiredStyleSheets =
    {
        "TakeoverCommonStyles",
        "TakeoverScreenStyles",
        "TakeoverBattleStyles",
    };

    public static void RunChecks(ICollection<string> failures, Tables tables)
    {
        CheckTheme(failures);
        CheckUxml(failures);
        CheckStyleSheets(failures);
        CheckSprites(failures);
        CheckCjkFont(failures);
        CheckSaveSchema(failures);
        CheckEdictRefund(tables, failures);
        CheckFactionMapping(failures);
    }

    private static void CheckCjkFont(ICollection<string> failures)
    {
        string ttf = "Assets/Resources/UI/Toolkit/Fonts/TakeoverCjk.ttf";
        string ttc = "Assets/Resources/UI/Toolkit/Fonts/TakeoverCjk.ttc";
        if (!File.Exists(ttf) && !File.Exists(ttc))
        {
            failures.Add("中文字体缺失：请执行菜单 TEngine → Takeover → 安装中文字体（从系统复制），" +
                         "或手动放入 Assets/Resources/UI/Toolkit/Fonts/TakeoverCjk.ttf");
            return;
        }

        if (Resources.Load<Font>("UI/Toolkit/Fonts/TakeoverCjk") == null)
            failures.Add("中文字体无法从 Resources 加载：UI/Toolkit/Fonts/TakeoverCjk");
    }

    private static void CheckTheme(ICollection<string> failures)
    {
        if (!File.Exists(ThemePath))
        {
            failures.Add($"UI 主题缺失: {ThemePath}");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath) == null)
            failures.Add($"UI 主题无法导入为 ThemeStyleSheet: {ThemePath}");
    }

    private static void CheckUxml(ICollection<string> failures)
    {
        foreach (KeyValuePair<string, string[]> pair in RequiredNodes)
        {
            string path = $"{UxmlRoot}/{pair.Key}.uxml";
            if (!File.Exists(path))
            {
                failures.Add($"UI 界面缺失: {path}");
                continue;
            }

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (tree == null)
            {
                failures.Add($"UI 界面无法导入: {path}");
                continue;
            }

            VisualElement root;
            try
            {
                root = tree.Instantiate();
            }
            catch (System.Exception exception)
            {
                failures.Add($"UI 界面实例化失败: {path} -> {exception.Message}");
                continue;
            }

            if (root == null)
            {
                failures.Add($"UI 界面实例化结果为空: {path}");
                continue;
            }

            foreach (string name in pair.Value)
            {
                if (root.name != name && root.Q(name) == null)
                    failures.Add($"UI 界面缺少节点: {path} -> {name}");
            }

            string text = File.ReadAllText(path);
            // 跳过 XML 声明里的 ?>，只检查正文是否残留 ? 占位。
            int declarationEnd = text.IndexOf("?>", System.StringComparison.Ordinal);
            string body = declarationEnd >= 0 ? text.Substring(declarationEnd + 2) : text;
            if (body.IndexOf('?') >= 0)
                failures.Add($"UI 文案仍是问号占位符: {path}");
        }
    }

    private static void CheckStyleSheets(ICollection<string> failures)
    {
        foreach (string name in RequiredStyleSheets)
        {
            string path = $"{UxmlRoot}/{name}.uss";
            if (!File.Exists(path))
            {
                failures.Add($"UI 样式缺失: {path}");
                continue;
            }
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet == null)
            {
                failures.Add($"UI 样式无法导入: {path}");
                continue;
            }
            if (sheet.importedWithErrors)
                failures.Add($"UI 样式导入有错误（会导致整体样式失效）: {path}");
            else if (sheet.importedWithWarnings)
                Debug.LogWarning($"[TakeoverUI] USS 导入有警告: {path}");
        }
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
