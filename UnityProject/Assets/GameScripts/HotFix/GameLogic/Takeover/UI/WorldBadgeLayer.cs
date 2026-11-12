using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>
    /// 战斗头顶徽标层：UI Toolkit 元素跟随世界坐标；小队徽标可按住拖动下令。
    /// </summary>
    public sealed class WorldBadgeLayer
    {
        private const float SquadBadgeSize = 42f;
        private const float BuildingBadgeWidth = 58f;
        private const float BuildingBadgeHeight = 26f;

        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);

        private readonly TakeoverRunner _runner;
        private readonly VisualElement _layer;
        private readonly Dictionary<int, SquadBadge> _squads = new Dictionary<int, SquadBadge>();
        private readonly Dictionary<int, BuildingBadge> _buildings = new Dictionary<int, BuildingBadge>();

        public WorldBadgeLayer(TakeoverRunner runner, VisualElement layer)
        {
            _runner = runner;
            _layer = layer;
            if (_layer != null) _layer.pickingMode = PickingMode.Ignore;
        }

        public void Sync()
        {
            if (_layer == null || _runner == null) return;
            if (!_runner.ShowSquadBadges)
            {
                ClearSquads();
            }
            else
            {
                SyncSquads();
            }

            if (!_runner.ShowBuildingBadges)
            {
                ClearBuildings();
            }
            else
            {
                SyncBuildings();
            }
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

                if (!_squads.TryGetValue(squad.Id, out SquadBadge badge))
                {
                    badge = CreateSquadBadge(squad);
                    _squads[squad.Id] = badge;
                }

                badge.Refresh(squad, squad == _runner.SelectedSquad);
                Position(badge.Root, _runner.GetSquadBadgeWorldPosition(squad),
                    SquadBadgeSize, SquadBadgeSize);
            }

            foreach (int id in new List<int>(_squads.Keys))
            {
                if (live.Contains(id)) continue;
                _squads[id].Root.RemoveFromHierarchy();
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

                if (!_buildings.TryGetValue(node.Id, out BuildingBadge badge))
                {
                    badge = CreateBuildingBadge(node);
                    _buildings[node.Id] = badge;
                }

                badge.Refresh(node, node == _runner.SelectedNode,
                    node.Building.Owner == _runner.HumanFaction);
                Position(badge.Root, _runner.GetBuildingBadgeWorldPosition(node),
                    BuildingBadgeWidth, BuildingBadgeHeight);
            }

            foreach (int id in new List<int>(_buildings.Keys))
            {
                if (live.Contains(id)) continue;
                _buildings[id].Root.RemoveFromHierarchy();
                _buildings.Remove(id);
            }
        }

        private void Position(VisualElement element, Vector3 worldPosition, float width, float height)
        {
            if (element == null || _layer == null) return;
            IPanel panel = _layer.panel;
            if (panel == null) return;

            Vector3 screen = _runner.WorldCamera != null
                ? _runner.WorldCamera.WorldToScreenPoint(worldPosition)
                : Vector3.zero;
            if (screen.z < 0f)
            {
                element.style.display = DisplayStyle.None;
                return;
            }
            element.style.display = DisplayStyle.Flex;

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
                panel, new Vector2(screen.x, screen.y));
            element.style.left = new StyleLength(panelPoint.x - width * 0.5f);
            element.style.top = new StyleLength(panelPoint.y - height * 0.5f);
        }

        private SquadBadge CreateSquadBadge(Squad squad)
        {
            var root = new VisualElement { pickingMode = PickingMode.Position };
            root.AddToClassList("squad-badge");

            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("squad-badge-ring");
            root.Add(ring);

            var hp = new VisualElement { pickingMode = PickingMode.Ignore };
            hp.AddToClassList("squad-badge-hp");
            root.Add(hp);

            var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
            portrait.AddToClassList("squad-badge-portrait");
            root.Add(portrait);
            UISpriteCache.ApplyBackgroundAsync(
                portrait, TakeoverVisualCatalog.UnitLocation(squad.Def)).Forget();

            var status = new VisualElement { pickingMode = PickingMode.Ignore };
            status.AddToClassList("squad-badge-status");
            var icons = new List<VisualElement>(3);
            for (int i = 0; i < 3; i++)
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("squad-badge-status-icon");
                icon.style.display = DisplayStyle.None;
                status.Add(icon);
                icons.Add(icon);
            }
            root.Add(status);

            root.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                _runner.BeginBadgeDrag(squad);
                evt.StopPropagation();
            });

            _layer.Add(root);
            UIFontProvider.ApplyToTree(root);
            UIToolkitTheme.Apply(root);
            return new SquadBadge(root, ring, hp, portrait, icons);
        }

        private BuildingBadge CreateBuildingBadge(MapNode node)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("building-badge");

            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.AddToClassList("building-badge-ring");
            root.Add(ring);

            var level = new Label { pickingMode = PickingMode.Ignore };
            level.AddToClassList("building-badge-level");
            root.Add(level);

            var hp = new VisualElement { pickingMode = PickingMode.Ignore };
            hp.AddToClassList("building-badge-hp");
            root.Add(hp);

            _layer.Add(root);
            UIFontProvider.ApplyToTree(root);
            UIToolkitTheme.Apply(root);
            return new BuildingBadge(root, ring, hp, level);
        }

        private void ClearSquads()
        {
            foreach (SquadBadge badge in _squads.Values) badge.Root.RemoveFromHierarchy();
            _squads.Clear();
        }

        private void ClearBuildings()
        {
            foreach (BuildingBadge badge in _buildings.Values) badge.Root.RemoveFromHierarchy();
            _buildings.Clear();
        }

        private sealed class SquadBadge
        {
            public readonly VisualElement Root;
            private readonly VisualElement _ring;
            private readonly VisualElement _hp;
            private readonly VisualElement _portrait;
            private readonly List<VisualElement> _icons;
            private readonly string[] _iconLocations;
            private Squad _squad;

            public SquadBadge(VisualElement root, VisualElement ring, VisualElement hp,
                VisualElement portrait, List<VisualElement> icons)
            {
                Root = root;
                _ring = ring;
                _hp = hp;
                _portrait = portrait;
                _icons = icons;
                _iconLocations = new string[icons.Count];
            }

            public void Refresh(Squad squad, bool selected)
            {
                if (_squad != squad)
                {
                    _squad = squad;
                    UISpriteCache.ApplyBackgroundAsync(
                        _portrait, TakeoverVisualCatalog.UnitLocation(squad.Def)).Forget();
                }

                float maxHp = squad.HpPerSoldier * squad.MaxSoldiers;
                float ratio = maxHp <= 0f ? 0f : Mathf.Clamp01(squad.Hp / maxHp);
                _hp.style.height = new StyleLength(Length.Percent(ratio * 100f));
                _hp.style.backgroundColor = new StyleColor(
                    ratio > 0.6f ? HpHigh : ratio > 0.3f ? HpMid : HpLow);

                Color ringColor = selected ? RingSelected :
                    squad.Owner.IsHuman ? RingHuman : RingEnemy;
                _ring.style.borderTopColor = new StyleColor(ringColor);
                _ring.style.borderBottomColor = new StyleColor(ringColor);
                _ring.style.borderLeftColor = new StyleColor(ringColor);
                _ring.style.borderRightColor = new StyleColor(ringColor);

                if (selected) Root.AddToClassList("is-selected");
                else Root.RemoveFromClassList("is-selected");

                RefreshStatuses();
            }

            private void RefreshStatuses()
            {
                bool frozen = false, poison = false, charge = false, shield = false, fear = false;
                foreach (CombatUnit unit in _squad.Units)
                {
                    if (unit.IsDead) continue;
                    if (unit.FrozenSeconds > 0f) frozen = true;
                    if (unit.PoisonSeconds > 0f) poison = true;
                    if (unit.ChargeSpeedSeconds > 0f || unit.DashSeconds > 0f) charge = true;
                    if (unit.DamageResistSeconds > 0f) shield = true;
                    if (unit.FearedSeconds > 0f) fear = true;
                }

                var active = new List<string>(5);
                if (frozen) active.Add("takeover_status_frozen");
                if (poison) active.Add("takeover_status_poison");
                if (charge) active.Add("takeover_status_charge");
                if (shield) active.Add("takeover_status_shield");
                if (fear) active.Add("takeover_status_fear");

                for (int i = 0; i < _icons.Count; i++)
                    SetIcon(i, i < active.Count ? active[i] : null);
            }

            private void SetIcon(int slot, string location)
            {
                if (slot < 0 || slot >= _icons.Count) return;
                VisualElement icon = _icons[slot];
                if (string.IsNullOrEmpty(location))
                {
                    icon.style.display = DisplayStyle.None;
                    _iconLocations[slot] = null;
                    return;
                }

                icon.style.display = DisplayStyle.Flex;
                if (_iconLocations[slot] == location) return;
                _iconLocations[slot] = location;
                UISpriteCache.ApplyBackgroundAsync(icon, location).Forget();
            }
        }

        private sealed class BuildingBadge
        {
            public readonly VisualElement Root;
            private readonly VisualElement _ring;
            private readonly VisualElement _hp;
            private readonly Label _level;

            public BuildingBadge(VisualElement root, VisualElement ring, VisualElement hp, Label level)
            {
                Root = root;
                _ring = ring;
                _hp = hp;
                _level = level;
            }

            public void Refresh(MapNode node, bool selected, bool humanOwned)
            {
                BuildingState building = node.Building;
                if (building == null) return;

                float ratio = building.MaxHp <= 0
                    ? 0f
                    : Mathf.Clamp01((float)building.Hp / building.MaxHp);
                _hp.style.width = new StyleLength(Length.Percent(ratio * 100f));
                _hp.style.backgroundColor = new StyleColor(
                    ratio > 0.6f ? HpHigh : ratio > 0.3f ? HpMid : HpLow);
                _level.text = (building.Level + 1).ToString();

                Color ringColor = selected ? RingSelected : humanOwned ? RingHuman : RingEnemy;
                _ring.style.borderTopColor = new StyleColor(ringColor);
                _ring.style.borderBottomColor = new StyleColor(ringColor);
                _ring.style.borderLeftColor = new StyleColor(ringColor);
                _ring.style.borderRightColor = new StyleColor(ringColor);
                if (selected) Root.AddToClassList("is-selected");
                else Root.RemoveFromClassList("is-selected");
            }
        }
    }
}
