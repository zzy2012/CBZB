using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>战役地图上的节点及其邻接关系、占领状态和建筑状态。</summary>
    public sealed class MapNodeEntity : EntityBase
    {
        /// <summary>关联的战斗模拟或节点状态。</summary>
        public BattleMapNodeComponent State { get; }
        /// <summary>对象的标识。</summary>
        public int Id { get => State.Id; set => State.Id = value; }
        /// <summary>地图节点的配置键。</summary>
        public string Key { get => State.Key; set => State.Key = value; }
        /// <summary>地图节点的类型。</summary>
        public NodeKind Kind { get => State.Kind; set => State.Kind = value; }
        /// <summary>地图节点所属的区域编号。</summary>
        public int Loc { get => State.Loc; set => State.Loc = value; }
        /// <summary>对象的所属方或所有者。</summary>
        public Faction Owner { get => State.Owner; set => State.Owner = value; }
        /// <summary>对象在战场中的位置。</summary>
        public Vector2 Pos { get => State.Position; set => State.Position = value; }
        /// <summary>该节点上的建筑状态。</summary>
        public BuildingState Building
        {
            get => GetComponent<BuildingState>();
            set
            {
                BuildingState previous = Building;
                if (previous == value) return;
                if (previous != null) RemoveComponent(previous);
                if (value == null) return;
                value.Node = this;
                AddComponent(value);
            }
        }
        /// <summary>该节点的宝箱是否已被收集。</summary>
        public bool ChestCollected { get => State.ChestCollected; set => State.ChestCollected = value; }
        /// <summary>与该节点相邻的地图节点。</summary>
        public List<MapNodeEntity> Neighbours => State.Neighbours;
        /// <summary>地图连接边的集合。</summary>
        public List<MapEdge> Edges => State.Edges;

        /// <summary>创建地图节点实体并挂载节点状态组件。</summary>
        public MapNodeEntity()
        {
            State = AddComponent(new BattleMapNodeComponent());
        }

        /// <summary>节点上是否存在建筑。</summary>
        public bool HasBuilding => Building != null;
        /// <summary>判断节点建筑是否存在且属于指定阵营。</summary>
        public bool IsOwnedBy(Faction faction) => Building != null && Building.Owner == faction;
    }
}
