using System.Collections.Generic;
using System.Linq;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 战役进度：解锁关卡、星级、经验、教令点。
    /// 星级规则来自原版 LevelInfo：&lt;5:30 金、&lt;9:00 银，否则铜。
    /// </summary>
    public static class Campaign
    {
        public static readonly HashSet<int> Unlocked = new HashSet<int> { 1 };
        public static readonly Dictionary<int, int> BestStars = new Dictionary<int, int>();
        public static int Xp;
        public static int EdictPoints;

        public static int StarsFor(float seconds) => seconds < 330f ? 3 : seconds < 540f ? 2 : 1;

        /// <summary>通关结算：记录最好星级、加经验、给教令点、解锁下一关。</summary>
        public static void Complete(int assetId, float seconds, int xp)
        {
            int stars = StarsFor(seconds);
            if (!BestStars.TryGetValue(assetId, out var old) || stars > old)
            {
                BestStars[assetId] = stars;
            }
            Xp += xp;
            EdictPoints += 1;

            var ids = Cfg.TbLevel.DataList.Select(l => l.AssetId).OrderBy(x => x).ToList();
            int idx = ids.IndexOf(assetId);
            if (idx >= 0 && idx + 1 < ids.Count)
            {
                Unlocked.Add(ids[idx + 1]);
            }
        }

        public static bool IsUnlocked(int assetId) => Unlocked.Contains(assetId);

        public static int NextLevel(int assetId)
        {
            var ids = Cfg.TbLevel.DataList.Select(l => l.AssetId).OrderBy(x => x).ToList();
            int idx = ids.IndexOf(assetId);
            return idx >= 0 && idx + 1 < ids.Count ? ids[idx + 1] : assetId;
        }
    }
}
// touch 2026-10-24T09:46:30.4261320+08:00
