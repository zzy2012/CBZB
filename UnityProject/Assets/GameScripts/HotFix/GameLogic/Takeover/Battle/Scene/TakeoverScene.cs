using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// Takeover 战斗场景及其确定性模拟逻辑。
    /// </summary>
    public sealed class TakeoverScene : SceneBase
    {
        /// <summary>参与方魔法值上限。</summary>
        public const float ManaCap = 500f;
        /// <summary>AI 购买单位时使用的资源阈值。</summary>
        public const int AiBuyThreshold = OriginalRules.AiBuyThreshold;
        /// <summary>按编队最小单位数索引的坐标配置。</summary>
        private readonly SortedDictionary<int, Vector2[]> _formationLayouts =
            new SortedDictionary<int, Vector2[]>();

        /// <summary>战场地图节点集合。</summary>
        public readonly List<MapNodeEntity> Nodes = new List<MapNodeEntity>();
        /// <summary>按配置键索引的地图节点。</summary>
        public readonly Dictionary<string, MapNodeEntity> NodeByKey = new Dictionary<string, MapNodeEntity>();
        /// <summary>地图连接边的集合。</summary>
        public readonly List<MapEdge> Edges = new List<MapEdge>();
        /// <summary>部队集合。</summary>
        public readonly List<SquadEntity> Squads = new List<SquadEntity>();
        /// <summary>战斗参与方集合。</summary>
        public readonly List<PlayerStateEntity> Players = new List<PlayerStateEntity>();
        /// <summary>当前生效的法术效果集合。</summary>
        public readonly List<ActiveSpellEffectEntity> ActiveSpells = new List<ActiveSpellEffectEntity>();

        /// <summary>人类玩家参与方。</summary>
        public readonly PlayerStateEntity Human;
        /// <summary>首个非人类参与方；不存在时返回默认参与方对象。</summary>
        public PlayerStateEntity Enemy => Players.FirstOrDefault(player => !player.IsHuman) ??
                                    new PlayerStateEntity { Faction = Faction.Empire };
        /// <summary>战斗已运行的时间，单位为秒。</summary>
        public float Time;
        /// <summary>战斗是否已经结束。</summary>
        public bool IsOver;
        /// <summary>战斗是否由人类玩家获胜。</summary>
        public bool HumanVictory;
        /// <summary>当前模拟的关卡资源标识。</summary>
        public int LevelAssetId;
        /// <summary>按照战斗耗时计算的通关星级。</summary>
        public int Stars => Time < 330f ? 3 : Time < 540f ? 2 : 1;

        /// <summary>用于确定性战斗模拟的随机数生成器。</summary>
        internal System.Random RandomSource => _rng;
        private readonly System.Random _rng;

        /// <summary>负责地图、寻路和移动逻辑的模拟系统。</summary>
        internal BattleMapSystem MapSystem => _mapSystem;
        private readonly BattleMapSystem _mapSystem;

        /// <summary>负责招募、建筑和资源规则的模拟系统。</summary>
        internal BattleEconomySystem EconomySystem => _economySystem;
        private readonly BattleEconomySystem _economySystem;

        /// <summary>负责部队、单位和建筑战斗的模拟系统。</summary>
        internal BattleCombatSystem CombatSystem => _combatSystem;
        private readonly BattleCombatSystem _combatSystem;

        /// <summary>负责法术施放和效果的模拟系统。</summary>
        internal BattleSpellSystem SpellSystem => _spellSystem;
        private readonly BattleSpellSystem _spellSystem;

        /// <summary>负责电脑参与方决策的模拟系统。</summary>
        internal BattleAiSystem AiSystem => _aiSystem;
        private readonly BattleAiSystem _aiSystem;

        /// <summary>供模拟系统写入事件的缓冲区。</summary>
        internal List<BattleEvent> EventBuffer => _events;
        private readonly List<BattleEvent> _events = new List<BattleEvent>();

        /// <summary>人类玩家本局已解锁的建筑标识集合。</summary>
        internal HashSet<int> HumanBuildingsUnlocked => _humanBuildingsUnlocked;
        private readonly HashSet<int> _humanBuildingsUnlocked = new HashSet<int>();

        /// <summary>下一个待分配的部队标识。</summary>
        private int _nextSquadId = 1;
        /// <summary>下一个待分配的士兵标识。</summary>
        private int _nextUnitId = 1;
        /// <summary>最近一次处理的整数模拟秒数。</summary>
        private int _lastLogicalSecond = -1;
        /// <summary>分配并返回下一个部队标识。</summary>
        internal int AllocateSquadId() => _nextSquadId++;
        /// <summary>分配并返回下一个士兵标识。</summary>
        internal int AllocateUnitId() => _nextUnitId++;
        /// <summary>按关卡、双方阵营、修正项和随机种子初始化模拟场景。</summary>
        public TakeoverScene(int levelAssetId, Faction humanFaction, Faction enemyFaction,
            int[] humanMods = null, int seed = 20120920)
        {
            _rng = new System.Random(seed);
            _mapSystem = new BattleMapSystem(this);
            _economySystem = new BattleEconomySystem(this);
            _combatSystem = new BattleCombatSystem(this);
            _spellSystem = new BattleSpellSystem(this);
            _aiSystem = new BattleAiSystem(this);
            InitializeFormationLayouts();
            LevelAssetId = levelAssetId;
            Human = new PlayerStateEntity
            {
                Id = 0,
                Name = "Player",
                Faction = humanFaction,
                IsHuman = true,
                AiEnabled = false,
            };
            Players.Add(Human);
            AddEntity(BattleEntityType.Player, Human);
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
                var player = new PlayerStateEntity
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
                AddEntity(BattleEntityType.Player, player);
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

        /// <summary>取出并清空模拟器积累的战斗事件。</summary>
        public IReadOnlyList<BattleEvent> DrainEvents()
        {
            BattleEvent[] copy = _events.ToArray();
            _events.Clear();
            return copy;
        }

        /// <summary>按指定时间增量推进战斗模拟。</summary>
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
            base.OnUpdate(dt);
        }

        /// <summary>通过场景更新入口推进战斗模拟。</summary>
        public override void OnUpdate(float deltaSeconds) => Tick(deltaSeconds);

        /// <summary>将持续法术效果注册到模拟场景。</summary>
        internal void AddSpellEffect(ActiveSpellEffectEntity effect)
        {
            ActiveSpells.Add(effect);
            AddEntity(BattleEntityType.SpellEffect, effect);
        }

        /// <summary>从模拟场景移除持续法术效果。</summary>
        internal void RemoveSpellEffect(ActiveSpellEffectEntity effect)
        {
            if (!ActiveSpells.Remove(effect)) return;
            RemoveEntity(BattleEntityType.SpellEffect, effect);
        }

        /// <summary>移除部队及其士兵对应的场景实体。</summary>
        internal void RemoveSquadEntities(SquadEntity squad)
        {
            foreach (CombatUnitEntity unit in squad.Units)
                RemoveEntity(BattleEntityType.Unit, unit);
            RemoveEntity(BattleEntityType.Squad, squad);
        }

        /// <summary>以指定参与方身份执行战斗命令并返回执行结果。</summary>
        public bool ExecuteCommand(PlayerStateEntity player, BattleCommand command)
        {
            if (player == null || IsOver) return false;
            MapNodeEntity node = command.NodeId > 0 ? FindNode(command.NodeId) : null;
            switch (command.Type)
            {
                case BattleCommandType.Move:
                {
                    int[] ids = command.SquadIds != null && command.SquadIds.Length > 0
                        ? command.SquadIds
                        : new[] { command.SquadId };
                    List<SquadEntity> ordered = ids.Select(id =>
                            player.Squads.FirstOrDefault(value => value.Id == id && !value.IsDead))
                        .Where(value => value != null)
                        .ToList();
                    if (ordered.Count == 0) return false;

                    var groups = new List<List<SquadEntity>>();
                    foreach (SquadEntity squad in ordered)
                    {
                        List<SquadEntity> group = groups.FirstOrDefault(value =>
                            Vector2.Distance(value[0].Pos, squad.Pos) < 3f);
                        if (group == null)
                        {
                            group = new List<SquadEntity>();
                            groups.Add(group);
                        }
                        group.Add(squad);
                    }
                    foreach (List<SquadEntity> group in groups)
                    {
                        for (int i = 0; i < group.Count; i++)
                        {
                            _mapSystem.OrderMove(group[i], command.Position, node);
                            group[i].MoveDelaySeconds = i * 4f;
                        }
                    }
                    return true;
                }
                case BattleCommandType.BuyUnit:
                {
                    GameConfig.takeover.Unit unit = Cfg.TbUnit.DataList.FirstOrDefault(value => value.Id == command.ConfigId);
                    return unit != null && _economySystem.BuyUnit(player, unit, node, out _) != null;
                }
                case BattleCommandType.Upgrade:
                    return _economySystem.TryUpgrade(player, node);
                case BattleCommandType.Build:
                    return _economySystem.TryBuild(player, node, command.ConfigId, out _);
                case BattleCommandType.CastSpell:
                {
                    GameConfig.takeover.Spell spell = Cfg.TbSpell.DataList.FirstOrDefault(value => value.Id == command.ConfigId);
                    return spell != null &&
                           _spellSystem.TryCastSpell(player, spell, command.Position, node, out _);
                }
                case BattleCommandType.GrantResources:
                    player.Gold = Mathf.Max(0f, player.Gold + command.GoldDelta);
                    player.Mana = Mathf.Clamp(player.Mana + command.ManaDelta, 0f, ManaCap);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>按标识查找地图节点，未找到时返回空。</summary>
        public MapNodeEntity FindNode(int id) => Nodes.FirstOrDefault(node => node.Id == id);
        /// <summary>查找指定阵营的参与方，未找到时返回空。</summary>
        public PlayerStateEntity PlayerOf(Faction faction) => Players.FirstOrDefault(player => player.Faction == faction);

        /// <summary>将初始修正项应用到指定参与方。</summary>
        private static void ApplyModifiers(PlayerStateEntity player, int[] modifiers)
        {
            if (player == null || modifiers == null) return;
            for (int i = 0; i < player.Mods.Length && i < modifiers.Length; i++)
                player.Mods[i] = modifiers[i];
        }

        /// <summary>根据关卡编号初始化对应战斗场景。</summary>
        private void InitializeScenario()
        {
            _humanBuildingsUnlocked.UnionWith(Cfg.BuildableBuildings.Select(building => building.Id));
            if (LevelAssetId == 1) InitializeLevelOne();
            else if (LevelAssetId == 2) InitializeLevelTwo();
            else SpawnScenarioOgre();
        }

        /// <summary>初始化第一关的开场状态和初始部队。</summary>
        private void InitializeLevelOne()
        {
            PlayerStateEntity empire = PlayerOf(Faction.Empire);
            if (empire == null) return;
            GameConfig.takeover.Unit undeadWarrior = _economySystem.FindUnit(Faction.Empire, UnitKind.Infantry, 1);
            if (undeadWarrior == null) return;
            empire.Gold = undeadWarrior.Cost * 2f;
            foreach (MapNodeEntity node in empire.OwnedNodes.Where(value => value.Building != null))
            {
                _economySystem.SpawnSquad(empire, undeadWarrior, node.Pos, node, 0, false);
                empire.Gold -= undeadWarrior.Cost;
            }
        }

        /// <summary>初始化第二关的特殊场景和初始状态。</summary>
        private void InitializeLevelTwo()
        {
            Human.Gold += 300f;
            PlayerStateEntity khaganate = PlayerOf(Faction.Khaganate);
            if (khaganate == null) return;

            GameConfig.takeover.Building farm =
                Cfg.BuildableBuildings.Single(building => building.Supply > 0);
            GameConfig.takeover.Building mine =
                Cfg.BuildableBuildings.Single(building => building.Gold > 0);
            GameConfig.takeover.Building obelisk =
                Cfg.BuildableBuildings.Single(building => building.Mana > 0);
            _economySystem.TryBuildFirstType(Human, mine.Id);
            _economySystem.TryBuildFirstType(Human, obelisk.Id);
            _humanBuildingsUnlocked.Clear();
            _humanBuildingsUnlocked.Add(farm.Id);

            GameConfig.takeover.Unit warrior = _economySystem.FindUnit(Faction.Khaganate, UnitKind.Infantry, 1);
            GameConfig.takeover.Unit hunter = _economySystem.FindUnit(Faction.Khaganate, UnitKind.Archers, 1);
            if (warrior == null) return;
            int villageCost = farm.BuildCost;
            int mineCost = mine.BuildCost;
            khaganate.Gold = warrior.Cost * 3f + (hunter?.Cost ?? 0) + villageCost + mineCost;
            _economySystem.TryBuildFirstType(khaganate, farm.Id);
            foreach (MapNodeEntity node in khaganate.OwnedNodes.Where(value => value.Building != null))
            {
                if (node.Kind == NodeKind.MageTower)
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

        /// <summary>在当前场景创建食人魔部队。</summary>
        private void SpawnScenarioOgre()
        {
            MapNodeEntity ogreNode = Nodes.FirstOrDefault(node => node.Kind == NodeKind.Ogre);
            if (ogreNode == null) return;
            PlayerStateEntity bandits = _mapSystem.EnsurePlayer(Faction.Bandits);
            GameConfig.takeover.Unit ogre = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Ogre");
            if (ogre != null)
                _economySystem.SpawnSquad(bandits, ogre, ogreNode.Pos, ogreNode, 0, false);
        }

        /// <summary>执行一次以整数秒为间隔的战斗逻辑处理。</summary>
        private void TickLogicalSecond()
        {
            foreach (PlayerStateEntity player in Players)
            {
                player.Economy.AdvanceIncome(ManaCap);
                // 人类玩家默认 AiEnabled = false，托管时要靠 AiControlled 单独放行
                bool aiDriven = player.IsHuman ? player.AiControlled : player.AiEnabled;
                if (aiDriven) _aiSystem.TickAiSecond(player);
            }
        }

        /// <summary>根据累计经验计算部队等级。</summary>
        internal static int LevelForXp(float xp) => xp >= 100f ? 2 : xp >= 25f ? 1 : 0;
        /// <summary>获取部队等级对应的战斗阶级。</summary>
        internal static int LevelTier(SquadEntity squad) => squad.LevelIndex;

        /// <summary>检查战斗结束条件并记录胜负结果。</summary>
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

        /// <summary>判断建筑是否属于关键建筑类型。</summary>
        public static bool IsKeyBuilding(BuildingState building) =>
            building != null && building.Kind >= 1 && building.Kind <= 4;

        /// <summary>判断指定节点所在区域是否由参与方控制。</summary>
        public bool IsLocControlledBy(PlayerStateEntity player, MapNodeEntity spot) =>
            spot != null && IsLocControlled(player, spot.Loc);

        /// <summary>判断指定区域是否由人类玩家控制。</summary>
        internal static bool IsLocControlled(PlayerStateEntity player, int location) =>
            player.OwnedNodes.Any(node => node.Loc == location && node.Building != null &&
                                          node.Building.Owner == player.Faction);

        /// <summary>按标识查找战场中的士兵。</summary>
        internal CombatUnitEntity FindUnitById(int id) =>
            Squads.SelectMany(squad => squad.Units).FirstOrDefault(unit => unit.Id == id && !unit.IsDead);

        /// <summary>判断部队中是否存在机械单位。</summary>
        internal static bool HasMechanism(SquadEntity squad) =>
            (squad.Abilities & AbilityMask.Mechanism) != 0;

        /// <summary>判断部队是否受恐惧控制限制。</summary>
        internal static bool HasFearLock(SquadEntity squad)
        {
            AbilityMask abilities = squad.Abilities;
            return (abilities & AbilityMask.Discipline) != 0 || HasMoraleLock(squad);
        }

        /// <summary>判断部队是否受士气状态限制。</summary>
        internal static bool HasMoraleLock(SquadEntity squad)
        {
            AbilityMask abilities = squad.Abilities;
            return (abilities & (AbilityMask.Mechanism | AbilityMask.Undead)) != 0;
        }

        /// <summary>计算部队用于碰撞和攻击距离判定的单位半径。</summary>
        internal static float UnitRadius(SquadEntity squad)
        {
            return (UnitKind)squad.Def.UnitType == UnitKind.Magic ? 15f :
                (UnitKind)squad.Def.UnitType == UnitKind.Cavalry ? 10f : 8f;
        }

        /// <summary>从配置加载编队坐标并校验索引连续性。</summary>
        private void InitializeFormationLayouts()
        {
            foreach (var group in Cfg.TbFormation.DataList.GroupBy(value => value.MinUnitCount))
            {
                if (group.Key < 1)
                    throw new InvalidOperationException("Formation minimum unit count must be positive.");

                GameConfig.takeover.Formation[] entries = group.OrderBy(value => value.UnitIndex).ToArray();
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].UnitIndex != i)
                        throw new InvalidOperationException(
                            $"Formation {group.Key} has a missing or duplicate unit index.");
                }

                _formationLayouts.Add(group.Key,
                    entries.Select(value => new Vector2(value.OffsetX, value.OffsetY)).ToArray());
            }

            if (_formationLayouts.Count == 0 || _formationLayouts.First().Key != 1)
                throw new InvalidOperationException("A formation layout for one unit is required.");
        }

        /// <summary>根据部队人数和士兵索引计算编队偏移。</summary>
        internal Vector2 FormationOffset(int index, int count, float rotation = 0f)
        {
            Vector2[] formation = _formationLayouts.First().Value;
            foreach (KeyValuePair<int, Vector2[]> layout in _formationLayouts)
            {
                if (layout.Key > count) break;
                formation = layout.Value;
            }

            Vector2 offset = formation[Mathf.Clamp(index, 0, formation.Length - 1)];
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            return new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
        }
    }
}
