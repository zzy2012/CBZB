using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>持有战斗模拟上下文的系统基类。</summary>
    internal abstract class BattleSimSystem
    {
        /// <summary>使用指定战斗模拟初始化系统基类。</summary>
        protected BattleSimSystem(BattleSim sim)
        {
            Sim = sim;
        }

        /// <summary>当前模拟系统所属的战斗模拟。</summary>
        protected BattleSim Sim { get; }
        /// <summary>当前模拟场景的地图节点集合。</summary>
        protected List<MapNodeEntity> Nodes => Sim.Nodes;
        /// <summary>当前模拟场景按配置键索引的节点。</summary>
        protected Dictionary<string, MapNodeEntity> NodeByKey => Sim.NodeByKey;
        /// <summary>当前模拟场景的地图连接边。</summary>
        protected List<MapEdge> Edges => Sim.Edges;
        /// <summary>当前模拟场景的部队集合。</summary>
        protected List<SquadEntity> Squads => Sim.Squads;
        /// <summary>当前模拟场景的参与方集合。</summary>
        protected List<PlayerStateEntity> Players => Sim.Players;
        /// <summary>当前模拟场景的持续法术效果集合。</summary>
        protected List<ActiveSpellEffectEntity> ActiveSpells => Sim.ActiveSpells;
        /// <summary>当前模拟场景的人类玩家。</summary>
        protected PlayerStateEntity Human => Sim.Human;
        /// <summary>当前模拟场景的敌方参与方。</summary>
        protected PlayerStateEntity Enemy => Sim.Enemy;
        /// <summary>当前模拟场景的关卡资源标识。</summary>
        protected int LevelAssetId => Sim.LevelAssetId;
        /// <summary>当前模拟场景已运行的时间。</summary>
        protected float Time
        {
            get => Sim.Time;
            set => Sim.Time = value;
        }

        /// <summary>当前模拟共用的确定性随机数生成器。</summary>
        protected System.Random _rng => Sim.RandomSource;
        /// <summary>当前模拟待发布的战斗事件缓冲区。</summary>
        protected List<BattleEvent> _events => Sim.EventBuffer;
        /// <summary>人类玩家本局已解锁的建筑标识集合。</summary>
        protected HashSet<int> _humanBuildingsUnlocked => Sim.HumanBuildingsUnlocked;

        /// <summary>当前模拟使用的地图逻辑系统。</summary>
        protected BattleMapSystem MapSystem => Sim.MapSystem;
        /// <summary>当前模拟使用的经济逻辑系统。</summary>
        protected BattleEconomySystem EconomySystem => Sim.EconomySystem;
        /// <summary>当前模拟使用的战斗逻辑系统。</summary>
        protected BattleCombatSystem CombatSystem => Sim.CombatSystem;
        /// <summary>当前模拟使用的法术逻辑系统。</summary>
        protected BattleSpellSystem SpellSystem => Sim.SpellSystem;
        /// <summary>当前模拟使用的 AI 逻辑系统。</summary>
        protected BattleAiSystem AiSystem => Sim.AiSystem;

        /// <summary>按标识查找地图节点。</summary>
        protected MapNodeEntity FindNode(int id) => Sim.FindNode(id);
        /// <summary>查找指定阵营对应的参与方。</summary>
        protected PlayerStateEntity PlayerOf(Faction faction) => Sim.PlayerOf(faction);
        /// <summary>按标识查找战场中的士兵。</summary>
        protected CombatUnitEntity FindUnitById(int id) => Sim.FindUnitById(id);

        /// <summary>判断建筑是否属于关键建筑类型。</summary>
        protected static bool IsKeyBuilding(BuildingState building) => BattleSim.IsKeyBuilding(building);
        /// <summary>判断指定区域是否由人类玩家控制。</summary>
        protected static bool IsLocControlled(PlayerStateEntity player, int location) =>
            BattleSim.IsLocControlled(player, location);
        /// <summary>判断部队中是否存在机械单位。</summary>
        protected static bool HasMechanism(SquadEntity squad) => BattleSim.HasMechanism(squad);
        /// <summary>判断部队是否受恐惧控制限制。</summary>
        protected static bool HasFearLock(SquadEntity squad) => BattleSim.HasFearLock(squad);
        /// <summary>判断部队是否受士气状态限制。</summary>
        protected static bool HasMoraleLock(SquadEntity squad) => BattleSim.HasMoraleLock(squad);
        /// <summary>计算部队碰撞和攻击距离判定所需的单位半径。</summary>
        protected static float UnitRadius(SquadEntity squad) => BattleSim.UnitRadius(squad);
        /// <summary>根据部队人数和士兵索引计算编队偏移。</summary>
        protected static Vector2 FormationOffset(int index, int count, float rotation = 0f) =>
            BattleSim.FormationOffset(index, count, rotation);
        /// <summary>根据累计经验计算部队等级。</summary>
        protected static int LevelForXp(float xp) => BattleSim.LevelForXp(xp);
        /// <summary>获取部队等级对应的战斗阶级。</summary>
        protected static int LevelTier(SquadEntity squad) => BattleSim.LevelTier(squad);
    }
}
