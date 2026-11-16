using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class BattleSquadMovementComponent : BattleComponent
    {
        public MapNode HomeNode;
        public MapNode AtNode;
        public MapNode TargetNode;
        public Vector2 TargetPosition;
        public Vector2 FormationCenter;
        public List<Vector2> Path;
        public int PathIndex;
        public bool HasMoveOrder;
        public float MoveDelaySeconds;

        public bool ConsumeMoveDelay(float deltaSeconds)
        {
            Squad squad = Entity as Squad;
            if (squad == null || squad.MoveDelaySeconds <= 0f) return false;
            squad.MoveDelaySeconds = Mathf.Max(0f, squad.MoveDelaySeconds - deltaSeconds);
            return true;
        }

        public void MoveUnitToward(CombatUnit unit, Vector2 target, float speed, float deltaSeconds)
        {
            if (unit == null || unit.IsDead) return;
            Vector2 delta = target - unit.Pos;
            float distance = delta.magnitude;
            if (distance <= 0.001f) return;
            unit.Movement.MoveToward(target, speed, deltaSeconds);
        }
    }
}
