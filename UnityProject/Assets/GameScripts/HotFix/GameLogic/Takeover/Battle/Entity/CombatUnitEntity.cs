using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>参与战斗的单个士兵及其生命、目标、状态效果和攻击数据。</summary>
    public sealed class CombatUnitEntity : EntityBase
    {
        /// <summary>实体的移动状态组件。</summary>
        public BattleUnitMovementComponent Movement { get; }
        /// <summary>士兵的状态效果组件。</summary>
        public BattleUnitStatusComponent Status { get; }
        /// <summary>士兵的战斗状态组件。</summary>
        public BattleUnitCombatComponent Combat { get; }
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>士兵在所属部队中的索引。</summary>
        public int Index;
        /// <summary>所属或绑定的部队。</summary>
        public SquadEntity Squad;
        /// <summary>对象在战场中的位置。</summary>
        public Vector2 Pos { get => Movement.Position; set => Movement.Position = value; }
        /// <summary>当前生命值。</summary>
        public float Hp { get => Combat.Hp; set => Combat.Hp = value; }
        /// <summary>当前最大生命值。</summary>
        public float MaxHp { get => Combat.MaxHp; set => Combat.MaxHp = value; }
        /// <summary>未应用临时效果时的基础最大生命值。</summary>
        public float BaseMaxHp { get => Combat.BaseMaxHp; set => Combat.BaseMaxHp = value; }
        /// <summary>攻击计时器。</summary>
        public float AttackTimer { get => Combat.AttackTimer; set => Combat.AttackTimer = value; }
        /// <summary>当前攻击目标单位的标识。</summary>
        public int TargetUnitId { get => Combat.TargetUnitId; set => Combat.TargetUnitId = value; }
        /// <summary>当前攻击目标建筑所在节点的标识。</summary>
        public int TargetBuildingNodeId { get => Combat.TargetBuildingNodeId; set => Combat.TargetBuildingNodeId = value; }
        /// <summary>当前行动状态。</summary>
        public UnitAction Action { get => Movement.Action; set => Movement.Action = value; }
        /// <summary>当前士气状态。</summary>
        public MoraleState MoraleState { get => Status.MoraleState; set => Status.MoraleState = value; }
        /// <summary>当前士气值。</summary>
        public float MoraleCurrent { get => Status.MoraleCurrent; set => Status.MoraleCurrent = value; }
        /// <summary>基础士气值。</summary>
        public float MoraleBase { get => Status.MoraleBase; set => Status.MoraleBase = value; }
        /// <summary>英勇效果的剩余时间，单位为秒。</summary>
        public float HeroicSeconds { get => Status.HeroicSeconds; set => Status.HeroicSeconds = value; }
        /// <summary>恐惧效果的剩余时间，单位为秒。</summary>
        public float FearedSeconds { get => Status.FearedSeconds; set => Status.FearedSeconds = value; }
        /// <summary>冰冻效果的剩余时间，单位为秒。</summary>
        public float FrozenSeconds { get => Status.FrozenSeconds; set => Status.FrozenSeconds = value; }
        /// <summary>中毒效果的剩余时间，单位为秒。</summary>
        public float PoisonSeconds { get => Status.PoisonSeconds; set => Status.PoisonSeconds = value; }
        /// <summary>中毒伤害的结算计时器。</summary>
        public float PoisonTick { get => Status.PoisonTick; set => Status.PoisonTick = value; }
        /// <summary>中毒效果每次结算的伤害值。</summary>
        public float PoisonDamage { get => Status.PoisonDamage; set => Status.PoisonDamage = value; }
        /// <summary>伤害抵抗效果的剩余时间，单位为秒。</summary>
        public float DamageResistSeconds { get => Status.DamageResistSeconds; set => Status.DamageResistSeconds = value; }
        /// <summary>当前伤害抵抗数值。</summary>
        public float DamageResist { get => Status.DamageResist; set => Status.DamageResist = value; }
        /// <summary>伤害增益的剩余时间，单位为秒。</summary>
        public float DamageBuffSeconds { get => Status.DamageBuffSeconds; set => Status.DamageBuffSeconds = value; }
        /// <summary>伤害增益的百分比数值。</summary>
        public float DamageBuffPercent { get => Status.DamageBuffPercent; set => Status.DamageBuffPercent = value; }
        /// <summary>最大伤害效果的剩余时间，单位为秒。</summary>
        public float MaxDamageSeconds { get => Status.MaxDamageSeconds; set => Status.MaxDamageSeconds = value; }
        /// <summary>魔法免疫效果的剩余时间，单位为秒。</summary>
        public float MagicImmunitySeconds { get => Status.MagicImmunitySeconds; set => Status.MagicImmunitySeconds = value; }
        /// <summary>当前是否可以发动冲锋。</summary>
        public bool ChargeAvailable { get => Movement.ChargeAvailable; set => Movement.ChargeAvailable = value; }
        /// <summary>单位当前是否处于追击状态。</summary>
        public bool Pursuing { get => Movement.Pursuing; set => Movement.Pursuing = value; }
        /// <summary>突进效果的剩余时间，单位为秒。</summary>
        public float DashSeconds { get => Movement.DashSeconds; set => Movement.DashSeconds = value; }
        /// <summary>冲锋加速效果的剩余时间，单位为秒。</summary>
        public float ChargeSpeedSeconds { get => Movement.ChargeSpeedSeconds; set => Movement.ChargeSpeedSeconds = value; }
        /// <summary>减速效果的剩余时间，单位为秒。</summary>
        public float SlowSeconds { get => Status.SlowSeconds; set => Status.SlowSeconds = value; }
        /// <summary>投射物目标单位的标识。</summary>
        public int ProjectileTargetUnitId { get => Combat.ProjectileTargetUnitId; set => Combat.ProjectileTargetUnitId = value; }
        /// <summary>投射物目标建筑所在节点的标识。</summary>
        public int ProjectileTargetNodeId { get => Combat.ProjectileTargetNodeId; set => Combat.ProjectileTargetNodeId = value; }
        /// <summary>投射物到达目标前的剩余时间。</summary>
        public float ProjectileRemaining { get => Combat.ProjectileRemaining; set => Combat.ProjectileRemaining = value; }
        /// <summary>当前投射物命中时造成的伤害。</summary>
        public float ProjectileDamage { get => Combat.ProjectileDamage; set => Combat.ProjectileDamage = value; }
        /// <summary>投射物的起始位置。</summary>
        public Vector2 ProjectileOrigin { get => Combat.ProjectileOrigin; set => Combat.ProjectileOrigin = value; }
        /// <summary>投射物的目标位置。</summary>
        public Vector2 ProjectilePoint { get => Combat.ProjectilePoint; set => Combat.ProjectilePoint = value; }

        /// <summary>创建士兵实体并挂载移动、状态和战斗组件。</summary>
        public CombatUnitEntity()
        {
            Movement = AddComponent(new BattleUnitMovementComponent());
            Status = AddComponent(new BattleUnitStatusComponent());
            Combat = AddComponent(new BattleUnitCombatComponent());
        }

        /// <summary>单位或部队是否已死亡。</summary>
        public bool IsDead => Hp <= 0f;
        /// <summary>当前生命值占最大生命值的比例；最大值不大于零时为零。</summary>
        public float HpRatio => MaxHp <= 0f ? 0f : Hp / MaxHp;
    }
}
