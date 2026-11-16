namespace GameLogic.Takeover
{
    /// <summary>保存持续法术效果的运行时状态。</summary>
    public sealed class BattleSpellEffectComponent : BattleComponent
    {
        /// <summary>对应的配置定义。</summary>
        public GameConfig.takeover.Spell Def;
        /// <summary>对象的所属方或所有者。</summary>
        public PlayerStateEntity Owner;
        /// <summary>所属阵营。</summary>
        public Faction Faction;
        /// <summary>法术效果的中心位置。</summary>
        public UnityEngine.Vector2 Center;
        /// <summary>法术效果的剩余持续时间。</summary>
        public float Remaining;
        /// <summary>法术效果的结算计时器。</summary>
        public float TickTimer;
        /// <summary>法术效果的运行时类别。</summary>
        public SpellEffectKind Kind;
        /// <summary>法术效果使用的随机种子。</summary>
        public int RandomSeed;
        /// <summary>法术效果当前的命中序号。</summary>
        public int ImpactIndex;

        /// <summary>按时间增量推进法术效果计时。</summary>
        public void Advance(float deltaSeconds)
        {
            ActiveSpellEffectEntity effect = Entity as ActiveSpellEffectEntity;
            if (effect == null) return;
            effect.Remaining -= deltaSeconds;
            effect.TickTimer -= deltaSeconds;
        }
    }
}
