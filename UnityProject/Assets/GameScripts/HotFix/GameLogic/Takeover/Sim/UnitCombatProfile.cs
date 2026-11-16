namespace GameLogic.Takeover
{
    /// <summary>单位动画、移动、投射物表现及战斗能力参数。</summary>
    public sealed class UnitCombatProfile
    {
        /// <summary>单位动画的播放速度。</summary>
        public float AnimationSpeed;
        /// <summary>投射物显示高度。</summary>
        public int ProjectileHeight;
        /// <summary>投射物显示范围参数。</summary>
        public int ShowProjectileRadius;
        /// <summary>单位的基础行走速度。</summary>
        public float BaseWalkSpeed;
        /// <summary>投射物的飞行时间参数。</summary>
        public float ProjectileTravel;
        /// <summary>单位拥有的战斗能力标记。</summary>
        public AbilityMask Abilities;
    }
}
