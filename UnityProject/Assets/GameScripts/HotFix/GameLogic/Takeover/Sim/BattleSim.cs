using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 对原版 1.0.4 首章战役进行确定性模拟。
    /// 不采用迁移阶段的临时逻辑；常量和状态变化根据反编译的关卡、部队、单位及法术代码还原。
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
        private readonly BattleMapSystem _mapSystem;
        private readonly BattleEconomySystem _economySystem;
        private readonly BattleCombatSystem _combatSystem;
        private readonly BattleSpellSystem _spellSystem;
        private readonly BattleAiSystem _aiSystem;
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

        internal BattleMapSystem MapSystem => _mapSystem;
        internal BattleEconomySystem EconomySystem => _economySystem;
        internal BattleCombatSystem CombatSystem => _combatSystem;
        internal BattleSpellSystem SpellSystem => _spellSystem;
        internal BattleAiSystem AiSystem => _aiSystem;
        internal System.Random RandomSource => _rng;
        internal List<BattleEvent> EventBuffer => _events;
        internal HashSet<int> HumanBuildingsUnlocked => _humanBuildingsUnlocked;
        internal int AllocateSquadId() => _nextSquadId++;
        internal int AllocateUnitId() => _nextUnitId++;
        internal MapNode TutorialTown { get => _tutorialTown; set => _tutorialTown = value; }
        internal Squad TutorialEnemySquad { get => _tutorialEnemySquad; set => _tutorialEnemySquad = value; }
        internal bool TutorialGuardPurchased { get => _tutorialGuardPurchased; set => _tutorialGuardPurchased = value; }
        internal bool TutorialEnemyReplaced { get => _tutorialEnemyReplaced; set => _tutorialEnemyReplaced = value; }
        internal bool TutorialTownCaptured { get => _tutorialTownCaptured; set => _tutorialTownCaptured = value; }
        internal Squad OgreSquad { get => _ogreSquad; set => _ogreSquad = value; }
        internal bool OgreEngaged { get => _ogreEngaged; set => _ogreEngaged = value; }
        internal bool OgreKilled { get => _ogreKilled; set => _ogreKilled = value; }
        internal bool L2MagicTowerCaptured { get => _l2MagicTowerCaptured; set => _l2MagicTowerCaptured = value; }

        public BattleSim(int levelAssetId, Faction humanFaction, Faction enemyFaction,
            int[] humanMods = null, int seed = 20120920)
        {
            _rng = new System.Random(seed);
            _mapSystem = new BattleMapSystem(this);
            _economySystem = new BattleEconomySystem(this);
            _combatSystem = new BattleCombatSystem(this);
            _spellSystem = new BattleSpellSystem(this);
            _aiSystem = new BattleAiSystem(this);
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
            _mapSystem.BuildMap(levelAssetId);
            var level = Cfg.TbLevel.DataList.FirstOrDefault(value => value.AssetId == levelAssetId);
            Human.Gold = (level?.StartGold ?? OriginalRules.PlayerStartGold) + Human.Mods[8];
            Human.Mana = level?.StartMana ?? OriginalRules.PlayerStartMana;
            _mapSystem.RefreshOwnership();
            InitializeScenario();
            _mapSystem.RefreshOwnership();
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
            _spellSystem.TickSpellEffects(dt);
            _combatSystem.TickSquads(dt);
            _combatSystem.TickBuildings(dt);

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

        public bool CanBuy(PlayerState player, GameConfig.takeover.Unit def, MapNode at, out string reason) =>
            _economySystem.CanBuy(player, def, at, out reason);

        public static bool AllowedAt(BuildingState building, GameConfig.takeover.Unit def) =>
            BattleEconomySystem.AllowedAt(building, def);

        public static bool AllowedAt(PlayerState player, BuildingState building, GameConfig.takeover.Unit def) =>
            BattleEconomySystem.AllowedAt(player, building, def);

        public Squad BuyUnit(PlayerState player, GameConfig.takeover.Unit def, MapNode at, out string reason) =>
            _economySystem.BuyUnit(player, def, at, out reason);

        public bool TryUpgrade(PlayerState player, MapNode at) => _economySystem.TryUpgrade(player, at);

        public bool CanPlace(PlayerState player, MapNode spot, int buildingId, out string reason) =>
            _economySystem.CanPlace(player, spot, buildingId, out reason);

        public bool TryBuild(PlayerState player, MapNode spot, int buildingId, out string reason) =>
            _economySystem.TryBuild(player, spot, buildingId, out reason);

        public bool TryCastSpell(PlayerState player, GameConfig.takeover.Spell spell,
            MapNode target, out string reason) => _spellSystem.TryCastSpell(player, spell, target, out reason);

        public bool TryCastSpell(PlayerState player, GameConfig.takeover.Spell spell,
            Vector2 position, MapNode target, out string reason) =>
            _spellSystem.TryCastSpell(player, spell, position, target, out reason);

        public void OrderMove(Squad squad, MapNode targetNode) => _mapSystem.OrderMove(squad, targetNode);

        public void OrderMove(Squad squad, Vector2 position, MapNode targetNode = null) =>
            _mapSystem.OrderMove(squad, position, targetNode);

        public MapNode NearestNode(Vector2 position) => _mapSystem.NearestNode(position);

        public List<MapNode> FindPath(MapNode from, MapNode to) => _mapSystem.FindPath(from, to);

        public List<GameConfig.takeover.Spell> AvailableSpells(PlayerState player) =>
            _spellSystem.AvailableSpells(player);

        private static void ApplyModifiers(PlayerState player, int[] modifiers)
        {
            if (player == null || modifiers == null) return;
            for (int i = 0; i < player.Mods.Length && i < modifiers.Length; i++)
                player.Mods[i] = modifiers[i];
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
            GameConfig.takeover.Unit undeadWarrior = _economySystem.FindUnit(Faction.Empire, UnitKind.Infantry, 1);
            if (undeadWarrior == null) return;
            empire.Gold = undeadWarrior.Cost * 2f;
            _tutorialTown = empire.OwnedNodes.FirstOrDefault(node => node.Building?.Kind == 1);
            foreach (MapNode node in empire.OwnedNodes.Where(value => value.Building != null))
            {
                Squad squad = _economySystem.SpawnSquad(empire, undeadWarrior, node.Pos, node, 0, false);
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

            _economySystem.TryBuildFirstType(Human, 6);
            _economySystem.TryBuildFirstType(Human, 7);
            _humanBuildingsUnlocked.Clear();
            _humanBuildingsUnlocked.Add(5);

            GameConfig.takeover.Unit warrior = _economySystem.FindUnit(Faction.Khaganate, UnitKind.Infantry, 1);
            GameConfig.takeover.Unit hunter = _economySystem.FindUnit(Faction.Khaganate, UnitKind.Archers, 1);
            if (warrior == null) return;
            var villageCost = Cfg.BuildingById(5)?.BuildCost ?? 150;
            var mineCost = Cfg.BuildingById(6)?.BuildCost ?? 100;
            khaganate.Gold = warrior.Cost * 3f + (hunter?.Cost ?? 0) + villageCost + mineCost;
            _economySystem.TryBuildFirstType(khaganate, 5);
            foreach (MapNode node in khaganate.OwnedNodes.Where(value => value.Building != null))
            {
                if (node.Building.Kind == 4)
                {
                    _economySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                    khaganate.Gold -= warrior.Cost;
                    continue;
                }
                node.Building.Catch = 3;
                _economySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                _economySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                _economySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                khaganate.Gold -= warrior.Cost * 3f;
                if (hunter != null)
                {
                    _economySystem.SpawnSquad(khaganate, hunter, node.Pos, node, 0, false);
                    khaganate.Gold -= hunter.Cost;
                }
            }
            SpawnScenarioOgre();
        }

        private void SpawnScenarioOgre()
        {
            MapNode ogreNode = Nodes.FirstOrDefault(node => node.Kind == NodeKind.Ogre);
            if (ogreNode == null) return;
            PlayerState bandits = _mapSystem.EnsurePlayer(Faction.Bandits);
            GameConfig.takeover.Unit ogre = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Ogre");
            if (ogre != null)
                _ogreSquad = _economySystem.SpawnSquad(bandits, ogre, ogreNode.Pos, ogreNode, 0, false);
        }

        private void TickLogicalSecond()
        {
            foreach (PlayerState player in Players)
            {
                player.Gold += player.NetGoldIncome();
                player.Mana = Mathf.Min(ManaCap, player.Mana + player.ManaIncome);
                player.Ultimate = Mathf.Min(player.UltimateMax, player.Ultimate + player.UltimateRate);
                player.SpellCooldown = Mathf.Max(0f, player.SpellCooldown - 1f);
                if (player.AiEnabled && !player.IsHuman) _aiSystem.TickAiSecond(player);
            }
        }

        internal static int LevelForXp(float xp) => xp >= 100f ? 2 : xp >= 25f ? 1 : 0;
        internal static int LevelTier(Squad squad) => squad.LevelIndex;

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

        internal static bool IsLocControlled(PlayerState player, int location) =>
            player.OwnedNodes.Any(node => node.Loc == location && node.Building != null &&
                                          node.Building.Owner == player.Faction);

        internal CombatUnit FindUnitById(int id) =>
            Squads.SelectMany(squad => squad.Units).FirstOrDefault(unit => unit.Id == id && !unit.IsDead);

        internal static bool HasMechanism(Squad squad) =>
            (squad.Abilities & AbilityMask.Mechanism) != 0;

        internal static bool HasFearLock(Squad squad)
        {
            AbilityMask abilities = squad.Abilities;
            return (abilities & AbilityMask.Discipline) != 0 || HasMoraleLock(squad);
        }

        internal static bool HasMoraleLock(Squad squad)
        {
            AbilityMask abilities = squad.Abilities;
            return (abilities & (AbilityMask.Mechanism | AbilityMask.Undead)) != 0;
        }

        internal static float UnitRadius(Squad squad)
        {
            return (UnitKind)squad.Def.UnitType == UnitKind.Magic ? 15f :
                (UnitKind)squad.Def.UnitType == UnitKind.Cavalry ? 10f : 8f;
        }

        internal static Vector2 FormationOffset(int index, int count, float rotation = 0f)
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
