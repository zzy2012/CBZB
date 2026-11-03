using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>玩家可提交给战斗模拟器处理的操作类型。</summary>
    public enum BattleCommandType
    {
        Move,
        BuyUnit,
        Upgrade,
        Build,
        CastSpell,
        GrantResources,
    }

    /// <summary>
    /// 模拟器接收的输入命令。表现层必须提交命令，不得直接修改 BattleSim 状态。
    /// </summary>
    [Serializable]
    public struct BattleCommand
    {
        public BattleCommandType Type;
        public int SquadId;
        public int[] SquadIds;
        public int NodeId;
        public int ConfigId;
        public int GoldDelta;
        public int ManaDelta;
        public Vector2 Position;

        public static BattleCommand Move(int squadId, int nodeId) =>
            new BattleCommand
            {
                Type = BattleCommandType.Move,
                SquadId = squadId,
                SquadIds = new[] { squadId },
                NodeId = nodeId,
            };

        public static BattleCommand Move(IEnumerable<int> squadIds, Vector2 position, int nodeId = 0) =>
            new BattleCommand
            {
                Type = BattleCommandType.Move,
                SquadIds = squadIds?.ToArray() ?? Array.Empty<int>(),
                Position = position,
                NodeId = nodeId,
            };

        public static BattleCommand BuyUnit(int unitId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.BuyUnit, ConfigId = unitId, NodeId = nodeId };

        public static BattleCommand Upgrade(int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Upgrade, NodeId = nodeId };

        public static BattleCommand Build(int buildingId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Build, ConfigId = buildingId, NodeId = nodeId };

        public static BattleCommand CastSpell(int spellId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.CastSpell, ConfigId = spellId, NodeId = nodeId };

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
        public string Kind;
        public int ActorId;
        public int TargetId;
        public float Value;
        public Vector2 Position;
        /// <summary>可选的表现文案（例如法术效果说明），由模拟器在触发时填好。</summary>
        public string Text;
    }

    /// <summary>供表现层读取的部队状态快照，不暴露模拟器内部对象。</summary>
    [Serializable]
    public sealed class SquadSnapshot
    {
        public int Id;
        public Faction Faction;
        public int UnitId;
        public string Name;
        public int AliveSoldiers;
        public int MaxSoldiers;
        public float Hp;
        public float MaxHp;
        public Vector2 Position;
        public int NodeId;
        public int TargetNodeId;
        public bool IsGarrison;
        public bool Frozen;
        public bool Poisoned;
        public int Level;
        public List<UnitSnapshot> Units = new List<UnitSnapshot>();
    }

    /// <summary>供表现层读取的单个士兵状态快照。</summary>
    [Serializable]
    public sealed class UnitSnapshot
    {
        public int Id;
        public Vector2 Position;
        public float Hp;
        public float MaxHp;
        public UnitAction Action;
        public MoraleState Morale;
        public bool Frozen;
        public bool Poisoned;
    }

    /// <summary>供表现层读取的参与方资源、阵营和可用法术快照。</summary>
    [Serializable]
    public sealed class PlayerSnapshot
    {
        public int Id;
        public Faction Faction;
        public bool IsHuman;
        public float Gold;
        public float Mana;
        public float Ultimate;
        public int SupplyUsed;
        public int SupplyCap;
        public List<int> AvailableSpellIds = new List<int>();
    }

    /// <summary>供表现层读取的战场法术实例快照。</summary>
    [Serializable]
    public sealed class SpellSnapshot
    {
        public int SpellId;
        public int CasterPlayerId;
        public Faction Faction;
        public Vector2 Position;
        public float Remaining;
        public SpellEffectKind Effect;
    }

    /// <summary>供表现层读取的建筑占领、等级和耐久快照。</summary>
    [Serializable]
    public sealed class BuildingSnapshot
    {
        public int NodeId;
        public int BuildingId;
        public Faction Owner;
        public int Level;
        public int Hp;
        public int MaxHp;
        public float CaptureProgress;
    }

    /// <summary>战斗完整只读视图数据，聚合双方资源、部队、建筑和法术快照。</summary>
    [Serializable]
    public sealed class BattleSnapshot
    {
        public float Time;
        public bool IsOver;
        public bool HumanVictory;
        public int Stars;
        public float Gold;
        public float Mana;
        public float Ultimate;
        public int SupplyUsed;
        public int SupplyCap;
        public List<SquadSnapshot> Squads = new List<SquadSnapshot>();
        public List<BuildingSnapshot> Buildings = new List<BuildingSnapshot>();
        public List<PlayerSnapshot> Players = new List<PlayerSnapshot>();
        public List<SpellSnapshot> ActiveSpells = new List<SpellSnapshot>();

        public static BattleSnapshot From(BattleSim sim)
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

            foreach (PlayerState player in sim.Players)
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
                    AvailableSpellIds = sim.AvailableSpells(player).Select(spell => spell.Id).ToList(),
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

            foreach (ActiveSpellEffect effect in sim.ActiveSpells)
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
    /// 供 Unity 表现层使用的定步长模拟器门面。
    /// 表现层通过读取快照渲染，不直接推进战斗逻辑。
    /// </summary>
    public sealed class BattleSimulation
    {
        public const float FixedStep = 1f / 30f;
        private readonly Queue<BattleCommand> _commands = new Queue<BattleCommand>();
        private float _accumulator;

        public BattleSim State { get; }
        public IReadOnlyList<MapNode> Nodes => State.Nodes;
        public IReadOnlyList<Squad> Squads => State.Squads;
        public IReadOnlyList<PlayerState> Players => State.Players;
        public IReadOnlyList<MapEdge> Edges => State.Edges;
        public PlayerState Human => State.Human;
        public bool IsOver => State.IsOver;
        public bool HumanVictory => State.HumanVictory;
        public float Time => State.Time;
        public int Stars => State.Stars;
        public int LevelAssetId => State.LevelAssetId;

        public BattleSimulation(int levelAssetId, Faction humanFaction, Faction enemyFaction,
            int[] humanMods = null, int seed = 20120920)
        {
            State = new BattleSim(levelAssetId, humanFaction, enemyFaction, humanMods, seed);
        }

        public void Enqueue(BattleCommand command) => _commands.Enqueue(command);

        /// <summary>GM 的「AI 托管」开关打开时，立刻让 AI 跑一拍，玩家马上能看到它开始买兵/建造。</summary>
        public void ForceAiTick(PlayerState player)
        {
            if (player == null) return;
            State.AiSystem.TickAiSecond(player);
        }

        public bool ExecuteNow(BattleCommand command) => State.ExecuteCommand(State.Human, command);

        /// <summary>单次 Tick 允许推进的最大模拟时间（秒）：只防卡死，不再限制倍速。</summary>
        private const float MaxTickSeconds = 40f;

        /// <summary>单次 Tick 的硬步数上限，防止极端卡顿后一次跑几千步卡住主线程。</summary>
        private const int MaxStepsPerTick = 1500;

        public void Tick(float realDeltaSeconds)
        {
            if (realDeltaSeconds <= 0f) return;
            // 之前这里用 Mathf.Min(realDeltaSeconds, 0.25f) 和 guard < 20，
            // 等于把倍速硬顶在 ~15 倍（0.25s × 帧率），所以 ×64 和 ×256 手感一样。
            float budget = Mathf.Min(realDeltaSeconds, MaxTickSeconds);
            _accumulator += budget;
            int maxSteps = Mathf.Clamp(Mathf.CeilToInt(budget / FixedStep) + 2, 1, MaxStepsPerTick);
            int guard = 0;
            while (_accumulator >= FixedStep && guard++ < maxSteps)
            {
                while (_commands.Count > 0)
                {
                    State.ExecuteCommand(State.Human, _commands.Dequeue());
                }
                State.Tick(FixedStep);
                _accumulator -= FixedStep;
            }
            // 撞到步数上限时丢掉积压，避免下一帧继续追赶
            if (guard >= maxSteps) _accumulator = 0f;
        }

        public IReadOnlyList<BattleEvent> DrainEvents() => State.DrainEvents();
    }
}
