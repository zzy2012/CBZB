using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>保存单个士兵的移动和行动状态。</summary>
    public sealed class BattleUnitMovementComponent : BattleComponent
    {
        /// <summary>对象在战场或界面中的位置。</summary>
        public Vector2 Position;
        /// <summary>当前行动状态。</summary>
        public UnitAction Action = UnitAction.Stand;
        /// <summary>当前是否可以发动冲锋。</summary>
        public bool ChargeAvailable;
        /// <summary>单位当前是否处于追击状态。</summary>
        public bool Pursuing;
        /// <summary>突进效果的剩余时间，单位为秒。</summary>
        public float DashSeconds;
        /// <summary>冲锋加速效果的剩余时间，单位为秒。</summary>
        public float ChargeSpeedSeconds;

        /// <summary>按指定速度和时间增量向目标位置移动。</summary>
        public void MoveToward(Vector2 target, float speed, float deltaSeconds)
        {
            CombatUnitEntity unit = Entity as CombatUnitEntity;
            if (unit == null || unit.IsDead) return;
            Vector2 delta = target - unit.Pos;
            float distance = delta.magnitude;
            if (distance <= 0.001f) return;
            unit.Pos += delta / distance * Mathf.Min(distance, speed * deltaSeconds);
        }
    }
}
