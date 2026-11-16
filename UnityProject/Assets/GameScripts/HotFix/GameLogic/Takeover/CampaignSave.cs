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
        /// <summary>关卡资源标识。</summary>
        public int AssetId;
        /// <summary>历史最高通关星级。</summary>
        public int BestStars;
        /// <summary>历史最佳通关时间，单位为秒；负值表示尚无记录。</summary>
        public float BestSeconds = -1f;
    }

    /// <summary>首章战役进度存档，包括解锁关卡、经验和关卡成绩。</summary>
    [Serializable]
    public sealed class CampaignSave
    {
        /// <summary>存档数据版本。</summary>
        public int Version = 2;
        /// <summary>最近选择的关卡资源标识。</summary>
        public int LastLevel = 1;
        /// <summary>累计经验值。</summary>
        public int Xp;
        /// <summary>已解锁的关卡标识集合。</summary>
        public List<int> Unlocked = new List<int> { 1 };
        /// <summary>各关卡的战役记录。</summary>
        public List<LevelRecord> Records = new List<LevelRecord>();
        /// <summary>教令点余额（首通 +2、首次三星 +2）。</summary>
        public int EdictPoints;
        /// <summary>每条教令已购档位（索引 = edictId-1，值 0..3）。</summary>
        public List<int> EdictTiers = new List<int>();

        /// <summary>判断存档中是否已解锁指定关卡。</summary>
        public bool IsUnlocked(int assetId) => Unlocked.Contains(assetId);

        /// <summary>获取指定关卡的记录，不存在时创建记录。</summary>
        public LevelRecord Record(int assetId)
        {
            var record = Records.FirstOrDefault(r => r.AssetId == assetId);
            if (record != null) return record;
            record = new LevelRecord { AssetId = assetId };
            Records.Add(record);
            return record;
        }

        /// <summary>创建默认战役存档。</summary>
        public static CampaignSave CreateDefault() => new CampaignSave();

        /// <summary>根据有效关卡标识整理并修正存档数据。</summary>
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

        /// <summary>规范化政令点数及每条政令的等级数据。</summary>
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
        /// <summary>战役进度存档文件名。</summary>
        private const string FileName = "takeover-campaign.json";

        /// <summary>战役存档文件的完整路径。</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        /// <summary>最近一次加载存档时产生的警告；无警告时为空。</summary>
        public static string LastLoadWarning { get; private set; }

        /// <summary>从存档文件加载战役进度，必要时记录加载警告。</summary>
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

        /// <summary>将战役进度写入存档文件。</summary>
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

        /// <summary>删除战役存档文件。</summary>
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

        /// <summary>反序列化存档文本并规范化战役数据。</summary>
        private static CampaignSave Parse(string json)
        {
            var save = JsonUtility.FromJson<CampaignSave>(json);
            if (save == null) throw new InvalidDataException("JSON 根节点为空");
            save.Normalize(new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            return save;
        }
    }
}
