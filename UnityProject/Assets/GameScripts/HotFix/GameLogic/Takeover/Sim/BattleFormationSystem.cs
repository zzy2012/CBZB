using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>从配置加载阵型并计算士兵的相对位置。</summary>
    internal sealed class BattleFormationSystem : BattleSimSystemBase
    {
        /// <summary>按编队最小单位数索引的坐标配置。</summary>
        private readonly SortedDictionary<int, Vector2[]> _layouts =
            new SortedDictionary<int, Vector2[]>();

        /// <summary>创建阵型系统并加载配置数据。</summary>
        internal BattleFormationSystem(TakeoverScene sim) : base(sim)
        {
            LoadLayouts();
        }

        /// <summary>根据部队人数和士兵索引计算编队偏移。</summary>
        internal Vector2 FormationOffset(int index, int count, float rotation = 0f)
        {
            Vector2[] formation = _layouts.First().Value;
            foreach (KeyValuePair<int, Vector2[]> layout in _layouts)
            {
                if (layout.Key > count) break;
                formation = layout.Value;
            }

            Vector2 offset = formation[Mathf.Clamp(index, 0, formation.Length - 1)];
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            return new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
        }

        /// <summary>从配置加载编队坐标并校验索引连续性。</summary>
        private void LoadLayouts()
        {
            foreach (var group in Cfg.TbFormation.DataList.GroupBy(value => value.MinUnitCount))
            {
                if (group.Key < 1)
                    throw new InvalidOperationException("Formation minimum unit count must be positive.");

                GameConfig.takeover.Formation[] entries = group.OrderBy(value => value.UnitIndex).ToArray();
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].UnitIndex != i)
                        throw new InvalidOperationException(
                            $"Formation {group.Key} has a missing or duplicate unit index.");
                }

                _layouts.Add(group.Key,
                    entries.Select(value => new Vector2(value.OffsetX, value.OffsetY)).ToArray());
            }

            if (_layouts.Count == 0 || _layouts.First().Key != 1)
                throw new InvalidOperationException("A formation layout for one unit is required.");
        }
    }
}
