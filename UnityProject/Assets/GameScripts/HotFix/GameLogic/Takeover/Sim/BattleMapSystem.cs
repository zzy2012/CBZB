using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal sealed class BattleMapSystem : BattleSimSystem
    {
        internal BattleMapSystem(BattleSim sim) : base(sim) { }

        internal void BuildMap(int levelAssetId)
        {
            foreach (GameConfig.takeover.MapNode config in Cfg.TbMapNode.DataList.Where(value => value.LevelId == levelAssetId))
            {
                Faction owner = OriginalRules.FromConfigFaction(config.Owner);
                var node = new MapNode
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
                    PlayerState player = EnsurePlayer(owner);
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
                Nodes.Add(node);
                NodeByKey[node.Key] = node;
            }

            foreach (GameConfig.takeover.MapEdge config in Cfg.TbMapEdge.DataList.Where(value => value.LevelId == levelAssetId))
            {
                if (!NodeByKey.TryGetValue(config.NodeA, out MapNode nodeA) ||
                    !NodeByKey.TryGetValue(config.NodeB, out MapNode nodeB))
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
                Edges.Add(edge);
            }
        }

        internal PlayerState EnsurePlayer(Faction faction)
        {
            PlayerState existing = PlayerOf(faction);
            if (existing != null) return existing;
            var player = new PlayerState
            {
                Id = Players.Count,
                Name = faction.ToString(),
                Faction = faction,
                AiEnabled = false,
            };
            Players.Add(player);
            return player;
        }

        internal MapNode FindNearestHostileNode(MapNode from, PlayerState player)
        {
            if (from == null) return null;
            return Nodes.Where(node => IsKeyBuilding(node.Building) && node.Building.Owner != player.Faction)
                .OrderBy(node => ShortestDistance(from, node))
                .FirstOrDefault();
        }

        internal float ShortestDistance(MapNode from, MapNode to)
        {
            if (from == null || to == null) return float.MaxValue;
            var distances = new Dictionary<MapNode, float>();
            var queue = new List<MapNode> { from };
            distances[from] = 0f;
            while (queue.Count > 0)
            {
                MapNode current = queue.OrderBy(node => distances[node]).First();
                queue.Remove(current);
                if (current == to) return distances[current];
                foreach (MapEdge edge in current.Edges)
                {
                    MapNode next = edge.NodeA == current ? edge.NodeB : edge.NodeA;
                    float candidate = distances[current] + Mathf.Max(1f, edge.Length);
                    if (distances.TryGetValue(next, out float existing) && existing <= candidate) continue;
                    distances[next] = candidate;
                    if (!queue.Contains(next)) queue.Add(next);
                }
            }
            return float.MaxValue;
        }

        internal void OrderMove(Squad squad, MapNode targetNode)
        {
            OrderMove(squad, targetNode?.Pos ?? squad.Pos, targetNode);
        }

        internal void OrderMove(Squad squad, Vector2 position, MapNode targetNode = null)
        {
            if (squad == null || squad.IsDead) return;
            squad.TargetNode = targetNode;
            squad.TargetPosition = position;
            squad.FormationCenter = squad.Pos;
            squad.Path = BuildPath(squad, position, targetNode);
            squad.PathIndex = squad.Path == null || squad.Path.Count == 0 ? 0 : Mathf.Min(1, squad.Path.Count - 1);
            squad.HasMoveOrder = squad.Path != null && squad.Path.Count > 0;
            foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead))
            {
                unit.Pursuing = false;
                unit.ChargeAvailable = (squad.Abilities & AbilityMask.Charge) != 0;
            }
        }

        internal List<Vector2> BuildPath(Squad squad, Vector2 target, MapNode targetNode)
        {
            MapNode start = squad.AtNode ?? NearestNode(squad.Pos);
            if (start == null || targetNode == null)
                return new List<Vector2> { squad.Pos, target };
            List<MapNode> nodePath = FindPath(start, targetNode);
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

        internal MapNode NearestNode(Vector2 position)
        {
            return Nodes.OrderBy(node => Vector2.SqrMagnitude(node.Pos - position)).FirstOrDefault();
        }

        internal MapNode NearestKeyNode(Vector2 position)
        {
            return Nodes.Where(node => node.Building != null)
                .OrderBy(node => Vector2.SqrMagnitude(node.Pos - position))
                .FirstOrDefault();
        }

        internal List<MapNode> FindPath(MapNode from, MapNode to)
        {
            if (from == null || to == null) return null;
            var distances = new Dictionary<MapNode, float> { [from] = 0f };
            var previous = new Dictionary<MapNode, MapNode>();
            var open = new List<MapNode> { from };
            while (open.Count > 0)
            {
                MapNode current = open.OrderBy(node => distances[node]).First();
                open.Remove(current);
                if (current == to) break;
                foreach (MapEdge edge in current.Edges)
                {
                    MapNode next = edge.NodeA == current ? edge.NodeB : edge.NodeA;
                    float candidate = distances[current] + Mathf.Max(1f, edge.Length);
                    if (distances.TryGetValue(next, out float currentDistance) && currentDistance <= candidate)
                        continue;
                    distances[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            if (!distances.ContainsKey(to)) return null;
            var path = new List<MapNode>();
            for (MapNode node = to; node != null; node = previous.TryGetValue(node, out MapNode prev) ? prev : null)
            {
                path.Add(node);
                if (node == from) break;
            }
            path.Reverse();
            return path;
        }

        internal void Capture(MapNode node, Squad capturer)
        {
            if (!IsKeyBuilding(node?.Building) || node.Building.Owner == capturer.Faction) return;
            Faction previousOwner = node.Building.Owner;
            node.Building.Owner = capturer.Faction;
            ReinitializeBuilding(node.Building, capturer.Owner);
            capturer.HomeNode = node;
            capturer.AtNode = node;
            capturer.Owner.Gold += capturer.Owner.Mods[22];
            if (LevelAssetId == 1 && node == _tutorialTown && !_tutorialTownCaptured)
            {
                _tutorialTownCaptured = true;
                PlayerState empire = PlayerOf(Faction.Empire);
                GameConfig.takeover.Unit replacement = EconomySystem.FindUnit(Faction.Empire, UnitKind.Infantry, 1);
                if (empire != null && replacement != null) empire.Gold += replacement.Cost;
                _events.Add(new BattleEvent
                {
                    Kind = "TutorialTownCaptured",
                    ActorId = capturer.Id,
                    TargetId = node.Id,
                    Position = node.Pos,
                });
            }
            if (LevelAssetId == 2 && node.Building.Kind == 4 && !_l2MagicTowerCaptured)
            {
                _l2MagicTowerCaptured = true;
                Human.Ultimate = Human.UltimateMax;
                _events.Add(new BattleEvent
                {
                    Kind = "UltimateUnlocked",
                    ActorId = Human.Id,
                    TargetId = node.Id,
                    Position = node.Pos,
                });
            }
            RefreshOwnership();
            _events.Add(new BattleEvent
            {
                Kind = "BuildingCaptured",
                ActorId = capturer.Id,
                TargetId = node.Id,
                Value = (int)previousOwner,
                Position = node.Pos,
            });
        }

        internal static void ReinitializeBuilding(BuildingState building, PlayerState owner)
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

        internal void RefreshOwnership()
        {
            foreach (PlayerState player in Players)
                player.OwnedNodes.RemoveAll(node => node.Building == null || node.Building.Owner != player.Faction);
            foreach (MapNode node in Nodes.Where(value => value.Building != null))
            {
                PlayerState owner = EnsurePlayer(node.Building.Owner);
                if (!owner.OwnedNodes.Contains(node)) owner.OwnedNodes.Add(node);
            }
        }
    }
}
