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

        /// <summary>负责执行玩家命令的模拟系统。</summary>
        internal BattleCommandSystem CommandSystem => _commandSystem;
        private readonly BattleCommandSystem _commandSystem;

        /// <summary>负责读取阵型配置并计算士兵位置的模拟系统。</summary>
        internal BattleFormationSystem FormationSystem => _formationSystem;
        private readonly BattleFormationSystem _formationSystem;

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
            _formationSystem = new BattleFormationSystem(this);
            _commandSystem = new BattleCommandSystem(this);
            LevelAssetId = levelAssetId;
            var scenarioInitializer = new TakeoverScenarioInitializer(this);
            Human = scenarioInitializer.InitializePlayers(humanFaction, enemyFaction, humanMods);
            _mapSystem.BuildMap(levelAssetId);
            scenarioInitializer.Initialize();
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

        /// <summary>按标识查找地图节点，未找到时返回空。</summary>
        public MapNodeEntity FindNode(int id) => Nodes.FirstOrDefault(node => node.Id == id);
        /// <summary>查找指定阵营的参与方，未找到时返回空。</summary>
        public PlayerStateEntity PlayerOf(Faction faction) => Players.FirstOrDefault(player => player.Faction == faction);

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

    }
}
