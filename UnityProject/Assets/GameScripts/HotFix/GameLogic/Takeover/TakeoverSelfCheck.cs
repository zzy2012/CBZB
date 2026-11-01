using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameConfig;
using UnityEngine;

namespace GameLogic.Takeover
{
    public static class TakeoverSelfCheck
    {
        public sealed class ChapterScenarioResult
        {
            public int LevelId;
            public bool IsOver;
            public bool HumanVictory;
            public float ElapsedSeconds;
            public int HumanSquads;
            public int EnemySquads;
        }

        public static IReadOnlyList<string> Run(Tables tables)
        {
            var failures = new List<string>();
            if (tables == null)
            {
                failures.Add("配置表为空");
                return failures;
            }

            Cfg.InstallValidationTables(tables);
            try
            {
                Check(tables.TbUnit.DataList.Count == 45, "兵种数量不是 45", failures);
                Check(tables.TbBuilding.DataList.Count == 7, "建筑数量不是 7", failures);
                Check(tables.TbSpell.DataList.Count == 16, "法术数量不是 16", failures);
                Check(tables.TbMapNode.DataList.Count == 748, "地图节点数量不是 748", failures);
                Check(tables.TbMapEdge.DataList.Count == 515, "地图道路数量不是 515", failures);
                Check(tables.TbLevel.DataList.Count == 24, "关卡数量不是 24", failures);
                Check(tables.TbEdict.DataList.Count == 81, "教令档位数量不是 81", failures);

                for (int level = 1; level <= 8; level++)
                {
                    int id = level;
                    Check(tables.TbLevel.DataList.Any(l => l.AssetId == id), $"缺少首章关卡 L{id}", failures);
                    var nodes = tables.TbMapNode.DataList.Where(n => n.LevelId == id).ToList();
                    var keys = new HashSet<string>(nodes.Select(n => n.NodeKey));
                    var edges = tables.TbMapEdge.DataList.Where(e => e.LevelId == id).ToList();
                    Check(nodes.Count > 0, $"L{id} 没有地图节点", failures);
                    Check(edges.Count > 0, $"L{id} 没有地图道路", failures);
                    Check(edges.All(e => keys.Contains(e.NodeA) && keys.Contains(e.NodeB)),
                        $"L{id} 存在悬空道路", failures);

                    try
                    {
                        var sim = new BattleSim(id, Faction.Westaria,
                            (Faction)tables.TbLevel.DataList.First(l => l.AssetId == id).EnemyFaction,
                            null, 1000 + id);
                        Check(sim.Nodes.Count == nodes.Count, $"L{id} 构筑节点数不一致", failures);
                        Check(sim.Human.Gold >= OriginalRules.PlayerStartGold, $"L{id} 起始金币异常", failures);
                        Check(sim.Squads.Count > 0, $"L{id} 没有守军", failures);
                        var first = BattleSnapshot.From(sim);
                        var secondSim = new BattleSim(id, Faction.Westaria,
                            (Faction)tables.TbLevel.DataList.First(l => l.AssetId == id).EnemyFaction,
                            null, 1000 + id);
                        secondSim.AutoAdvance = false;
                        sim.AutoAdvance = false;
                        for (int i = 0; i < 60; i++)
                        {
                            sim.Tick(1f / 30f);
                            secondSim.Tick(1f / 30f);
                        }
                        var second = BattleSnapshot.From(secondSim);
                        Check(first.Squads.Count == BattleSnapshot.From(sim).Squads.Count
                              && second.Squads.Count == first.Squads.Count,
                            $"L{id} 固定种子初始快照不一致", failures);
                        Check(Math.Abs(sim.Time - 2f) < 0.001f, $"L{id} 固定步进时间不准确", failures);
                        if (id == 1)
                        {
                            var home = sim.Human.OwnedNodes.FirstOrDefault(n => n.Building != null);
                            var buyable = home == null ? null : tables.TbUnit.DataList
                                .Where(u => u.Faction == (int)Faction.Westaria)
                                .FirstOrDefault(u => sim.CanBuy(sim.Human, u, home, out _));
                            int beforeBuy = sim.Human.Squads.Count;
                            Check(buyable != null && sim.BuyUnit(sim.Human, buyable, home, out _) != null
                                  && sim.Human.Squads.Count == beforeBuy + 1,
                                "L1 招募流程失败", failures);
                            if (home?.Building != null)
                            {
                                sim.Human.Gold = 1000;
                                int oldLevel = home.Building.Level;
                                Check(sim.TryUpgrade(sim.Human, home)
                                      && home.Building.Level == oldLevel + 1,
                                    "L1 建筑升级失败", failures);
                            }
                            var buildSpot = sim.Nodes.FirstOrDefault(n => n.Kind == NodeKind.BuildSpot
                                && n.Building == null && sim.IsLocControlledBy(sim.Human, n));
                            if (buildSpot != null)
                            {
                                sim.Human.Gold = 1000;
                                Check(sim.TryBuild(sim.Human, buildSpot, 5, out _)
                                      && buildSpot.Building != null && buildSpot.Building.Supply == 1,
                                    "L1 农场建造失败", failures);
                            }
                            var spell = tables.TbSpell.DataList.FirstOrDefault(s =>
                                s.Faction == (int)Faction.Westaria && s.SpellType != 2);
                            if (spell != null)
                            {
                                sim.Human.Mana = BattleSim.ManaCap;
                                var target = sim.Nodes.FirstOrDefault(n => n.Building != null
                                    && n.Building.Owner != Faction.Westaria);
                                Check(target != null && sim.TryCastSpell(sim.Human, spell, target, out _),
                                    "L1 普通法术无法施放", failures);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        failures.Add($"L{id} 初始化或运行失败: {exception.GetType().Name}: {exception.Message}");
                    }
                }

                Check(OriginalRules.TryAbility("Charge 2;ShieldWall 15", "Charge", out int charge)
                      && charge == 2, "特技参数解析失败", failures);
                Check(OriginalRules.EnemyModifiers(4).Count == 6, "L4 敌方修正器数量错误", failures);
                Check(OriginalRules.EnemyModifiers(7).Count == 12, "L7 敌方修正器数量错误", failures);

                var spellFactions = tables.TbSpell.DataList.Select(s => s.Faction).Distinct().OrderBy(x => x).ToArray();
                Check(spellFactions.SequenceEqual(new[] { 0, 1, 2, 3 }), "四族法术不完整", failures);

                var save = CampaignSave.CreateDefault();
                save.Unlocked.Add(2);
                var record = save.Record(2);
                record.BestStars = 3;
                record.BestSeconds = 123f;
                var restored = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(save));
                restored.Normalize(Campaign.ChapterOneLevels);
                Check(restored.IsUnlocked(2) && restored.Record(2).BestStars == 3
                      && Mathf.Approximately(restored.Record(2).BestSeconds, 123f),
                    "存档 JSON 往返失败", failures);

                string assetRoot = Path.Combine("Assets", "AssetRaw", "Actor", "Takeover", "Sprites");
                foreach (var unit in tables.TbUnit.DataList)
                {
                    string location = TakeoverVisualCatalog.UnitLocation(unit);
                    Check(File.Exists(Path.Combine(assetRoot, location + ".png")),
                        $"单位 {unit.Id} {unit.CnName} 缺少静态精灵", failures);
                    for (int frame = 0; frame < 3; frame++)
                    {
                        Check(File.Exists(Path.Combine(assetRoot, location + $"_f{frame}.png")),
                            $"单位 {unit.Id} {unit.CnName} 缺少动画帧 {frame}", failures);
                    }
                }
            }
            finally
            {
                Cfg.ClearValidationTables();
            }

            return failures;
        }

        /// <summary>Deterministic, intentionally simple player bot used to catch
        /// initialization and soft-lock failures in every first-chapter map.</summary>
        public static IReadOnlyList<ChapterScenarioResult> RunChapterSoak(Tables tables, float maxSeconds = 900f)
        {
            var results = new List<ChapterScenarioResult>();
            if (tables == null) return results;
            Cfg.InstallValidationTables(tables);
            try
            {
                for (int levelId = 1; levelId <= 8; levelId++)
                {
                    var level = tables.TbLevel.DataList.First(l => l.AssetId == levelId);
                    var sim = new BattleSim(levelId, Faction.Westaria,
                        (Faction)level.EnemyFaction, null, 7000 + levelId)
                    {
                        AutoAdvance = false,
                    };
                    float nextBuy = 0f;
                    float nextOrder = 0f;
                    while (!sim.IsOver && sim.Time < maxSeconds)
                    {
                        if (sim.Time >= nextBuy)
                        {
                            nextBuy = sim.Time + 0.75f;
                            ScriptPlayerStep(sim);
                        }
                        if (sim.Time >= nextOrder)
                        {
                            nextOrder = sim.Time + 3f;
                            var target = sim.Nodes
                                .Where(n => BattleSim.IsKeyBuilding(n.Building)
                                            && n.Building.Owner != Faction.Westaria)
                                .OrderBy(n => sim.Human.OwnedNodes.Count == 0
                                    ? 0f
                                    : (n.Pos - sim.Human.OwnedNodes[0].Pos).sqrMagnitude)
                                .FirstOrDefault();
                            if (target != null)
                                sim.PlayerOrderAllAttack(target);
                        }
                        sim.Tick(1f / 30f);
                    }

                    results.Add(new ChapterScenarioResult
                    {
                        LevelId = levelId,
                        IsOver = sim.IsOver,
                        HumanVictory = sim.HumanVictory,
                        ElapsedSeconds = sim.Time,
                        HumanSquads = sim.Human.Squads.Count(s => !s.IsDead),
                        EnemySquads = sim.Enemy.Squads.Count(s => !s.IsDead),
                    });
                }
            }
            finally
            {
                Cfg.ClearValidationTables();
            }
            return results;
        }

        private static void ScriptPlayerStep(BattleSim sim)
        {
            if (sim.Human.FreeSupply <= 0 && sim.Human.Gold >= 150)
            {
                var spot = sim.Nodes.FirstOrDefault(n => n.Kind == NodeKind.BuildSpot
                    && n.Building == null && sim.IsLocControlledBy(sim.Human, n));
                if (spot != null) sim.TryBuild(sim.Human, spot, 5, out _);
            }

            foreach (var node in sim.Human.OwnedNodes.Where(n => n.Building != null))
            {
                var unit = Cfg.TbUnit.DataList
                    .Where(u => u.Faction == (int)Faction.Westaria)
                    .Where(u => sim.CanBuy(sim.Human, u, node, out _))
                    .OrderBy(u => u.Cost)
                    .FirstOrDefault();
                if (unit != null)
                {
                    sim.BuyUnit(sim.Human, unit, node, out _);
                    break;
                }
            }
        }

        private static void Check(bool condition, string message, ICollection<string> failures)
        {
            if (!condition) failures.Add(message);
        }
    }
}
