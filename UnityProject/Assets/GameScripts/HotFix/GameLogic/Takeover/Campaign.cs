using System.Collections.Generic;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 首章进度数据由带版本号的本地存档持久化。
    /// 当前版本暂不启用教令系统。
    /// </summary>
    public static class Campaign
    {
        public static readonly int[] ChapterOneLevels = { 1, 2, 3, 4, 5, 6, 7, 8 };

        private static CampaignSave _save;

        public static CampaignSave Data => _save ?? (_save = CampaignSaveService.Load());
        public static IReadOnlyList<int> Unlocked => Data.Unlocked;
        public static int Xp => Data.Xp;
        public static string LastLoadWarning => CampaignSaveService.LastLoadWarning;

        public static int StarsFor(float seconds) => seconds < 330f ? 3 : seconds < 540f ? 2 : 1;

        /// <summary>首次通关奖励的教令点。</summary>
        public const int EdictPointsFirstClear = 2;

        /// <summary>首次三星额外奖励的教令点。</summary>
        public const int EdictPointsFirstThreeStars = 2;

        public static int EdictPoints => Data.EdictPoints;

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

        /// <summary>已购教令汇总出的 45 长度修正器数组，进关时交给 BattleSim。</summary>
        public static int[] BuildHumanMods()
        {
            Data.EnsureEdictTiers();
            return TakeoverEdicts.BuildMods(Data.EdictTiers);
        }

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

        public static bool IsUnlocked(int assetId) => assetId >= 1 && assetId <= 8 && Data.IsUnlocked(assetId);

        public static int NextLevel(int assetId)
        {
            int index = System.Array.IndexOf(ChapterOneLevels, assetId);
            return index >= 0 && index + 1 < ChapterOneLevels.Length
                ? ChapterOneLevels[index + 1]
                : assetId;
        }

        public static int LastLevel => CampaignSaveService.LastLoadWarning == null ? Data.LastLevel : 1;

        public static void SetLastLevel(int assetId)
        {
            if (!IsUnlocked(assetId)) return;
            Data.LastLevel = assetId;
            CampaignSaveService.Save(Data);
        }

        public static LevelRecord Record(int assetId) => Data.Record(assetId);

        public static void Reset()
        {
            CampaignSaveService.Delete();
            _save = CampaignSave.CreateDefault();
            CampaignSaveService.Save(_save);
        }
    }
}
