using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>原版 1.0.4 阵营索引，禁止调整顺序。</summary>
    public enum Faction
    {
        Empire = 0,
        Westaria = 1,
        Khaganate = 2,
        Cult = 3,
        Bandits = 4,
    }

    /// <summary>兵种的战斗分类，用于匹配单位属性与战斗规则。</summary>
    public enum UnitKind
    {
        Infantry = 0,
        Archers = 1,
        Cavalry = 2,
        Siege = 3,
        Magic = 4,
    }

    /// <summary>战场地图节点的地形或建筑点类型。</summary>
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

    /// <summary>战斗单位当前执行的行动状态。</summary>
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

    /// <summary>战斗单位的士气等级。</summary>
    public enum MoraleState
    {
        Feared = -1,
        Neutral = 0,
        Heroic = 1,
        High = 2,
        Low = 3,
    }

    /// <summary>单位或小队拥有的原版战斗能力标记，可按位组合。</summary>
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

    /// <summary>连接两个地图节点并保存行军路径点的道路。</summary>
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

    /// <summary>战役地图上的节点及其邻接关系、占领状态和建筑状态。</summary>
    public sealed class MapNode : BattleEntity
    {
        public BattleMapNodeComponent State { get; }
        public int Id { get => State.Id; set => State.Id = value; }
        public string Key { get => State.Key; set => State.Key = value; }
        public NodeKind Kind { get => State.Kind; set => State.Kind = value; }
        public int Loc { get => State.Loc; set => State.Loc = value; }
        public Faction Owner { get => State.Owner; set => State.Owner = value; }
        public Vector2 Pos { get => State.Position; set => State.Position = value; }
        public BuildingState Building
        {
            get => GetComponent<BuildingState>();
            set
            {
                BuildingState previous = Building;
                if (previous == value) return;
                if (previous != null) RemoveComponent(previous);
                if (value == null) return;
                value.Node = this;
                AddComponent(value);
            }
        }
        public bool ChestCollected { get => State.ChestCollected; set => State.ChestCollected = value; }
        public List<MapNode> Neighbours => State.Neighbours;
        public List<MapEdge> Edges => State.Edges;

        public MapNode()
        {
            State = AddComponent(new BattleMapNodeComponent());
        }

        public bool HasBuilding => Building != null;
        public bool IsOwnedBy(Faction faction) => Building != null && Building.Owner == faction;
    }

    /// <summary>地图节点上建筑的所有权、耐久、生产和占领进度。</summary>
    public sealed class BuildingState : BattleComponent
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

    /// <summary>参与战斗的单个士兵及其生命、目标、状态效果和攻击数据。</summary>
    public sealed class CombatUnit : BattleEntity
    {
        public BattleUnitMovementComponent Movement { get; }
        public BattleUnitStatusComponent Status { get; }
        public BattleUnitCombatComponent Combat { get; }
        public int Id;
        public int Index;
        public Squad Squad;
        public Vector2 Pos { get => Movement.Position; set => Movement.Position = value; }
        public float Hp { get => Combat.Hp; set => Combat.Hp = value; }
        public float MaxHp { get => Combat.MaxHp; set => Combat.MaxHp = value; }
        public float BaseMaxHp { get => Combat.BaseMaxHp; set => Combat.BaseMaxHp = value; }
        public float AttackTimer { get => Combat.AttackTimer; set => Combat.AttackTimer = value; }
        public int TargetUnitId { get => Combat.TargetUnitId; set => Combat.TargetUnitId = value; }
        public int TargetBuildingNodeId { get => Combat.TargetBuildingNodeId; set => Combat.TargetBuildingNodeId = value; }
        public UnitAction Action { get => Movement.Action; set => Movement.Action = value; }
        public MoraleState MoraleState { get => Status.MoraleState; set => Status.MoraleState = value; }
        public float MoraleCurrent { get => Status.MoraleCurrent; set => Status.MoraleCurrent = value; }
        public float MoraleBase { get => Status.MoraleBase; set => Status.MoraleBase = value; }
        public float HeroicSeconds { get => Status.HeroicSeconds; set => Status.HeroicSeconds = value; }
        public float FearedSeconds { get => Status.FearedSeconds; set => Status.FearedSeconds = value; }
        public float FrozenSeconds { get => Status.FrozenSeconds; set => Status.FrozenSeconds = value; }
        public float PoisonSeconds { get => Status.PoisonSeconds; set => Status.PoisonSeconds = value; }
        public float PoisonTick { get => Status.PoisonTick; set => Status.PoisonTick = value; }
        public float PoisonDamage { get => Status.PoisonDamage; set => Status.PoisonDamage = value; }
        public float DamageResistSeconds { get => Status.DamageResistSeconds; set => Status.DamageResistSeconds = value; }
        public float DamageResist { get => Status.DamageResist; set => Status.DamageResist = value; }
        public float DamageBuffSeconds { get => Status.DamageBuffSeconds; set => Status.DamageBuffSeconds = value; }
        public float DamageBuffPercent { get => Status.DamageBuffPercent; set => Status.DamageBuffPercent = value; }
        public float MaxDamageSeconds { get => Status.MaxDamageSeconds; set => Status.MaxDamageSeconds = value; }
        public float MagicImmunitySeconds { get => Status.MagicImmunitySeconds; set => Status.MagicImmunitySeconds = value; }
        public bool ChargeAvailable { get => Movement.ChargeAvailable; set => Movement.ChargeAvailable = value; }
        public bool Pursuing { get => Movement.Pursuing; set => Movement.Pursuing = value; }
        public float DashSeconds { get => Movement.DashSeconds; set => Movement.DashSeconds = value; }
        public float ChargeSpeedSeconds { get => Movement.ChargeSpeedSeconds; set => Movement.ChargeSpeedSeconds = value; }
        public float SlowSeconds { get => Status.SlowSeconds; set => Status.SlowSeconds = value; }
        public int ProjectileTargetUnitId { get => Combat.ProjectileTargetUnitId; set => Combat.ProjectileTargetUnitId = value; }
        public int ProjectileTargetNodeId { get => Combat.ProjectileTargetNodeId; set => Combat.ProjectileTargetNodeId = value; }
        public float ProjectileRemaining { get => Combat.ProjectileRemaining; set => Combat.ProjectileRemaining = value; }
        public float ProjectileDamage { get => Combat.ProjectileDamage; set => Combat.ProjectileDamage = value; }
        public Vector2 ProjectileOrigin { get => Combat.ProjectileOrigin; set => Combat.ProjectileOrigin = value; }
        public Vector2 ProjectilePoint { get => Combat.ProjectilePoint; set => Combat.ProjectilePoint = value; }

        public CombatUnit()
        {
            Movement = AddComponent(new BattleUnitMovementComponent());
            Status = AddComponent(new BattleUnitStatusComponent());
            Combat = AddComponent(new BattleUnitCombatComponent());
        }

        public bool IsDead => Hp <= 0f;
        public float HpRatio => MaxHp <= 0f ? 0f : Hp / MaxHp;
    }

    /// <summary>由同类士兵组成的部队，负责承载编队、移动、归属和成长状态。</summary>
    public sealed class Squad : BattleEntity
    {
        public BattleSquadMovementComponent Movement { get; }
        public int Id;
        public PlayerState Owner;
        public GameConfig.takeover.Unit Def;
        public AbilityMask Abilities;
        public UnitCombatProfile Profile;
        public float Xp;
        public int LevelIndex;
        public MapNode HomeNode { get => Movement.HomeNode; set => Movement.HomeNode = value; }
        public MapNode AtNode { get => Movement.AtNode; set => Movement.AtNode = value; }
        public MapNode TargetNode { get => Movement.TargetNode; set => Movement.TargetNode = value; }
        public Vector2 TargetPosition { get => Movement.TargetPosition; set => Movement.TargetPosition = value; }
        public Vector2 FormationCenter { get => Movement.FormationCenter; set => Movement.FormationCenter = value; }
        public List<Vector2> Path { get => Movement.Path; set => Movement.Path = value; }
        public int PathIndex { get => Movement.PathIndex; set => Movement.PathIndex = value; }
        public bool HasMoveOrder { get => Movement.HasMoveOrder; set => Movement.HasMoveOrder = value; }
        public float MoveDelaySeconds { get => Movement.MoveDelaySeconds; set => Movement.MoveDelaySeconds = value; }
        public bool IsGarrison => HomeNode?.Building != null && HomeNode.Building.Owner == Faction;
        public readonly List<CombatUnit> Units = new List<CombatUnit>();

        public Squad()
        {
            Movement = AddComponent(new BattleSquadMovementComponent());
        }

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

    /// <summary>战斗参与方的资源、阵营、领地、部队和战斗加成状态。</summary>
    public sealed class PlayerState : BattleEntity
    {
        public BattlePlayerEconomyComponent Economy { get; }
        public int Id;
        public string Name;
        public Faction Faction;
        public bool IsHuman;
        public bool AiEnabled = true;
        /// <summary>由 GM 开关控制：为 true 时玩家也交给 AI 托管（人类玩家默认 false）。</summary>
        public bool AiControlled;
        public bool SpellsEnabled = true;
        public int UnitTypeLimit = 4;
        public float Gold { get => Economy.Gold; set => Economy.Gold = value; }
        public float Mana { get => Economy.Mana; set => Economy.Mana = value; }
        public float Ultimate { get => Economy.Ultimate; set => Economy.Ultimate = value; }
        public float UltimateMax { get => Economy.UltimateMax; set => Economy.UltimateMax = value; }
        public float SpellCooldown { get => Economy.SpellCooldown; set => Economy.SpellCooldown = value; }
        public readonly int[] Mods = new int[45];
        public readonly List<MapNode> OwnedNodes = new List<MapNode>();
        public readonly List<Squad> Squads = new List<Squad>();

        public PlayerState()
        {
            Economy = AddComponent(new BattlePlayerEconomyComponent());
        }

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

    /// <summary>法术效果的运行时类别。</summary>
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

    /// <summary>战场上持续生效的法术实例及其范围、剩余时间和触发状态。</summary>
    public sealed class ActiveSpellEffect : BattleEntity
    {
        public BattleSpellEffectComponent Runtime { get; }
        public GameConfig.takeover.Spell Def { get => Runtime.Def; set => Runtime.Def = value; }
        public PlayerState Owner { get => Runtime.Owner; set => Runtime.Owner = value; }
        public Faction Faction { get => Runtime.Faction; set => Runtime.Faction = value; }
        public Vector2 Center { get => Runtime.Center; set => Runtime.Center = value; }
        public float Remaining { get => Runtime.Remaining; set => Runtime.Remaining = value; }
        public float TickTimer { get => Runtime.TickTimer; set => Runtime.TickTimer = value; }
        public SpellEffectKind Kind { get => Runtime.Kind; set => Runtime.Kind = value; }
        public int RandomSeed { get => Runtime.RandomSeed; set => Runtime.RandomSeed = value; }
        public int ImpactIndex { get => Runtime.ImpactIndex; set => Runtime.ImpactIndex = value; }

        public ActiveSpellEffect()
        {
            Runtime = AddComponent(new BattleSpellEffectComponent());
        }
    }

    /// <summary>关卡中敌方参与者的阵营、初始资源、AI 开关和修正项配置。</summary>
    public sealed class EnemyPlayerSetup
    {
        public Faction Faction;
        public int StartGold;
        public int StartMana;
        public bool AiEnabled = true;
        public readonly List<ModifierValue> Modifiers = new List<ModifierValue>();
    }

    /// <summary>单位动画、移动、投射物表现及战斗能力参数。</summary>
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
