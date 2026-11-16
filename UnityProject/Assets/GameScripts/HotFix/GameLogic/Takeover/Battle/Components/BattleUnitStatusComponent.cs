using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>保存单个士兵的士气和持续状态效果。</summary>
    public sealed class BattleUnitStatusComponent : BattleComponent
    {
        /// <summary>当前士气状态。</summary>
        public MoraleState MoraleState = MoraleState.Neutral;
        /// <summary>当前士气值。</summary>
        public float MoraleCurrent;
        /// <summary>基础士气值。</summary>
        public float MoraleBase;
        /// <summary>英勇效果的剩余时间，单位为秒。</summary>
        public float HeroicSeconds;
        /// <summary>恐惧效果的剩余时间，单位为秒。</summary>
        public float FearedSeconds;
        /// <summary>冰冻效果的剩余时间，单位为秒。</summary>
        public float FrozenSeconds;
        /// <summary>中毒效果的剩余时间，单位为秒。</summary>
        public float PoisonSeconds;
        /// <summary>中毒伤害的结算计时器。</summary>
        public float PoisonTick;
        /// <summary>中毒效果每次结算的伤害值。</summary>
        public float PoisonDamage;
        /// <summary>伤害抵抗效果的剩余时间，单位为秒。</summary>
        public float DamageResistSeconds;
        /// <summary>当前伤害抵抗数值。</summary>
        public float DamageResist;
        /// <summary>伤害增益的剩余时间，单位为秒。</summary>
        public float DamageBuffSeconds;
        /// <summary>伤害增益的百分比数值。</summary>
        public float DamageBuffPercent;
        /// <summary>最大伤害效果的剩余时间，单位为秒。</summary>
        public float MaxDamageSeconds;
        /// <summary>魔法免疫效果的剩余时间，单位为秒。</summary>
        public float MagicImmunitySeconds;
        /// <summary>减速效果的剩余时间，单位为秒。</summary>
        public float SlowSeconds;

        /// <summary>按时间增量更新士兵持续状态并结算相关战斗效果。</summary>
        internal void Tick(float deltaSeconds, BattleCombatSystem combatSystem)
        {
            CombatUnitEntity unit = Entity as CombatUnitEntity;
            if (unit == null) return;

            FrozenSeconds = Mathf.Max(0f, FrozenSeconds - deltaSeconds);
            unit.DashSeconds = Mathf.Max(0f, unit.DashSeconds - deltaSeconds);
            unit.ChargeSpeedSeconds = Mathf.Max(0f, unit.ChargeSpeedSeconds - deltaSeconds);
            SlowSeconds = Mathf.Max(0f, SlowSeconds - deltaSeconds);
            DamageResistSeconds = Mathf.Max(0f, DamageResistSeconds - deltaSeconds);
            DamageBuffSeconds = Mathf.Max(0f, DamageBuffSeconds - deltaSeconds);
            MaxDamageSeconds = Mathf.Max(0f, MaxDamageSeconds - deltaSeconds);
            MagicImmunitySeconds = Mathf.Max(0f, MagicImmunitySeconds - deltaSeconds);
            HeroicSeconds = Mathf.Max(0f, HeroicSeconds - deltaSeconds);
            FearedSeconds = Mathf.Max(0f, FearedSeconds - deltaSeconds);
            if (HeroicSeconds > 0f) MoraleState = MoraleState.Heroic;
            else if (FearedSeconds > 0f) MoraleState = MoraleState.Feared;
            else if (MoraleCurrent <= MoraleBase * 0.35f) MoraleState = MoraleState.Low;
            else if (MoraleCurrent >= MoraleBase * 0.65f) MoraleState = MoraleState.High;
            else MoraleState = MoraleState.Neutral;

            if (PoisonSeconds <= 0f) return;
            PoisonSeconds = Mathf.Max(0f, PoisonSeconds - deltaSeconds);
            PoisonTick -= deltaSeconds;
            if (PoisonTick <= 0f)
            {
                PoisonTick = 3f;
                combatSystem.ApplyDamage(unit, PoisonDamage, null, false);
            }
        }
    }
}
