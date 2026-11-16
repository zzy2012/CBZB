using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>保存部队的移动命令、路径和编队位置。</summary>
    public sealed class BattleSquadMovementComponent : BattleComponent
    {
        /// <summary>部队所属的驻守节点。</summary>
        public MapNodeEntity HomeNode;
        /// <summary>部队当前所在的地图节点。</summary>
        public MapNodeEntity AtNode;
        /// <summary>部队移动的目标节点。</summary>
        public MapNodeEntity TargetNode;
        /// <summary>部队移动的目标位置。</summary>
        public Vector2 TargetPosition;
        /// <summary>部队编队的中心位置。</summary>
        public Vector2 FormationCenter;
        /// <summary>部队移动路径的坐标点。</summary>
        public List<Vector2> Path;
        /// <summary>部队当前处理的路径点索引。</summary>
        public int PathIndex;
        /// <summary>部队当前是否有移动命令。</summary>
        public bool HasMoveOrder;
        /// <summary>部队移动延迟的剩余时间，单位为秒。</summary>
        public float MoveDelaySeconds;

        /// <summary>消耗移动延迟并返回该次更新是否仍受延迟影响。</summary>
        public bool ConsumeMoveDelay(float deltaSeconds)
        {
            SquadEntity squad = Entity as SquadEntity;
            if (squad == null || squad.MoveDelaySeconds <= 0f) return false;
            squad.MoveDelaySeconds = Mathf.Max(0f, squad.MoveDelaySeconds - deltaSeconds);
            return true;
        }

        /// <summary>按指定速度和时间增量将士兵向目标位置移动。</summary>
        public void MoveUnitToward(CombatUnitEntity unit, Vector2 target, float speed, float deltaSeconds)
        {
            if (unit == null || unit.IsDead) return;
            Vector2 delta = target - unit.Pos;
            float distance = delta.magnitude;
            if (distance <= 0.001f) return;
            unit.Movement.MoveToward(target, speed, deltaSeconds);
        }
    }
}
