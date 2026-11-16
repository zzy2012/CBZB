namespace GameLogic.Takeover
{
    /// <summary>保存参与方的资源及法术冷却状态。</summary>
    public sealed class BattlePlayerEconomyComponent : BattleComponent
    {
        /// <summary>当前金币数量。</summary>
        public float Gold;
        /// <summary>当前魔法值。</summary>
        public float Mana;
        /// <summary>当前终极技能能量。</summary>
        public float Ultimate;
        /// <summary>终极技能能量的上限。</summary>
        public float UltimateMax = 200f;
        /// <summary>法术冷却的剩余时间。</summary>
        public float SpellCooldown;

        /// <summary>推进一次资源收入结算，并限制魔法值上限。</summary>
        public void AdvanceIncome(float manaCap)
        {
            PlayerStateEntity player = Entity as PlayerStateEntity;
            if (player == null) return;
            player.Gold += player.NetGoldIncome();
            player.Mana = UnityEngine.Mathf.Min(manaCap, player.Mana + player.ManaIncome);
            player.Ultimate = UnityEngine.Mathf.Min(player.UltimateMax, player.Ultimate + player.UltimateRate);
            player.SpellCooldown = UnityEngine.Mathf.Max(0f, player.SpellCooldown - 1f);
        }
    }
}
