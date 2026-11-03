using System;
using System.Collections.Generic;
using System.Linq;
using GameConfig.takeover;

namespace GameLogic.Takeover.UI
{
    /// <summary>战斗菜单中的一个动作。</summary>
    public sealed class BattleAction
    {
        public string Group;
        public string Id;
        public string Label;
        public string Detail;
        public bool Enabled;
        public Action Click;
    }

    /// <summary>当前选中目标的标题/正文与动作列表，供战斗菜单与检查器共用。</summary>
    public sealed class BattleActionPlan
    {
        public string TargetKey = "none";
        public string Title = string.Empty;
        public string Body = string.Empty;
        public bool ShowMenu;
        public readonly List<BattleAction> Actions = new List<BattleAction>();
    }

    /// <summary>
    /// 把“选中了什么”翻译成界面数据。原 TakeoverInspectorWidget 的布局逻辑
    /// 不再负责创建 UGUI 按钮，只产出这份纯数据计划。
    /// </summary>
    public static class TakeoverActionPlanner
    {
        public static BattleActionPlan Build(TakeoverRunner runner)
        {
            if (runner == null || runner.Simulation == null || runner.LevelSelectOpen)
                return null;

            if (runner.SelectedSquad != null)
                return BuildSquad(runner, runner.SelectedSquad);

            MapNode node = runner.SelectedNode;
            if (node == null) return null;

            if (node.Building != null && node.Building.Owner == runner.HumanFaction &&
                BattleSim.IsKeyBuilding(node.Building))
                return BuildOwnBuilding(runner, node);

            if (node.Building != null)
                return BuildEnemyBuilding(node);

            if (node.Kind == NodeKind.BuildSpot && node.Building == null)
                return BuildSpot(runner, node);

            return null;
        }

        private static BattleActionPlan BuildSquad(TakeoverRunner runner, Squad squad)
        {
            var plan = new BattleActionPlan
            {
                TargetKey = $"squad:{squad.Id}",
                Title = $"{squad.Def.CnName}  {squad.AliveSoldiers}/{squad.MaxSoldiers}",
                Body =
                    $"HP {squad.Hp:F0}/{squad.HpPerSoldier * squad.MaxSoldiers:F0}\n" +
                    $"等级 {squad.LevelIndex}   经验 {squad.Xp:F0}\n" +
                    $"伤害基础 {squad.Def.DmgMin}-{squad.Def.DmgMax} × {squad.AliveSoldiers}\n" +
                    $"射程 {squad.Def.Range}   移速 {squad.Speed:F1}\n" +
                    $"{(squad.TargetNode != null ? $"目标 {squad.TargetNode.Key}" : "待命")}",
                ShowMenu = false,
            };
            return plan;
        }

        private static BattleActionPlan BuildOwnBuilding(TakeoverRunner runner, MapNode node)
        {
            BuildingState building = node.Building;
            var plan = new BattleActionPlan
            {
                TargetKey = $"node:{node.Id}",
                Title = $"{node.Key}  等级 {building.Level + 1}/3",
                Body =
                    $"HP {building.Hp}/{building.MaxHp}\n" +
                    $"金币 +{building.GoldPerSec}/s   法力 +{building.ManaPerSec}/s\n" +
                    $"补给 +{building.Supply}",
                ShowMenu = true,
            };

            List<Unit> units = runner.UnitsAt(node);
            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                Unit captured = unit;
                plan.Actions.Add(new BattleAction
                {
                    Group = "units",
                    Id = $"Buy{unit.Id}",
                    Label = unit.CnName,
                    Detail = $"{unit.Cost}金  补给{unit.Upkeep}",
                    Enabled = runner.CanBuy(unit, node),
                    Click = () => runner.Buy(captured, node),
                });
            }

            Building definition = Cfg.BuildingById(building.Kind);
            int upgradeCost = building.Level == 0
                ? definition?.UpgradeCost1 ?? 350
                : definition?.UpgradeCost2 ?? 500;
            bool maxed = building.Level >= 2;
            plan.Actions.Add(new BattleAction
            {
                Group = "upgrade",
                Id = "Upgrade",
                Label = maxed ? "已满级" : "升级城堡",
                Detail = maxed ? string.Empty : $"{upgradeCost} 金",
                Enabled = !maxed && runner.Simulation != null &&
                          runner.Simulation.Human.Gold >= upgradeCost,
                Click = () => runner.Upgrade(node),
            });

            return plan;
        }

        private static BattleActionPlan BuildEnemyBuilding(MapNode node)
        {
            return new BattleActionPlan
            {
                TargetKey = $"node:{node.Id}",
                Title = $"{node.Key}  {node.Building.Owner}",
                Body =
                    $"HP {node.Building.Hp}/{node.Building.MaxHp}\n" +
                    $"等级 {node.Building.Level + 1}/3\n" +
                    "拖动己方部队到此处进行攻击",
                ShowMenu = false,
            };
        }

        private static BattleActionPlan BuildSpot(TakeoverRunner runner, MapNode node)
        {
            bool controlled = runner.Simulation.State.IsLocControlledBy(
                runner.Simulation.Human, node);
            var plan = new BattleActionPlan
            {
                TargetKey = $"node:{node.Id}",
                Title = $"空地 {node.Key}",
                Body = controlled ? "在己方控制区段建造附属建筑" : "不在己方控制区",
                ShowMenu = controlled,
            };

            if (!controlled) return plan;

            for (int i = 0; i < BattleSim.SupplyBuildingIds.Length; i++)
            {
                int buildingId = BattleSim.SupplyBuildingIds[i];
                Building definition = Cfg.BuildingById(buildingId);
                if (definition == null) continue;
                string effect = definition.Supply > 0
                    ? "补给 +1"
                    : definition.Gold > 0 ? "金币 +1/s" : "法力 +1/s";
                int capturedId = buildingId;
                plan.Actions.Add(new BattleAction
                {
                    Group = "build",
                    Id = $"Build{buildingId}",
                    Label = definition.CnName,
                    Detail = $"{definition.BuildCost}金  {effect}",
                    Enabled = runner.CanBuild(node, buildingId),
                    Click = () => runner.Build(node, capturedId),
                });
            }

            return plan;
        }
    }
}
