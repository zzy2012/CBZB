using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverInspectorWidget : TakeoverActionListWidget
    {
        private const float InspectorActionTopY = 90f;
        private const float InspectorActionSpacing = 38f;
        private const float InspectorInfoOnlyHeight = 134f;
        private const float InspectorActionBaseHeight = 174f;

        private readonly List<TakeoverActionPlan> _plan = new List<TakeoverActionPlan>(8);
        private TakeoverRunner _runner;
        private BattleSimulation _lastSimulation;
        private Text _title;
        private Text _body;
        private RectTransform _actions;

        protected override Transform ActionButtonParent => _actions != null ? _actions : transform;

        protected override void ScriptGenerator()
        {
            _title = Required<Text>("m_text_InspectorTitle");
            _body = Required<Text>("m_text_InspectorBody");
            _actions = Required<RectTransform>("m_rect_Actions");
        }

        public void SetRunner(TakeoverRunner runner)
        {
            _runner = runner;
            Refresh();
        }

        public void Refresh()
        {
            if (_runner == null) return;
            BattleSimulation simulation = _runner.Simulation;
            if (_lastSimulation != simulation)
            {
                _lastSimulation = simulation;
                InvalidateActionButtons();
            }

            _plan.Clear();
            if (simulation == null || _runner.LevelSelectOpen)
            {
                gameObject.SetActive(false);
                RefreshActionButtons(_plan, "none", stateTint: true);
                return;
            }

            bool visible = BuildInspectorPlan();
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            if (!visible)
            {
                RefreshActionButtons(_plan, "none", stateTint: true);
                return;
            }

            float panelHeight = _plan.Count == 0
                ? InspectorInfoOnlyHeight
                : InspectorActionBaseHeight + (_plan.Count - 1) * InspectorActionSpacing;
            if (!Mathf.Approximately(rectTransform.sizeDelta.y, panelHeight))
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, panelHeight);

            string targetKey = _runner.SelectedSquad != null
                ? $"squad:{_runner.SelectedSquad.Id}"
                : _runner.SelectedNode != null ? $"node:{_runner.SelectedNode.Id}" : "none";
            RefreshActionButtons(_plan, targetKey, stateTint: true);
        }

        private bool BuildInspectorPlan()
        {
            if (_runner.SelectedSquad != null)
            {
                Squad squad = _runner.SelectedSquad;
                _title.text = $"{squad.Def.CnName}  {squad.AliveSoldiers}/{squad.MaxSoldiers}";
                _body.text =
                    $"HP {squad.Hp:F0}/{squad.HpPerSoldier * squad.MaxSoldiers:F0}\n" +
                    $"等级 {squad.LevelIndex}   经验 {squad.Xp:F0}\n" +
                    $"伤害基础 {squad.Def.DmgMin}-{squad.Def.DmgMax} × {squad.AliveSoldiers}\n" +
                    $"射程 {squad.Def.Range}   移速 {squad.Speed:F1}\n" +
                    $"{(squad.TargetNode != null ? $"目标 {squad.TargetNode.Key}" : "待命")}";
                return true;
            }

            MapNode node = _runner.SelectedNode;
            if (node == null) return false;

            if (node.Building != null && node.Building.Owner == _runner.HumanFaction &&
                BattleSim.IsKeyBuilding(node.Building))
            {
                var building = node.Building;
                _title.text = $"{node.Key}  等级 {building.Level}/2";
                _body.text =
                    $"HP {building.Hp}/{building.MaxHp}\n金币 +{building.GoldPerSec}/s   " +
                    $"法力 +{building.ManaPerSec}/s\n补给 +{building.Supply}";
                List<GameConfig.takeover.Unit> units = _runner.UnitsAt(node);
                for (int i = 0; i < units.Count; i++)
                {
                    GameConfig.takeover.Unit unit = units[i];
                    AddPlan($"Buy{unit.Id}", $"{unit.CnName}  {unit.Cost}金  补给{unit.Upkeep}",
                        new Vector2(0f, InspectorActionTopY - i * InspectorActionSpacing),
                        new Vector2(250f, 32f),
                        () => _runner.Buy(unit, node),
                        () => _runner.CanBuy(unit, node));
                }

                var buildingDef = Cfg.BuildingById(building.Kind);
                int upgradeCost = building.Level == 0
                    ? buildingDef?.UpgradeCost1 ?? 350
                    : buildingDef?.UpgradeCost2 ?? 500;
                AddPlan("Upgrade", building.Level >= 2 ? "已满级" : $"升级 {upgradeCost} 金",
                    new Vector2(0f, InspectorActionTopY - units.Count * InspectorActionSpacing),
                    new Vector2(250f, 32f),
                    () => _runner.Upgrade(node),
                    () => building.Level < 2 && _runner.Simulation != null &&
                          _runner.Simulation.Human.Gold >= upgradeCost);
                return true;
            }

            if (node.Building != null)
            {
                _title.text = $"{node.Building.Node.Key}  {node.Building.Owner}";
                _body.text =
                    $"HP {node.Building.Hp}/{node.Building.MaxHp}\n" +
                    $"等级 {node.Building.Level}/2\n" +
                    "拖动己方部队到此处进行攻击";
                return true;
            }

            if (node.Kind == NodeKind.BuildSpot && node.Building == null)
            {
                _title.text = $"空地 {node.Key}";
                bool controlled = _runner.Simulation.State.IsLocControlledBy(
                    _runner.Simulation.Human, node);
                _body.text = controlled ? "在己方控制区段建造附属建筑" : "不在己方控制区";
                if (!controlled) return true;

                for (int i = 0; i < BattleSim.SupplyBuildingIds.Length; i++)
                {
                    int id = BattleSim.SupplyBuildingIds[i];
                    var definition = Cfg.BuildingById(id);
                    if (definition == null) continue;
                    string effect = definition.Supply > 0
                        ? "补给 +1"
                        : definition.Gold > 0 ? "金币 +1/s" : "法力 +1/s";
                    AddPlan($"Build{id}", $"{definition.CnName}  {definition.BuildCost}金  {effect}",
                        new Vector2(0f, InspectorActionTopY - i * InspectorActionSpacing),
                        new Vector2(250f, 32f),
                        () => _runner.Build(node, id),
                        () => _runner.CanBuild(node, id));
                }
                return true;
            }

            return false;
        }

        private void AddPlan(
            string name, string label, Vector2 position, Vector2 size,
            UnityAction click, Func<bool> enabled)
        {
            _plan.Add(new TakeoverActionPlan
            {
                Name = name,
                Label = label,
                Position = position,
                Size = size,
                Click = click,
                Enabled = enabled,
            });
        }

        private T Required<T>(string path) where T : UnityEngine.Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverInspectorWidget prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
