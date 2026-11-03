using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>单个关卡的最佳星级和最快通关时间记录。</summary>
    [Serializable]
    public sealed class LevelRecord
    {
        public int AssetId;
        public int BestStars;
        public float BestSeconds = -1f;
    }

    /// <summary>首章战役进度存档，包括解锁关卡、经验和关卡成绩。</summary>
    [Serializable]
    public sealed class CampaignSave
    {
        public int Version = 2;
        public int LastLevel = 1;
        public int Xp;
        public List<int> Unlocked = new List<int> { 1 };
        public List<LevelRecord> Records = new List<LevelRecord>();
        /// <summary>教令点余额（首通 +2、首次三星 +2）。</summary>
        public int EdictPoints;
        /// <summary>每条教令已购档位（索引 = edictId-1，值 0..3）。</summary>
        public List<int> EdictTiers = new List<int>();

        public bool IsUnlocked(int assetId) => Unlocked.Contains(assetId);

        public LevelRecord Record(int assetId)
        {
            var record = Records.FirstOrDefault(r => r.AssetId == assetId);
            if (record != null) return record;
            record = new LevelRecord { AssetId = assetId };
            Records.Add(record);
            return record;
        }

        public static CampaignSave CreateDefault() => new CampaignSave();

        public void Normalize(IReadOnlyCollection<int> validLevelIds)
        {
            if (Version <= 0) Version = 1;
            if (Version < 2) Version = 2;
            NormalizeEdicts(validLevelIds);
            if (validLevelIds == null || validLevelIds.Count == 0)
            {
                Unlocked = new List<int> { 1 };
                LastLevel = 1;
                Records = new List<LevelRecord>();
                return;
            }

            Unlocked = (Unlocked ?? new List<int>())
                .Where(validLevelIds.Contains)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            if (Unlocked.Count == 0) Unlocked.Add(validLevelIds.Min());
            if (!validLevelIds.Contains(LastLevel)) LastLevel = Unlocked[0];

            Records = (Records ?? new List<LevelRecord>())
                .Where(r => r != null && validLevelIds.Contains(r.AssetId))
                .GroupBy(r => r.AssetId)
                .Select(group => group.OrderByDescending(r => r.BestStars)
                    .ThenBy(r => r.BestSeconds < 0f ? float.MaxValue : r.BestSeconds)
                    .First())
                .ToList();
        }

        /// <summary>教令数据规范化：点数不为负、档位表补齐到 27 条并夹在 0..3。</summary>
        public void EnsureEdictTiers()
        {
            NormalizeEdicts(null);
        }

        private void NormalizeEdicts(IReadOnlyCollection<int> validLevelIds)
        {
            if (EdictPoints < 0) EdictPoints = 0;

            var tiers = EdictTiers ?? new List<int>();
            var normalized = new List<int>(TakeoverEdicts.EdictCount);
            for (int i = 0; i < TakeoverEdicts.EdictCount; i++)
            {
                int value = i < tiers.Count ? tiers[i] : 0;
                normalized.Add(Mathf.Clamp(value, 0, TakeoverEdicts.MaxTier));
            }
            EdictTiers = normalized;
        }
    }

    /// <summary>负责战役进度存档的读取、规范化、备份、写入和删除。</summary>
    public static class CampaignSaveService
    {
        private const string FileName = "takeover-campaign.json";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        public static string LastLoadWarning { get; private set; }

        public static CampaignSave Load()
        {
            LastLoadWarning = null;
            try
            {
                if (!File.Exists(SavePath)) return CampaignSave.CreateDefault();
                return Parse(File.ReadAllText(SavePath));
            }
            catch (Exception primaryError)
            {
                try
                {
                    var backup = SavePath + ".bak";
                    if (File.Exists(backup)) return Parse(File.ReadAllText(backup));
                }
                catch
                {
                    // 下方会向调用方返回原始错误诊断信息。
                }

                LastLoadWarning = $"存档损坏，已恢复默认进度：{primaryError.Message}";
                return CampaignSave.CreateDefault();
            }
        }

        public static void Save(CampaignSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            try
            {
                var directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var temp = SavePath + ".tmp";
                var backup = SavePath + ".bak";
                File.WriteAllText(temp, JsonUtility.ToJson(save, true));

                if (File.Exists(SavePath))
                {
                    try
                    {
                        File.Replace(temp, SavePath, backup, true);
                    }
                    catch (Exception replaceError)
                        when (replaceError is PlatformNotSupportedException || replaceError is IOException)
                    {
                        File.Copy(SavePath, backup, true);
                        File.Delete(SavePath);
                        File.Move(temp, SavePath);
                    }
                }
                else
                {
                    File.Move(temp, SavePath);
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[Takeover] 保存战役进度失败: {exception}");
            }
        }

        public static void Delete()
        {
            foreach (var path in new[] { SavePath, SavePath + ".bak", SavePath + ".tmp" })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception exception)
                {
                    Log.Warning($"[Takeover] 删除存档失败 {path}: {exception.Message}");
                }
            }
        }

        private static CampaignSave Parse(string json)
        {
            var save = JsonUtility.FromJson<CampaignSave>(json);
            if (save == null) throw new InvalidDataException("JSON 根节点为空");
            save.Normalize(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            return save;
        }
    }
}
