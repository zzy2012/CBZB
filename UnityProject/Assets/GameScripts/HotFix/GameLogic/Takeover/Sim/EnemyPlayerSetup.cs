using System.Collections.Generic;

namespace GameLogic.Takeover
{
    /// <summary>关卡中敌方参与者的阵营、初始资源、AI 开关和修正项配置。</summary>
    public sealed class EnemyPlayerSetup
    {
        /// <summary>所属阵营。</summary>
        public Faction Faction;
        /// <summary>参与方的初始金币数量。</summary>
        public int StartGold;
        /// <summary>参与方的初始魔法值。</summary>
        public int StartMana;
        /// <summary>是否启用该参与方的 AI 行为。</summary>
        public bool AiEnabled = true;
        /// <summary>参与方初始战斗修正项集合。</summary>
        public readonly List<ModifierValue> Modifiers = new List<ModifierValue>();
    }
}
