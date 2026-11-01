using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// Deterministic simulation of the 1.0.4 first campaign. The migration
    /// scaffolding is intentionally ignored; constants and state transitions
    /// below are derived from the decompiled level, squad, unit and spell code.
    /// </summary>
    public sealed class BattleSim
    {
        public const float ManaCap = 500f;
        public const int AiBuyThreshold = OriginalRules.AiBuyThreshold;
        public static readonly int[] SupplyBuildingIds = { 5, 6, 7 };
        private static readonly Vector2[] FormationThree =
        {
            new Vector2(0f, 0f), new Vector2(-14f, 7f), new Vector2(-14f, -7f),
        };
        private static readonly Vector2[] FormationFive =
        {
            new Vector2(0f, 0f), new Vector2(-21f, 14f), new Vector2(-21f, -14f),
            new Vector2(-42f, 14f), new Vector2(-42f, -14f),
        };
        private static readonly Vector2[] FormationSeven =
        {
            new Vector2(0f, 0f), new Vector2(-21f, 14f), new Vector2(-21f, -14f),
            new Vector2(-42f, 14f), new Vector2(-42f, -14f),
            new Vector2(-63f, 14f), new Vector2(-63f, -14f),
        };
        private static readonly Vector2[] FormationThreeByTwo =
        {
            new Vector2(0f, 0f), new Vector2(-14f, 10.5f), new Vector2(-14f, -10.5f),
            new Vector2(-21f, 0f), new Vector2(-31.5f, 10.5f), new Vector2(-31.5f, -10.5f),
        };
        private static readonly Vector2[] FormationThreeByFour =
        {
            new Vector2(0f, 0f), new Vector2(0f, 14f), new Vector2(0f, -14f),
            new Vector2(-14f, 0f), new Vector2(-14f, 14f), new Vector2(-14f, -14f),
            new Vector2(-28f, 0f), new Vector2(-28f, 14f), new Vector2(-28f, -14f),
            new Vector2(-42f, 0f), new Vector2(-42f, 14f), new Vector2(-42f, -14f),
        };
        private static readonly Vector2[] FormationOne = { Vector2.zero };

        public readonly List<MapNode> Nodes = new List<MapNode>();
        public readonly Dictionary<string, MapNode> NodeByKey = new Dictionary<string, MapNode>();
        public readonly List<MapEdge> Edges = new List<MapEdge>();
        public readonly List<Squad> Squads = new List<Squad>();
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<ActiveSpellEffect> ActiveSpells = new List<ActiveSpellEffect>();

        public readonly PlayerState Human;
        public PlayerState Enemy => Players.FirstOrDefault(player => !player.IsHuman) ??
                                    new PlayerState { Faction = Faction.Empire };
        public float Time;
        public bool IsOver;
        public bool HumanVictory;
        public int LevelAssetId;
        public int Stars => Time < 330f ? 3 : Time < 540f ? 2 : 1;

        private readonly System.Random _rng;
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private readonly HashSet<int> _humanBuildingsUnlocked = new HashSet<int>();
        private int _nextSquadId = 1;
        private int _nextUnitId = 1;
        private int _lastLogicalSecond = -1;
        private MapNode _tutorialTown;
        private Squad _tutorialEnemySquad;
        private bool _tutorialGuardPurchased;
        private bool _tutorialEnemyReplaced;
        private bool _tutorialTownCaptured;
        private Squad _ogreSquad;
        private bool _ogreEngaged;
        private bool _ogreKilled;
        private bool _l2MagicTowerCaptured;

        public BattleSim(int levelAssetId, Faction humanFaction, Faction enemyFaction,
            int[] humanMods = null, int seed = 20120920)
        {
            _rng = new System.Random(seed);
            LevelAssetId = levelAssetId;
            Human = new PlayerState
            {
                Id = 0,
                Name = "Player",
                Faction = humanFaction,
                IsHuman = true,
                AiEnabled = false,
            };
            Players.Add(Human);
            ApplyModifiers(Human, humanMods);
            OriginalRules.ApplyBaselineEdicts(Human);

            IReadOnlyList<EnemyPlayerSetup> setups = OriginalRules.EnemyPlayers(levelAssetId);
            if (setups.Count == 0)
            {
                setups = new[]
                {
                    new EnemyPlayerSetup
                    {
                        Faction = enemyFaction,
                        AiEnabled = true,
                    },
                };
            }
            foreach (EnemyPlayerSetup setup in setups)
            {
                var player = new PlayerState
                {
                    Id = Players.Count,
                    Name = setup.Faction + " AI " + Players.Count,
                    Faction = setup.Faction,
                    AiEnabled = setup.AiEnabled,
                    Gold = setup.StartGold,
                    Mana = setup.StartMana,
                };
                foreach (ModifierValue modifier in setup.Modifiers)
                {
                    if (modifier.Id >= 0 && modifier.Id < player.Mods.Length)
                        player.Mods[modifier.Id] += modifier.Value;
                }
                player.UltimateMax = 200f * player.ModMul(12);
                Players.Add(player);
            }

            Human.UltimateMax = 200f * Human.ModMul(12);
            BuildMap(levelAssetId);
            var level = Cfg.TbLevel.DataList.FirstOrDefault(value => value.AssetId == levelAssetId);
            Human.Gold = (level?.StartGold ?? OriginalRules.PlayerStartGold) + Human.Mods[8];
            Human.Mana = level?.StartMana ?? OriginalRules.PlayerStartMana;
            RefreshOwnership();
            InitializeScenario();
            RefreshOwnership();
        }

        public IReadOnlyList<BattleEvent> DrainEvents()
        {
            BattleEvent[] copy = _events.ToArray();
            _events.Clear();
            return copy;
        }

        public void Tick(float dt)
        {
            if (IsOver || dt <= 0f) return;
            Time += dt;
            TickSpellEffects(dt);
            TickSquads(dt);
            TickBuildings(dt);

            int logicalSecond = Mathf.FloorToInt(Time + 0.0001f);
            if (logicalSecond > _lastLogicalSecond)
            {
                _lastLogicalSecond = logicalSecond;
                TickLogicalSecond();
            }
            CheckEnd();
        }

        public bool ExecuteCommand(PlayerState player, BattleCommand command)
        {
            if (player == null || IsOver) return false;
            MapNode node = command.NodeId > 0 ? FindNode(command.NodeId) : null;
            switch (command.Type)
            {
                case BattleCommandType.Move:
                {
                    int[] ids = command.SquadIds != null && command.SquadIds.Length > 0
                        ? command.SquadIds
                        : new[] { command.SquadId };
                    List<Squad> ordered = ids.Select(id =>
                            player.Squads.FirstOrDefault(value => value.Id == id && !value.IsDead))
                        .Where(value => value != null)
                        .ToList();
                    if (ordered.Count == 0) return false;

                    var groups = new List<List<Squad>>();
                    foreach (Squad squad in ordered)
                    {
                        List<Squad> group = groups.FirstOrDefault(value =>
                            Vector2.Distance(value[0].Pos, squad.Pos) < 3f);
                        if (group == null)
                        {
                            group = new List<Squad>();
                            groups.Add(group);
                        }
                        group.Add(squad);
                    }
                    foreach (List<Squad> group in groups)
                    {
                        for (int i = 0; i < group.Count; i++)
                        {
                            OrderMove(group[i], command.Position, node);
                            group[i].MoveDelaySeconds = i * 4f;
                        }
                    }
                    return true;
                }
                case BattleCommandType.BuyUnit:
                {
                    GameConfig.takeover.Unit unit = Cfg.TbUnit.DataList.FirstOrDefault(value => value.Id == command.ConfigId);
                    return unit != null && BuyUnit(player, unit, node, out _) != null;
                }
                case BattleCommandType.Upgrade:
                    return TryUpgrade(player, node);
                case BattleCommandType.Build:
                    return TryBuild(player, node, command.ConfigId, out _);
                case BattleCommandType.CastSpell:
                {
                    GameConfig.takeover.Spell spell = Cfg.TbSpell.DataList.FirstOrDefault(value => value.Id == command.ConfigId);
                    return spell != null && TryCastSpell(player, spell, command.Position, node, out _);
                }
                case BattleCommandType.GrantResources:
                    player.Gold = Mathf.Max(0f, player.Gold + command.GoldDelta);
                    player.Mana = Mathf.Clamp(player.Mana + command.ManaDelta, 0f, ManaCap);
                    return true;
                default:
                    return false;
            }
        }

        public MapNode FindNode(int id) => Nodes.FirstOrDefault(node => node.Id == id);
        public PlayerState PlayerOf(Faction faction) => Players.FirstOrDefault(player => player.Faction == faction);

        private static void ApplyModifiers(PlayerState player, int[] modifiers)
        {
            if (player == null || modifiers == null) return;
            for (int i = 0; i < player.Mods.Length && i < modifiers.Length; i++)
                player.Mods[i] = modifiers[i];
        }

        private void BuildMap(int levelAssetId)
        {
            foreach (GameConfig.takeover.MapNode config in Cfg.TbMapNode.DataList.Where(value => value.LevelId == levelAssetId))
            {
                Faction owner = OriginalRules.FromConfigFaction(config.Owner);
                var node = new MapNode
                {
                    Id = config.Id,
                    Key = config.NodeKey,
                    Kind = (NodeKind)config.Kind,
                    Loc = config.Loc,
                    Owner = owner,
                    Pos = new Vector2(config.X, config.Y * OriginalRules.WorldYScale),
                };
                if (node.Kind >= NodeKind.Castle && node.Kind <= NodeKind.MageTower)
                {
                    PlayerState player = EnsurePlayer(owner);
                    var buildingDef = Cfg.BuildingByKind((int)node.Kind);
                    if (buildingDef != null)
                    {
                        int towerBonus = buildingDef.Id == 4 ? player?.Mods[38] ?? 0 : 0;
                        node.Building = new BuildingState
                        {
                            Node = node,
                            Owner = owner,
                            Kind = buildingDef.Id,
                            BaseHp = Mathf.RoundToInt(buildingDef.Hp * (player?.ModMul(11) ?? 1f)),
                            GoldPerSec = buildingDef.Gold + towerBonus,
                            ManaPerSec = buildingDef.Mana + towerBonus,
                            Supply = buildingDef.Supply + towerBonus,
                            UltimatePerSec = buildingDef.Ultimate,
                        };
                        node.Building.Hp = node.Building.MaxHp;
                    }
                }
                Nodes.Add(node);
                NodeByKey[node.Key] = node;
            }

            foreach (GameConfig.takeover.MapEdge config in Cfg.TbMapEdge.DataList.Where(value => value.LevelId == levelAssetId))
            {
                if (!NodeByKey.TryGetValue(config.NodeA, out MapNode nodeA) ||
                    !NodeByKey.TryGetValue(config.NodeB, out MapNode nodeB))
                    continue;
                var edge = new MapEdge
                {
                    Id = config.Id,
                    NodeA = nodeA,
                    NodeB = nodeB,
                };
                edge.Waypoints.Add(nodeA.Pos);
                edge.Waypoints.AddRange(OriginalMapWaypoints.For(levelAssetId, config.NodeA, config.NodeB));
                edge.Waypoints.Add(nodeB.Pos);
                nodeA.Neighbours.Add(nodeB);
                nodeB.Neighbours.Add(nodeA);
                nodeA.Edges.Add(edge);
                nodeB.Edges.Add(edge);
                Edges.Add(edge);
            }
        }

        private PlayerState EnsurePlayer(Faction faction)
        {
            PlayerState existing = PlayerOf(faction);
            if (existing != null) return existing;
            var player = new PlayerState
            {
                Id = Players.Count,
                Name = faction.ToString(),
                Faction = faction,
                AiEnabled = false,
            };
            Players.Add(player);
            return player;
        }

        private void InitializeScenario()
        {
            _humanBuildingsUnlocked.UnionWith(SupplyBuildingIds);
            if (LevelAssetId == 1) InitializeLevelOne();
            else if (LevelAssetId == 2) InitializeLevelTwo();
            else SpawnScenarioOgre();
        }

        private void InitializeLevelOne()
        {
            foreach (PlayerState player in Players) player.SpellsEnabled = false;
            Human.UnitTypeLimit = 0;
            PlayerState empire = PlayerOf(Faction.Empire);
            if (empire == null) return;
            empire.AiEnabled = false;
            GameConfig.takeover.Unit undeadWarrior = FindUnit(Faction.Empire, UnitKind.Infantry, 1);
            if (undeadWarrior == null) return;
            empire.Gold = undeadWarrior.Cost * 2f;
            _tutorialTown = empire.OwnedNodes.FirstOrDefault(node => node.Building?.Kind == 1);
            foreach (MapNode node in empire.OwnedNodes.Where(value => value.Building != null))
            {
                Squad squad = SpawnSquad(empire, undeadWarrior, node.Pos, node, 0, false);
                empire.Gold -= undeadWarrior.Cost;
                if (node == _tutorialTown) _tutorialEnemySquad = squad;
            }
        }

        private void InitializeLevelTwo()
        {
            Human.Gold += 300f;
            PlayerState khaganate = PlayerOf(Faction.Khaganate);
            if (khaganate == null) return;
            khaganate.AiEnabled = false;
            Human.SpellsEnabled = false;

            TryBuildFirstType(Human, 6);
            TryBuildFirstType(Human, 7);
            _humanBuildingsUnlocked.Clear();
            _humanBuildingsUnlocked.Add(5);

            GameConfig.takeover.Unit warrior = FindUnit(Faction.Khaganate, UnitKind.Infantry, 1);
            GameConfig.takeover.Unit hunter = FindUnit(Faction.Khaganate, UnitKind.Archers, 1);
            if (warrior == null) return;
            var villageCost = Cfg.BuildingById(5)?.BuildCost ?? 150;
            var mineCost = Cfg.BuildingById(6)?.BuildCost ?? 100;
            khaganate.Gold = warrior.Cost * 3f + (hunter?.Cost ?? 0) + villageCost + mineCost;
            TryBuildFirstType(khaganate, 5);
            foreach (MapNode node in khaganate.OwnedNodes.Where(value => value.Building != null))
            {
                if (node.Building.Kind == 4)
                {
                    SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                    khaganate.Gold -= warrior.Cost;
                    continue;
                }
                node.Building.Catch = 3;
                SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                khaganate.Gold -= warrior.Cost * 3f;
                if (hunter != null)
                {
                    SpawnSquad(khaganate, hunter, node.Pos, node, 0, false);
                    khaganate.Gold -= hunter.Cost;
                }
            }
            SpawnScenarioOgre();
        }

        private void SpawnScenarioOgre()
        {
            MapNode ogreNode = Nodes.FirstOrDefault(node => node.Kind == NodeKind.Ogre);
            if (ogreNode == null) return;
            PlayerState bandits = EnsurePlayer(Faction.Bandits);
            GameConfig.takeover.Unit ogre = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Ogre");
            if (ogre != null)
                _ogreSquad = SpawnSquad(bandits, ogre, ogreNode.Pos, ogreNode, 0, false);
        }

        private GameConfig.takeover.Unit FindUnit(Faction faction, UnitKind kind, int tier)
        {
            int configFaction = OriginalRules.ToConfigFaction(faction);
            return Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Faction == configFaction &&
                                                              unit.UnitType == (int)kind &&
                                                              unit.Tier == tier);
        }

        private bool TryBuildFirstType(PlayerState player, int buildingId)
        {
            MapNode spot = Nodes.FirstOrDefault(node => node.Kind == NodeKind.BuildSpot &&
                                                        node.Building == null &&
                                                        IsLocControlled(player, node.Loc));
            return spot != null && TryBuild(player, spot, buildingId, out _);
        }

        private void TickLogicalSecond()
        {
            foreach (PlayerState player in Players)
            {
                player.Gold += player.NetGoldIncome();
                player.Mana = Mathf.Min(ManaCap, player.Mana + player.ManaIncome);
                player.Ultimate = Mathf.Min(player.UltimateMax, player.Ultimate + player.UltimateRate);
                player.SpellCooldown = Mathf.Max(0f, player.SpellCooldown - 1f);
                if (player.AiEnabled && !player.IsHuman) TickAiSecond(player);
            }
        }

        private void TickSquads(float dt)
        {
            for (int squadIndex = 0; squadIndex < Squads.Count; squadIndex++)
            {
                Squad squad = Squads[squadIndex];
                if (squad.IsDead) continue;
                bool squadEngaged = false;
                foreach (CombatUnit unit in squad.Units)
                {
                    if (unit.IsDead) continue;
                    TickUnitStatuses(unit, dt);
                    TickProjectile(unit, dt);
                    if (unit.IsDead || unit.FrozenSeconds > 0f) continue;

                    CombatUnit targetUnit = FindTargetUnit(unit);
                    MapNode targetBuilding = FindTargetBuilding(unit, squad);
                    unit.TargetUnitId = targetUnit?.Id ?? -1;
                    unit.TargetBuildingNodeId = targetBuilding?.Id ?? -1;

                    if (targetUnit != null)
                    {
                        squadEngaged = true;
                        unit.Action = squad.Def.Range > 0 ? UnitAction.Ranged : UnitAction.Melee;
                        unit.Pursuing = false;
                        AttackUnit(unit, targetUnit, dt);
                    }
                    else if (targetBuilding != null)
                    {
                        squadEngaged = true;
                        unit.Action = squad.Def.Range > 0 ? UnitAction.Ranged : UnitAction.Melee;
                        AttackBuilding(unit, targetBuilding, dt);
                    }
                    else
                    {
                        unit.Action = squad.HasMoveOrder ? UnitAction.Moving : UnitAction.Stand;
                    }
                }

                if (!squadEngaged) AdvanceSquad(squad, dt);
                if (squad.Def.Name == "Giantbots" && squad.Owner.Mods[40] > 0)
                {
                    foreach (CombatUnit unit in squad.Units)
                    {
                        if (unit.IsDead) continue;
                        unit.Hp = Mathf.Min(unit.MaxHp,
                            unit.Hp + unit.MaxHp * squad.Owner.Mods[40] / 100f * dt);
                    }
                }
                ResolveUnitSeparation(squad);
                TryCollectChest(squad);
                if (squad.TargetNode?.Building != null && squad.TargetNode.Building.Hp <= 0 &&
                    squad.Units.Where(unit => !unit.IsDead).Any(unit =>
                        Vector2.Distance(unit.Pos, squad.TargetNode.Pos) <= 42f))
                {
                    Capture(squad.TargetNode, squad);
                }
            }

            ApplyDivinePresence(dt);
            for (int i = Squads.Count - 1; i >= 0; i--)
            {
                if (!Squads[i].IsDead) continue;
                KillSquad(Squads[i]);
            }
        }

        private void ApplyDivinePresence(float dt)
        {
            foreach (Squad source in Squads)
            {
                if (source.IsDead) continue;
                if (!OriginalRules.TryAbility(source.Def.Abilities, "DivinePresence", out int power) || power <= 0)
                    continue;
                foreach (Squad allySquad in Squads)
                {
                    if (allySquad.IsDead || allySquad.Faction != source.Faction) continue;
                    foreach (CombatUnit ally in allySquad.Units)
                    {
                        if (ally.IsDead || HasMoraleLock(allySquad) ||
                            Vector2.Distance(ally.Pos, source.Pos) > 100f)
                            continue;
                        ally.MoraleCurrent = Mathf.Min(ally.MoraleBase,
                            ally.MoraleCurrent + power * dt * 5f);
                    }
                }
            }
        }

        private void TickUnitStatuses(CombatUnit unit, float dt)
        {
            unit.FrozenSeconds = Mathf.Max(0f, unit.FrozenSeconds - dt);
            unit.DashSeconds = Mathf.Max(0f, unit.DashSeconds - dt);
            unit.ChargeSpeedSeconds = Mathf.Max(0f, unit.ChargeSpeedSeconds - dt);
            unit.SlowSeconds = Mathf.Max(0f, unit.SlowSeconds - dt);
            unit.DamageResistSeconds = Mathf.Max(0f, unit.DamageResistSeconds - dt);
            unit.DamageBuffSeconds = Mathf.Max(0f, unit.DamageBuffSeconds - dt);
            unit.MaxDamageSeconds = Mathf.Max(0f, unit.MaxDamageSeconds - dt);
            unit.MagicImmunitySeconds = Mathf.Max(0f, unit.MagicImmunitySeconds - dt);
            unit.HeroicSeconds = Mathf.Max(0f, unit.HeroicSeconds - dt);
            unit.FearedSeconds = Mathf.Max(0f, unit.FearedSeconds - dt);
            if (unit.HeroicSeconds > 0f) unit.MoraleState = MoraleState.Heroic;
            else if (unit.FearedSeconds > 0f) unit.MoraleState = MoraleState.Feared;
            else if (unit.MoraleCurrent <= unit.MoraleBase * 0.35f) unit.MoraleState = MoraleState.Low;
            else if (unit.MoraleCurrent >= unit.MoraleBase * 0.65f) unit.MoraleState = MoraleState.High;
            else unit.MoraleState = MoraleState.Neutral;

            if (unit.PoisonSeconds > 0f)
            {
                unit.PoisonSeconds = Mathf.Max(0f, unit.PoisonSeconds - dt);
                unit.PoisonTick -= dt;
                if (unit.PoisonTick <= 0f)
                {
                    unit.PoisonTick = 3f;
                    ApplyDamage(unit, unit.PoisonDamage, null, false);
                }
            }
        }

        private void TickProjectile(CombatUnit unit, float dt)
        {
            if (unit.ProjectileTargetUnitId < 0 && unit.ProjectileTargetNodeId < 0) return;
            unit.ProjectileRemaining -= dt;
            if (unit.ProjectileRemaining > 0f) return;

            int unitId = unit.ProjectileTargetUnitId;
            int nodeId = unit.ProjectileTargetNodeId;
            float damage = unit.ProjectileDamage;
            Vector2 point = unit.ProjectilePoint;
            unit.ProjectileTargetUnitId = -1;
            unit.ProjectileTargetNodeId = -1;
            unit.ProjectileRemaining = 0f;
            unit.ProjectileDamage = 0f;
            if (unitId >= 0)
            {
                CombatUnit target = FindUnitById(unitId);
                if (target != null && !target.IsDead)
                {
                    ApplyDamage(target, damage, unit.Squad, false);
                    ApplyAttackAbilities(unit, target, damage);
                }
            }
            else if (nodeId >= 0)
            {
                MapNode node = FindNode(nodeId);
                if (node?.Building != null && node.Building.Hp > 0)
                    ApplyBuildingDamage(node.Building, damage, unit.Squad);
            }
            _events.Add(new BattleEvent
            {
                Kind = "ProjectileHit",
                ActorId = unit.Id,
                TargetId = Mathf.Max(unitId, nodeId),
                Value = damage,
                Position = point,
            });
        }

        private void AdvanceSquad(Squad squad, float dt)
        {
            if (squad.MoveDelaySeconds > 0f)
            {
                squad.MoveDelaySeconds = Mathf.Max(0f, squad.MoveDelaySeconds - dt);
                return;
            }
            if (!squad.HasMoveOrder || squad.Path == null || squad.Path.Count == 0)
            {
                RepairAtHome(squad, dt);
                return;
            }

            Vector2 center = squad.FormationCenter;
            Vector2 waypoint = squad.Path[Mathf.Clamp(squad.PathIndex, 0, squad.Path.Count - 1)];
            Vector2 delta = waypoint - center;
            float distance = delta.magnitude;
            float speed = squad.Speed;
            float movement = speed * dt;
            Vector2 direction = distance < 0.001f ? Vector2.right : delta / distance;
            Vector2 newCenter = distance <= movement || distance < 3f
                ? waypoint
                : center + direction * movement;
            squad.FormationCenter = newCenter;
            float facing = Mathf.Atan2(direction.y, direction.x);
            bool formationArrived = true;
            for (int i = 0; i < squad.Units.Count; i++)
            {
                CombatUnit unit = squad.Units[i];
                if (unit.IsDead) continue;
                Vector2 formationOffset = FormationOffset(i, squad.Units.Count, facing);
                Vector2 desired = newCenter + formationOffset;
                Vector2 unitDelta = desired - unit.Pos;
                float unitDistance = unitDelta.magnitude;
                if (unitDistance <= 0.001f) continue;
                float unitSpeed = squad.Speed;
                if ((squad.Abilities & AbilityMask.HorrificDash) != 0 &&
                    !unit.Pursuing && FindNearestHostile(unit, squad) != null)
                {
                    unit.Pursuing = true;
                    unit.DashSeconds = 0.5f;
                }
                if ((squad.Abilities & AbilityMask.Charge) != 0 &&
                    unit.ChargeAvailable && !unit.Pursuing && FindNearestHostile(unit, squad) != null)
                {
                    unit.Pursuing = true;
                    unit.ChargeSpeedSeconds = 1.5f;
                }
                if (unit.DashSeconds > 0f) unitSpeed *= 15f;
                if (unit.ChargeSpeedSeconds > 0f) unitSpeed *= 1.75f;
                unit.Pos += unitDelta / unitDistance * Mathf.Min(unitDistance, unitSpeed * dt);
                unit.DashSeconds = Mathf.Max(0f, unit.DashSeconds - dt);
                unit.ChargeSpeedSeconds = Mathf.Max(0f, unit.ChargeSpeedSeconds - dt);
                if (Vector2.Distance(unit.Pos, desired) > 2f) formationArrived = false;
            }
            squad.AtNode = null;
            if (distance <= movement || distance < 3f)
            {
                bool finalWaypoint = squad.PathIndex >= squad.Path.Count - 1;
                if (!finalWaypoint)
                {
                    squad.PathIndex++;
                }
                else if (formationArrived)
                {
                    FinishMove(squad);
                }
            }
        }

        private void FinishMove(Squad squad)
        {
            squad.HasMoveOrder = false;
            squad.Path = null;
            squad.PathIndex = 0;
            if (squad.TargetNode != null &&
                squad.Units.Where(unit => !unit.IsDead).Any(unit =>
                    Vector2.Distance(unit.Pos, squad.TargetNode.Pos) <= 70f))
            {
                squad.AtNode = squad.TargetNode;
                if (squad.TargetNode.Building?.Owner == squad.Faction)
                    squad.HomeNode = squad.TargetNode;
            }
        }

        private void RepairAtHome(Squad squad, float dt)
        {
            if (squad.HomeNode?.Building == null || squad.HomeNode.Building.Owner != squad.Faction) return;
            if (squad.Units.Where(unit => !unit.IsDead)
                .All(unit => Vector2.Distance(unit.Pos, squad.HomeNode.Pos) > 60f))
                return;
            foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead && value.Hp < value.MaxHp))
            {
                if (HasMechanism(squad)) continue;
                float repair = unit.MaxHp / 4f + 1f;
                unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + repair * dt * 2f);
            }
        }

        private void TryCollectChest(Squad squad)
        {
            foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead))
            {
                MapNode node = Nodes.FirstOrDefault(value => value.Kind == NodeKind.Chest &&
                                                             !value.ChestCollected &&
                                                             Vector2.Distance(value.Pos, unit.Pos) <= 30f);
                if (node == null) continue;
                node.ChestCollected = true;
                squad.Owner.Gold += 500f;
                _events.Add(new BattleEvent
                {
                    Kind = "ChestCollected",
                    ActorId = squad.Id,
                    TargetId = node.Id,
                    Value = 500f,
                    Position = node.Pos,
                });
                return;
            }
        }

        private CombatUnit FindTargetUnit(CombatUnit unit)
        {
            CombatUnit best = null;
            float bestDistance = float.MaxValue;
            foreach (Squad squad in Squads)
            {
                if (squad.IsDead || squad.Faction == unit.Squad.Faction) continue;
                foreach (CombatUnit candidate in squad.Units)
                {
                    if (candidate.IsDead) continue;
                    float distance = Vector2.Distance(unit.Pos, candidate.Pos);
                    float reach = Mathf.Max(90f, unit.Squad.Def.Range) + UnitRadius(candidate.Squad);
                    if (distance > reach || distance >= bestDistance) continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private CombatUnit FindNearestHostile(CombatUnit unit, Squad self)
        {
            CombatUnit best = null;
            float bestDistance = 160f * 160f;
            foreach (Squad squad in Squads)
            {
                if (squad.Faction == self.Faction) continue;
                foreach (CombatUnit candidate in squad.Units)
                {
                    if (candidate.IsDead) continue;
                    float distance = Vector2.SqrMagnitude(candidate.Pos - unit.Pos);
                    if (distance > bestDistance) continue;
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        private MapNode FindTargetBuilding(CombatUnit unit, Squad squad)
        {
            if (!IsKeyBuilding(squad.TargetNode?.Building) || squad.TargetNode.Building.Owner == squad.Faction ||
                squad.TargetNode.Building.Hp <= 0)
                return null;
            if (Vector2.Distance(unit.Pos, squad.TargetNode.Pos) >
                Mathf.Max(90f, squad.Def.Range) + 50f)
                return null;
            return squad.TargetNode;
        }

        private void AttackUnit(CombatUnit attacker, CombatUnit target, float dt)
        {
            if (LevelAssetId == 2 && target.Squad == _ogreSquad && !_ogreEngaged &&
                attacker.Squad.Owner.IsHuman)
            {
                _ogreEngaged = true;
                Human.SpellsEnabled = true;
                _events.Add(new BattleEvent
                {
                    Kind = "OgreFightStarted",
                    ActorId = attacker.Id,
                    TargetId = target.Id,
                    Position = target.Pos,
                });
            }
            attacker.AttackTimer -= dt;
            if (attacker.AttackTimer > 0f) return;
            float slowMultiplier = attacker.SlowSeconds > 0f ? 2f : 1f;
            attacker.AttackTimer = Mathf.Max(0.1f, attacker.Squad.Def.AttackSpeed * 0.1f * slowMultiplier);
            AddSquadXp(attacker.Squad,
                Mathf.Max(0.01f, attacker.Squad.Def.AttackSpeed * 0.1f /
                                 Mathf.Max(1, attacker.Squad.MaxSoldiers)));
            float damage = RollDamage(attacker, target);
            if (attacker.Squad.Def.Range > 0)
            {
                attacker.ProjectileTargetUnitId = target.Id;
                attacker.ProjectileTargetNodeId = -1;
                attacker.ProjectileOrigin = attacker.Pos;
                attacker.ProjectilePoint = target.Pos;
                attacker.ProjectileDamage = damage;
                attacker.ProjectileRemaining = attacker.Squad.Profile.ProjectileTravel / 10f * (0.5f + (float)_rng.NextDouble());
            }
            else
            {
                ApplyDamage(target, damage, attacker.Squad, false);
                ApplyAttackAbilities(attacker, target, damage);
            }
        }

        private void AttackBuilding(CombatUnit attacker, MapNode node, float dt)
        {
            attacker.AttackTimer -= dt;
            if (attacker.AttackTimer > 0f) return;
            float slowMultiplier = attacker.SlowSeconds > 0f ? 2f : 1f;
            attacker.AttackTimer = Mathf.Max(0.1f, attacker.Squad.Def.AttackSpeed * 0.1f * slowMultiplier);
            float damage = RollDamage(attacker, null);
            if (attacker.Squad.Def.Range > 0)
            {
                attacker.ProjectileTargetUnitId = -1;
                attacker.ProjectileTargetNodeId = node.Id;
                attacker.ProjectileOrigin = attacker.Pos;
                attacker.ProjectilePoint = node.Pos;
                attacker.ProjectileDamage = damage;
                attacker.ProjectileRemaining = attacker.Squad.Profile.ProjectileTravel / 10f * (0.5f + (float)_rng.NextDouble());
            }
            else
            {
                ApplyBuildingDamage(node.Building, damage, attacker.Squad);
            }
            if (node.Building != null) node.Building.LastAttackerUnitId = attacker.Id;
        }

        private float RollDamage(CombatUnit attacker, CombatUnit target)
        {
            GameConfig.takeover.Unit def = attacker.Squad.Def;
            float roll = attacker.MaxDamageSeconds > 0f
                ? def.DmgMax
                : Mathf.Lerp(def.DmgMin, def.DmgMax, (float)_rng.NextDouble());
            float levelMultiplier = 1f + LevelTier(attacker.Squad) * 0.1f;
            roll *= levelMultiplier;
            if (def.UnitType == (int)UnitKind.Archers || def.UnitType == (int)UnitKind.Siege)
                roll *= attacker.Squad.Owner.ModMul(13);
            if (attacker.MoraleState == MoraleState.Heroic) roll *= 1.5f;
            else if (attacker.MoraleState == MoraleState.Feared) roll *= 0.5f;
            if (attacker.DamageBuffSeconds > 0f) roll *= 1f + attacker.DamageBuffPercent / 100f;
            if (target != null && target.DamageResistSeconds > 0f) roll *= 1f - target.DamageResist / 100f;

            AbilityMask attackerAbilities = attacker.Squad.Abilities;
            if ((attackerAbilities & AbilityMask.Charge) != 0 && attacker.ChargeAvailable)
            {
                OriginalRules.TryAbility(def.Abilities, "Charge", out int charge);
                int chargeModifier = attacker.Squad.Owner.Mods[7];
                roll += Mathf.Floor(roll * (charge * 10 + chargeModifier) / 100f);
                attacker.ChargeAvailable = false;
            }
            if (target != null && target.Squad.Def.UnitType == (int)UnitKind.Cavalry &&
                (attackerAbilities & AbilityMask.LongSpear) != 0)
            {
                OriginalRules.TryAbility(def.Abilities, "LongSpear", out int spear);
                roll *= 1f + spear / 100f;
            }
            if (target != null)
            {
                AbilityMask targetAbilities = target.Squad.Abilities;
                if ((targetAbilities & AbilityMask.ShieldWall) != 0 &&
                    def.Range > 0)
                {
                    OriginalRules.TryAbility(target.Squad.Def.Abilities, "ShieldWall", out int shield);
                    roll -= Mathf.RoundToInt(roll * shield / 100f);
                }
                if (def.Range > 0 &&
                    Vector2.Distance(attacker.Pos, target.Pos) <= UnitRadius(target.Squad) + 10f &&
                    (attackerAbilities & AbilityMask.MartialArtists) == 0)
                {
                    roll *= Mathf.Max(0f, (100f - def.MeleePenalty) / 100f);
                }
            }

            if (target == null)
            {
                roll *= 1f + attacker.Squad.Owner.Mods[18] / 100f;
                if ((attackerAbilities & AbilityMask.SiegeWeapon) != 0)
                {
                    OriginalRules.TryAbility(def.Abilities, "SiegeWeapon", out int siege);
                    roll *= 1f + siege / 100f;
                }
            }
            if ((attackerAbilities & AbilityMask.Mechanism) != 0 &&
                _rng.NextDouble() < attacker.Squad.Owner.Mods[32] / 100d)
                roll *= 2f;

            if (target == null && def.UnitType != (int)UnitKind.Siege)
            {
                // Buildings use the same per-unit roll; siege modifiers are applied above.
            }
            return Mathf.Max(1f, roll);
        }

        private void ApplyAttackAbilities(CombatUnit attacker, CombatUnit target, float damage, bool allowSplash = true)
        {
            AbilityMask abilities = attacker.Squad.Abilities;
            AbilityMask targetAbilities = target.Squad.Abilities;
            if ((abilities & AbilityMask.Poisonous) != 0 &&
                (targetAbilities & (AbilityMask.PoisonImmunity | AbilityMask.Undead | AbilityMask.Mechanism)) == 0)
            {
                OriginalRules.TryAbility(attacker.Squad.Def.Abilities, "Poisonous", out int poison);
                target.PoisonDamage = poison + attacker.Squad.Owner.Mods[24];
                target.PoisonSeconds = 3f;
                target.PoisonTick = 3f;
            }
            if ((abilities & (AbilityMask.FreezeArrow | AbilityMask.FreezeBlast)) != 0)
            {
                target.FrozenSeconds = Mathf.Max(target.FrozenSeconds, 3f + attacker.Squad.Owner.Mods[30]);
            }
            if (_rng.NextDouble() < attacker.Squad.Owner.Mods[33] / 100d)
                target.FrozenSeconds = Mathf.Max(target.FrozenSeconds, 3f + attacker.Squad.Owner.Mods[30]);
            if ((abilities & AbilityMask.Intimidiation) != 0 &&
                (targetAbilities & (AbilityMask.Discipline | (AbilityMask.Mechanism | AbilityMask.Undead))) == 0)
            {
                OriginalRules.TryAbility(attacker.Squad.Def.Abilities, "Intimidiation", out int intimidation);
                target.MoraleCurrent = Mathf.Max(0f, target.MoraleCurrent - intimidation);
            }
            if (allowSplash && (abilities & AbilityMask.SplashDamage) != 0)
            {
                OriginalRules.TryAbility(attacker.Squad.Def.Abilities, "SplashDamage", out int radius);
                foreach (Squad squad in Squads)
                {
                    if (squad.Faction == attacker.Squad.Faction || squad.IsDead) continue;
                    foreach (CombatUnit unit in squad.Units)
                    {
                        if (unit == target || unit.IsDead) continue;
                        if (Vector2.Distance(unit.Pos, target.Pos) <= radius)
                        {
                            ApplyDamage(unit, damage, attacker.Squad, false);
                            ApplyAttackAbilities(attacker, unit, damage, false);
                        }
                    }
                }
            }
        }

        private void ApplyDamage(CombatUnit target, float damage, Squad source, bool magic)
        {
            if (target == null || target.IsDead) return;
            if (magic && target.MagicImmunitySeconds > 0f) return;
            if (target.DamageResistSeconds > 0f) damage *= 1f - target.DamageResist / 100f;
            target.Hp = Mathf.Max(0f, target.Hp - damage);
            if (!HasMoraleLock(target.Squad))
                target.MoraleCurrent = Mathf.Max(0f, target.MoraleCurrent - damage * 0.5f);
            if (!target.IsDead)
            {
                _events.Add(new BattleEvent
                {
                    Kind = "UnitDamaged",
                    ActorId = source?.Id ?? -1,
                    TargetId = target.Id,
                    Value = damage,
                    Position = target.Pos,
                });
                return;
            }
            target.Hp = 0f;
            target.Action = UnitAction.Dead;
            _events.Add(new BattleEvent
            {
                Kind = "UnitKilled",
                ActorId = source?.Id ?? -1,
                TargetId = target.Id,
                Value = damage,
                Position = target.Pos,
            });
        }

        private void ApplyBuildingDamage(BuildingState building, float damage, Squad source)
        {
            if (building == null || building.Hp <= 0) return;
            building.Hp = Mathf.Max(0, building.Hp - Mathf.RoundToInt(damage));
            _events.Add(new BattleEvent
            {
                Kind = building.Hp <= 0 ? "BuildingDestroyed" : "BuildingDamaged",
                ActorId = source?.Id ?? -1,
                TargetId = building.Node.Id,
                Value = damage,
                Position = building.Node.Pos,
            });
            if (building.Hp <= 0)
                RefreshOwnership();
        }

        private void TickBuildings(float dt)
        {
            foreach (MapNode node in Nodes.Where(value => value.Building != null))
            {
                BuildingState building = node.Building;
                if (building.Hp <= 0 || building.LastAttackerUnitId < 0) continue;
                CombatUnit attacker = FindUnitById(building.LastAttackerUnitId);
                if (attacker == null || attacker.IsDead) continue;
                PlayerState owner = PlayerOf(building.Owner);
                if (owner == null) continue;
                foreach (Squad defender in owner.Squads.Where(value => !value.IsDead && value.HomeNode == node))
                {
                    if (defender.Path == null && defender.TargetNode != node)
                        OrderMove(defender, attacker.Pos, null);
                }
                building.LastAttackerUnitId = -1;
            }
        }

        private void KillSquad(Squad squad)
        {
            if (squad == _ogreSquad && !_ogreKilled)
            {
                _ogreKilled = true;
                PlayerState khaganate = PlayerOf(Faction.Khaganate);
                if (khaganate != null)
                {
                    khaganate.AiEnabled = true;
                    khaganate.Gold = 0f;
                }
                _events.Add(new BattleEvent
                {
                    Kind = "OgreDefeated",
                    ActorId = -1,
                    TargetId = squad.Id,
                    Position = squad.Pos,
                });
            }
            if (squad == _tutorialEnemySquad && !_tutorialEnemyReplaced)
            {
                _tutorialEnemyReplaced = true;
                PlayerState empire = PlayerOf(Faction.Empire);
                GameConfig.takeover.Unit replacement = FindUnit(Faction.Empire, UnitKind.Infantry, 1);
                if (empire != null && replacement != null)
                {
                    empire.Gold += replacement.Cost;
                    SpawnSquad(empire, replacement, _tutorialTown.Pos, _tutorialTown, 0, false);
                    empire.Gold -= replacement.Cost;
                    empire.AiEnabled = true;
                    Human.UnitTypeLimit = 1;
                    _events.Add(new BattleEvent
                    {
                        Kind = "TutorialEnemyReinforced",
                        ActorId = empire.Id,
                        TargetId = _tutorialTown.Id,
                        Position = _tutorialTown.Pos,
                    });
                }
            }
            squad.Owner.Squads.Remove(squad);
            Squads.Remove(squad);
            if (squad.Owner != null && squad.Owner.Mods[26] > 0)
                squad.Owner.Ultimate = Mathf.Min(squad.Owner.UltimateMax,
                    squad.Owner.Ultimate + squad.Owner.UltimateMax * squad.Owner.Mods[26] / 100f);
        }

        private void ResolveUnitSeparation(Squad squad)
        {
            for (int i = 0; i < squad.Units.Count; i++)
            {
                CombatUnit a = squad.Units[i];
                if (a.IsDead) continue;
                for (int j = i + 1; j < squad.Units.Count; j++)
                {
                    CombatUnit b = squad.Units[j];
                    if (b.IsDead) continue;
                    Vector2 delta = b.Pos - a.Pos;
                    float distance = delta.magnitude;
                    float minimum = UnitRadius(squad) * 1.6f;
                    if (distance >= minimum) continue;
                    if (distance < 0.001f) delta = Vector2.right;
                    else delta /= distance;
                    float push = (minimum - distance) * 0.5f;
                    a.Pos -= delta * push;
                    b.Pos += delta * push;
                }
            }
        }

        private void TickSpellEffects(float dt)
        {
            for (int i = ActiveSpells.Count - 1; i >= 0; i--)
            {
                ActiveSpellEffect effect = ActiveSpells[i];
                effect.Remaining -= dt;
                effect.TickTimer -= dt;

                if (effect.Kind == SpellEffectKind.Cataclysm)
                {
                    while (effect.TickTimer <= 0f && effect.ImpactIndex < 90)
                    {
                        TickCataclysmImpact(effect);
                        effect.ImpactIndex++;
                        effect.TickTimer += 0.065f;
                    }
                }
                else if (effect.TickTimer <= 0f)
                {
                    ApplySpellEffect(effect);
                    effect.TickTimer += 0.5f;
                }

                if (effect.Remaining <= 0f) ActiveSpells.RemoveAt(i);
            }
        }

        private void ApplySpellEffect(ActiveSpellEffect effect)
        {
            float radiusSquared = effect.Def.Radius * effect.Def.Radius;
            switch (effect.Kind)
            {
                case SpellEffectKind.Consecration:
                case SpellEffectKind.Desecration:
                    foreach (Squad squad in Squads.Where(value => !value.IsDead && value.Faction == effect.Faction))
                    {
                        if (HasMechanism(squad)) continue;
                        foreach (CombatUnit unit in squad.Units)
                        {
                            if (Vector2.SqrMagnitude(unit.Pos - effect.Center) > radiusSquared) continue;
                            if (unit.Hp <= 0f)
                            {
                                unit.Hp = Mathf.Min(unit.MaxHp, effect.Def.Power);
                                unit.Action = UnitAction.Stand;
                            }
                            else
                            {
                                unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + effect.Def.Power);
                            }
                        }
                    }
                    break;
                case SpellEffectKind.Heroism:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        if (HasMoraleLock(unit.Squad)) continue;
                        unit.HeroicSeconds = Mathf.Max(unit.HeroicSeconds, 10f);
                        unit.FearedSeconds = 0f;
                        unit.MoraleCurrent = unit.MoraleBase;
                    }
                    break;
                case SpellEffectKind.Bloodlust:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        unit.DamageBuffSeconds = 0.75f;
                        unit.DamageBuffPercent = Mathf.Max(0f, (effect.Def.Power - 1f) * 100f);
                    }
                    break;
                case SpellEffectKind.Liberation:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                        unit.MagicImmunitySeconds = 0.75f;
                    break;
                case SpellEffectKind.FrostEnchantment:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        unit.MaxDamageSeconds = 0.75f;
                        unit.DamageResistSeconds = 0.75f;
                        unit.DamageResist = 25f;
                    }
                    break;
                case SpellEffectKind.Stoneworms:
                case SpellEffectKind.IceBlast:
                    if (effect.ImpactIndex++ > 0) break;
                    foreach (CombatUnit unit in HostileUnits(effect.Faction, effect.Center, radiusSquared, false))
                    {
                        if (effect.Kind == SpellEffectKind.IceBlast)
                        {
                            if (unit.MagicImmunitySeconds <= 0f)
                                unit.FrozenSeconds = Mathf.Max(unit.FrozenSeconds, effect.Def.Time);
                        }
                        else
                            ApplyDamage(unit, Mathf.Ceil((effect.Def.Power * 0.5f) +
                                (float)_rng.NextDouble() * effect.Def.Power * 0.5f), effect.Owner?.Squads.FirstOrDefault(), true);
                    }
                    break;
            }
        }

        private void TickCataclysmImpact(ActiveSpellEffect effect)
        {
            Vector2 point = new Vector2((float)_rng.NextDouble() * OriginalRules.NativeStageWidth,
                (float)_rng.NextDouble() * OriginalRules.NativeStageHeight * OriginalRules.WorldYScale);
            float damage = 4f + (float)_rng.NextDouble() * 4f;
            foreach (CombatUnit unit in Squads.SelectMany(squad => squad.Units).Where(unit => !unit.IsDead).ToList())
            {
                if (Vector2.Distance(unit.Pos, point) > effect.Def.Radius) continue;
                float applied = unit.Squad.Faction == effect.Faction ? damage * 0.5f : damage;
                ApplyDamage(unit, applied, effect.Owner?.Squads.FirstOrDefault(), true);
            }
            foreach (MapNode node in Nodes.Where(value => value.Building != null && value.Building.Hp > 0))
            {
                if (Vector2.Distance(node.Pos, point) <= effect.Def.Radius)
                    ApplyBuildingDamage(node.Building, damage * 0.5f, effect.Owner?.Squads.FirstOrDefault());
            }
            _events.Add(new BattleEvent
            {
                Kind = "CataclysmImpact",
                ActorId = effect.Owner?.Id ?? -1,
                TargetId = effect.ImpactIndex,
                Value = damage,
                Position = point,
            });
        }

        private IEnumerable<CombatUnit> AlliedUnits(Faction faction, Vector2 center, float radiusSquared)
        {
            return Squads.Where(squad => !squad.IsDead && squad.Faction == faction)
                .SelectMany(squad => squad.Units)
                .Where(unit => !unit.IsDead && Vector2.SqrMagnitude(unit.Pos - center) <= radiusSquared);
        }

        private IEnumerable<CombatUnit> HostileUnits(Faction faction, Vector2 center, float radiusSquared, bool excludeBandits)
        {
            return Squads.Where(squad => !squad.IsDead && squad.Faction != faction &&
                                         (!excludeBandits || squad.Faction != Faction.Bandits))
                .SelectMany(squad => squad.Units)
                .Where(unit => !unit.IsDead && Vector2.SqrMagnitude(unit.Pos - center) <= radiusSquared);
        }

        public bool CanBuy(PlayerState player, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            reason = null;
            if (player == null || def == null || at?.Building == null) { reason = "无效招募目标"; return false; }
            if (!IsKeyBuilding(at.Building)) { reason = "附属建筑不能招募"; return false; }
            if (at.Building.Owner != player.Faction || !at.Building.IsActive) { reason = "不是己方可用建筑"; return false; }
            if (!AllowedAt(player, at.Building, def)) { reason = "该建筑不能生产此兵种"; return false; }
            if (player.Gold < UnitCost(player, def)) { reason = "金币不足"; return false; }
            if (player.FreeSupply < def.Upkeep) { reason = "补给不足"; return false; }
            return true;
        }

        public static bool AllowedAt(BuildingState building, GameConfig.takeover.Unit def)
        {
            if (building == null || def == null) return false;
            var buildingDef = Cfg.BuildingById(building.Kind);
            return buildingDef != null && buildingDef.AllowedTypes.Contains(def.UnitType) && def.Tier == 1;
        }

        public static bool AllowedAt(PlayerState player, BuildingState building, GameConfig.takeover.Unit def)
        {
            if (building == null || def == null) return false;
            var buildingDef = Cfg.BuildingById(building.Kind);
            if (buildingDef == null || !buildingDef.AllowedTypes.Contains(def.UnitType) ||
                def.UnitType > (player?.UnitTypeLimit ?? 4))
                return false;
            int selectedTier = Mathf.Clamp(1 + (player?.Mods[TierModifier(def.UnitType)] ?? 0), 1, 3);
            return def.Tier == selectedTier;
        }

        public Squad BuyUnit(PlayerState player, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            if (!CanBuy(player, def, at, out reason)) return null;
            player.Gold -= UnitCost(player, def);
            int initialXp = at.Building.Level == 0 ? 0 : at.Building.Level == 1 ? 25 : 100;
            Squad squad = SpawnSquad(player, def, at.Pos, at, initialXp, true);
            if (LevelAssetId == 1 && player.IsHuman && def.Name == "Guards" && !_tutorialGuardPurchased)
            {
                _tutorialGuardPurchased = true;
                if (_tutorialEnemySquad != null)
                    OrderMove(_tutorialEnemySquad, new Vector2(340f, 290f * OriginalRules.WorldYScale), null);
            }
            return squad;
        }

        private static int UnitCost(PlayerState player, GameConfig.takeover.Unit def)
        {
            float multiplier = OriginalRules.UsesUndeadRecruitDiscount(def) ? player.ModMul(28) : 1f;
            return Mathf.RoundToInt(def.Cost * multiplier);
        }

        private Squad SpawnSquad(PlayerState owner, GameConfig.takeover.Unit def, Vector2 position,
            MapNode home, int initialXp, bool checkSupply)
        {
            if (checkSupply && owner.FreeSupply < def.Upkeep) return null;
            AbilityMask abilities = OriginalRules.ParseAbilities(def.Abilities);
            int count = def.UnitsCount + ((abilities & AbilityMask.Undead) != 0 && def.Name == "Immortals"
                ? owner.Mods[23]
                : 0);
            var squad = new Squad
            {
                Id = _nextSquadId++,
                Owner = owner,
                Def = def,
                Abilities = abilities,
                Profile = OriginalRules.UnitProfile(def),
                HomeNode = home,
                AtNode = home,
                TargetPosition = position,
                FormationCenter = position,
                Xp = initialXp * owner.ModMul(37),
            };
            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPosition = position + FormationOffset(i, count);
                var unit = new CombatUnit
                {
                    Id = _nextUnitId++,
                    Index = i,
                    Squad = squad,
                    Pos = spawnPosition,
                    MaxHp = UnitHp(owner, def),
                    BaseMaxHp = UnitHp(owner, def),
                    AttackTimer = 0f,
                    MoraleBase = Mathf.Round(def.Morale * owner.ModMul(15)) * 10f,
                    ChargeAvailable = (abilities & AbilityMask.Charge) != 0,
                };
                unit.Hp = unit.MaxHp;
                unit.MoraleCurrent = unit.MoraleBase;
                squad.Units.Add(unit);
            }
            squad.LevelIndex = LevelForXp(squad.Xp);
            ApplyLevel(squad);
            owner.Squads.Add(squad);
            Squads.Add(squad);
            _events.Add(new BattleEvent
            {
                Kind = "SquadSpawned",
                ActorId = owner.Id,
                TargetId = squad.Id,
                Value = def.Id,
                Position = position,
            });
            return squad;
        }

        private static float UnitHp(PlayerState player, GameConfig.takeover.Unit def)
        {
            float multiplier = 1f;
            if (def.UnitType == (int)UnitKind.Infantry) multiplier *= player.ModMul(16);
            if (def.UnitType == (int)UnitKind.Cavalry) multiplier *= player.ModMul(39);
            return Mathf.Ceil(def.Hp * multiplier);
        }

        private static void ApplyLevel(Squad squad)
        {
            foreach (CombatUnit unit in squad.Units)
            {
                float hpRatio = unit.MaxHp <= 0f ? 1f : unit.Hp / unit.MaxHp;
                unit.MaxHp = Mathf.Ceil(unit.BaseMaxHp * (1f + squad.LevelIndex * 0.1f));
                unit.Hp = unit.MaxHp * hpRatio;
            }
        }

        private void AddSquadXp(Squad squad, float amount)
        {
            if (squad == null || squad.IsDead) return;
            squad.Xp += amount * squad.Owner.ModMul(37);
            int level = LevelForXp(squad.Xp);
            if (level <= squad.LevelIndex) return;
            squad.LevelIndex = level;
            ApplyLevel(squad);
        }

        private static int LevelForXp(float xp) => xp >= 100f ? 2 : xp >= 25f ? 1 : 0;
        private static int LevelTier(Squad squad) => squad.LevelIndex;

        public bool TryUpgrade(PlayerState player, MapNode at)
        {
            if (at?.Building == null || at.Building.Owner != player.Faction || at.Building.Level >= BuildingState.MaxLevel)
                return false;
            if (!IsKeyBuilding(at.Building)) return false;
            var def = Cfg.BuildingById(at.Building.Kind);
            if (def == null) return false;
            int cost = at.Building.Level == 0 ? def.UpgradeCost1 : def.UpgradeCost2;
            if (player.Gold < cost) return false;
            player.Gold -= cost;
            int before = at.Building.MaxHp;
            at.Building.Level++;
            at.Building.Hp += Mathf.Max(0, at.Building.MaxHp - before);
            return true;
        }

        public bool CanPlace(PlayerState player, MapNode spot, int buildingId, out string reason)
        {
            reason = null;
            var def = Cfg.BuildingById(buildingId);
            if (player == null || spot == null || spot.Kind != NodeKind.BuildSpot || spot.Building != null || def == null)
            {
                reason = "不是可建造空位";
                return false;
            }
            if (!_humanBuildingsUnlocked.Contains(buildingId) && player.IsHuman)
            {
                reason = "该建筑尚未解锁";
                return false;
            }
            if (!IsLocControlled(player, spot.Loc))
            {
                reason = "不在己方控制范围";
                return false;
            }
            int cost = Mathf.RoundToInt(def.BuildCost * player.ModMul(31));
            if (player.Gold < cost)
            {
                reason = "金币不足";
                return false;
            }
            return true;
        }

        public bool TryBuild(PlayerState player, MapNode spot, int buildingId, out string reason)
        {
            if (!CanPlace(player, spot, buildingId, out reason)) return false;
            var def = Cfg.BuildingById(buildingId);
            player.Gold -= Mathf.RoundToInt(def.BuildCost * player.ModMul(31));
            spot.Building = new BuildingState
            {
                Node = spot,
                Owner = player.Faction,
                Kind = def.Id,
                BaseHp = def.Hp,
                Hp = def.Hp,
                GoldPerSec = def.Gold,
                ManaPerSec = def.Mana,
                Supply = def.Supply,
                UltimatePerSec = def.Ultimate,
            };
            if (!player.OwnedNodes.Contains(spot)) player.OwnedNodes.Add(spot);
            if (LevelAssetId == 2 && player.IsHuman && def.Id == 5)
                _humanBuildingsUnlocked.UnionWith(new[] { 6, 7 });
            return true;
        }

        public bool TryCastSpell(PlayerState player, GameConfig.takeover.Spell spell,
            MapNode target, out string reason)
        {
            return TryCastSpell(player, spell, target?.Pos ?? Vector2.zero, target, out reason);
        }

        public bool TryCastSpell(PlayerState player, GameConfig.takeover.Spell spell,
            Vector2 position, MapNode target, out string reason)
        {
            reason = null;
            if (player == null || spell == null) { reason = "无效法术"; return false; }
            if (OriginalRules.FromConfigFaction(spell.Faction) != player.Faction) { reason = "阵营不匹配"; return false; }
            if (!OriginalRules.IsSpellUnlocked(player, spell.Id)) { reason = "法术尚未解锁"; return false; }

            bool ultimate = spell.SpellType == 2;
            if (ultimate)
            {
                if (player.Ultimate + 0.0001f < player.UltimateMax) { reason = "终极技能尚未充满"; return false; }
                if (!player.IsHuman && !CanAiCastUltimate(player)) { reason = "终极技能条件不满足"; return false; }
                player.Ultimate = 0f;
            }
            else
            {
                int cost = Mathf.RoundToInt(spell.Cost * player.ModMul(35));
                if (player.Mana < cost) { reason = "法力不足"; return false; }
                player.Mana -= cost;
            }

            CastSpellEffect(player, spell, position, target);
            _events.Add(new BattleEvent
            {
                Kind = ultimate ? "UltimateCast" : "SpellCast",
                ActorId = player.Id,
                TargetId = target?.Id ?? -1,
                Value = spell.Id,
                Position = position,
            });
            return true;
        }

        private void CastSpellEffect(PlayerState player, GameConfig.takeover.Spell spell, Vector2 position, MapNode target)
        {
            int originalId = OriginalRules.OriginalSpellIndex(spell.Id);
            switch (originalId)
            {
                case 0:
                    foreach (CombatUnit unit in HostileUnits(player.Faction, position,
                                 spell.Radius * spell.Radius, false))
                    {
                        float percent = spell.Power * 0.5f + (float)_rng.NextDouble() * spell.Power * 0.5f;
                        percent *= player.ModMul(10);
                        ApplyDamage(unit, Mathf.Ceil(unit.MaxHp * percent / 100f), null, true);
                    }
                    break;
                case 1:
                case 2:
                case 6:
                case 10:
                case 14:
                    ActiveSpells.Add(new ActiveSpellEffect
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = spell.Time * player.ModMul(6),
                        TickTimer = 0f,
                        Kind = originalId == 1 ? SpellEffectKind.Consecration :
                            originalId == 2 ? SpellEffectKind.Heroism :
                            originalId == 6 ? SpellEffectKind.Bloodlust :
                            originalId == 10 ? SpellEffectKind.Liberation : SpellEffectKind.Desecration,
                    });
                    ApplySpellEffect(ActiveSpells[ActiveSpells.Count - 1]);
                    break;
                case 3:
                    SpawnSpecialUnit(player, "Avatar", position);
                    break;
                case 4:
                    ActiveSpells.Add(new ActiveSpellEffect
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = 1f + player.Mods[21],
                        TickTimer = 1f,
                        Kind = SpellEffectKind.Stoneworms,
                    });
                    break;
                case 5:
                    SpawnSpecialUnit(player, "Immortals", target?.Pos ?? position);
                    break;
                case 7:
                    ActiveSpells.Add(new ActiveSpellEffect
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = 6.5f,
                        TickTimer = 0f,
                        Kind = SpellEffectKind.Cataclysm,
                    });
                    break;
                case 8:
                    ActiveSpells.Add(new ActiveSpellEffect
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = 1f,
                        TickTimer = 0.75f,
                        Kind = SpellEffectKind.IceBlast,
                    });
                    break;
                case 9:
                    ActiveSpells.Add(new ActiveSpellEffect
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = spell.Time,
                        TickTimer = 0f,
                        Kind = SpellEffectKind.FrostEnchantment,
                    });
                    ApplySpellEffect(ActiveSpells[ActiveSpells.Count - 1]);
                    break;
                case 11:
                    ApplyGrandTransformation(player);
                    break;
                case 12:
                    foreach (CombatUnit unit in HostileUnits(player.Faction, position,
                                 spell.Radius * spell.Radius, true))
                    {
                        if (unit.MagicImmunitySeconds > 0f || HasFearLock(unit.Squad)) continue;
                        unit.HeroicSeconds = 0f;
                        unit.FearedSeconds = Mathf.Max(unit.FearedSeconds, 10f);
                        unit.MoraleCurrent = 0f;
                    }
                    break;
                case 13:
                    bool archer = _rng.NextDouble() > 0.5d;
                    SpawnSpecialUnit(player, archer ? "Undying Archers" : "Undying Warriors", target?.Pos ?? position);
                    break;
                case 15:
                    CallToTheGrave(player);
                    break;
            }
        }

        private void SpawnSpecialUnit(PlayerState player, string unitName, Vector2 position)
        {
            GameConfig.takeover.Unit def = Cfg.TbUnit.DataList.FirstOrDefault(unit =>
                unit.Name == unitName && OriginalRules.FromConfigFaction(unit.Faction) == player.Faction);
            if (def == null && unitName == "Avatar")
                def = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Avatar");
            if (def == null) return;
            MapNode node = NearestKeyNode(position);
            SpawnSquad(player, def, position, node, 0, false);
        }

        private void ApplyGrandTransformation(PlayerState player)
        {
            bool kill = true;
            foreach (CombatUnit unit in Squads.Where(squad => squad.Faction != player.Faction &&
                                                              squad.Faction != Faction.Bandits)
                         .SelectMany(squad => squad.Units).ToList())
            {
                if (unit.IsDead || !kill) { kill = !kill; continue; }
                kill = !kill;
                ApplyDamage(unit, unit.Hp, null, true);
                if (!unit.IsDead) continue;
                if (_rng.NextDouble() > 0.5d) player.Mana += 20f;
                else player.Gold += 20f;
            }
        }

        private void CallToTheGrave(PlayerState player)
        {
            List<Squad> candidates = Squads.Where(squad => !squad.IsDead &&
                                                           squad.Faction != player.Faction &&
                                                           squad.Faction != Faction.Bandits)
                .ToList();
            for (int i = 0; i < 2 && candidates.Count > 0; i++)
            {
                Squad source = candidates[_rng.Next(candidates.Count)];
                int tier = Mathf.Clamp(1 + player.Mods[TierModifier(source.Def.UnitType)], 1, 3);
                GameConfig.takeover.Unit undead = Cfg.TbUnit.DataList.FirstOrDefault(unit =>
                    OriginalRules.FromConfigFaction(unit.Faction) == player.Faction &&
                    unit.UnitType == source.Def.UnitType && unit.Tier == tier);
                if (undead == null) break;
                Vector2 position = source.Pos;
                int xp = (int)source.Xp;
                source.Owner.Squads.Remove(source);
                Squads.Remove(source);
                MapNode home = player.OwnedNodes.FirstOrDefault(node => node.Building != null);
                Squad created = SpawnSquad(player, undead, position, home, xp, false);
                MapNode targetNode = FindNearestHostileNode(home ?? created.AtNode, player);
                if (targetNode != null) OrderMove(created, targetNode.Pos, targetNode);
                candidates.Remove(source);
            }
        }

        private void TickAiSecond(PlayerState player)
        {
            SortieDefenders(player);
            BuildAiSupplies(player);
            if (player.FreeSupply > 0 && player.Gold >= AiBuyThreshold)
                BuyAiUnit(player);
            CastAiSpells(player);
            TryAiUltimate(player);
        }

        private void SortieDefenders(PlayerState player)
        {
            foreach (MapNode node in player.OwnedNodes.Where(value =>
                         IsKeyBuilding(value.Building) && value.Building.IsActive).ToList())
            {
                List<Squad> defenders = player.Squads
                    .Where(squad => !squad.IsDead && squad.HomeNode == node && !squad.HasMoveOrder &&
                                    Vector2.Distance(squad.Pos, node.Pos) <= 60f)
                    .ToList();
                if (defenders.Count <= node.Building.Catch) continue;
                MapNode target = FindNearestHostileNode(node, player);
                if (target != null) OrderMove(defenders[0], target.Pos, target);
            }
        }

        private void BuildAiSupplies(PlayerState player)
        {
            int buildingCount = player.ActiveBuildings;
            if (player.SupplyCap - player.SupplyUsed <= 0 && player.Gold >= (Cfg.BuildingById(5)?.BuildCost ?? 150))
                TryBuildFirstType(player, 5);
            if (player.ManaIncome < Mathf.RoundToInt(buildingCount / 3f))
                TryBuildFirstType(player, 7);
            if (player.BaseGoldIncome < 2 && buildingCount > player.BaseGoldIncome)
                TryBuildFirstType(player, 6);
        }

        private void BuyAiUnit(PlayerState player)
        {
            MapNode bestNode = null;
            float bestScore = -999999f;
            List<GameConfig.takeover.Unit> bestCandidates = null;
            foreach (MapNode node in player.OwnedNodes.Where(value =>
                         IsKeyBuilding(value.Building) && value.Building.IsActive))
            {
                float score = BuildingThreat(node);
                score += (float)(0.5d - _rng.NextDouble()) * score / 3f;
                var candidates = Cfg.TbUnit.DataList.Where(unit =>
                        OriginalRules.FromConfigFaction(unit.Faction) == player.Faction &&
                        unit.UnitType <= player.LivingSquads &&
                        AllowedAt(player, node.Building, unit) &&
                        player.Gold >= UnitCost(player, unit) &&
                        player.FreeSupply >= unit.Upkeep)
                    .ToList();
                if (candidates.Count == 0 || score < bestScore) continue;
                bestNode = node;
                bestScore = score;
                bestCandidates = candidates;
            }
            if (bestNode != null && bestCandidates.Count > 0)
                BuyUnit(player, bestCandidates[_rng.Next(bestCandidates.Count)], bestNode, out _);
        }

        private float BuildingThreat(MapNode node)
        {
            float score = Squads.Count(squad => !squad.IsDead && squad.Faction != node.Building.Owner &&
                                                  Vector2.Distance(squad.Pos, node.Pos) <= 220f) * 20f;
            int defenders = Squads.Count(squad => !squad.IsDead && squad.Faction == node.Building.Owner &&
                                                    squad.HomeNode == node &&
                                                    Vector2.Distance(squad.Pos, node.Pos) <= 60f);
            foreach (MapNode other in Nodes.Where(value => IsKeyBuilding(value.Building) && value.Building.IsActive &&
                                                           value.Building.Owner != node.Building.Owner))
            {
                float distance = Mathf.Max(1f, Vector2.Distance(node.Pos, other.Pos));
                int enemyDefenders = Squads.Count(squad => !squad.IsDead &&
                                                           squad.Faction == other.Building.Owner &&
                                                           squad.HomeNode == other);
                score += 10000f / distance + 10f * (enemyDefenders - defenders);
            }
            return score;
        }

        private void CastAiSpells(PlayerState player)
        {
            if (!player.SpellsEnabled || player.SpellCooldown > 0f) return;
            foreach (GameConfig.takeover.Spell spell in AvailableSpells(player)
                         .Where(value => value.SpellType != 2)
                         .OrderBy(value => value.Id))
            {
                int cost = Mathf.RoundToInt(spell.Cost * player.ModMul(35));
                if (cost > player.Mana) continue;
                Vector2 position = FindAiSpellTarget(player, spell);
                if (!HasAiSpellTarget(player, spell, position)) continue;
                if (TryCastSpell(player, spell, position, NearestKeyNode(position), out _))
                {
                    player.SpellCooldown = OriginalRules.SpellRetrySeconds;
                    break;
                }
            }
        }

        private Vector2 FindAiSpellTarget(PlayerState player, GameConfig.takeover.Spell spell)
        {
            int id = OriginalRules.OriginalSpellIndex(spell.Id);
            bool beneficial = id == 1 || id == 2 || id == 6 || id == 9 || id == 10 || id == 14;
            IEnumerable<CombatUnit> units = beneficial
                ? Squads.Where(squad => !squad.IsDead && squad.Faction == player.Faction).SelectMany(squad => squad.Units)
                : Squads.Where(squad => !squad.IsDead && squad.Faction != player.Faction &&
                                         squad.Faction != Faction.Bandits).SelectMany(squad => squad.Units);
            CombatUnit best = units.Where(unit => !unit.IsDead)
                .OrderByDescending(unit => beneficial ? 1f - unit.HpRatio : 1f)
                .ThenBy(unit => Vector2.Distance(unit.Pos, player.OwnedNodes.FirstOrDefault()?.Pos ?? Vector2.zero))
                .FirstOrDefault();
            if (best != null) return best.Pos + FormationOffset(0, 1);
            return player.OwnedNodes.FirstOrDefault()?.Pos ??
                   new Vector2(OriginalRules.NativeStageWidth * 0.5f, OriginalRules.NativeStageHeight);
        }

        private bool HasAiSpellTarget(PlayerState player, GameConfig.takeover.Spell spell, Vector2 position)
        {
            int id = OriginalRules.OriginalSpellIndex(spell.Id);
            bool beneficial = id == 1 || id == 2 || id == 6 || id == 9 || id == 10 || id == 14;
            if (id == 3 || id == 5 || id == 7 || id == 11 || id == 13 || id == 15) return true;
            return Squads.Any(squad => !squad.IsDead &&
                                       (beneficial ? squad.Faction == player.Faction : squad.Faction != player.Faction) &&
                                       Vector2.Distance(squad.Pos, position) <= spell.Radius);
        }

        private void TryAiUltimate(PlayerState player)
        {
            if (player.Ultimate + 0.0001f < player.UltimateMax || !CanAiCastUltimate(player)) return;
            GameConfig.takeover.Spell ultimate = AvailableSpells(player).FirstOrDefault(spell => spell.SpellType == 2);
            if (ultimate == null) return;
            Vector2 position = FindAiSpellTarget(player, ultimate);
            TryCastSpell(player, ultimate, position, NearestKeyNode(position), out _);
        }

        private bool CanAiCastUltimate(PlayerState player)
        {
            int own = Squads.Count(squad => !squad.IsDead && squad.Faction == player.Faction);
            int enemies = Squads.Count(squad => !squad.IsDead && squad.Faction != player.Faction &&
                                                squad.Faction != Faction.Bandits);
            switch (player.Faction)
            {
                case Faction.Empire:
                    return enemies > 5;
                case Faction.Westaria:
                    return Squads.Count(squad => !squad.IsDead && squad.Faction == player.Faction &&
                                                 !HasMechanism(squad)) >= 3;
                case Faction.Khaganate:
                    return true;
                case Faction.Cult:
                    return enemies > 3 && own < enemies;
                default:
                    return false;
            }
        }

        private MapNode FindNearestHostileNode(MapNode from, PlayerState player)
        {
            if (from == null) return null;
            return Nodes.Where(node => IsKeyBuilding(node.Building) && node.Building.Owner != player.Faction)
                .OrderBy(node => ShortestDistance(from, node))
                .FirstOrDefault();
        }

        private float ShortestDistance(MapNode from, MapNode to)
        {
            if (from == null || to == null) return float.MaxValue;
            var distances = new Dictionary<MapNode, float>();
            var queue = new List<MapNode> { from };
            distances[from] = 0f;
            while (queue.Count > 0)
            {
                MapNode current = queue.OrderBy(node => distances[node]).First();
                queue.Remove(current);
                if (current == to) return distances[current];
                foreach (MapEdge edge in current.Edges)
                {
                    MapNode next = edge.NodeA == current ? edge.NodeB : edge.NodeA;
                    float candidate = distances[current] + Mathf.Max(1f, edge.Length);
                    if (distances.TryGetValue(next, out float existing) && existing <= candidate) continue;
                    distances[next] = candidate;
                    if (!queue.Contains(next)) queue.Add(next);
                }
            }
            return float.MaxValue;
        }

        public void OrderMove(Squad squad, MapNode targetNode)
        {
            OrderMove(squad, targetNode?.Pos ?? squad.Pos, targetNode);
        }

        public void OrderMove(Squad squad, Vector2 position, MapNode targetNode = null)
        {
            if (squad == null || squad.IsDead) return;
            squad.TargetNode = targetNode;
            squad.TargetPosition = position;
            squad.FormationCenter = squad.Pos;
            squad.Path = BuildPath(squad, position, targetNode);
            squad.PathIndex = squad.Path == null || squad.Path.Count == 0 ? 0 : Mathf.Min(1, squad.Path.Count - 1);
            squad.HasMoveOrder = squad.Path != null && squad.Path.Count > 0;
            foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead))
            {
                unit.Pursuing = false;
                unit.ChargeAvailable = (squad.Abilities & AbilityMask.Charge) != 0;
            }
        }

        private List<Vector2> BuildPath(Squad squad, Vector2 target, MapNode targetNode)
        {
            MapNode start = squad.AtNode ?? NearestNode(squad.Pos);
            if (start == null || targetNode == null)
                return new List<Vector2> { squad.Pos, target };
            List<MapNode> nodePath = FindPath(start, targetNode);
            if (nodePath == null || nodePath.Count == 0)
                return new List<Vector2> { squad.Pos, target };
            var points = new List<Vector2> { squad.Pos };
            for (int i = 1; i < nodePath.Count; i++)
            {
                MapEdge edge = nodePath[i - 1].Edges.FirstOrDefault(value =>
                    (value.NodeA == nodePath[i - 1] && value.NodeB == nodePath[i]) ||
                    (value.NodeB == nodePath[i - 1] && value.NodeA == nodePath[i]));
                if (edge == null)
                {
                    points.Add(nodePath[i].Pos);
                    continue;
                }
                bool forward = edge.NodeA == nodePath[i - 1];
                IEnumerable<Vector2> waypoints = forward ? edge.Waypoints : edge.Waypoints.AsEnumerable().Reverse();
                points.AddRange(waypoints.Skip(1));
            }
            if (Vector2.Distance(points[points.Count - 1], target) > 0.01f) points.Add(target);
            return points;
        }

        public MapNode NearestNode(Vector2 position)
        {
            return Nodes.OrderBy(node => Vector2.SqrMagnitude(node.Pos - position)).FirstOrDefault();
        }

        private MapNode NearestKeyNode(Vector2 position)
        {
            return Nodes.Where(node => node.Building != null)
                .OrderBy(node => Vector2.SqrMagnitude(node.Pos - position))
                .FirstOrDefault();
        }

        public List<MapNode> FindPath(MapNode from, MapNode to)
        {
            if (from == null || to == null) return null;
            var distances = new Dictionary<MapNode, float> { [from] = 0f };
            var previous = new Dictionary<MapNode, MapNode>();
            var open = new List<MapNode> { from };
            while (open.Count > 0)
            {
                MapNode current = open.OrderBy(node => distances[node]).First();
                open.Remove(current);
                if (current == to) break;
                foreach (MapEdge edge in current.Edges)
                {
                    MapNode next = edge.NodeA == current ? edge.NodeB : edge.NodeA;
                    float candidate = distances[current] + Mathf.Max(1f, edge.Length);
                    if (distances.TryGetValue(next, out float currentDistance) && currentDistance <= candidate)
                        continue;
                    distances[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            if (!distances.ContainsKey(to)) return null;
            var path = new List<MapNode>();
            for (MapNode node = to; node != null; node = previous.TryGetValue(node, out MapNode prev) ? prev : null)
            {
                path.Add(node);
                if (node == from) break;
            }
            path.Reverse();
            return path;
        }

        private void Capture(MapNode node, Squad capturer)
        {
            if (!IsKeyBuilding(node?.Building) || node.Building.Owner == capturer.Faction) return;
            Faction previousOwner = node.Building.Owner;
            node.Building.Owner = capturer.Faction;
            ReinitializeBuilding(node.Building, capturer.Owner);
            capturer.HomeNode = node;
            capturer.AtNode = node;
            capturer.Owner.Gold += capturer.Owner.Mods[22];
            if (LevelAssetId == 1 && node == _tutorialTown && !_tutorialTownCaptured)
            {
                _tutorialTownCaptured = true;
                PlayerState empire = PlayerOf(Faction.Empire);
                GameConfig.takeover.Unit replacement = FindUnit(Faction.Empire, UnitKind.Infantry, 1);
                if (empire != null && replacement != null) empire.Gold += replacement.Cost;
                _events.Add(new BattleEvent
                {
                    Kind = "TutorialTownCaptured",
                    ActorId = capturer.Id,
                    TargetId = node.Id,
                    Position = node.Pos,
                });
            }
            if (LevelAssetId == 2 && node.Building.Kind == 4 && !_l2MagicTowerCaptured)
            {
                _l2MagicTowerCaptured = true;
                Human.UltimateForced = true;
                Human.Ultimate = Human.UltimateMax;
                _events.Add(new BattleEvent
                {
                    Kind = "UltimateUnlocked",
                    ActorId = Human.Id,
                    TargetId = node.Id,
                    Position = node.Pos,
                });
            }
            RefreshOwnership();
            _events.Add(new BattleEvent
            {
                Kind = "BuildingCaptured",
                ActorId = capturer.Id,
                TargetId = node.Id,
                Value = (int)previousOwner,
                Position = node.Pos,
            });
        }

        private static void ReinitializeBuilding(BuildingState building, PlayerState owner)
        {
            int hp = building.Hp;
            var def = Cfg.BuildingById(building.Kind);
            if (def == null) return;
            int towerBonus = building.Kind == 4 ? owner.Mods[38] : 0;
            building.BaseHp = Mathf.RoundToInt(def.Hp * owner.ModMul(11));
            building.GoldPerSec = def.Gold + towerBonus;
            building.ManaPerSec = def.Mana + towerBonus;
            building.Supply = def.Supply + towerBonus;
            building.UltimatePerSec = def.Ultimate;
            building.Hp = Mathf.Clamp(hp, 0, building.MaxHp);
        }

        private void CheckEnd()
        {
            bool allHuman = Nodes.Where(node => IsKeyBuilding(node.Building))
                .All(node => node.Building.Owner == Human.Faction);
            bool noHuman = Nodes.Where(node => IsKeyBuilding(node.Building))
                .All(node => node.Building.Owner != Human.Faction);
            if (!allHuman && !noHuman) return;
            IsOver = true;
            HumanVictory = allHuman;
        }

        public static bool IsKeyBuilding(BuildingState building) =>
            building != null && building.Kind >= 1 && building.Kind <= 4;

        public bool IsLocControlledBy(PlayerState player, MapNode spot) =>
            spot != null && IsLocControlled(player, spot.Loc);

        private static bool IsLocControlled(PlayerState player, int location) =>
            player.OwnedNodes.Any(node => node.Loc == location && node.Building != null &&
                                          node.Building.Owner == player.Faction);

        private void RefreshOwnership()
        {
            foreach (PlayerState player in Players)
                player.OwnedNodes.RemoveAll(node => node.Building == null || node.Building.Owner != player.Faction);
            foreach (MapNode node in Nodes.Where(value => value.Building != null))
            {
                PlayerState owner = EnsurePlayer(node.Building.Owner);
                if (!owner.OwnedNodes.Contains(node)) owner.OwnedNodes.Add(node);
            }
        }

        public List<GameConfig.takeover.Spell> AvailableSpells(PlayerState player)
        {
            return Cfg.TbSpell.DataList
                .Where(spell => OriginalRules.FromConfigFaction(spell.Faction) == player.Faction)
                .Where(spell => OriginalRules.IsSpellUnlocked(player, spell.Id))
                .OrderBy(spell => spell.SpellType)
                .ThenBy(spell => spell.Id)
                .ToList();
        }

        private static int TierModifier(int unitType)
        {
            switch ((UnitKind)unitType)
            {
                case UnitKind.Infantry: return 0;
                case UnitKind.Cavalry: return 1;
                case UnitKind.Archers: return 2;
                case UnitKind.Siege: return 3;
                case UnitKind.Magic: return 4;
                default: return 0;
            }
        }

        private CombatUnit FindUnitById(int id) =>
            Squads.SelectMany(squad => squad.Units).FirstOrDefault(unit => unit.Id == id && !unit.IsDead);

        private static bool HasMechanism(Squad squad) =>
            (squad.Abilities & AbilityMask.Mechanism) != 0;

        private static bool HasFearLock(Squad squad)
        {
            AbilityMask abilities = squad.Abilities;
            return (abilities & AbilityMask.Discipline) != 0 || HasMoraleLock(squad);
        }

        private static bool HasMoraleLock(Squad squad)
        {
            AbilityMask abilities = squad.Abilities;
            return (abilities & (AbilityMask.Mechanism | AbilityMask.Undead)) != 0;
        }

        private static float UnitRadius(Squad squad)
        {
            return (UnitKind)squad.Def.UnitType == UnitKind.Magic ? 15f :
                (UnitKind)squad.Def.UnitType == UnitKind.Cavalry ? 10f : 8f;
        }

        private static Vector2 FormationOffset(int index, int count, float rotation = 0f)
        {
            Vector2[] formation;
            if (count >= 9)
            {
                formation = FormationThreeByFour;
            }
            else if (count >= 7)
            {
                formation = FormationSeven;
            }
            else if (count >= 6)
            {
                formation = FormationThreeByTwo;
            }
            else if (count >= 5)
            {
                formation = FormationFive;
            }
            else if (count >= 3)
            {
                formation = FormationThree;
            }
            else
            {
                formation = FormationOne;
            }
            Vector2 offset = formation[Mathf.Clamp(index, 0, formation.Length - 1)];
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            return new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
        }
    }
}
