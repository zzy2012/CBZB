using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>保存地图节点的归属、位置和连接状态。</summary>
    public sealed class BattleMapNodeComponent : BattleComponent
    {
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>地图节点的配置键。</summary>
        public string Key;
        /// <summary>地图节点的类型。</summary>
        public NodeKind Kind;
        /// <summary>地图节点所属的区域编号。</summary>
        public int Loc;
        /// <summary>对象的所属方或所有者。</summary>
        public Faction Owner;
        /// <summary>对象在战场或界面中的位置。</summary>
        public Vector2 Position;
        /// <summary>该节点的宝箱是否已被收集。</summary>
        public bool ChestCollected;
        /// <summary>与该节点相邻的地图节点。</summary>
        public readonly List<MapNodeEntity> Neighbours = new List<MapNodeEntity>();
        /// <summary>地图连接边的集合。</summary>
        public readonly List<MapEdge> Edges = new List<MapEdge>();
    }
}
