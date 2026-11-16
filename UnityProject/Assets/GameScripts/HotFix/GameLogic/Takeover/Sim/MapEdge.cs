using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>连接两个地图节点并保存行军路径点的道路。</summary>
    public sealed class MapEdge
    {
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>地图边的一端节点。</summary>
        public MapNodeEntity NodeA;
        /// <summary>地图边的另一端节点。</summary>
        public MapNodeEntity NodeB;
        /// <summary>地图边的路径坐标点。</summary>
        public readonly List<Vector2> Waypoints = new List<Vector2>();

        /// <summary>地图边沿途经点的路径总长度。</summary>
        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Waypoints.Count; i++)
                    length += Vector2.Distance(Waypoints[i - 1], Waypoints[i]);
                return length;
            }
        }
    }
}
