using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>Raw 1.0.4 faction indexes. Do not reorder.</summary>
    public enum Faction
    {
        Empire = 0,
        Westaria = 1,
        Khaganate = 2,
        Cult = 3,
        Bandits = 4,
    }

    public enum UnitKind
    {
        Infantry = 0,
        Archers = 1,
        Cavalry = 2,
        Siege = 3,
        Magic = 4,
    }

    public enum NodeKind
    {
        Empty = 0,
        BuildSpot = 1,
        Chest = 2,
        Ogre = 3,
        Castle = 4,
        Town = 5,
        Camp = 6,
        MageTower = 7,
    }

    public enum UnitAction
    {
        Stand,
        Moving,
        Melee,
        Ranged,
        Dead,
        Sleeping,
        Repairing,
    }

    public enum MoraleState
    {
        Feared = -1,
        Neutral = 0,
        Heroic = 1,
        High = 2,
        Low = 3,
    }

    [Flags]
    public enum AbilityMask
    {
        None = 0,
        ShieldWall = 1 << 0,
        Charge = 1 << 1,
        SplashDamage = 1 << 2,
        SiegeWeapon = 1 << 3,
        DivinePresence = 1 << 4,
        Poisonous = 1 << 5,
        Intimidiation = 1 << 6,
        Discipline = 1 << 7,
        Mechanism = 1 << 8,
        MartialArtists = 1 << 9,
        Undead = 1 << 10,
        HorrificDash = 1 << 11,
        PoisonImmunity = 1 << 12,
        LongSpear = 1 << 13,
        Explosive = 1 << 14,
        FreezeArrow = 1 << 15,
        FreezeBlast = 1 << 16,
    }

    public sealed class MapEdge
    {
        public int Id;
        public MapNode NodeA;
        public MapNode NodeB;
        public readonly List<Vector2> Waypoints = new List<Vector2>();

        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Waypoints.Count; i++)
                    length += Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
                return length;
            }
        }
    }

    public sealed class MapNode
    {
        public int Id;
        public string Key;
        public NodeKind Kind;
        public int Loc;
        public Faction Owner;
        public Vector2 Pos;
        public BuildingState Building;
        public bool ChestCollected;
        public readonly List<MapNode> Neighbours = new List<MapNode>();
        public readonly List<MapEdge> Edges = new List<MapEdge>();

        public bool HasBuilding => Building != null;
        public bool IsOwnedBy(Faction faction) => Building != null && Building.Owner == faction;
    }

    public sealed class BuildingState
    {
        public const int MaxLevel = 2;
        public const int DefenderCatchDefault = 1;

        public MapNode Node;
        public Faction Owner;
        public int Kind;
        public int Level;
        public int BaseHp;
        public int Hp;
        public int GoldPerSec;
        public int ManaPerSec;
        public int Supply;
        public int UltimatePerSec;
        public int Catch = DefenderCatchDefault;
        public float CaptureProgress;
        public int LastAttackerUnitId = -1;
        public float RepairProgress;

        public int MaxHp => Mathf.RoundToInt(BaseHp * (1f + 0.5f * Level));
        public bool IsCaptured => Hp <= 0;
        public bool IsActive => Hp > 0;
    }

    public sealed class CombatUnit
    {
        public int Id;
        public int Index;
        public Squad Squad;
        public Vector2 Pos;
        public float Hp;
        public float MaxHp;
        public float BaseMaxHp;
        public float AttackTimer;
        public int TargetUnitId = -1;
        public int TargetBuildingNodeId = -1;
        public UnitAction Action = UnitAction.Stand;
        public MoraleState MoraleState = MoraleState.Neutral;
        public float MoraleCurrent;
        public float MoraleBase;
        public float HeroicSeconds;
        public float FearedSeconds;
        public float FrozenSeconds;
        public float PoisonSeconds;
        public float PoisonTick;
        public float PoisonDamage;
        public float DamageResistSeconds;
        public float DamageResist;
        public float DamageBuffSeconds;
        public float DamageBuffPercent;
        public float MaxDamageSeconds;
        public float MagicImmunitySeconds;
        public bool ChargeAvailable;
        public bool Pursuing;
        public float DashSeconds;
        public float ChargeSpeedSeconds;
        public float SlowSeconds;
        public int ProjectileTargetUnitId = -1;
        public int ProjectileTargetNodeId = -1;
        public float ProjectileRemaining;
        public float ProjectileDamage;
        public Vector2 ProjectileOrigin;
        public Vector2 ProjectilePoint;

        public bool IsDead => Hp <= 0f;
        public float HpRatio => MaxHp <= 0f ? 0f : Hp / MaxHp;
    }

    public sealed class Squad
    {
        public int Id;
        public PlayerState Owner;
        public GameConfig.takeover.Unit Def;
        public AbilityMask Abilities;
        public UnitCombatProfile Profile;
        public float Xp;
        public int LevelIndex;
        public MapNode HomeNode;
        public MapNode AtNode;
        public MapNode TargetNode;
        public Vector2 TargetPosition;
        public Vector2 FormationCenter;
        public List<Vector2> Path;
        public int PathIndex;
        public bool HasMoveOrder;
        public float MoveDelaySeconds;
        public bool IsGarrison => HomeNode?.Building != null && HomeNode.Building.Owner == Faction;
        public bool Retreating => false;
        public readonly List<CombatUnit> Units = new List<CombatUnit>();
        public List<CombatUnit> SoldierUnits => Units;

        public Faction Faction => Owner.Faction;
        public int MaxSoldiers => Units.Count;
        public int AliveSoldiers
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Units.Count; i++)
                    if (!Units[i].IsDead) count++;
                return count;
            }
        }
        public int Soldiers
        {
            get => AliveSoldiers;
            set { }
        }
        public float Hp
        {
            get
            {
                float hp = 0f;
                for (int i = 0; i < Units.Count; i++)
                    hp += Mathf.Max(0f, Units[i].Hp);
                return hp;
            }
        }
        public float HpPerSoldier => Units.Count == 0 ? 0f : Units[0].MaxHp;
        public Vector2 Pos
        {
            get
            {
                Vector2 sum = Vector2.zero;
                int alive = 0;
                for (int i = 0; i < Units.Count; i++)
                {
                    CombatUnit unit = Units[i];
                    if (unit.IsDead) continue;
                    sum += unit.Pos;
                    alive++;
                }
                return alive == 0 ? TargetPosition : sum / alive;
            }
        }
        public bool IsDead => AliveSoldiers == 0;
        public float Speed => Mathf.Max(1f, Def.Speed * Owner.ModMul(Def.UnitType == (int)UnitKind.Siege ? 27 : 19) * 0.5f);
    }

    public sealed class PlayerState
    {
        public int Id;
        public string Name;
        public Faction Faction;
        public bool IsHuman;
        public bool AiEnabled = true;
        public bool SpellsEnabled = true;
        public bool UltimateForced;
        public int UnitTypeLimit = 4;
        public float Gold;
        public float Mana;
        public float Ultimate;
        public float UltimateMax = 200f;
        public float SpellCooldown;
        public readonly int[] Mods = new int[45];
        public readonly List<MapNode> OwnedNodes = new List<MapNode>();
        public readonly List<Squad> Squads = new List<Squad>();

        public float ModMul(int id) => (100 + Mods[Mathf.Clamp(id, 0, Mods.Length - 1)]) / 100f;

        public int ActiveBuildings => OwnedNodes.Count(node =>
            node.Building != null && node.Building.IsActive && BattleSim.IsKeyBuilding(node.Building));
        public int SupplyCap => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.Supply);
        public int SupplyUsed => Squads.Where(squad => !squad.IsDead).Sum(squad => squad.Def.Upkeep);
        public int FreeSupply => SupplyCap - SupplyUsed;
        public int BaseGoldIncome => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.GoldPerSec);
        public int ManaIncome => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.ManaPerSec);
        public int UltimateRate => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.UltimatePerSec);
        public int LivingSquads => Squads.Count(squad => !squad.IsDead);

        public int NetGoldIncome()
        {
            int income = BaseGoldIncome;
            int squads = LivingSquads;
            if (income == 0 || squads == 0) return income;
            int squadsForPenalty = squads >= 10 ? 10 : squads;
            int modifier = 30 + (squadsForPenalty - 1) * 5;
            return Mathf.Max(1, Mathf.RoundToInt(income * (100 - modifier) / 100f));
        }
    }

    public enum SpellEffectKind
    {
        Consecration,
        Heroism,
        Bloodlust,
        Liberation,
        Desecration,
        FrostEnchantment,
        Stoneworms,
        IceBlast,
        Cataclysm,
    }

    public sealed class ActiveSpellEffect
    {
        public GameConfig.takeover.Spell Def;
        public PlayerState Owner;
        public Faction Faction;
        public Vector2 Center;
        public float Remaining;
        public float TickTimer;
        public SpellEffectKind Kind;
        public int RandomSeed;
        public int ImpactIndex;
    }

    public sealed class EnemyPlayerSetup
    {
        public Faction Faction;
        public int StartGold;
        public int StartMana;
        public bool AiEnabled = true;
        public readonly List<ModifierValue> Modifiers = new List<ModifierValue>();
    }

    public sealed class UnitCombatProfile
    {
        public float AnimationSpeed;
        public int ProjectileHeight;
        public int ShowProjectileRadius;
        public float BaseWalkSpeed;
        public float ProjectileTravel;
        public AbilityMask Abilities;
    }
}
