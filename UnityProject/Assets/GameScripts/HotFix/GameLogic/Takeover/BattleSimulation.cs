using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>玩家可提交给战斗模拟器处理的操作类型。</summary>
    public enum BattleCommandType
    {
        /// <summary>移动部队。</summary>
        Move,
        /// <summary>招募单位。</summary>
        BuyUnit,
        /// <summary>升级建筑。</summary>
        Upgrade,
        /// <summary>建造建筑。</summary>
        Build,
        /// <summary>施放法术。</summary>
        CastSpell,
        /// <summary>通过调试命令增减资源。</summary>
        GrantResources,
    }

    /// <summary>
    /// 模拟器接收的输入命令。表现层必须提交命令，不得直接修改 TakeoverScene 状态。
    /// </summary>
    [Serializable]
    public struct BattleCommand
    {
        /// <summary>战斗命令的类型。</summary>
        public BattleCommandType Type;
        /// <summary>命令涉及的部队标识。</summary>
        public int SquadId;
        /// <summary>命令涉及的部队标识集合。</summary>
        public int[] SquadIds;
        /// <summary>关联的地图节点标识。</summary>
        public int NodeId;
        /// <summary>命令使用的配置项标识。</summary>
        public int ConfigId;
        /// <summary>命令对金币的增减量。</summary>
        public int GoldDelta;
        /// <summary>命令对魔法值的增减量。</summary>
        public int ManaDelta;
        /// <summary>移动或法术命令的目标位置。</summary>
        public Vector2 Position;

        /// <summary>创建将部队移动到目标节点或位置的命令。</summary>
        public static BattleCommand Move(int squadId, int nodeId) =>
            new BattleCommand
            {
                Type = BattleCommandType.Move,
                SquadId = squadId,
                SquadIds = new[] { squadId },
                NodeId = nodeId,
            };

        /// <summary>创建将部队移动到目标节点或位置的命令。</summary>
        public static BattleCommand Move(IEnumerable<int> squadIds, Vector2 position, int nodeId = 0) =>
            new BattleCommand
            {
                Type = BattleCommandType.Move,
                SquadIds = squadIds?.ToArray() ?? Array.Empty<int>(),
                Position = position,
                NodeId = nodeId,
            };

        /// <summary>创建在指定节点招募单位的命令。</summary>
        public static BattleCommand BuyUnit(int unitId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.BuyUnit, ConfigId = unitId, NodeId = nodeId };

        /// <summary>创建升级指定节点建筑的命令。</summary>
        public static BattleCommand Upgrade(int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Upgrade, NodeId = nodeId };

        /// <summary>创建在指定节点建造建筑的命令。</summary>
        public static BattleCommand Build(int buildingId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Build, ConfigId = buildingId, NodeId = nodeId };

        /// <summary>创建在指定节点或位置施放法术的命令。</summary>
        public static BattleCommand CastSpell(int spellId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.CastSpell, ConfigId = spellId, NodeId = nodeId };

        /// <summary>创建在指定节点或位置施放法术的命令。</summary>
        public static BattleCommand CastSpell(int spellId, Vector2 position, int nodeId = 0) =>
            new BattleCommand
            {
                Type = BattleCommandType.CastSpell,
                ConfigId = spellId,
                Position = position,
                NodeId = nodeId,
            };

        /// <summary>仅供游戏内 GM 控制使用的调试资源发放命令。</summary>
        public static BattleCommand GrantResources(int goldDelta, int manaDelta) =>
            new BattleCommand
            {
                Type = BattleCommandType.GrantResources,
                GoldDelta = goldDelta,
                ManaDelta = manaDelta,
            };
    }

    /// <summary>模拟器向表现层发布的战斗事件数据。</summary>
    [Serializable]
    public struct BattleEvent
    {
        /// <summary>战斗事件的类型标识。</summary>
        public string Kind;
        /// <summary>事件发起者的标识。</summary>
        public int ActorId;
        /// <summary>事件目标的标识。</summary>
        public int TargetId;
        /// <summary>事件或修正项对应的数值。</summary>
        public float Value;
        /// <summary>战斗事件发生的位置。</summary>
        public Vector2 Position;
        /// <summary>可选的表现文案（例如法术效果说明），由模拟器在触发时填好。</summary>
        public string Text;
    }

    /// <summary>供表现层读取的部队状态快照，不暴露模拟器内部对象。</summary>
    [Serializable]
    public sealed class SquadSnapshot
    {
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>所属阵营。</summary>
        public Faction Faction;
        /// <summary>单位配置标识。</summary>
        public int UnitId;
        /// <summary>部队对应单位的显示名称。</summary>
        public string Name;
        /// <summary>存活士兵数量。</summary>
        public int AliveSoldiers;
        /// <summary>部队的士兵总数，包含已死亡士兵。</summary>
        public int MaxSoldiers;
        /// <summary>当前生命值。</summary>
        public float Hp;
        /// <summary>当前最大生命值。</summary>
        public float MaxHp;
        /// <summary>对象在战场或界面中的位置。</summary>
        public Vector2 Position;
        /// <summary>关联的地图节点标识。</summary>
        public int NodeId;
        /// <summary>目标地图节点的标识。</summary>
        public int TargetNodeId;
        /// <summary>部队的驻守节点是否仍由本阵营占有。</summary>
        public bool IsGarrison;
        /// <summary>当前是否处于冰冻状态。</summary>
        public bool Frozen;
        /// <summary>当前是否处于中毒状态。</summary>
        public bool Poisoned;
        /// <summary>当前等级。</summary>
        public int Level;
        /// <summary>部队中的士兵状态快照。</summary>
        public List<UnitSnapshot> Units = new List<UnitSnapshot>();
    }

    /// <summary>供表现层读取的单个士兵状态快照。</summary>
    [Serializable]
    public sealed class UnitSnapshot
    {
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>对象在战场或界面中的位置。</summary>
        public Vector2 Position;
        /// <summary>当前生命值。</summary>
        public float Hp;
        /// <summary>当前最大生命值。</summary>
        public float MaxHp;
        /// <summary>当前行动状态。</summary>
        public UnitAction Action;
        /// <summary>当前士气状态。</summary>
        public MoraleState Morale;
        /// <summary>当前是否处于冰冻状态。</summary>
        public bool Frozen;
        /// <summary>当前是否处于中毒状态。</summary>
        public bool Poisoned;
    }

    /// <summary>供表现层读取的参与方资源、阵营和可用法术快照。</summary>
    [Serializable]
    public sealed class PlayerSnapshot
    {
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>所属阵营。</summary>
        public Faction Faction;
        /// <summary>该参与方是否为人类玩家。</summary>
        public bool IsHuman;
        /// <summary>当前金币数量。</summary>
        public float Gold;
        /// <summary>当前魔法值。</summary>
        public float Mana;
        /// <summary>当前终极技能能量。</summary>
        public float Ultimate;
        /// <summary>当前部队占用的补给数量。</summary>
        public int SupplyUsed;
        /// <summary>参与方的补给容量上限。</summary>
        public int SupplyCap;
        /// <summary>当前可用的法术配置标识集合。</summary>
        public List<int> AvailableSpellIds = new List<int>();
    }

    /// <summary>供表现层读取的战场法术实例快照。</summary>
    [Serializable]
    public sealed class SpellSnapshot
    {
        /// <summary>法术配置标识。</summary>
        public int SpellId;
        /// <summary>法术施放者的参与方标识。</summary>
        public int CasterPlayerId;
        /// <summary>所属阵营。</summary>
        public Faction Faction;
        /// <summary>对象在战场或界面中的位置。</summary>
        public Vector2 Position;
        /// <summary>法术效果的剩余持续时间。</summary>
        public float Remaining;
        /// <summary>法术效果的运行时类别。</summary>
        public SpellEffectKind Effect;
    }

    /// <summary>供表现层读取的建筑占领、等级和耐久快照。</summary>
    [Serializable]
    public sealed class BuildingSnapshot
    {
        /// <summary>关联的地图节点标识。</summary>
        public int NodeId;
        /// <summary>建筑配置标识。</summary>
        public int BuildingId;
        /// <summary>对象的所属方或所有者。</summary>
        public Faction Owner;
        /// <summary>当前等级。</summary>
        public int Level;
        /// <summary>当前生命值。</summary>
        public int Hp;
        /// <summary>当前最大生命值。</summary>
        public int MaxHp;
        /// <summary>建筑的占领进度。</summary>
        public float CaptureProgress;
    }

    /// <summary>战斗完整只读视图数据，聚合双方资源、部队、建筑和法术快照。</summary>
    [Serializable]
    public sealed class BattleSnapshot
    {
        /// <summary>战斗已运行的时间，单位为秒。</summary>
        public float Time;
        /// <summary>战斗是否已经结束。</summary>
        public bool IsOver;
        /// <summary>战斗是否由人类玩家获胜。</summary>
        public bool HumanVictory;
        /// <summary>按照战斗耗时计算的通关星级。</summary>
        public int Stars;
        /// <summary>当前金币数量。</summary>
        public float Gold;
        /// <summary>当前魔法值。</summary>
        public float Mana;
        /// <summary>当前终极技能能量。</summary>
        public float Ultimate;
        /// <summary>当前部队占用的补给数量。</summary>
        public int SupplyUsed;
        /// <summary>参与方的补给容量上限。</summary>
        public int SupplyCap;
        /// <summary>存活部队的状态快照集合。</summary>
        public List<SquadSnapshot> Squads = new List<SquadSnapshot>();
        /// <summary>战场建筑的状态快照集合。</summary>
        public List<BuildingSnapshot> Buildings = new List<BuildingSnapshot>();
        /// <summary>战斗参与方的状态快照集合。</summary>
        public List<PlayerSnapshot> Players = new List<PlayerSnapshot>();
        /// <summary>当前生效法术的状态快照集合。</summary>
        public List<SpellSnapshot> ActiveSpells = new List<SpellSnapshot>();

        /// <summary>从当前模拟状态构建战斗快照。</summary>
        public static BattleSnapshot From(TakeoverScene sim)
        {
            var snapshot = new BattleSnapshot
            {
                Time = sim.Time,
                IsOver = sim.IsOver,
                HumanVictory = sim.HumanVictory,
                Stars = sim.Stars,
                Gold = sim.Human.Gold,
                Mana = sim.Human.Mana,
                Ultimate = sim.Human.Ultimate,
                SupplyUsed = sim.Human.SupplyUsed,
                SupplyCap = sim.Human.SupplyCap,
            };

            foreach (var squad in sim.Squads.Where(s => !s.IsDead))
            {
                snapshot.Squads.Add(new SquadSnapshot
                {
                    Id = squad.Id,
                    Faction = squad.Faction,
                    UnitId = squad.Def.Id,
                    Name = squad.Def.CnName,
                    AliveSoldiers = squad.AliveSoldiers,
                    MaxSoldiers = squad.MaxSoldiers,
                    Hp = squad.Hp,
                    MaxHp = squad.Units.Sum(unit => unit.MaxHp),
                    Position = squad.Pos,
                    NodeId = squad.AtNode?.Id ?? 0,
                    TargetNodeId = squad.TargetNode?.Id ?? 0,
                    IsGarrison = squad.IsGarrison,
                    Frozen = squad.Units.Any(unit => unit.FrozenSeconds > 0f),
                    Poisoned = squad.Units.Any(unit => unit.PoisonSeconds > 0f),
                    Level = squad.LevelIndex,
                    Units = squad.Units.Select(unit => new UnitSnapshot
                    {
                        Id = unit.Id,
                        Position = unit.Pos,
                        Hp = unit.Hp,
                        MaxHp = unit.MaxHp,
                        Action = unit.Action,
                        Morale = unit.MoraleState,
                        Frozen = unit.FrozenSeconds > 0f,
                        Poisoned = unit.PoisonSeconds > 0f,
                    }).ToList(),
                });
            }

            foreach (PlayerStateEntity player in sim.Players)
            {
                snapshot.Players.Add(new PlayerSnapshot
                {
                    Id = player.Id,
                    Faction = player.Faction,
                    IsHuman = player.IsHuman,
                    Gold = player.Gold,
                    Mana = player.Mana,
                    Ultimate = player.Ultimate,
                    SupplyUsed = player.SupplyUsed,
                    SupplyCap = player.SupplyCap,
                    AvailableSpellIds = sim.SpellSystem.AvailableSpells(player)
                        .Select(spell => spell.Id).ToList(),
                });
            }

            foreach (var node in sim.Nodes.Where(n => n.Building != null))
            {
                snapshot.Buildings.Add(new BuildingSnapshot
                {
                    NodeId = node.Id,
                    BuildingId = node.Building.Kind,
                    Owner = node.Building.Owner,
                    Level = node.Building.Level,
                    Hp = node.Building.Hp,
                    MaxHp = node.Building.MaxHp,
                    CaptureProgress = node.Building.CaptureProgress,
                });
            }

            foreach (ActiveSpellEffectEntity effect in sim.ActiveSpells)
            {
                snapshot.ActiveSpells.Add(new SpellSnapshot
                {
                    SpellId = effect.Def.Id,
                    CasterPlayerId = effect.Owner?.Id ?? -1,
                    Faction = effect.Faction,
                    Position = effect.Center,
                    Remaining = effect.Remaining,
                    Effect = effect.Kind,
                });
            }

            return snapshot;
        }
    }

    /// <summary>
    /// 供 Unity 表现层使用的定步长模拟器，场景数据通过 State 直接读取。
    /// </summary>
    public sealed class BattleSimulation
    {
        /// <summary>等待固定模拟步处理的战斗命令队列。</summary>
        private readonly Queue<BattleCommand> _commands = new Queue<BattleCommand>();
        /// <summary>负责推进底层战斗场景的管理器。</summary>
        private readonly BattleManager _battleManager = new BattleManager();

        /// <summary>关联的战斗场景状态。</summary>
        public TakeoverScene State { get; }

        /// <summary>按关卡、双方阵营、修正项和随机种子创建战斗模拟。</summary>
        public BattleSimulation(int levelAssetId, Faction humanFaction, Faction enemyFaction,
            int[] humanMods = null, int seed = 20120920)
        {
            State = new TakeoverScene(levelAssetId, humanFaction, enemyFaction, humanMods, seed);
            _battleManager.CreateBattle(new TakeoverBattle(State));
        }

        /// <summary>将命令加入队列，等待固定步执行。</summary>
        public void Enqueue(BattleCommand command) => _commands.Enqueue(command);

        /// <summary>按实际时间推进固定步战斗模拟。</summary>
        public void Tick(float realDeltaSeconds) =>
            _battleManager.Update(realDeltaSeconds, ExecuteQueuedCommands);

        /// <summary>按队列顺序处理等待执行的玩家命令。</summary>
        private void ExecuteQueuedCommands()
        {
            while (_commands.Count > 0)
                State.ExecuteCommand(State.Human, _commands.Dequeue());
        }

        /// <summary>销毁当前战斗并释放模拟管理器。</summary>
        public void Dispose() => _battleManager.Dispose();
    }
}
