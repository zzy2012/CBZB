using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>由同类士兵组成的部队，负责承载编队、移动、归属和成长状态。</summary>
    public sealed class SquadEntity : EntityBase
    {
        /// <summary>实体的移动状态组件。</summary>
        public BattleSquadMovementComponent Movement { get; }
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>对象的所属方或所有者。</summary>
        public PlayerStateEntity Owner;
        /// <summary>对应的配置定义。</summary>
        public GameConfig.takeover.Unit Def;
        /// <summary>单位拥有的战斗能力标记。</summary>
        public AbilityMask Abilities;
        /// <summary>单位的战斗表现和能力参数。</summary>
        public UnitCombatProfile Profile;
        /// <summary>累计经验值。</summary>
        public float Xp;
        /// <summary>部队当前等级的索引。</summary>
        public int LevelIndex;
        /// <summary>部队所属的驻守节点。</summary>
        public MapNodeEntity HomeNode { get => Movement.HomeNode; set => Movement.HomeNode = value; }
        /// <summary>部队当前所在的地图节点。</summary>
        public MapNodeEntity AtNode { get => Movement.AtNode; set => Movement.AtNode = value; }
        /// <summary>部队移动的目标节点。</summary>
        public MapNodeEntity TargetNode { get => Movement.TargetNode; set => Movement.TargetNode = value; }
        /// <summary>部队移动的目标位置。</summary>
        public Vector2 TargetPosition { get => Movement.TargetPosition; set => Movement.TargetPosition = value; }
        /// <summary>部队编队的中心位置。</summary>
        public Vector2 FormationCenter { get => Movement.FormationCenter; set => Movement.FormationCenter = value; }
        /// <summary>部队移动路径的坐标点。</summary>
        public List<Vector2> Path { get => Movement.Path; set => Movement.Path = value; }
        /// <summary>部队当前处理的路径点索引。</summary>
        public int PathIndex { get => Movement.PathIndex; set => Movement.PathIndex = value; }
        /// <summary>部队当前是否有移动命令。</summary>
        public bool HasMoveOrder { get => Movement.HasMoveOrder; set => Movement.HasMoveOrder = value; }
        /// <summary>部队移动延迟的剩余时间，单位为秒。</summary>
        public float MoveDelaySeconds { get => Movement.MoveDelaySeconds; set => Movement.MoveDelaySeconds = value; }
        /// <summary>部队的驻守节点是否仍由本阵营占有。</summary>
        public bool IsGarrison => HomeNode?.Building != null && HomeNode.Building.Owner == Faction;
        /// <summary>部队中的士兵集合。</summary>
        public readonly List<CombatUnitEntity> Units = new List<CombatUnitEntity>();

        /// <summary>创建部队实体并挂载移动组件。</summary>
        public SquadEntity()
        {
            Movement = AddComponent(new BattleSquadMovementComponent());
        }

        /// <summary>所属阵营。</summary>
        public Faction Faction => Owner.Faction;
        /// <summary>部队的士兵总数，包含已死亡士兵。</summary>
        public int MaxSoldiers => Units.Count;
        /// <summary>存活士兵数量。</summary>
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
        /// <summary>部队所有士兵的非负生命值之和。</summary>
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
        /// <summary>部队中单个士兵的最大生命值；没有士兵时为零。</summary>
        public float HpPerSoldier => Units.Count == 0 ? 0f : Units[0].MaxHp;
        /// <summary>存活士兵的平均位置；没有存活士兵时返回目标位置。</summary>
        public Vector2 Pos
        {
            get
            {
                Vector2 sum = Vector2.zero;
                int alive = 0;
                for (int i = 0; i < Units.Count; i++)
                {
                    CombatUnitEntity unit = Units[i];
                    if (unit.IsDead) continue;
                    sum += unit.Pos;
                    alive++;
                }
                return alive == 0 ? TargetPosition : sum / alive;
            }
        }
        /// <summary>单位或部队是否已死亡。</summary>
        public bool IsDead => AliveSoldiers == 0;
        /// <summary>根据单位配置和参与方修正计算的部队移动速度。</summary>
        public float Speed => Mathf.Max(1f, Def.Speed * Owner.ModMul(Def.UnitType == (int)UnitKind.Siege ? 27 : 19) * 0.5f);
    }
}
