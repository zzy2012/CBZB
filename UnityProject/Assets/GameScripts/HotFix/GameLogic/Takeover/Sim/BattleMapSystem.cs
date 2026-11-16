using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>处理战场地图、寻路和移动命令。</summary>
    internal sealed class BattleMapSystem : BattleSimSystemBase
    {
        /// <summary>创建并关联当前战斗模拟的地图系统。</summary>
        internal BattleMapSystem(BattleSim sim) : base(sim) { }

        /// <summary>依据关卡配置创建地图节点、连接边和建筑状态。</summary>
        internal void BuildMap(int levelAssetId)
        {
            foreach (GameConfig.takeover.MapNode config in Cfg.TbMapNode.DataList.Where(value => value.LevelId == levelAssetId))
            {
                Faction owner = OriginalRules.FromConfigFaction(config.Owner);
                var node = new MapNodeEntity
                {
                    Id = config.Id,
                    Key = config.NodeKey,
                    Kind = (NodeKind)config.Kind,
                    Loc = config.Loc,
                    Owner = owner,
                    Pos = new Vector2(config.X, config.Y * OriginalRules.WorldYScale),
                };
                if (node.Kind >= NodeKind.Castle && node.Kind <= NodeKind.MageTower)
                {
                    PlayerStateEntity player = EnsurePlayer(owner);
                    var buildingDef = Cfg.BuildingByKind((int)node.Kind);
                    if (buildingDef != null)
                    {
                        int towerBonus = buildingDef.Id == 4 ? player?.Mods[38] ?? 0 : 0;
                        node.Building = new BuildingState
                        {
                            Node = node,
                            Owner = owner,
                            Kind = buildingDef.Id,
                            BaseHp = Mathf.RoundToInt(buildingDef.Hp * (player?.ModMul(11) ?? 1f)),
                            GoldPerSec = buildingDef.Gold + towerBonus,
                            ManaPerSec = buildingDef.Mana + towerBonus,
                            Supply = buildingDef.Supply + towerBonus,
                            UltimatePerSec = buildingDef.Ultimate,
                        };
                        node.Building.Hp = node.Building.MaxHp;
                    }
                }
                Sim.Nodes.Add(node);
                Sim.AddEntity(BattleEntityType.MapNode, node);
                Sim.NodeByKey[node.Key] = node;
            }

            foreach (GameConfig.takeover.MapEdge config in Cfg.TbMapEdge.DataList.Where(value => value.LevelId == levelAssetId))
            {
                if (!Sim.NodeByKey.TryGetValue(config.NodeA, out MapNodeEntity nodeA) ||
                    !Sim.NodeByKey.TryGetValue(config.NodeB, out MapNodeEntity nodeB))
                    continue;
                var edge = new MapEdge
                {
                    Id = config.Id,
                    NodeA = nodeA,
                    NodeB = nodeB,
                };
                edge.Waypoints.Add(nodeA.Pos);
                edge.Waypoints.AddRange(OriginalMapWaypoints.For(levelAssetId, config.NodeA, config.NodeB));
                edge.Waypoints.Add(nodeB.Pos);
                nodeA.Neighbours.Add(nodeB);
                nodeB.Neighbours.Add(nodeA);
                nodeA.Edges.Add(edge);
                nodeB.Edges.Add(edge);
                Sim.Edges.Add(edge);
            }
        }

        /// <summary>确保地图配置中的参与方已加入当前模拟。</summary>
        internal PlayerStateEntity EnsurePlayer(Faction faction)
        {
            PlayerStateEntity existing = Sim.PlayerOf(faction);
            if (existing != null) return existing;
            var player = new PlayerStateEntity
            {
                Id = Sim.Players.Count,
                Name = faction.ToString(),
                Faction = faction,
                AiEnabled = false,
            };
            Sim.Players.Add(player);
            Sim.AddEntity(BattleEntityType.Player, player);
            return player;
        }

        /// <summary>查找最近的敌方控制地图节点。</summary>
        internal MapNodeEntity FindNearestHostileNode(MapNodeEntity from, PlayerStateEntity player)
        {
            if (from == null) return null;
            return Sim.Nodes.Where(node => BattleSim.IsKeyBuilding(node.Building) && node.Building.Owner != player.Faction)
                .OrderBy(node => ShortestDistance(from, node))
                .FirstOrDefault();
        }

        /// <summary>计算两个地图节点之间的最短路径距离。</summary>
        internal float ShortestDistance(MapNodeEntity from, MapNodeEntity to)
        {
            if (from == null || to == null) return float.MaxValue;
            var distances = new Dictionary<MapNodeEntity, float>();
            var queue = new List<MapNodeEntity> { from };
            distances[from] = 0f;
            while (queue.Count > 0)
            {
                MapNodeEntity current = queue.OrderBy(node => distances[node]).First();
                queue.Remove(current);
                if (current == to) return distances[current];
                foreach (MapEdge edge in current.Edges)
                {
                    MapNodeEntity next = edge.NodeA == current ? edge.NodeB : edge.NodeA;
                    float candidate = distances[current] + Mathf.Max(1f, edge.Length);
                    if (distances.TryGetValue(next, out float existing) && existing <= candidate) continue;
                    distances[next] = candidate;
                    if (!queue.Contains(next)) queue.Add(next);
                }
            }
            return float.MaxValue;
        }

        /// <summary>为指定部队设置目标节点或目标位置。</summary>
        internal void OrderMove(SquadEntity squad, MapNodeEntity targetNode)
        {
            OrderMove(squad, targetNode?.Pos ?? squad.Pos, targetNode);
        }

        /// <summary>为指定部队设置目标节点或目标位置。</summary>
        internal void OrderMove(SquadEntity squad, Vector2 position, MapNodeEntity targetNode = null)
        {
            if (squad == null || squad.IsDead) return;
            squad.TargetNode = targetNode;
            squad.TargetPosition = position;
            squad.FormationCenter = squad.Pos;
            squad.Path = BuildPath(squad, position, targetNode);
            squad.PathIndex = squad.Path == null || squad.Path.Count == 0 ? 0 : Mathf.Min(1, squad.Path.Count - 1);
            squad.HasMoveOrder = squad.Path != null && squad.Path.Count > 0;
            foreach (CombatUnitEntity unit in squad.Units.Where(value => !value.IsDead))
            {
                unit.Pursuing = false;
                unit.ChargeAvailable = (squad.Abilities & AbilityMask.Charge) != 0;
            }
        }

        /// <summary>根据地图连接关系构造部队行军路径点。</summary>
        internal List<Vector2> BuildPath(SquadEntity squad, Vector2 target, MapNodeEntity targetNode)
        {
            MapNodeEntity start = squad.AtNode ?? NearestNode(squad.Pos);
            if (start == null || targetNode == null)
                return new List<Vector2> { squad.Pos, target };
            List<MapNodeEntity> nodePath = FindPath(start, targetNode);
            if (nodePath == null || nodePath.Count == 0)
                return new List<Vector2> { squad.Pos, target };
            var points = new List<Vector2> { squad.Pos };
            for (int i = 1; i < nodePath.Count; i++)
            {
                MapEdge edge = nodePath[i - 1].Edges.FirstOrDefault(value =>
                    (value.NodeA == nodePath[i - 1] && value.NodeB == nodePath[i]) ||
                    (value.NodeB == nodePath[i - 1] && value.NodeA == nodePath[i]));
                if (edge == null)
                {
                    points.Add(nodePath[i].Pos);
                    continue;
                }
                bool forward = edge.NodeA == nodePath[i - 1];
                IEnumerable<Vector2> waypoints = forward ? edge.Waypoints : edge.Waypoints.AsEnumerable().Reverse();
                points.AddRange(waypoints.Skip(1));
            }
            if (Vector2.Distance(points[points.Count - 1], target) > 0.01f) points.Add(target);
            return points;
        }

        /// <summary>查找距离指定位置最近的地图节点。</summary>
        internal MapNodeEntity NearestNode(Vector2 position)
        {
            return Sim.Nodes.OrderBy(node => Vector2.SqrMagnitude(node.Pos - position)).FirstOrDefault();
        }

        /// <summary>查找距离指定位置最近的关键节点。</summary>
        internal MapNodeEntity NearestKeyNode(Vector2 position)
        {
            return Sim.Nodes.Where(node => node.Building != null)
                .OrderBy(node => Vector2.SqrMagnitude(node.Pos - position))
                .FirstOrDefault();
        }

        /// <summary>使用地图连接关系查找节点间路径。</summary>
        internal List<MapNodeEntity> FindPath(MapNodeEntity from, MapNodeEntity to)
        {
            if (from == null || to == null) return null;
            var distances = new Dictionary<MapNodeEntity, float> { [from] = 0f };
            var previous = new Dictionary<MapNodeEntity, MapNodeEntity>();
            var open = new List<MapNodeEntity> { from };
            while (open.Count > 0)
            {
                MapNodeEntity current = open.OrderBy(node => distances[node]).First();
                open.Remove(current);
                if (current == to) break;
                foreach (MapEdge edge in current.Edges)
                {
                    MapNodeEntity next = edge.NodeA == current ? edge.NodeB : edge.NodeA;
                    float candidate = distances[current] + Mathf.Max(1f, edge.Length);
                    if (distances.TryGetValue(next, out float currentDistance) && currentDistance <= candidate)
                        continue;
                    distances[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            if (!distances.ContainsKey(to)) return null;
            var path = new List<MapNodeEntity>();
            for (MapNodeEntity node = to; node != null; node = previous.TryGetValue(node, out MapNodeEntity prev) ? prev : null)
            {
                path.Add(node);
                if (node == from) break;
            }
            path.Reverse();
            return path;
        }

        /// <summary>处理部队对中立或敌方建筑的占领。</summary>
        internal void Capture(MapNodeEntity node, SquadEntity capturer)
        {
            if (!BattleSim.IsKeyBuilding(node?.Building) || node.Building.Owner == capturer.Faction) return;
            Faction previousOwner = node.Building.Owner;
            node.Building.Owner = capturer.Faction;
            ReinitializeBuilding(node.Building, capturer.Owner);
            capturer.HomeNode = node;
            capturer.AtNode = node;
            capturer.Owner.Gold += capturer.Owner.Mods[22];
            RefreshOwnership();
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = "BuildingCaptured",
                ActorId = capturer.Id,
                TargetId = node.Id,
                Value = (int)previousOwner,
                Position = node.Pos,
            });
        }

        /// <summary>根据节点当前状态重建建筑运行状态。</summary>
        internal static void ReinitializeBuilding(BuildingState building, PlayerStateEntity owner)
        {
            int hp = building.Hp;
            var def = Cfg.BuildingById(building.Kind);
            if (def == null) return;
            int towerBonus = building.Kind == 4 ? owner.Mods[38] : 0;
            building.BaseHp = Mathf.RoundToInt(def.Hp * owner.ModMul(11));
            building.GoldPerSec = def.Gold + towerBonus;
            building.ManaPerSec = def.Mana + towerBonus;
            building.Supply = def.Supply + towerBonus;
            building.UltimatePerSec = def.Ultimate;
            building.Hp = Mathf.Clamp(hp, 0, building.MaxHp);
        }

        /// <summary>根据建筑归属刷新各参与方拥有的节点集合。</summary>
        internal void RefreshOwnership()
        {
            foreach (PlayerStateEntity player in Sim.Players)
                player.OwnedNodes.RemoveAll(node => node.Building == null || node.Building.Owner != player.Faction);
            foreach (MapNodeEntity node in Sim.Nodes.Where(value => value.Building != null))
            {
                PlayerStateEntity owner = EnsurePlayer(node.Building.Owner);
                if (!owner.OwnedNodes.Contains(node)) owner.OwnedNodes.Add(node);
            }
        }
    }
}
