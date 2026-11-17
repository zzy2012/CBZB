using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>处理法术施放和持续效果。</summary>
    internal sealed class BattleSpellSystem : BattleSimSystemBase
    {
        /// <summary>创建并关联当前战斗模拟的法术系统。</summary>
        internal BattleSpellSystem(TakeoverScene sim) : base(sim) { }

        /// <summary>推进持续法术效果并处理到期移除。</summary>
        internal void TickSpellEffects(float dt)
        {
            for (int i = Sim.ActiveSpells.Count - 1; i >= 0; i--)
            {
                ActiveSpellEffectEntity effect = Sim.ActiveSpells[i];
                effect.Runtime.Advance(dt);

                if (effect.Kind == SpellEffectKind.Cataclysm)
                {
                    while (effect.TickTimer <= 0f && effect.ImpactIndex < 90)
                    {
                        TickCataclysmImpact(effect);
                        effect.ImpactIndex++;
                        effect.TickTimer += 0.065f;
                    }
                }
                else if (effect.TickTimer <= 0f)
                {
                    ApplySpellEffect(effect);
                    effect.TickTimer += 0.5f;
                }

                if (effect.Remaining <= 0f) Sim.RemoveSpellEffect(effect);
            }
        }

        /// <summary>将指定法术效果应用到模拟中的目标。</summary>
        internal void ApplySpellEffect(ActiveSpellEffectEntity effect)
        {
            float radiusSquared = effect.Def.Radius * effect.Def.Radius;
            switch (effect.Kind)
            {
                case SpellEffectKind.Consecration:
                case SpellEffectKind.Desecration:
                    foreach (SquadEntity squad in Sim.Squads.Where(value => !value.IsDead && value.Faction == effect.Faction))
                    {
                        if (TakeoverScene.HasMechanism(squad)) continue;
                        foreach (CombatUnitEntity unit in squad.Units)
                        {
                            if (Vector2.SqrMagnitude(unit.Pos - effect.Center) > radiusSquared) continue;
                            if (unit.Hp <= 0f)
                            {
                                unit.Hp = Mathf.Min(unit.MaxHp, effect.Def.Power);
                                unit.Action = UnitAction.Stand;
                            }
                            else
                            {
                                unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + effect.Def.Power);
                            }
                        }
                    }
                    break;
                case SpellEffectKind.Heroism:
                    foreach (CombatUnitEntity unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        if (TakeoverScene.HasMoraleLock(unit.Squad)) continue;
                        unit.HeroicSeconds = Mathf.Max(unit.HeroicSeconds, 10f);
                        unit.FearedSeconds = 0f;
                        unit.MoraleCurrent = unit.MoraleBase;
                    }
                    break;
                case SpellEffectKind.Bloodlust:
                    foreach (CombatUnitEntity unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        unit.DamageBuffSeconds = 0.75f;
                        unit.DamageBuffPercent = Mathf.Max(0f, (effect.Def.Power - 1f) * 100f);
                    }
                    break;
                case SpellEffectKind.Liberation:
                    foreach (CombatUnitEntity unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                        unit.MagicImmunitySeconds = 0.75f;
                    break;
                case SpellEffectKind.FrostEnchantment:
                    foreach (CombatUnitEntity unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        unit.MaxDamageSeconds = 0.75f;
                        unit.DamageResistSeconds = 0.75f;
                        unit.DamageResist = 25f;
                    }
                    break;
                case SpellEffectKind.Stoneworms:
                case SpellEffectKind.IceBlast:
                    if (effect.ImpactIndex++ > 0) break;
                    foreach (CombatUnitEntity unit in HostileUnits(effect.Faction, effect.Center, radiusSquared, false))
                    {
                        if (effect.Kind == SpellEffectKind.IceBlast)
                        {
                            if (unit.MagicImmunitySeconds <= 0f)
                                unit.FrozenSeconds = Mathf.Max(unit.FrozenSeconds, effect.Def.Time);
                        }
                        else
                            Sim.CombatSystem.ApplyDamage(unit, Mathf.Ceil((effect.Def.Power * 0.5f) +
                                (float)Sim.RandomSource.NextDouble() * effect.Def.Power * 0.5f), effect.Owner?.Squads.FirstOrDefault(), true);
                    }
                    break;
            }
        }

        /// <summary>推进 Cataclysm 的延迟冲击并结算命中。</summary>
        internal void TickCataclysmImpact(ActiveSpellEffectEntity effect)
        {
            Vector2 point = new Vector2((float)Sim.RandomSource.NextDouble() * OriginalRules.NativeStageWidth,
                (float)Sim.RandomSource.NextDouble() * OriginalRules.NativeStageHeight * OriginalRules.WorldYScale);
            float damage = 4f + (float)Sim.RandomSource.NextDouble() * 4f;
            foreach (CombatUnitEntity unit in Sim.Squads.SelectMany(squad => squad.Units).Where(unit => !unit.IsDead).ToList())
            {
                if (Vector2.Distance(unit.Pos, point) > effect.Def.Radius) continue;
                float applied = unit.Squad.Faction == effect.Faction ? damage * 0.5f : damage;
                Sim.CombatSystem.ApplyDamage(unit, applied, effect.Owner?.Squads.FirstOrDefault(), true);
            }
            foreach (MapNodeEntity node in Sim.Nodes.Where(value => value.Building != null && value.Building.Hp > 0))
            {
                if (Vector2.Distance(node.Pos, point) <= effect.Def.Radius)
                    Sim.CombatSystem.ApplyBuildingDamage(node.Building, damage * 0.5f, effect.Owner?.Squads.FirstOrDefault());
            }
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = "CataclysmImpact",
                ActorId = effect.Owner?.Id ?? -1,
                TargetId = effect.ImpactIndex,
                Value = damage,
                Position = point,
            });
        }

        /// <summary>获取指定参与方控制的存活士兵。</summary>
        internal IEnumerable<CombatUnitEntity> AlliedUnits(Faction faction, Vector2 center, float radiusSquared)
        {
            return Sim.Squads.Where(squad => !squad.IsDead && squad.Faction == faction)
                .SelectMany(squad => squad.Units)
                .Where(unit => !unit.IsDead && Vector2.SqrMagnitude(unit.Pos - center) <= radiusSquared);
        }

        /// <summary>获取与指定参与方敌对的存活士兵。</summary>
        internal IEnumerable<CombatUnitEntity> HostileUnits(Faction faction, Vector2 center, float radiusSquared, bool excludeBandits)
        {
            return Sim.Squads.Where(squad => !squad.IsDead && squad.Faction != faction &&
                                         (!excludeBandits || squad.Faction != Faction.Bandits))
                .SelectMany(squad => squad.Units)
                .Where(unit => !unit.IsDead && Vector2.SqrMagnitude(unit.Pos - center) <= radiusSquared);
        }

        /// <summary>检查条件并尝试对指定目标施放法术。</summary>
        internal bool TryCastSpell(PlayerStateEntity player, GameConfig.takeover.Spell spell,
            MapNodeEntity target, out string reason)
        {
            return TryCastSpell(player, spell, target?.Pos ?? Vector2.zero, target, out reason);
        }

        /// <summary>检查条件并尝试对指定目标施放法术。</summary>
        internal bool TryCastSpell(PlayerStateEntity player, GameConfig.takeover.Spell spell,
            Vector2 position, MapNodeEntity target, out string reason)
        {
            reason = null;
            if (player == null || spell == null) { reason = "无效法术"; return false; }
            if (OriginalRules.FromConfigFaction(spell.Faction) != player.Faction) { reason = "阵营不匹配"; return false; }
            if (!OriginalRules.IsSpellUnlocked(player, spell.Id)) { reason = "法术尚未解锁"; return false; }

            bool ultimate = spell.SpellType == 2;
            if (ultimate)
            {
                if (player.Ultimate + 0.0001f < player.UltimateMax) { reason = "终极技能尚未充满"; return false; }
                if (!player.IsHuman && !Sim.AiSystem.CanAiCastUltimate(player)) { reason = "终极技能条件不满足"; return false; }
                player.Ultimate = 0f;
            }
            else
            {
                int cost = Mathf.RoundToInt(spell.Cost * player.ModMul(35));
                if (player.Mana < cost) { reason = "法力不足"; return false; }
                player.Mana -= cost;
            }

            CastSpellEffect(player, spell, position, target);
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = ultimate ? "UltimateCast" : "SpellCast",
                ActorId = player.Id,
                TargetId = target?.Id ?? -1,
                Value = spell.Id,
                Position = position,
                Text = DescribeSpellText(OriginalRules.OriginalSpellIndex(spell.Id), spell),
            });
            return true;
        }

        /// <summary>给表现层用的效果说明（施法播报里显示「谁、放了什么、干了什么」）。</summary>
        internal static string DescribeSpellText(int originalId, GameConfig.takeover.Spell spell)
        {
            switch (originalId)
            {
                case 0:
                    return $"对范围内敌人造成 {Mathf.RoundToInt(spell.Power)}% 生命伤害";
                case 1:
                    return $"范围内友军持续回复 {Mathf.RoundToInt(spell.Power)} 生命";
                case 2:
                    return "范围内友军士气回满，持续 10 秒";
                case 3:
                    return "召唤化身参战";
                case 4:
                    return $"召唤石虫，对范围内敌人造成 {Mathf.RoundToInt(spell.Power)} 伤害";
                case 5:
                    return "召唤不朽者参战";
                case 6:
                    return $"范围内友军伤害 +{Mathf.RoundToInt((spell.Power - 1f) * 100f)}%";
                case 7:
                    return $"全屏天灾落雷，持续 {spell.Time:0.#} 秒";
                case 8:
                    return $"冰冻范围内敌人 {spell.Time:0.#} 秒";
                case 9:
                    return $"范围内友军减伤 25%，持续 {spell.Time:0.#} 秒";
                case 10:
                    return $"范围内友军免疫法术，持续 {spell.Time:0.#} 秒";
                case 11:
                    return "全军变身强化";
                case 12:
                    return "使范围内敌人恐惧 10 秒";
                case 13:
                    return "召唤亡灵射手/战士参战";
                case 14:
                    return "范围内友军持续回血";
                case 15:
                    return "召唤亡者参战";
                default:
                    return $"范围 {Mathf.RoundToInt(spell.Radius)} 的效果";
            }
        }

        /// <summary>创建法术运行时效果并执行即时影响。</summary>
        internal void CastSpellEffect(PlayerStateEntity player, GameConfig.takeover.Spell spell, Vector2 position, MapNodeEntity target)
        {
            int originalId = OriginalRules.OriginalSpellIndex(spell.Id);
            switch (originalId)
            {
                case 0:
                    foreach (CombatUnitEntity unit in HostileUnits(player.Faction, position,
                                 spell.Radius * spell.Radius, false))
                    {
                        float percent = spell.Power * 0.5f + (float)Sim.RandomSource.NextDouble() * spell.Power * 0.5f;
                        percent *= player.ModMul(10);
                        Sim.CombatSystem.ApplyDamage(unit, Mathf.Ceil(unit.MaxHp * percent / 100f), null, true);
                    }
                    break;
                case 1:
                case 2:
                case 6:
                case 10:
                case 14:
                    Sim.AddSpellEffect(new ActiveSpellEffectEntity
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = spell.Time * player.ModMul(6),
                        TickTimer = 0f,
                        Kind = originalId == 1 ? SpellEffectKind.Consecration :
                            originalId == 2 ? SpellEffectKind.Heroism :
                            originalId == 6 ? SpellEffectKind.Bloodlust :
                            originalId == 10 ? SpellEffectKind.Liberation : SpellEffectKind.Desecration,
                    });
                    ApplySpellEffect(Sim.ActiveSpells[Sim.ActiveSpells.Count - 1]);
                    break;
                case 3:
                    SpawnSpecialUnit(player, "Avatar", position);
                    break;
                case 4:
                    Sim.AddSpellEffect(new ActiveSpellEffectEntity
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = 1f + player.Mods[21],
                        TickTimer = 1f,
                        Kind = SpellEffectKind.Stoneworms,
                    });
                    break;
                case 5:
                    SpawnSpecialUnit(player, "Immortals", target?.Pos ?? position);
                    break;
                case 7:
                    Sim.AddSpellEffect(new ActiveSpellEffectEntity
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = 6.5f,
                        TickTimer = 0f,
                        Kind = SpellEffectKind.Cataclysm,
                    });
                    break;
                case 8:
                    Sim.AddSpellEffect(new ActiveSpellEffectEntity
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = 1f,
                        TickTimer = 0.75f,
                        Kind = SpellEffectKind.IceBlast,
                    });
                    break;
                case 9:
                    Sim.AddSpellEffect(new ActiveSpellEffectEntity
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = spell.Time,
                        TickTimer = 0f,
                        Kind = SpellEffectKind.FrostEnchantment,
                    });
                    ApplySpellEffect(Sim.ActiveSpells[Sim.ActiveSpells.Count - 1]);
                    break;
                case 11:
                    ApplyGrandTransformation(player);
                    break;
                case 12:
                    foreach (CombatUnitEntity unit in HostileUnits(player.Faction, position,
                                 spell.Radius * spell.Radius, true))
                    {
                        if (unit.MagicImmunitySeconds > 0f || TakeoverScene.HasFearLock(unit.Squad)) continue;
                        unit.HeroicSeconds = 0f;
                        unit.FearedSeconds = Mathf.Max(unit.FearedSeconds, 10f);
                        unit.MoraleCurrent = 0f;
                    }
                    break;
                case 13:
                    bool archer = Sim.RandomSource.NextDouble() > 0.5d;
                    SpawnSpecialUnit(player, archer ? "Undying Archers" : "Undying Warriors", target?.Pos ?? position);
                    break;
                case 15:
                    CallToTheGrave(player);
                    break;
            }
        }

        /// <summary>在指定位置创建法术召唤的特殊单位。</summary>
        internal void SpawnSpecialUnit(PlayerStateEntity player, string unitName, Vector2 position)
        {
            GameConfig.takeover.Unit def = Cfg.TbUnit.DataList.FirstOrDefault(unit =>
                unit.Name == unitName && OriginalRules.FromConfigFaction(unit.Faction) == player.Faction);
            if (def == null && unitName == "Avatar")
                def = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Avatar");
            if (def == null) return;
            MapNodeEntity node = Sim.MapSystem.NearestKeyNode(position);
            Sim.EconomySystem.SpawnSquad(player, def, position, node, 0, false);
        }

        /// <summary>对目标部队应用 Grand Transformation 效果。</summary>
        internal void ApplyGrandTransformation(PlayerStateEntity player)
        {
            bool kill = true;
            foreach (CombatUnitEntity unit in Sim.Squads.Where(squad => squad.Faction != player.Faction &&
                                                              squad.Faction != Faction.Bandits)
                         .SelectMany(squad => squad.Units).ToList())
            {
                if (unit.IsDead || !kill) { kill = !kill; continue; }
                kill = !kill;
                Sim.CombatSystem.ApplyDamage(unit, unit.Hp, null, true);
                if (!unit.IsDead) continue;
                if (Sim.RandomSource.NextDouble() > 0.5d) player.Mana += 20f;
                else player.Gold += 20f;
            }
        }

        /// <summary>执行 Call to the Grave 法术的单位复活效果。</summary>
        internal void CallToTheGrave(PlayerStateEntity player)
        {
            List<SquadEntity> candidates = Sim.Squads.Where(squad => !squad.IsDead &&
                                                           squad.Faction != player.Faction &&
                                                           squad.Faction != Faction.Bandits)
                .ToList();
            for (int i = 0; i < 2 && candidates.Count > 0; i++)
            {
                SquadEntity source = candidates[Sim.RandomSource.Next(candidates.Count)];
                int tier = Mathf.Clamp(1 + player.Mods[BattleEconomySystem.TierModifier(source.Def.UnitType)], 1, 3);
                GameConfig.takeover.Unit undead = Cfg.TbUnit.DataList.FirstOrDefault(unit =>
                    OriginalRules.FromConfigFaction(unit.Faction) == player.Faction &&
                    unit.UnitType == source.Def.UnitType && unit.Tier == tier);
                if (undead == null) break;
                Vector2 position = source.Pos;
                int xp = (int)source.Xp;
                source.Owner.Squads.Remove(source);
                Sim.Squads.Remove(source);
                Sim.RemoveSquadEntities(source);
                MapNodeEntity home = player.OwnedNodes.FirstOrDefault(node => node.Building != null);
                SquadEntity created = Sim.EconomySystem.SpawnSquad(player, undead, position, home, xp, false);
                MapNodeEntity targetNode = Sim.MapSystem.FindNearestHostileNode(home ?? created.AtNode, player);
                if (targetNode != null) Sim.MapSystem.OrderMove(created, targetNode.Pos, targetNode);
                candidates.Remove(source);
            }
        }

        /// <summary>获取参与方当前满足解锁条件的法术。</summary>
        internal List<GameConfig.takeover.Spell> AvailableSpells(PlayerStateEntity player)
        {
            return Cfg.TbSpell.DataList
                .Where(spell => OriginalRules.FromConfigFaction(spell.Faction) == player.Faction)
                .Where(spell => OriginalRules.IsSpellUnlocked(player, spell.Id))
                .OrderBy(spell => spell.SpellType)
                .ThenBy(spell => spell.Id)
                .ToList();
        }
    }
}
