using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>地图节点上建筑的所有权、耐久、生产和占领进度。</summary>
    public sealed class BuildingState : BattleComponent
    {
        /// <summary>建筑允许达到的最高等级。</summary>
        public const int MaxLevel = 2;
        /// <summary>建筑守卫捕获参数的默认值。</summary>
        public const int DefenderCatchDefault = 1;

        /// <summary>关联的地图节点。</summary>
        public MapNodeEntity Node;
        /// <summary>对象的所属方或所有者。</summary>
        public Faction Owner;
        /// <summary>建筑类型。</summary>
        public int Kind;
        /// <summary>当前等级。</summary>
        public int Level;
        /// <summary>建筑的基础生命值。</summary>
        public int BaseHp;
        /// <summary>当前生命值。</summary>
        public int Hp;
        /// <summary>建筑每秒提供的金币收入。</summary>
        public int GoldPerSec;
        /// <summary>建筑每秒提供的魔法收入。</summary>
        public int ManaPerSec;
        /// <summary>建筑提供的补给容量。</summary>
        public int Supply;
        /// <summary>建筑每秒提供的终极技能能量。</summary>
        public int UltimatePerSec;
        /// <summary>建筑守卫的捕获参数。</summary>
        public int Catch = DefenderCatchDefault;
        /// <summary>建筑的占领进度。</summary>
        public float CaptureProgress;
        /// <summary>最近一次攻击该建筑的单位标识。</summary>
        public int LastAttackerUnitId = -1;
        /// <summary>建筑的修复进度。</summary>
        public float RepairProgress;

        /// <summary>当前最大生命值。</summary>
        public int MaxHp => Mathf.RoundToInt(BaseHp * (1f + 0.5f * Level));
        /// <summary>建筑生命值是否已降至零或以下。</summary>
        public bool IsCaptured => Hp <= 0;
        /// <summary>建筑是否仍有生命值并处于生效状态。</summary>
        public bool IsActive => Hp > 0;
    }
}
