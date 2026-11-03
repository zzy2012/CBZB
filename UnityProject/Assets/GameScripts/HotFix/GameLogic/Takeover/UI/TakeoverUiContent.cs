using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.takeover;
using GameLogic.UI;
using UnityEngine;

namespace GameLogic.Takeover.UI
{
    /// <summary>阵营卡展示数据。</summary>
    public sealed class FactionCardData
    {
        public Faction Faction;
        public string Name;
        public string ShortName;
        public int Rank;
        public string Description;
        public string LockedDescription;
        public Color DarkColor;
        public Color LightColor;
    }

    /// <summary>
    /// 新 UI 的集中文案与展示数据。关卡名/描述是占位内容，改这里即可，不影响逻辑。
    /// </summary>
    public static class TakeoverUiContent
    {
        public const string GameTitle = "城邦争霸";
        public const string StudioMark = "* IRON OAK *";
        public const string StudioSubMark = "边境远征 · UI Toolkit 重构版";

        private static readonly string[] LevelNames =
        {
            "边境哨站",
            "Havoc",
            "灰烬山口",
            "古王都",
            "北境要塞",
            "黑石峡谷",
            "暮光平原",
            "王座前线",
        };

        private static readonly Vector2[] LevelNodePositions =
        {
            new Vector2(16f, 35f),
            new Vector2(19f, 19f),
            new Vector2(41f, 24f),
            new Vector2(62f, 33f),
            new Vector2(76f, 18f),
            new Vector2(55f, 55f),
            new Vector2(33f, 62f),
            new Vector2(70f, 70f),
        };

        private static readonly string[] TutorialLines =
        {
            "点击己方城堡打开招募与升级面板；按住士兵头顶徽标拖到目标即可下令。",
            "训练步兵、弓兵、骑兵或攻城器械会消耗金币与补给；先建造农场提高补给上限。",
            "升级据点可以强化防御；占领地图上所有敌方主建筑即可取得胜利。",
            "提示：空格暂停，R 重开本关，Esc 打开暂停菜单。",
        };

        private static readonly FactionCardData[] Cards =
        {
            new FactionCardData
            {
                Faction = Faction.Westaria,
                Name = "维斯塔洛拉之国",
                ShortName = "维斯塔洛拉王国",
                Rank = 1,
                Description = "强大的步兵与坚韧的城防，是正面战场的可靠力量。",
                LockedDescription = string.Empty,
                DarkColor = new Color32(0x45, 0x66, 0x2b, 0xff),
                LightColor = new Color32(0x95, 0xa8, 0x43, 0xff),
            },
            new FactionCardData
            {
                Faction = Faction.Khaganate,
                Name = "卡瑞米森部落",
                ShortName = "卡瑞米森部落",
                Rank = 2,
                Description = "完成战役 1 中的关卡 1–2 解锁。",
                LockedDescription = "完成第一战役的前两关即可解锁。",
                DarkColor = new Color32(0x78, 0x3b, 0x2a, 0xff),
                LightColor = new Color32(0xb6, 0x5b, 0x34, 0xff),
            },
            new FactionCardData
            {
                Faction = Faction.Cult,
                Name = "艾斯德雷邪教",
                ShortName = "艾斯德雷邪教",
                Rank = 3,
                Description = "完成战役 1 中的关卡 1–2 解锁。",
                LockedDescription = "完成第一战役的前两关即可解锁。",
                DarkColor = new Color32(0x31, 0x56, 0x6a, 0xff),
                LightColor = new Color32(0x57, 0x97, 0xb2, 0xff),
            },
        };

        public static IReadOnlyList<FactionCardData> FactionCards => Cards;

        public static string FactionName(Faction faction)
        {
            switch (faction)
            {
                case Faction.Westaria: return "维斯塔洛拉之国";
                case Faction.Khaganate: return "卡瑞米森部落";
                case Faction.Cult: return "艾斯德雷邪教";
                case Faction.Empire: return "瑞维迪斯帝国";
                case Faction.Bandits: return "中立强盗";
                default: return faction.ToString();
            }
        }

        public static string FactionShortName(Faction faction)
        {
            return faction == Faction.Westaria ? "维斯塔洛拉王国" : FactionName(faction);
        }

        public static FactionCardData CardOf(Faction faction)
        {
            return Cards.FirstOrDefault(card => card.Faction == faction);
        }

        public static Color FactionColor(Faction faction)
        {
            FactionCardData card = CardOf(faction);
            return card != null ? card.DarkColor : new Color(0.32f, 0.32f, 0.32f);
        }

        /// <summary>阵营肖像取该阵营一阶步兵的静态帧，避免新增美术资源。</summary>
        public static string FactionPortraitLocation(Faction faction)
        {
            try
            {
                Unit unit = Cfg.TbUnit.DataList
                    .Where(value => OriginalRules.FromConfigFaction(value.Faction) == faction)
                    .OrderBy(value => value.UnitType)
                    .ThenBy(value => value.Tier)
                    .FirstOrDefault();
                return unit != null ? TakeoverVisualCatalog.UnitLocation(unit) : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public static string LevelName(int levelAssetId)
        {
            int index = TakeoverFactions.IndexOf(Faction.Westaria, levelAssetId);
            if (index < 0) index = levelAssetId - 1;
            return index >= 0 && index < LevelNames.Length ? LevelNames[index] : $"第 {levelAssetId} 关";
        }

        public static string LevelTitle(int levelAssetId) =>
            $"第 {levelAssetId} 关 · {LevelName(levelAssetId)}";

        public static Vector2 NodePosition(int index)
        {
            if (index < 0 || index >= LevelNodePositions.Length) return new Vector2(50f, 50f);
            return LevelNodePositions[index];
        }

        public static int LevelXp(int levelAssetId)
        {
            try
            {
                Level level = Cfg.TbLevel.DataList.FirstOrDefault(value => value.AssetId == levelAssetId);
                return level != null ? level.Xp : 1000;
            }
            catch (Exception)
            {
                return 1000;
            }
        }

        public static string EnemyFactionNames(int levelAssetId)
        {
            try
            {
                var names = OriginalRules.EnemyPlayers(levelAssetId)
                    .Select(setup => FactionName(setup.Faction))
                    .Distinct()
                    .ToList();
                if (names.Count > 0) return string.Join(" / ", names);

                Level level = Cfg.TbLevel.DataList.FirstOrDefault(value => value.AssetId == levelAssetId);
                return level != null ? FactionName(OriginalRules.FromConfigFaction(level.EnemyFaction)) : "未知";
            }
            catch (Exception)
            {
                return "未知";
            }
        }

        public static string LevelDescription(int levelAssetId)
        {
            return $"敌方：{EnemyFactionNames(levelAssetId)}　过关 XP {UIFormat.Number(LevelXp(levelAssetId))}　三星时限 05:30";
        }

        public static IReadOnlyList<string> TutorialTexts => TutorialLines;

        public static string TutorialLine(int index)
        {
            if (TutorialLines.Length == 0) return string.Empty;
            int wrapped = ((index % TutorialLines.Length) + TutorialLines.Length) % TutorialLines.Length;
            return TutorialLines[wrapped];
        }

        public static int TutorialCount => TutorialLines.Length;

        public static string EdictIcon(int edictId)
        {
            string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
            int index = (edictId - 1) % numerals.Length;
            return numerals[index];
        }

        public static string VersionText => $"版本 {Application.version} · UI Toolkit 重构";
    }
}
