using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>战斗参与方的资源、阵营、领地、部队和战斗加成状态。</summary>
    public sealed class PlayerStateEntity : EntityBase
    {
        /// <summary>参与方的经济状态组件。</summary>
        public BattlePlayerEconomyComponent Economy { get; }
        /// <summary>对象的标识。</summary>
        public int Id;
        /// <summary>对象的名称。</summary>
        public string Name;
        /// <summary>所属阵营。</summary>
        public Faction Faction;
        /// <summary>该参与方是否为人类玩家。</summary>
        public bool IsHuman;
        /// <summary>是否启用该参与方的 AI 行为。</summary>
        public bool AiEnabled = true;
        /// <summary>由 GM 开关控制：为 true 时玩家也交给 AI 托管（人类玩家默认 false）。</summary>
        public bool AiControlled;
        /// <summary>该参与方是否允许施放法术。</summary>
        public bool SpellsEnabled = true;
        /// <summary>参与方允许使用的兵种类型数量上限。</summary>
        public int UnitTypeLimit = 4;
        /// <summary>当前金币数量。</summary>
        public float Gold { get => Economy.Gold; set => Economy.Gold = value; }
        /// <summary>当前魔法值。</summary>
        public float Mana { get => Economy.Mana; set => Economy.Mana = value; }
        /// <summary>当前终极技能能量。</summary>
        public float Ultimate { get => Economy.Ultimate; set => Economy.Ultimate = value; }
        /// <summary>终极技能能量的上限。</summary>
        public float UltimateMax { get => Economy.UltimateMax; set => Economy.UltimateMax = value; }
        /// <summary>法术冷却的剩余时间。</summary>
        public float SpellCooldown { get => Economy.SpellCooldown; set => Economy.SpellCooldown = value; }
        /// <summary>按修正项标识索引的战斗修正值。</summary>
        public readonly int[] Mods = new int[45];
        /// <summary>参与方拥有的地图节点。</summary>
        public readonly List<MapNodeEntity> OwnedNodes = new List<MapNodeEntity>();
        /// <summary>部队集合。</summary>
        public readonly List<SquadEntity> Squads = new List<SquadEntity>();

        /// <summary>创建参与方实体并挂载经济组件。</summary>
        public PlayerStateEntity()
        {
            Economy = AddComponent(new BattlePlayerEconomyComponent());
        }

        /// <summary>将指定修正项转换为乘数，索引会限制在有效范围内。</summary>
        public float ModMul(int id) => (100 + Mods[Mathf.Clamp(id, 0, Mods.Length - 1)]) / 100f;

        /// <summary>当前生效的关键建筑数量。</summary>
        public int ActiveBuildings => OwnedNodes.Count(node =>
            node.Building != null && node.Building.IsActive && BattleSim.IsKeyBuilding(node.Building));
        /// <summary>参与方的补给容量上限。</summary>
        public int SupplyCap => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.Supply);
        /// <summary>当前部队占用的补给数量。</summary>
        public int SupplyUsed => Squads.Where(squad => !squad.IsDead).Sum(squad => squad.Def.Upkeep);
        /// <summary>尚未使用的补给容量。</summary>
        public int FreeSupply => SupplyCap - SupplyUsed;
        /// <summary>生效建筑提供的基础金币收入。</summary>
        public int BaseGoldIncome => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.GoldPerSec);
        /// <summary>生效建筑提供的魔法收入。</summary>
        public int ManaIncome => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.ManaPerSec);
        /// <summary>生效建筑提供的终极技能能量增长速率。</summary>
        public int UltimateRate => OwnedNodes.Where(node => node.Building != null && node.Building.IsActive)
            .Sum(node => node.Building.UltimatePerSec);
        /// <summary>尚有存活士兵的部队数量。</summary>
        public int LivingSquads => Squads.Count(squad => !squad.IsDead);

        /// <summary>计算应用部队数量惩罚后的金币净收入。</summary>
        public int NetGoldIncome()
        {
            int income = BaseGoldIncome;
            int squads = LivingSquads;
            if (income == 0 || squads == 0) return income;
            int squadsForPenalty = squads >= 10 ? 10 : squads;
            int modifier = 30 + (squadsForPenalty - 1) * 5;
            return Mathf.Max(1, Mathf.RoundToInt(income * (100 - modifier) / 100f));
        }
    }
}
