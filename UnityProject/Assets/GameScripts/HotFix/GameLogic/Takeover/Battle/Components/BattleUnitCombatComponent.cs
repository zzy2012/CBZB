using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>保存单个士兵的生命、攻击和投射物状态。</summary>
    public sealed class BattleUnitCombatComponent : BattleComponent
    {
        /// <summary>当前生命值。</summary>
        public float Hp;
        /// <summary>当前最大生命值。</summary>
        public float MaxHp;
        /// <summary>未应用临时效果时的基础最大生命值。</summary>
        public float BaseMaxHp;
        /// <summary>攻击计时器。</summary>
        public float AttackTimer;
        /// <summary>当前攻击目标单位的标识。</summary>
        public int TargetUnitId = -1;
        /// <summary>当前攻击目标建筑所在节点的标识。</summary>
        public int TargetBuildingNodeId = -1;
        /// <summary>投射物到达目标前的剩余时间。</summary>
        public float ProjectileRemaining;
        /// <summary>当前投射物命中时造成的伤害。</summary>
        public float ProjectileDamage;
        /// <summary>投射物目标单位的标识。</summary>
        public int ProjectileTargetUnitId = -1;
        /// <summary>投射物目标建筑所在节点的标识。</summary>
        public int ProjectileTargetNodeId = -1;
        /// <summary>投射物的起始位置。</summary>
        public Vector2 ProjectileOrigin;
        /// <summary>投射物的目标位置。</summary>
        public Vector2 ProjectilePoint;
    }
}
