using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
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
    /// Input accepted by the simulation. View code must submit commands instead of
    /// mutating BattleSim state directly.
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

        /// <summary>Debug-only resource grant used by the in-game GM controls.</summary>
        public static BattleCommand GrantResources(int goldDelta, int manaDelta) =>
            new BattleCommand
            {
                Type = BattleCommandType.GrantResources,
                GoldDelta = goldDelta,
                ManaDelta = manaDelta,
            };
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
        public int Level;
        public List<UnitSnapshot> Units = new List<UnitSnapshot>();
    }

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
                    Retreating = squad.Retreating,
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
        public IReadOnlyList<PlayerState> Players => State.Players;
        public IReadOnlyList<MapEdge> Edges => State.Edges;
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
