using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class BattleUnitCombatComponent : BattleComponent
    {
        public float Hp;
        public float MaxHp;
        public float BaseMaxHp;
        public float AttackTimer;
        public int TargetUnitId = -1;
        public int TargetBuildingNodeId = -1;
        public float ProjectileRemaining;
        public float ProjectileDamage;
        public int ProjectileTargetUnitId = -1;
        public int ProjectileTargetNodeId = -1;
        public Vector2 ProjectileOrigin;
        public Vector2 ProjectilePoint;
    }
}
