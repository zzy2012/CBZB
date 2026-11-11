using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;

namespace GameLogic.Takeover.UI
{
    public sealed class WorldBadgeLayer
    {
        private readonly TakeoverRunner _runner;
        private readonly RectTransform _layer;
        private readonly Dictionary<int, TakeoverSquadBadgeView> _squads =
            new Dictionary<int, TakeoverSquadBadgeView>();
        private readonly Dictionary<int, TakeoverBuildingBadgeView> _buildings =
            new Dictionary<int, TakeoverBuildingBadgeView>();

        public WorldBadgeLayer(TakeoverRunner runner, RectTransform layer)
        {
            _runner = runner;
            _layer = layer;
        }

        public void Sync()
        {
            if (_layer == null || _runner == null) return;
            if (!_runner.ShowSquadBadges) ClearSquads();
            else SyncSquads();
            if (!_runner.ShowBuildingBadges) ClearBuildings();
            else SyncBuildings();
        }

        public void Clear()
        {
            ClearSquads();
            ClearBuildings();
        }

        private void SyncSquads()
        {
            BattleSimulation simulation = _runner.Simulation;
            if (simulation == null)
            {
                ClearSquads();
                return;
            }

            var live = new HashSet<int>();
            foreach (Squad squad in simulation.Squads)
            {
                if (squad.IsDead) continue;
                live.Add(squad.Id);
                if (!_squads.TryGetValue(squad.Id, out TakeoverSquadBadgeView badge))
                {
                    badge = new TakeoverSquadBadgeView(_layer, squad,
                        () => _runner.BeginBadgeDrag(squad), _runner.GetCancellationTokenOnDestroy());
                    _squads.Add(squad.Id, badge);
                }
                badge.Refresh(squad, squad == _runner.SelectedSquad);
                Position(badge.Root, _runner.GetSquadBadgeWorldPosition(squad));
            }

            var removed = new List<int>();
            foreach (int id in _squads.Keys)
                if (!live.Contains(id)) removed.Add(id);
            foreach (int id in removed)
            {
                _squads[id].Destroy();
                _squads.Remove(id);
            }
        }

        private void SyncBuildings()
        {
            BattleSimulation simulation = _runner.Simulation;
            if (simulation == null)
            {
                ClearBuildings();
                return;
            }

            var live = new HashSet<int>();
            foreach (MapNode node in simulation.Nodes)
            {
                if (node.Building == null || !BattleSim.IsKeyBuilding(node.Building)) continue;
                live.Add(node.Id);
                if (!_buildings.TryGetValue(node.Id, out TakeoverBuildingBadgeView badge))
                {
                    badge = new TakeoverBuildingBadgeView(_layer);
                    _buildings.Add(node.Id, badge);
                }
                badge.Refresh(node, node == _runner.SelectedNode,
                    node.Building.Owner == _runner.HumanFaction);
                Position(badge.Root, _runner.GetBuildingBadgeWorldPosition(node));
            }

            var removed = new List<int>();
            foreach (int id in _buildings.Keys)
                if (!live.Contains(id)) removed.Add(id);
            foreach (int id in removed)
            {
                _buildings[id].Destroy();
                _buildings.Remove(id);
            }
        }

        private void Position(RectTransform element, Vector3 worldPosition)
        {
            if (element == null || _layer == null) return;
            Camera camera = _runner.WorldCamera;
            Vector3 screen = camera != null ? camera.WorldToScreenPoint(worldPosition) : Vector3.zero;
            if (screen.z < 0f)
            {
                element.gameObject.SetActive(false);
                return;
            }

            element.gameObject.SetActive(true);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _layer, screen, null, out Vector2 local))
                element.anchoredPosition = local;
        }

        private void ClearSquads()
        {
            foreach (TakeoverSquadBadgeView badge in _squads.Values) badge.Destroy();
            _squads.Clear();
        }

        private void ClearBuildings()
        {
            foreach (TakeoverBuildingBadgeView badge in _buildings.Values) badge.Destroy();
            _buildings.Clear();
        }
    }
}
