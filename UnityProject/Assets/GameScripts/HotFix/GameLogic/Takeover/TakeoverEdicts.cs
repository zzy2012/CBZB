using System.Collections.Generic;
using System.Linq;
using GameConfig.takeover;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 教令树（原版 27 条教令 × 3 档，落在 edict.xlsx / takeover.TbEdict）。
    /// 阵营按 edict_id 分组：(edictId-1)/9 → 0 Westaria、1 Khaganate、2 Cult；
    /// 帝国没有教令数据。玩家购买的档位存在战役存档里，进关时换算成 Mods[45]。
    /// </summary>
    public static class TakeoverEdicts
    {
        public const int EdictsPerFaction = 9;
        public const int MaxTier = 3;
        public const int ModCount = 45;
        /// <summary>教令总数（27 条 × 3 档 = 81 行配置）。</summary>
        public const int EdictCount = 27;

        /// <summary>玩家阵营对应的教令分组；帝国等没有教令数据的阵营返回 -1。</summary>
        public static int FactionGroup(Faction faction)
        {
            switch (faction)
            {
                case Faction.Westaria: return 0;
                case Faction.Khaganate: return 1;
                case Faction.Cult: return 2;
                default: return -1;
            }
        }

        /// <summary>该教令是否属于这个阵营（EdictId 1..27）。</summary>
        public static bool OwnsEdict(Faction faction, int edictId)
        {
            int group = FactionGroup(faction);
            if (group < 0 || edictId < 1) return false;
            return (edictId - 1) / EdictsPerFaction == group;
        }

        /// <summary>某阵营的全部教令 ID（升序）。</summary>
        public static IReadOnlyList<int> EdictIdsOf(Faction faction)
        {
            int group = FactionGroup(faction);
            if (group < 0) return new int[0];
            int first = group * EdictsPerFaction + 1;
            return Enumerable.Range(first, EdictsPerFaction).ToArray();
        }

        /// <summary>某条教令的全部档位（按 Tier 升序）。</summary>
        public static IReadOnlyList<Edict> TiersOf(int edictId) =>
            Cfg.TbEdict.DataList.Where(row => row.EdictId == edictId)
                .OrderBy(row => row.Tier)
                .ToList();

        /// <summary>某条教令某一档的配置行；没有则返回 null。</summary>
        public static Edict TierRow(int edictId, int tier) =>
            Cfg.TbEdict.DataList.FirstOrDefault(row => row.EdictId == edictId && row.Tier == tier);

        /// <summary>每条教令三档同价，取第一档的 Cost；找不到返回 0。</summary>
        public static int CostOf(int edictId)
        {
            Edict row = Cfg.TbEdict.DataList.FirstOrDefault(value => value.EdictId == edictId);
            return row?.Cost ?? 0;
        }

        /// <summary>购买下一档需要多少点。</summary>
        public static int NextTierCost(int edictId) => CostOf(edictId);

        /// <summary>把已购档位汇总成 45 长度的修正器数组（逐档累加）。</summary>
        public static int[] BuildMods(IReadOnlyList<int> tiers)
        {
            var mods = new int[ModCount];
            if (tiers == null) return mods;
            for (int edictId = 1; edictId <= tiers.Count; edictId++)
            {
                int tier = tiers[edictId - 1];
                if (tier <= 0) continue;
                for (int t = 1; t <= tier && t <= MaxTier; t++)
                {
                    Edict row = TierRow(edictId, t);
                    if (row == null) continue;
                    int index = row.ModifierId;
                    if (index < 0 || index >= mods.Length) continue;
                    mods[index] += row.Value;
                }
            }
            return mods;
        }

        /// <summary>把已购档位直接叠加到某个玩家的 Mods 上（用于存档回放/自检）。</summary>
        public static void ApplyTo(PlayerState player, IReadOnlyList<int> tiers)
        {
            if (player == null) return;
            int[] mods = BuildMods(tiers);
            for (int i = 0; i < player.Mods.Length && i < mods.Length; i++)
                player.Mods[i] += mods[i];
        }

        /// <summary>当前档案选择的玩家阵营（教令树按阵营分组）。</summary>
        public static Faction CurrentFaction() => Campaign.SelectedFaction;

        /// <summary>面板标题里的阵营中文名。</summary>
        public static string FactionDisplayName(Faction faction)
        {
            switch (faction)
            {
                case Faction.Westaria: return "西利亚公国";
                case Faction.Khaganate: return "游牧汗国";
                case Faction.Cult: return "暗影教团";
                case Faction.Empire: return "帝国";
                default: return faction.ToString();
            }
        }
    }
}
