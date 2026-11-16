namespace GameLogic.Takeover
{
    public sealed class BattlePlayerEconomyComponent : BattleComponent
    {
        public float Gold;
        public float Mana;
        public float Ultimate;
        public float UltimateMax = 200f;
        public float SpellCooldown;

        public void AdvanceIncome(float manaCap)
        {
            PlayerState player = Entity as PlayerState;
            if (player == null) return;
            player.Gold += player.NetGoldIncome();
            player.Mana = UnityEngine.Mathf.Min(manaCap, player.Mana + player.ManaIncome);
            player.Ultimate = UnityEngine.Mathf.Min(player.UltimateMax, player.Ultimate + player.UltimateRate);
            player.SpellCooldown = UnityEngine.Mathf.Max(0f, player.SpellCooldown - 1f);
        }
    }
}
