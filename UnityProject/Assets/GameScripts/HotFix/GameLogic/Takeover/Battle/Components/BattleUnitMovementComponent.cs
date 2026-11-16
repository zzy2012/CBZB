using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class BattleUnitMovementComponent : BattleComponent
    {
        public Vector2 Position;
        public UnitAction Action = UnitAction.Stand;
        public bool ChargeAvailable;
        public bool Pursuing;
        public float DashSeconds;
        public float ChargeSpeedSeconds;

        public void MoveToward(Vector2 target, float speed, float deltaSeconds)
        {
            CombatUnit unit = Entity as CombatUnit;
            if (unit == null || unit.IsDead) return;
            Vector2 delta = target - unit.Pos;
            float distance = delta.magnitude;
            if (distance <= 0.001f) return;
            unit.Pos += delta / distance * Mathf.Min(distance, speed * deltaSeconds);
        }
    }
}
