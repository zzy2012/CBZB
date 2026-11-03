using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>一次战斗结算产出的局外奖励。</summary>
    public struct CampaignReward
    {
        public int AwardedXp;
        public int Stars;
        public int EdictPoints;
        public int Medals;
        public int Gold;
        public bool FirstClear;
        public bool ThreeStarFirstTime;
        public bool UnlockedNext;
    }

    /// <summary>
    /// 远征档案（3 个存档槽）的战役进度与局外数值入口。
    /// 当前只有第一章（西利亚）可玩；第二/三章的关卡映射已预留。
    /// </summary>
    public static class Campaign
    {
        public static readonly int[] ChapterOneLevels = { 1, 2, 3, 4, 5, 6, 7, 8 };

        /// <summary>首次通关奖励的教令点。</summary>
        public const int EdictPointsFirstClear = 2;

        /// <summary>首次三星额外奖励的教令点。</summary>
        public const int EdictPointsFirstThreeStars = 2;

        public const int SlotCount = CampaignSaveService.SlotCount;

        /// <summary>局外等级从 11 级起步，和 index.html 的展示口径一致。</summary>
        public const int BasePlayerLevel = 11;

        public const int XpPerLevel = 1500;

        private static CampaignSave _save;
        private static int _currentSlot;

        public static int CurrentSlot => _currentSlot;

        public static CampaignSave Data
        {
            get
            {
                if (_save == null)
                {
                    _save = CampaignSaveService.Load(_currentSlot);
                    _save.Normalize();
                }
                return _save;
            }
        }

        public static string LastLoadWarning => CampaignSaveService.LastLoadWarning;

        public static CampaignSave Peek(int slot) => CampaignSaveService.Load(slot);

        public static bool HasSave(int slot) => CampaignSaveService.HasSave(slot);

        /// <summary>切换存档槽；不会立刻落盘，下一次 Save 写当前槽。</summary>
        public static void SelectProfile(int slot)
        {
            _currentSlot = Mathf.Clamp(slot, 0, SlotCount - 1);
            _save = null;
        }

        public static void Save()
        {
            CampaignSaveService.Save(Data, _currentSlot);
        }

        #region 阵营与关卡

        public static Faction SelectedFaction => Data.SelectedFaction;

        public static void SetSelectedFaction(Faction faction)
        {
            if (!TakeoverFactions.IsPlayable(faction)) return;
            if (Data.SelectedFaction == faction) return;
            Data.SelectedFaction = faction;
            Data.Normalize();
            Save();
        }

        public static IReadOnlyList<int> LevelsOf(Faction faction) => TakeoverFactions.LevelsOf(faction);

        public static IReadOnlyList<int> CurrentLevels => TakeoverFactions.LevelsOf(SelectedFaction);

        public static bool FactionPlayable(Faction faction) => TakeoverFactions.IsPlayable(faction);

        /// <summary>index.html 的阵营解锁规则：西利亚恒解锁，其余通关任意 2 关后解锁。</summary>
        public static bool FactionUnlocked(Faction faction)
        {
            if (faction == Faction.Westaria) return true;
            return ClearedCount >= 2;
        }

        public static int ClearedCount => Data.Records.Count(record => record.BestStars > 0);

        public static bool IsUnlocked(int assetId)
        {
            return CurrentLevels.Contains(assetId) && Data.IsUnlocked(assetId);
        }

        public static int NextLevel(int assetId)
        {
            IReadOnlyList<int> levels = CurrentLevels;
            int index = -1;
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] == assetId) { index = i; break; }
            }
            return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : assetId;
        }

        public static int LastLevel => Data.LastLevel;

        public static void SetLastLevel(int assetId)
        {
            if (!IsUnlocked(assetId)) return;
            Data.LastLevel = assetId;
            Save();
        }

        public static LevelRecord Record(int assetId) => Data.Record(assetId);

        #endregion

        #region 局外数值

        public static int Xp => Data.Xp;

        public static int PlayerLevel => LevelForXp(Data.Xp);

        public static int LevelForXp(int xp) => BasePlayerLevel + Mathf.Max(0, xp) / XpPerLevel;

        public static int XpToNextLevel => Data.Xp % XpPerLevel;

        public static int Medals => Data.Medals;

        public static int MedalCap => CurrentLevels.Count;

        public static int Gold => Data.Gold;

        public static int Honor => Data.Honor;

        public static int TutorialStep => Data.TutorialStep;

        public static int EdictPoints => Data.EdictPoints;

        /// <summary>历史累计获得的教令点（当前 + 已花费）。</summary>
        public static int EarnedSkillPoints => Data.EdictPoints + CalculateEdictRefund(Data.EdictTiers);

        public static void AddHonor(int amount)
        {
            if (amount <= 0) return;
            Data.Honor += amount;
            Save();
        }

        public static void SetTutorialStep(int step)
        {
            Data.TutorialStep = Mathf.Max(0, step);
            Save();
        }

        #endregion

        #region 教令

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
            Faction faction = SelectedFaction;
            if (!TakeoverEdicts.OwnsEdict(faction, edictId)) return false;
            int tier = EdictTier(edictId);
            if (tier >= TakeoverEdicts.MaxTier) return false;
            return Data.EdictPoints >= TakeoverEdicts.NextTierCost(edictId);
        }

        /// <summary>购买下一档；成功返回 null，失败返回原因。</summary>
        public static string TryBuyEdict(int edictId)
        {
            Faction faction = SelectedFaction;
            if (!TakeoverEdicts.OwnsEdict(faction, edictId)) return "该阵营没有这条教令";
            int tier = EdictTier(edictId);
            if (tier >= TakeoverEdicts.MaxTier) return "已经满级";
            int cost = TakeoverEdicts.NextTierCost(edictId);
            if (Data.EdictPoints < cost) return $"教令点不足（需要 {cost} 点）";

            var data = Data;
            data.EnsureEdictTiers();
            data.EdictPoints -= cost;
            data.EdictTiers[edictId - 1] = tier + 1;
            Save();
            return null;
        }

        /// <summary>重选技能：清空所有档位并全额退还已花教令点。</summary>
        public static int ResetEdicts()
        {
            var data = Data;
            data.EnsureEdictTiers();
            int refund = CalculateEdictRefund(data.EdictTiers);
            for (int i = 0; i < data.EdictTiers.Count; i++) data.EdictTiers[i] = 0;
            data.EdictPoints += refund;
            Save();
            return refund;
        }

        /// <summary>按“每条教令三档同价”的配置计算退款总额。</summary>
        public static int CalculateEdictRefund(IReadOnlyList<int> tiers)
        {
            if (tiers == null) return 0;
            int refund = 0;
            for (int edictId = 1; edictId <= TakeoverEdicts.EdictCount; edictId++)
            {
                int tier = edictId <= tiers.Count ? tiers[edictId - 1] : 0;
                if (tier <= 0) continue;
                refund += Mathf.Min(tier, TakeoverEdicts.MaxTier) * TakeoverEdicts.CostOf(edictId);
            }
            return refund;
        }

        /// <summary>已购教令汇总出的 45 长度修正器数组，进关时交给 BattleSim。</summary>
        public static int[] BuildHumanMods()
        {
            Data.EnsureEdictTiers();
            return TakeoverEdicts.BuildMods(Data.EdictTiers);
        }

        #endregion

        #region 结算

        public static int StarsFor(float seconds) => seconds < 330f ? 3 : seconds < 540f ? 2 : 1;

        /// <summary>
        /// 战斗结算：经验、星级、下一关、教令点、徽章与累计金币。
        /// 战败不调用本方法。
        /// </summary>
        public static CampaignReward CompleteBattle(int assetId, float seconds, int xp)
        {
            var reward = new CampaignReward();
            if (!CurrentLevels.Contains(assetId)) return reward;

            var data = Data;
            var record = data.Record(assetId);
            bool firstClear = record.BestStars <= 0;
            int awardedXp = firstClear ? xp : Mathf.RoundToInt(xp / 5f);
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

            bool unlockedNext = false;
            int index = TakeoverFactions.IndexOf(data.SelectedFaction, assetId);
            IReadOnlyList<int> levels = CurrentLevels;
            if (index >= 0 && index + 1 < levels.Count)
            {
                int next = levels[index + 1];
                if (!data.Unlocked.Contains(next))
                {
                    data.Unlocked.Add(next);
                    unlockedNext = true;
                }
            }

            int medalAward = data.Medals < levels.Count ? 1 : 0;
            data.Medals = Mathf.Clamp(data.Medals + medalAward, 0, levels.Count);
            int goldAward = 1000 + 250 * stars;
            data.Gold += goldAward;

            data.Normalize(levels);
            Save();

            reward.AwardedXp = awardedXp;
            reward.Stars = stars;
            reward.EdictPoints = edictPoints;
            reward.Medals = medalAward;
            reward.Gold = goldAward;
            reward.FirstClear = firstClear;
            reward.ThreeStarFirstTime = firstThreeStars;
            reward.UnlockedNext = unlockedNext;
            return reward;
        }

        public static void Reset()
        {
            CampaignSaveService.Delete(_currentSlot);
            _save = CampaignSave.CreateDefault();
            _save.Normalize();
            Save();
        }

        #endregion
    }
}
