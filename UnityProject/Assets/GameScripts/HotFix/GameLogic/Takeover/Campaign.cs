using System.Collections.Generic;

namespace GameLogic.Takeover
{
    /// <summary>
    /// First-chapter progress backed by the versioned local save file.
    /// Edicts are intentionally disabled in this build.
    /// </summary>
    public static class Campaign
    {
        public static readonly int[] ChapterOneLevels = { 1, 2, 3, 4, 5, 6, 7, 8 };

        private static CampaignSave _save;

        public static CampaignSave Data => _save ?? (_save = CampaignSaveService.Load());
        public static IReadOnlyList<int> Unlocked => Data.Unlocked;
        public static int Xp => Data.Xp;
        public static int EdictPoints => 0;
        public static string LastLoadWarning => CampaignSaveService.LastLoadWarning;

        public static int StarsFor(float seconds) => seconds < 330f ? 3 : seconds < 540f ? 2 : 1;

        public static int Complete(int assetId, float seconds, int xp)
        {
            if (assetId < 1 || assetId > 8) return 0;

            var data = Data;
            var record = data.Record(assetId);
            int awardedXp = record.BestStars > 0
                ? UnityEngine.Mathf.RoundToInt(xp / 5f)
                : xp;
            int stars = StarsFor(seconds);
            if (stars > record.BestStars) record.BestStars = stars;
            if (record.BestSeconds < 0f || seconds < record.BestSeconds) record.BestSeconds = seconds;
            data.Xp += awardedXp;
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
