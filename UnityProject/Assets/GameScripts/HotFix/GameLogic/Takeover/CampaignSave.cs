using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    [Serializable]
    public sealed class LevelRecord
    {
        public int AssetId;
        public int BestStars;
        public float BestSeconds = -1f;
    }

    [Serializable]
    public sealed class CampaignSave
    {
        public int Version = 1;
        public int LastLevel = 1;
        public int Xp;
        public List<int> Unlocked = new List<int> { 1 };
        public List<LevelRecord> Records = new List<LevelRecord>();

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
    }

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
                    // The caller receives the original diagnostic below.
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
                Debug.LogError($"[Takeover] 保存战役进度失败: {exception}");
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
                    Debug.LogWarning($"[Takeover] 删除存档失败 {path}: {exception.Message}");
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
