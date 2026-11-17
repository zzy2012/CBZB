using System.Collections.Generic;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 首章进度数据由带版本号的本地存档持久化。
    /// 当前版本暂不启用教令系统。
    /// </summary>
    public static class Campaign
    {
        /// <summary>第一章包含的关卡资源标识。</summary>
        public static readonly int[] ChapterOneLevels = { 1, 2, 3, 4, 5, 6, 7, 8 };

        /// <summary>当前缓存的战役存档对象。</summary>
        private static CampaignSave _save;

        /// <summary>当前战役存档数据，首次访问时加载。</summary>
        public static CampaignSave Data => _save ?? (_save = CampaignSaveService.Load());
        /// <summary>已解锁的关卡标识集合。</summary>
        public static IReadOnlyList<int> Unlocked => Data.Unlocked;
        /// <summary>累计经验值。</summary>
        public static int Xp => Data.Xp;
        /// <summary>最近一次加载存档时产生的警告；无警告时为空。</summary>
        public static string LastLoadWarning => CampaignSaveService.LastLoadWarning;

        /// <summary>根据通关耗时计算星级。</summary>
        public static int StarsFor(float seconds) => seconds < 330f ? 3 : seconds < 540f ? 2 : 1;

        /// <summary>首次通关奖励的教令点。</summary>
        public const int EdictPointsFirstClear = 2;

        /// <summary>首次三星额外奖励的教令点。</summary>
        public const int EdictPointsFirstThreeStars = 2;

        /// <summary>可用于购买政令的点数。</summary>
        public static int EdictPoints => Data.EdictPoints;

        /// <summary>获取指定政令当前已购买的等级。</summary>
        public static int EdictTier(int edictId)
        {
            Data.EnsureEdictTiers();
            var tiers = Data.EdictTiers;
            if (tiers == null || edictId < 1 || edictId > tiers.Count) return 0;
            return tiers[edictId - 1];
        }

        /// <summary>能否购买下一档：阵营匹配、未满级、点数足够。</summary>
        public static bool CanBuyEdict(int edictId)
        {
            Faction faction = TakeoverEdicts.CurrentFaction();
            if (!TakeoverEdicts.OwnsEdict(faction, edictId)) return false;
            int tier = EdictTier(edictId);
            if (tier >= TakeoverEdicts.MaxTier) return false;
            return Data.EdictPoints >= TakeoverEdicts.NextTierCost(edictId);
        }

        /// <summary>购买下一档；成功返回 null，失败返回原因。</summary>
        public static string TryBuyEdict(int edictId)
        {
            Faction faction = TakeoverEdicts.CurrentFaction();
            if (!TakeoverEdicts.OwnsEdict(faction, edictId)) return "该阵营没有这条教令";
            int tier = EdictTier(edictId);
            if (tier >= TakeoverEdicts.MaxTier) return "已经满级";
            int cost = TakeoverEdicts.NextTierCost(edictId);
            if (Data.EdictPoints < cost) return $"教令点不足（需要 {cost} 点）";

            var data = Data;
            data.EnsureEdictTiers();
            data.EdictPoints -= cost;
            data.EdictTiers[edictId - 1] = tier + 1;
            CampaignSaveService.Save(data);
            return null;
        }

        /// <summary>已购教令汇总出的 45 长度修正器数组，进关时交给 TakeoverScene。</summary>
        public static int[] BuildHumanMods()
        {
            Data.EnsureEdictTiers();
            return TakeoverEdicts.BuildMods(Data.EdictTiers);
        }

        /// <summary>记录关卡完成结果并返回本次获得的经验值。</summary>
        public static int Complete(int assetId, float seconds, int xp)
        {
            if (assetId < 1 || assetId > 8) return 0;

            var data = Data;
            var record = data.Record(assetId);
            bool firstClear = record.BestStars <= 0;
            int awardedXp = record.BestStars > 0
                ? UnityEngine.Mathf.RoundToInt(xp / 5f)
                : xp;
            int stars = StarsFor(seconds);
            bool firstThreeStars = stars >= 3 && record.BestStars < 3;
            if (stars > record.BestStars) record.BestStars = stars;
            if (record.BestSeconds < 0f || seconds < record.BestSeconds) record.BestSeconds = seconds;
            data.Xp += awardedXp;

            int edictPoints = 0;
            if (firstClear) edictPoints += EdictPointsFirstClear;
            if (firstThreeStars) edictPoints += EdictPointsFirstThreeStars;
            data.EdictPoints += edictPoints;
            data.LastLevel = assetId;

            int index = System.Array.IndexOf(ChapterOneLevels, assetId);
            if (index >= 0 && index + 1 < ChapterOneLevels.Length)
            {
                int next = ChapterOneLevels[index + 1];
                if (!data.Unlocked.Contains(next)) data.Unlocked.Add(next);
            }
            data.Normalize(ChapterOneLevels);
            CampaignSaveService.Save(data);
            return awardedXp;
        }

        /// <summary>判断指定关卡是否已解锁。</summary>
        public static bool IsUnlocked(int assetId) => assetId >= 1 && assetId <= 8 && Data.IsUnlocked(assetId);

        /// <summary>获取指定关卡之后的关卡标识。</summary>
        public static int NextLevel(int assetId)
        {
            int index = System.Array.IndexOf(ChapterOneLevels, assetId);
            return index >= 0 && index + 1 < ChapterOneLevels.Length
                ? ChapterOneLevels[index + 1]
                : assetId;
        }

        /// <summary>最近选择的关卡资源标识。</summary>
        public static int LastLevel => CampaignSaveService.LastLoadWarning == null ? Data.LastLevel : 1;

        /// <summary>记录最近选择的关卡。</summary>
        public static void SetLastLevel(int assetId)
        {
            if (!IsUnlocked(assetId)) return;
            Data.LastLevel = assetId;
            CampaignSaveService.Save(Data);
        }

        /// <summary>获取指定关卡的战役记录。</summary>
        public static LevelRecord Record(int assetId) => Data.Record(assetId);

        /// <summary>重置战役进度及存档。</summary>
        public static void Reset()
        {
            CampaignSaveService.Delete();
            _save = CampaignSave.CreateDefault();
            CampaignSaveService.Save(_save);
        }
    }
}
