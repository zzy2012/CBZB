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

    /// <summary>
    /// 单个远征档案的战役进度存档。
    /// 版本 3 起支持：三存档槽、所选阵营、累计徽章/金币/荣誉、教程步数。
    /// </summary>
    [Serializable]
    public sealed class CampaignSave
    {
        public int Version = 3;
        /// <summary>玩家选择的阵营；当前只有西利亚可真正开局。</summary>
        public Faction SelectedFaction = Faction.Westaria;
        public int LastLevel = 1;
        public int Xp;
        public List<int> Unlocked = new List<int> { 1 };
        public List<LevelRecord> Records = new List<LevelRecord>();
        /// <summary>教令点余额（首通 +2、首次三星 +2），也就是界面的“技能点”。</summary>
        public int EdictPoints;
        /// <summary>每条教令已购档位（索引 = edictId-1，值 0..3）。</summary>
        public List<int> EdictTiers = new List<int>();
        /// <summary>累计徽章（胜利次数，上限 = 当前战役关卡数）。</summary>
        public int Medals;
        /// <summary>累计展示金币（胜利奖励，不影响战斗内经济）。</summary>
        public int Gold;
        /// <summary>累计荣誉（升级城堡 +25）。</summary>
        public int Honor;
        /// <summary>教程提示步数（0 起）。</summary>
        public int TutorialStep;

        public bool IsUnlocked(int assetId) => Unlocked != null && Unlocked.Contains(assetId);

        public LevelRecord Record(int assetId)
        {
            var record = Records.FirstOrDefault(r => r.AssetId == assetId);
            if (record != null) return record;
            record = new LevelRecord { AssetId = assetId };
            Records.Add(record);
            return record;
        }

        public static CampaignSave CreateDefault() => new CampaignSave();

        /// <summary>按存档当前阵营推关卡列表做规范化（读档入口使用）。</summary>
        public void Normalize()
        {
            SelectedFaction = TakeoverFactions.NormalizeSelected(SelectedFaction);
            Normalize(TakeoverFactions.LevelsOf(SelectedFaction));
        }

        public void Normalize(IReadOnlyCollection<int> validLevelIds)
        {
            if (Version < 1) Version = 1;
            if (Version < 3) Version = 3;

            EnsureEdictTiers();
            if (Medals < 0) Medals = 0;
            if (Gold < 0) Gold = 0;
            if (Honor < 0) Honor = 0;
            if (TutorialStep < 0) TutorialStep = 0;

            if (validLevelIds == null || validLevelIds.Count == 0)
            {
                Unlocked = new List<int> { 1 };
                LastLevel = 1;
                Records = new List<LevelRecord>();
                Medals = 0;
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

            if (Medals > validLevelIds.Count) Medals = validLevelIds.Count;
            // 历史档没有徽章字段时，用已通关数补齐，避免旧档显示 0。
            if (Medals == 0)
            {
                int cleared = Records.Count(r => r.BestStars > 0);
                Medals = Mathf.Clamp(cleared, 0, validLevelIds.Count);
            }
        }

        /// <summary>教令数据规范化：点数不为负、档位表补齐到 27 条并夹在 0..3。</summary>
        public void EnsureEdictTiers()
        {
            NormalizeEdicts();
        }

        private void NormalizeEdicts()
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

    /// <summary>
    /// 三个远征档案槽的读取、规范化、备份、写入和删除。
    /// 槽 1 兼容旧版单档文件名（takeover-campaign.json）。
    /// </summary>
    public static class CampaignSaveService
    {
        public const int SlotCount = 3;
        private const string FilePrefix = "takeover-campaign-slot-";
        private const string LegacyFileName = "takeover-campaign.json";

        public static string SlotPath(int slot) =>
            Path.Combine(Application.persistentDataPath, $"{FilePrefix}{slot + 1}.json");

        public static string LegacyPath => Path.Combine(Application.persistentDataPath, LegacyFileName);

        public static string LastLoadWarning { get; private set; }

        public static bool HasSave(int slot) =>
            File.Exists(SlotPath(slot)) || (slot == 0 && File.Exists(LegacyPath));

        /// <summary>读档；不存在时返回默认档。损坏时回退 .bak 并记录 LastLoadWarning。</summary>
        public static CampaignSave Load(int slot)
        {
            return Read(slot, out _);
        }

        /// <summary>存档弹窗使用：不存在的槽返回 false，损坏档返回默认档 + warning。</summary>
        public static bool TryProbe(int slot, out CampaignSave save, out string warning)
        {
            warning = null;
            if (!HasSave(slot))
            {
                save = CampaignSave.CreateDefault();
                return false;
            }
            save = Read(slot, out warning);
            return true;
        }

        public static void Save(CampaignSave save, int slot)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            string path = SlotPath(slot);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var temp = path + ".tmp";
                var backup = path + ".bak";
                File.WriteAllText(temp, JsonUtility.ToJson(save, true));

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temp, path, backup, true);
                    }
                    catch (Exception replaceError)
                        when (replaceError is PlatformNotSupportedException || replaceError is IOException)
                    {
                        File.Copy(path, backup, true);
                        File.Delete(path);
                        File.Move(temp, path);
                    }
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[Takeover] 保存远征档案 {slot + 1} 失败: {exception}");
            }
        }

        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            var paths = new List<string> { path, path + ".bak", path + ".tmp" };
            if (slot == 0)
            {
                paths.Add(LegacyPath);
                paths.Add(LegacyPath + ".bak");
                paths.Add(LegacyPath + ".tmp");
            }

            foreach (string candidate in paths)
            {
                try
                {
                    if (File.Exists(candidate)) File.Delete(candidate);
                }
                catch (Exception exception)
                {
                    Log.Warning($"[Takeover] 删除远征档案失败 {candidate}: {exception.Message}");
                }
            }
        }

        private static CampaignSave Read(int slot, out string warning)
        {
            warning = null;
            LastLoadWarning = null;

            string path = SlotPath(slot);
            if (!File.Exists(path) && slot == 0 && File.Exists(LegacyPath))
                path = LegacyPath;

            if (!File.Exists(path))
            {
                var fresh = CampaignSave.CreateDefault();
                fresh.Normalize();
                return fresh;
            }

            try
            {
                return Parse(File.ReadAllText(path));
            }
            catch (Exception primaryError)
            {
                string backup = path + ".bak";
                if (File.Exists(backup))
                {
                    try
                    {
                        var restored = Parse(File.ReadAllText(backup));
                        warning = $"档案 {slot + 1} 主文件损坏，已从备份恢复：{primaryError.Message}";
                        LastLoadWarning = warning;
                        return restored;
                    }
                    catch
                    {
                        // 继续走默认档。
                    }
                }

                warning = $"档案 {slot + 1} 损坏，已恢复默认进度：{primaryError.Message}";
                LastLoadWarning = warning;
                var fallback = CampaignSave.CreateDefault();
                fallback.Normalize();
                return fallback;
            }
        }

        private static CampaignSave Parse(string json)
        {
            var save = JsonUtility.FromJson<CampaignSave>(json);
            if (save == null) throw new InvalidDataException("JSON 根节点为空");
            save.Normalize();
            return save;
        }
    }
}
