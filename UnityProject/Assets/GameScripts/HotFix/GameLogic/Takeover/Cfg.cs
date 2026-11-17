using System.Collections.Generic;
using System.Linq;
using GameConfig;
using GameConfig.takeover;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 配置访问封装：业务代码不直接散落 ConfigSystem.Instance.Tables。
    /// 表由 Luban 生成（Configs/GameConfig/Datas/takeover 相关 xlsx）。
    /// </summary>
    public static class Cfg
    {
        /// <summary>自检期间安装的临时配置表。</summary>
        private static Tables _validationTables;
        /// <summary>当前使用的配置表实例；未安装自检表时读取全局配置。</summary>
        private static Tables T => _validationTables ?? ConfigSystem.Instance.Tables;

        /// <summary>供编辑器校验加载生成的配置字节，无需启动游戏流程。</summary>
        public static void InstallValidationTables(Tables tables) => _validationTables = tables;
        /// <summary>清除自检使用的临时配置表。</summary>
        public static void ClearValidationTables() => _validationTables = null;

        /// <summary>单位配置表。</summary>
        public static TbUnit TbUnit => T.TbUnit;
        /// <summary>建筑配置表。</summary>
        public static TbBuilding TbBuilding => T.TbBuilding;
        /// <summary>可建造的附属建筑配置。</summary>
        public static IEnumerable<Building> BuildableBuildings =>
            TbBuilding.DataList.Where(building => building.BuildCost > 0);
        /// <summary>提供补给的可建造建筑配置。</summary>
        public static Building SupplyBuilding =>
            BuildableBuildings.Single(building => building.Supply > 0);
        /// <summary>提供金币收入的可建造建筑配置。</summary>
        public static Building GoldBuilding =>
            BuildableBuildings.Single(building => building.Gold > 0);
        /// <summary>提供法力收入的可建造建筑配置。</summary>
        public static Building ManaBuilding =>
            BuildableBuildings.Single(building => building.Mana > 0);
        /// <summary>编队偏移配置表。</summary>
        public static TbFormation TbFormation => T.TbFormation;
        /// <summary>法术配置表。</summary>
        public static TbSpell TbSpell => T.TbSpell;
        /// <summary>政令配置表。</summary>
        public static TbEdict TbEdict => T.TbEdict;
        /// <summary>地图节点配置表。</summary>
        public static TbMapNode TbMapNode => T.TbMapNode;
        /// <summary>地图边配置表。</summary>
        public static TbMapEdge TbMapEdge => T.TbMapEdge;
        /// <summary>关卡配置表。</summary>
        public static TbLevel TbLevel => T.TbLevel;

        /// <summary>按建筑配置标识缓存的建筑配置。</summary>
        private static Dictionary<int, Building> _byId;

        /// <summary>按配置标识查找建筑配置。</summary>
        public static Building BuildingById(int id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, Building>();
                foreach (var b in TbBuilding.DataList) _byId[b.Id] = b;
            }
            return _byId.TryGetValue(id, out var v) ? v : null;
        }

        /// <summary>节点类型 → 建筑定义：城镇=1 要塞=2 营地=3 魔法塔=4。</summary>
        public static Building BuildingByKind(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Town: return BuildingById(1);
                case NodeKind.Castle: return BuildingById(2);
                case NodeKind.Camp: return BuildingById(3);
                case NodeKind.MageTower: return BuildingById(4);
                default: return null;
            }
        }

        /// <summary>按节点类型查找建筑配置。</summary>
        public static Building BuildingByKind(int kind) => BuildingByKind((NodeKind)kind);

        /// <summary>获取属于指定阵营的单位配置。</summary>
        public static IEnumerable<Unit> UnitsOf(Faction faction) =>
            TbUnit.DataList.Where(u => OriginalRules.FromConfigFaction(u.Faction) == faction);
    }
}
