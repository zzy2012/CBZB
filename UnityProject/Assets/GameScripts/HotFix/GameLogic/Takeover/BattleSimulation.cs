using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    public enum BattleCommandType
    {
        Move,
        Attack,
        Retreat,
        BuyUnit,
        Upgrade,
        Build,
        CastSpell,
    }

    /// <summary>
    /// Input accepted by the simulation. View code must submit commands instead of
    /// mutating BattleSim state directly.
    /// </summary>
    [Serializable]
    public struct BattleCommand
    {
        public BattleCommandType Type;
        public int SquadId;
        public int NodeId;
        public int ConfigId;

        public static BattleCommand Move(int squadId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Move, SquadId = squadId, NodeId = nodeId };

        public static BattleCommand Attack(int squadId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Attack, SquadId = squadId, NodeId = nodeId };

        public static BattleCommand Retreat(int squadId) =>
            new BattleCommand { Type = BattleCommandType.Retreat, SquadId = squadId };

        public static BattleCommand BuyUnit(int unitId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.BuyUnit, ConfigId = unitId, NodeId = nodeId };

        public static BattleCommand Upgrade(int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Upgrade, NodeId = nodeId };

        public static BattleCommand Build(int buildingId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.Build, ConfigId = buildingId, NodeId = nodeId };

        public static BattleCommand CastSpell(int spellId, int nodeId) =>
            new BattleCommand { Type = BattleCommandType.CastSpell, ConfigId = spellId, NodeId = nodeId };
    }

    [Serializable]
    public struct BattleResult
    {
        public bool IsOver;
        public bool HumanVictory;
        public float ElapsedSeconds;
        public int Stars;

        public static BattleResult From(BattleSim sim) =>
            new BattleResult
            {
                IsOver = sim.IsOver,
                HumanVictory = sim.HumanVictory,
                ElapsedSeconds = sim.Time,
                Stars = sim.Stars,
            };
    }

    [Serializable]
    public struct BattleEvent
    {
        public string Kind;
        public int ActorId;
        public int TargetId;
        public float Value;
        public Vector2 Position;
    }

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
        public bool Retreating;
        public bool Frozen;
        public bool Poisoned;
    }

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
                    MaxHp = squad.HpPerSoldier * squad.MaxSoldiers,
                    Position = squad.Pos,
                    NodeId = squad.AtNode?.Id ?? 0,
                    TargetNodeId = squad.TargetNode?.Id ?? 0,
                    IsGarrison = squad.IsGarrison,
                    Retreating = squad.Retreating,
                    Frozen = squad.FrozenSeconds > 0f,
                    Poisoned = squad.PoisonSeconds > 0f,
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

            return snapshot;
        }
    }

    /// <summary>
    /// Fixed-step simulation facade used by the Unity view. Rendering samples
    /// snapshots and never advances combat directly.
    /// </summary>
    public sealed class BattleSimulation
    {
        public const float FixedStep = 1f / 30f;
        private readonly Queue<BattleCommand> _commands = new Queue<BattleCommand>();
        private float _accumulator;

        public BattleSim State { get; }
        public BattleResult Result => BattleResult.From(State);
        public IReadOnlyList<MapNode> Nodes => State.Nodes;
        public IReadOnlyList<Squad> Squads => State.Squads;
        public PlayerState Human => State.Human;
        public PlayerState Enemy => State.Enemy;
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

        public bool ExecuteNow(BattleCommand command) => State.ExecuteCommand(State.Human, command);

        public void Tick(float realDeltaSeconds)
        {
            if (realDeltaSeconds <= 0f) return;
            _accumulator += Mathf.Min(realDeltaSeconds, 0.25f);
            int guard = 0;
            while (_accumulator >= FixedStep && guard++ < 20)
            {
                while (_commands.Count > 0)
                {
                    State.ExecuteCommand(State.Human, _commands.Dequeue());
                }
                State.Tick(FixedStep);
                _accumulator -= FixedStep;
            }
        }

        public BattleSnapshot Snapshot() => BattleSnapshot.From(State);

        public IReadOnlyList<BattleEvent> DrainEvents() => State.DrainEvents();
    }
}
