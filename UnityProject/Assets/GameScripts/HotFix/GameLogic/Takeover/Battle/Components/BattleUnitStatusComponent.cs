using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class BattleUnitStatusComponent : BattleComponent
    {
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
        public float SlowSeconds;

        internal void Tick(float deltaSeconds, BattleCombatSystem combatSystem)
        {
            CombatUnit unit = Entity as CombatUnit;
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
