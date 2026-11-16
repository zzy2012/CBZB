using System;

namespace GameLogic.Takeover
{
    /// <summary>原版 1.0.4 阵营索引，禁止调整顺序。</summary>
    public enum Faction
    {
        /// <summary>Empire 阵营。</summary>
        Empire = 0,
        /// <summary>Westaria 阵营。</summary>
        Westaria = 1,
        /// <summary>Khaganate 阵营。</summary>
        Khaganate = 2,
        /// <summary>Cult 阵营。</summary>
        Cult = 3,
        /// <summary>Bandits 阵营。</summary>
        Bandits = 4,
    }

    /// <summary>兵种的战斗分类，用于匹配单位属性与战斗规则。</summary>
    public enum UnitKind
    {
        /// <summary>步兵。</summary>
        Infantry = 0,
        /// <summary>弓箭兵。</summary>
        Archers = 1,
        /// <summary>骑兵。</summary>
        Cavalry = 2,
        /// <summary>攻城单位。</summary>
        Siege = 3,
        /// <summary>魔法单位。</summary>
        Magic = 4,
    }

    /// <summary>战场地图节点的地形或建筑点类型。</summary>
    public enum NodeKind
    {
        /// <summary>空节点。</summary>
        Empty = 0,
        /// <summary>可建造位置。</summary>
        BuildSpot = 1,
        /// <summary>宝箱位置。</summary>
        Chest = 2,
        /// <summary>食人魔位置。</summary>
        Ogre = 3,
        /// <summary>城堡。</summary>
        Castle = 4,
        /// <summary>城镇。</summary>
        Town = 5,
        /// <summary>营地。</summary>
        Camp = 6,
        /// <summary>法师塔。</summary>
        MageTower = 7,
    }

    /// <summary>战斗单位当前执行的行动状态。</summary>
    public enum UnitAction
    {
        /// <summary>站立待命。</summary>
        Stand,
        /// <summary>正在移动。</summary>
        Moving,
        /// <summary>正在进行近战攻击。</summary>
        Melee,
        /// <summary>正在进行远程攻击。</summary>
        Ranged,
        /// <summary>已经死亡。</summary>
        Dead,
        /// <summary>处于睡眠状态。</summary>
        Sleeping,
        /// <summary>正在修复。</summary>
        Repairing,
    }

    /// <summary>战斗单位的士气等级。</summary>
    public enum MoraleState
    {
        /// <summary>受到恐惧影响。</summary>
        Feared = -1,
        /// <summary>士气处于中性状态。</summary>
        Neutral = 0,
        /// <summary>处于英勇状态。</summary>
        Heroic = 1,
        /// <summary>士气高昂。</summary>
        High = 2,
        /// <summary>士气低落。</summary>
        Low = 3,
    }

    /// <summary>单位或小队拥有的原版战斗能力标记，可按位组合。</summary>
    [Flags]
    public enum AbilityMask
    {
        /// <summary>没有能力标记。</summary>
        None = 0,
        /// <summary>盾墙能力。</summary>
        ShieldWall = 1 << 0,
        /// <summary>冲锋能力。</summary>
        Charge = 1 << 1,
        /// <summary>范围溅射伤害能力。</summary>
        SplashDamage = 1 << 2,
        /// <summary>攻城武器能力。</summary>
        SiegeWeapon = 1 << 3,
        /// <summary>Divine Presence 能力。</summary>
        DivinePresence = 1 << 4,
        /// <summary>施加中毒的能力。</summary>
        Poisonous = 1 << 5,
        /// <summary>威慑能力。</summary>
        Intimidiation = 1 << 6,
        /// <summary>纪律能力。</summary>
        Discipline = 1 << 7,
        /// <summary>机械单位能力。</summary>
        Mechanism = 1 << 8,
        /// <summary>武术能力。</summary>
        MartialArtists = 1 << 9,
        /// <summary>亡灵能力。</summary>
        Undead = 1 << 10,
        /// <summary>恐怖突进能力。</summary>
        HorrificDash = 1 << 11,
        /// <summary>毒素免疫能力。</summary>
        PoisonImmunity = 1 << 12,
        /// <summary>长矛能力。</summary>
        LongSpear = 1 << 13,
        /// <summary>爆炸能力。</summary>
        Explosive = 1 << 14,
        /// <summary>冰冻箭能力。</summary>
        FreezeArrow = 1 << 15,
        /// <summary>冰冻冲击能力。</summary>
        FreezeBlast = 1 << 16,
    }

    /// <summary>法术效果的运行时类别。</summary>
    public enum SpellEffectKind
    {
        /// <summary>Consecration 法术效果。</summary>
        Consecration,
        /// <summary>Heroism 法术效果。</summary>
        Heroism,
        /// <summary>Bloodlust 法术效果。</summary>
        Bloodlust,
        /// <summary>Liberation 法术效果。</summary>
        Liberation,
        /// <summary>Desecration 法术效果。</summary>
        Desecration,
        /// <summary>Frost Enchantment 法术效果。</summary>
        FrostEnchantment,
        /// <summary>Stoneworms 法术效果。</summary>
        Stoneworms,
        /// <summary>Ice Blast 法术效果。</summary>
        IceBlast,
        /// <summary>Cataclysm 法术效果。</summary>
        Cataclysm,
    }
}
