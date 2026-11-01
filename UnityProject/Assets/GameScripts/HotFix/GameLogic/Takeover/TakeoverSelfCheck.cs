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
            public Vector2 HumanPosition;
            public Vector2 EnemyPosition;
            public float HumanHp;
            public float EnemyHp;
            public bool HumanMoving;
            public bool UnitsInBounds;
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

                int chapterWaypointCount = 0;
                for (int level = 1; level <= 8; level++)
                {
                    int id = level;
                    Check(tables.TbLevel.DataList.Any(l => l.AssetId == id), $"缺少首章关卡 L{id}", failures);
                    var nodes = tables.TbMapNode.DataList.Where(n => n.LevelId == id).ToList();
                    var keys = new HashSet<string>(nodes.Select(n => n.NodeKey));
                    var edges = tables.TbMapEdge.DataList.Where(e => e.LevelId == id).ToList();
                    Check(nodes.All(n => n.X >= 0f && n.X <= OriginalRules.NativeStageWidth
                                         && n.Y >= 0f && n.Y <= OriginalRules.NativeStageHeight),
                        $"L{id} 节点超出 800×600 地图坐标范围", failures);
                    Check(nodes.Count > 0, $"L{id} 没有地图节点", failures);
                    Check(edges.Count > 0, $"L{id} 没有地图道路", failures);
                    Check(edges.All(e => keys.Contains(e.NodeA) && keys.Contains(e.NodeB)),
                        $"L{id} 存在悬空道路", failures);

                    try
                    {
                        Faction primaryEnemy = OriginalRules.EnemyPlayers(id).First().Faction;
                        var sim = new BattleSim(id, Faction.Westaria, primaryEnemy,
                            null, 1000 + id);
                        Check(sim.Nodes.Count == nodes.Count, $"L{id} 构筑节点数不一致", failures);
                        int levelWaypoints = sim.Edges.Sum(edge => Math.Max(0, edge.Waypoints.Count - 2));
                        chapterWaypointCount += levelWaypoints;
                        Check(sim.Edges.All(edge => edge.Waypoints.Count >= 2 &&
                                                    edge.Waypoints.Skip(1).Take(edge.Waypoints.Count - 2)
                                                        .All(point => point.x >= 0f && point.x <= 800f &&
                                                                      point.y >= 0f && point.y <= 1200f)),
                            $"L{id} 道路 waypoint 不完整或越界", failures);
                        Check(sim.Human.Gold >= OriginalRules.PlayerStartGold, $"L{id} 起始金币异常", failures);
                        Check(id <= 2
                                ? sim.Squads.Any(squad => squad.Faction != Faction.Bandits)
                                : !sim.Squads.Any(squad => squad.Faction != Faction.Bandits),
                            $"L{id} 初始部队不符合原版脚本", failures);
                        if (id == 2)
                        {
                            Check(Mathf.Approximately(sim.Human.Gold, 300f),
                                $"L2 自动建设后金币应为 300，实际 {sim.Human.Gold}", failures);
                            Check(sim.Human.OwnedNodes.Any(node => node.Building?.Kind == 6) &&
                                  sim.Human.OwnedNodes.Any(node => node.Building?.Kind == 7),
                                "L2 未自动建造矿场和英雄碑", failures);
                            Check(sim.AvailableSpells(sim.Human).Count == 0,
                                "L2 食人魔战前不应开放玩家法术", failures);
                        }
                        var first = BattleSnapshot.From(sim);
                        var secondSim = new BattleSim(id, Faction.Westaria, primaryEnemy,
                            null, 1000 + id);
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
                                .Where(u => OriginalRules.FromConfigFaction(u.Faction) == Faction.Westaria)
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
                            sim.Human.SpellsEnabled = true;
                            var spell = tables.TbSpell.DataList.FirstOrDefault(s =>
                                OriginalRules.FromConfigFaction(s.Faction) == Faction.Westaria &&
                                s.SpellType != 2 && OriginalRules.IsSpellUnlocked(sim.Human, s.Id));
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
                Check(chapterWaypointCount >= 300,
                    $"首章原版道路 waypoint 数量不足: {chapterWaypointCount}", failures);
                var gmSim = new BattleSim(1, Faction.Westaria, Faction.Empire, null, 9001);
                float gmStartGold = gmSim.Human.Gold;
                float gmStartMana = gmSim.Human.Mana;
                Check(gmSim.ExecuteCommand(gmSim.Human, BattleCommand.GrantResources(1000, 500)) &&
                      Mathf.Approximately(gmSim.Human.Gold, gmStartGold + 1000f) &&
                      Mathf.Approximately(gmSim.Human.Mana, BattleSim.ManaCap) &&
                      gmSim.Human.Mana > gmStartMana,
                    "GM 资源命令失败", failures);

                Check(OriginalRules.TryAbility("Charge 2;ShieldWall 15", "Charge", out int charge)
                      && charge == 2, "特技参数解析失败", failures);
                Check(OriginalRules.EnemyModifiers(4).Count == 6, "L4 敌方修正器数量错误", failures);
                Check(OriginalRules.EnemyModifiers(7).Count == 12, "L7 敌方修正器数量错误", failures);
                Check(OriginalRules.EnemyPlayers(4).Count == 2 &&
                      OriginalRules.EnemyPlayers(5).Count == 2 &&
                      OriginalRules.EnemyPlayers(7).Count == 2,
                    "多敌方关卡玩家拆分错误", failures);
                Check(OriginalRules.FromConfigFaction(0) == Faction.Westaria &&
                      OriginalRules.FromConfigFaction(1) == Faction.Khaganate &&
                      OriginalRules.FromConfigFaction(2) == Faction.Cult &&
                      OriginalRules.FromConfigFaction(3) == Faction.Empire,
                    "配置阵营到原版阵营映射错误", failures);
                CheckScenarioCombat(tables, failures);

                var incomePlayer = new PlayerState { Faction = Faction.Westaria };
                incomePlayer.OwnedNodes.Add(new MapNode
                {
                    Building = new BuildingState { Owner = Faction.Westaria, Kind = 1, BaseHp = 500, Hp = 500, GoldPerSec = 100 },
                });
                Check(incomePlayer.NetGoldIncome() == 100, "零队收入不应扣税", failures);
                incomePlayer.Squads.Add(DummySquad(incomePlayer));
                Check(incomePlayer.NetGoldIncome() == 70, "一队收入惩罚错误", failures);
                while (incomePlayer.Squads.Count < 9) incomePlayer.Squads.Add(DummySquad(incomePlayer));
                Check(incomePlayer.NetGoldIncome() == 30, "九队收入惩罚错误", failures);
                incomePlayer.Squads.Add(DummySquad(incomePlayer));
                Check(incomePlayer.NetGoldIncome() == 25, "十队收入惩罚错误", failures);

                var spellFactions = tables.TbSpell.DataList.Select(s => s.Faction).Distinct().OrderBy(x => x).ToArray();
                Check(spellFactions.SequenceEqual(new[] { 0, 1, 2, 3 }), "四族法术不完整", failures);
                CheckSpellExecution(tables, failures);

                var abilityMask = OriginalRules.ParseAbilities(
                    "ShieldWall 15;Charge 2;LongSpear 50;Poisonous 1;FreezeArrow 50;" +
                    "SplashDamage 25;SiegeWeapon 50;Intimidiation 20;DivinePresence 10;" +
                    "HorrificDash 5;Discipline;Mechanism;Undead;PoisonImmunity");
                AbilityMask requiredAbilities =
                    AbilityMask.ShieldWall | AbilityMask.Charge | AbilityMask.LongSpear |
                    AbilityMask.Poisonous | AbilityMask.FreezeArrow | AbilityMask.SplashDamage |
                    AbilityMask.SiegeWeapon | AbilityMask.Intimidiation | AbilityMask.DivinePresence |
                    AbilityMask.HorrificDash | AbilityMask.Discipline | AbilityMask.Mechanism |
                    AbilityMask.Undead | AbilityMask.PoisonImmunity;
                Check((abilityMask & requiredAbilities) == requiredAbilities, "首章兵种特技解析不完整", failures);

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
                    var sim = new BattleSim(levelId, Faction.Westaria,
                        OriginalRules.EnemyPlayers(levelId).First().Faction, null, 7000 + levelId);
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
                            {
                                foreach (Squad squad in sim.Human.Squads.Where(squad => !squad.IsDead && !squad.HasMoveOrder))
                                    sim.OrderMove(squad, target);
                            }
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
                        EnemySquads = sim.Players.Where(player => !player.IsHuman).Sum(player =>
                            player.Squads.Count(s => !s.IsDead)),
                        HumanPosition = sim.Human.Squads.FirstOrDefault(squad => !squad.IsDead)?.Pos ?? Vector2.zero,
                        EnemyPosition = sim.Players.Where(player => !player.IsHuman)
                            .SelectMany(player => player.Squads).FirstOrDefault(squad => !squad.IsDead)?.Pos ?? Vector2.zero,
                        HumanHp = sim.Human.Squads.Where(squad => !squad.IsDead).Sum(squad => squad.Hp),
                        EnemyHp = sim.Players.Where(player => !player.IsHuman)
                            .SelectMany(player => player.Squads).Where(squad => !squad.IsDead).Sum(squad => squad.Hp),
                        HumanMoving = sim.Human.Squads.Any(squad => !squad.IsDead && squad.HasMoveOrder),
                        UnitsInBounds = sim.Squads.SelectMany(squad => squad.Units)
                            .Where(unit => !unit.IsDead)
                            .All(unit => unit.Pos.x >= -100f && unit.Pos.x <= 900f &&
                                         unit.Pos.y >= -100f && unit.Pos.y <= 1300f),
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
                    .Where(u => OriginalRules.FromConfigFaction(u.Faction) == Faction.Westaria)
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

        private static Squad DummySquad(PlayerState player)
        {
            var squad = new Squad
            {
                Owner = player,
                Def = Cfg.TbUnit.DataList[0],
                Xp = 0f,
            };
            squad.Units.Add(new CombatUnit
            {
                Squad = squad,
                Hp = 1f,
                MaxHp = 1f,
                BaseMaxHp = 1f,
            });
            return squad;
        }

        private static void CheckSpellExecution(Tables tables, ICollection<string> failures)
        {
            var sim = new BattleSim(8, Faction.Westaria, Faction.Empire, null, 9091);
            MapNode target = sim.Nodes.First(node => node.Building != null);
            foreach (GameConfig.takeover.Spell spell in tables.TbSpell.DataList.OrderBy(value => value.Id))
            {
                Faction faction = OriginalRules.FromConfigFaction(spell.Faction);
                PlayerState player = sim.Players.FirstOrDefault(value => value.Faction == faction);
                if (player == null)
                {
                    player = new PlayerState
                    {
                        Id = sim.Players.Count,
                        Name = "spell-test",
                        Faction = faction,
                        IsHuman = true,
                    };
                    sim.Players.Add(player);
                }
                player.IsHuman = true;
                player.SpellsEnabled = true;
                player.Mana = BattleSim.ManaCap;
                player.UltimateMax = 200f;
                player.Ultimate = 200f;
                switch (OriginalRules.OriginalSpellIndex(spell.Id))
                {
                    case 0: player.Mods[9] = 1; break;
                    case 1: player.Mods[5] = 1; break;
                    case 2: player.Mods[14] = 1; break;
                    case 4: player.Mods[20] = 1; break;
                    case 5: player.Mods[25] = 1; break;
                    case 6: player.Mods[17] = 1; break;
                    case 8: player.Mods[29] = 1; break;
                    case 9: player.Mods[34] = 1; break;
                    case 10: player.Mods[36] = 1; break;
                    case 12: player.Mods[41] = 1; break;
                    case 13: player.Mods[42] = 1; break;
                    case 14: player.Mods[43] = 1; break;
                }
                Check(sim.TryCastSpell(player, spell, target.Pos, target, out string reason),
                    $"法术 {spell.Id} {spell.Name} 无法执行: {reason}", failures);
            }
            for (int i = 0; i < 240; i++) sim.Tick(OriginalRules.LogicStep);
        }

        public static string DebugMovement(Tables tables, int levelId = 1, float seconds = 30f)
        {
            Cfg.InstallValidationTables(tables);
            try
            {
                var sim = new BattleSim(levelId, Faction.Westaria,
                    OriginalRules.EnemyPlayers(levelId).First().Faction, null, 7711);
                MapNode home = sim.Human.OwnedNodes.First(node => node.Building != null);
                GameConfig.takeover.Unit guard = tables.TbUnit.DataList.First(unit =>
                    unit.Name == "Guards" && OriginalRules.FromConfigFaction(unit.Faction) == Faction.Westaria);
                Squad squad = sim.BuyUnit(sim.Human, guard, home, out _);
                MapNode target = sim.Nodes
                    .Where(node => BattleSim.IsKeyBuilding(node.Building) && node.Building.Owner != Faction.Westaria)
                    .OrderBy(node => (node.Pos - home.Pos).sqrMagnitude)
                    .First();
                sim.OrderMove(squad, target);
                var text = new System.Text.StringBuilder();
                text.AppendLine($"home={home.Key}{home.Pos} target={target.Key}{target.Pos} path={squad.Path.Count} " +
                                $"points={string.Join(" -> ", squad.Path.Select(point => point.ToString()))}");
                for (int i = 0; i < Mathf.CeilToInt(seconds / OriginalRules.LogicStep); i++)
                {
                    sim.Tick(OriginalRules.LogicStep);
                    if (i % 30 != 0) continue;
                    text.AppendLine($"t={sim.Time:F1} pos={squad.Pos} idx={squad.PathIndex} moving={squad.HasMoveOrder}");
                }
                return text.ToString();
            }
            finally
            {
                Cfg.ClearValidationTables();
            }
        }

        private static void CheckScenarioCombat(Tables tables, ICollection<string> failures)
        {
            var sim = new BattleSim(1, Faction.Westaria, Faction.Empire, null, 4488);
            int waypointCount = sim.Edges.Sum(edge => Math.Max(0, edge.Waypoints.Count - 2));
            Check(waypointCount >= 10, $"原版道路 waypoint 缺失: {waypointCount}", failures);
            Check(sim.Edges.SelectMany(edge => edge.Waypoints.Skip(1).Take(edge.Waypoints.Count - 2))
                    .All(point => point.x >= 0f && point.x <= 800f && point.y >= 0f && point.y <= 1200f),
                "原版道路 waypoint 超出地图范围", failures);

            MapNode home = sim.Human.OwnedNodes.First(node => node.Building != null);
            GameConfig.takeover.Unit guard = tables.TbUnit.DataList.First(unit =>
                unit.Name == "Guards" && OriginalRules.FromConfigFaction(unit.Faction) == Faction.Westaria);
            Squad playerSquad = sim.BuyUnit(sim.Human, guard, home, out _);
            Check(playerSquad != null, "L1 无法招募 Guards", failures);
            Squad enemySquad = sim.Squads.FirstOrDefault(squad => squad.Faction == Faction.Empire);
            Check(enemySquad != null, "L1 没有剧本敌军", failures);
            if (playerSquad != null && enemySquad != null)
            {
                foreach (CombatUnit unit in enemySquad.Units)
                    unit.Pos = playerSquad.Pos + new Vector2(12f, 0f);
                float before = enemySquad.Hp;
                for (int i = 0; i < 180; i++) sim.Tick(OriginalRules.LogicStep);
                Check(enemySquad.IsDead || enemySquad.Hp < before, "逐单位战斗没有产生伤害", failures);
            }

            MapNode enemyBuilding = sim.Nodes.First(node =>
                node.Building != null && BattleSim.IsKeyBuilding(node.Building) &&
                node.Building.Owner != Faction.Westaria);
            enemyBuilding.Building.Level = 2;
            enemyBuilding.Building.Hp = 0;
            foreach (CombatUnit unit in playerSquad.Units.Where(unit => !unit.IsDead))
                unit.Pos = enemyBuilding.Pos;
            playerSquad.TargetNode = enemyBuilding;
            playerSquad.HasMoveOrder = false;
            sim.Tick(OriginalRules.LogicStep);
            Check(enemyBuilding.Building.Owner == Faction.Westaria && enemyBuilding.Building.Level == 2,
                "占领未保留建筑等级或未变更归属", failures);
        }
    }
}
