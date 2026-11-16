namespace GameLogic.Takeover
{
    public sealed class BattleSpellEffectComponent : BattleComponent
    {
        public GameConfig.takeover.Spell Def;
        public PlayerState Owner;
        public Faction Faction;
        public UnityEngine.Vector2 Center;
        public float Remaining;
        public float TickTimer;
        public SpellEffectKind Kind;
        public int RandomSeed;
        public int ImpactIndex;

        public void Advance(float deltaSeconds)
        {
            ActiveSpellEffect effect = Entity as ActiveSpellEffect;
            if (effect == null) return;
            effect.Remaining -= deltaSeconds;
            effect.TickTimer -= deltaSeconds;
        }
    }
}
