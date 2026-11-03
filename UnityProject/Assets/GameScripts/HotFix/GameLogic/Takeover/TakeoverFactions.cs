using System;
using System.Collections.Generic;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 阵营与关卡资源 ID 的映射。
    /// 第一章（西利亚）已经导入完整素材；第二/三章只有配置数据，地图底图与
    /// waypoint 尚未导入，因此 <see cref="IsPlayable"/> 暂时只放行西利亚。
    /// 三章都保留关卡映射，后续补齐素材时只需要把 IsPlayable 打开。
    /// </summary>
    public static class TakeoverFactions
    {
        private static readonly int[] WestariaLevels = { 1, 2, 3, 4, 5, 6, 7, 8 };
        private static readonly int[] KhaganateLevels = { 11, 12, 13, 14, 15, 16, 17, 18 };
        private static readonly int[] CultLevels = { 21, 22, 23, 24, 25, 26, 27, 28 };
        private static readonly int[] EmptyLevels = Array.Empty<int>();

        /// <summary>单章关卡数（三章都是 8 关）。</summary>
        public const int LevelsPerCampaign = 8;

        public static IReadOnlyList<int> LevelsOf(Faction faction)
        {
            switch (faction)
            {
                case Faction.Westaria: return WestariaLevels;
                case Faction.Khaganate: return KhaganateLevels;
                case Faction.Cult: return CultLevels;
                default: return EmptyLevels;
            }
        }

        /// <summary>当前版本真正能开局的阵营（只有第一章导入了地图/waypoint）。</summary>
        public static bool IsPlayable(Faction faction)
        {
            return faction == Faction.Westaria;
        }

        /// <summary>是否有配置数据（第二/三章为 true，但 IsPlayable 仍为 false）。</summary>
        public static bool HasLevels(Faction faction)
        {
            return LevelsOf(faction).Count > 0;
        }

        /// <summary>写进存档的阵营必须合法；非可玩阵营回退西利亚。</summary>
        public static Faction NormalizeSelected(Faction faction)
        {
            return IsPlayable(faction) ? faction : Faction.Westaria;
        }

        public static int IndexOf(Faction faction, int levelId)
        {
            IReadOnlyList<int> levels = LevelsOf(faction);
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i] == levelId) return i;
            }
            return -1;
        }
    }
}
