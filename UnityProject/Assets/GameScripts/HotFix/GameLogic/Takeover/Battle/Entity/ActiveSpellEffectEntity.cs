using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>战场上持续生效的法术实例及其范围、剩余时间和触发状态。</summary>
    public sealed class ActiveSpellEffectEntity : EntityBase
    {
        /// <summary>法术效果的运行时状态组件。</summary>
        public BattleSpellEffectComponent Runtime { get; }
        /// <summary>对应的配置定义。</summary>
        public GameConfig.takeover.Spell Def { get => Runtime.Def; set => Runtime.Def = value; }
        /// <summary>对象的所属方或所有者。</summary>
        public PlayerStateEntity Owner { get => Runtime.Owner; set => Runtime.Owner = value; }
        /// <summary>所属阵营。</summary>
        public Faction Faction { get => Runtime.Faction; set => Runtime.Faction = value; }
        /// <summary>法术效果的中心位置。</summary>
        public Vector2 Center { get => Runtime.Center; set => Runtime.Center = value; }
        /// <summary>法术效果的剩余持续时间。</summary>
        public float Remaining { get => Runtime.Remaining; set => Runtime.Remaining = value; }
        /// <summary>法术效果的结算计时器。</summary>
        public float TickTimer { get => Runtime.TickTimer; set => Runtime.TickTimer = value; }
        /// <summary>法术效果的运行时类别。</summary>
        public SpellEffectKind Kind { get => Runtime.Kind; set => Runtime.Kind = value; }
        /// <summary>法术效果使用的随机种子。</summary>
        public int RandomSeed { get => Runtime.RandomSeed; set => Runtime.RandomSeed = value; }
        /// <summary>法术效果当前的命中序号。</summary>
        public int ImpactIndex { get => Runtime.ImpactIndex; set => Runtime.ImpactIndex = value; }

        /// <summary>创建持续法术实体并挂载运行时组件。</summary>
        public ActiveSpellEffectEntity()
        {
            Runtime = AddComponent(new BattleSpellEffectComponent());
        }
    }
}
