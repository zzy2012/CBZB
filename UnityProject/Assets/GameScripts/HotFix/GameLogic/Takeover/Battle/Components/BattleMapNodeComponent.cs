using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class BattleMapNodeComponent : BattleComponent
    {
        public int Id;
        public string Key;
        public NodeKind Kind;
        public int Loc;
        public Faction Owner;
        public Vector2 Position;
        public bool ChestCollected;
        public readonly List<MapNode> Neighbours = new List<MapNode>();
        public readonly List<MapEdge> Edges = new List<MapEdge>();
    }
}
