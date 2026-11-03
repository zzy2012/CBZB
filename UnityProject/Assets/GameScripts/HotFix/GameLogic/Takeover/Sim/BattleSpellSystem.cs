using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal sealed class BattleSpellSystem : BattleSimSystem
    {
        internal BattleSpellSystem(BattleSim sim) : base(sim) { }

        internal void TickSpellEffects(float dt)
        {
            for (int i = ActiveSpells.Count - 1; i >= 0; i--)
            {
                ActiveSpellEffect effect = ActiveSpells[i];
                effect.Remaining -= dt;
                effect.TickTimer -= dt;

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

                if (effect.Remaining <= 0f) ActiveSpells.RemoveAt(i);
            }
        }

        internal void ApplySpellEffect(ActiveSpellEffect effect)
        {
            float radiusSquared = effect.Def.Radius * effect.Def.Radius;
            switch (effect.Kind)
            {
                case SpellEffectKind.Consecration:
                case SpellEffectKind.Desecration:
                    foreach (Squad squad in Squads.Where(value => !value.IsDead && value.Faction == effect.Faction))
                    {
                        if (HasMechanism(squad)) continue;
                        foreach (CombatUnit unit in squad.Units)
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
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        if (HasMoraleLock(unit.Squad)) continue;
                        unit.HeroicSeconds = Mathf.Max(unit.HeroicSeconds, 10f);
                        unit.FearedSeconds = 0f;
                        unit.MoraleCurrent = unit.MoraleBase;
                    }
                    break;
                case SpellEffectKind.Bloodlust:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        unit.DamageBuffSeconds = 0.75f;
                        unit.DamageBuffPercent = Mathf.Max(0f, (effect.Def.Power - 1f) * 100f);
                    }
                    break;
                case SpellEffectKind.Liberation:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                        unit.MagicImmunitySeconds = 0.75f;
                    break;
                case SpellEffectKind.FrostEnchantment:
                    foreach (CombatUnit unit in AlliedUnits(effect.Faction, effect.Center, radiusSquared))
                    {
                        unit.MaxDamageSeconds = 0.75f;
                        unit.DamageResistSeconds = 0.75f;
                        unit.DamageResist = 25f;
                    }
                    break;
                case SpellEffectKind.Stoneworms:
                case SpellEffectKind.IceBlast:
                    if (effect.ImpactIndex++ > 0) break;
                    foreach (CombatUnit unit in HostileUnits(effect.Faction, effect.Center, radiusSquared, false))
                    {
                        if (effect.Kind == SpellEffectKind.IceBlast)
                        {
                            if (unit.MagicImmunitySeconds <= 0f)
                                unit.FrozenSeconds = Mathf.Max(unit.FrozenSeconds, effect.Def.Time);
                        }
                        else
                            CombatSystem.ApplyDamage(unit, Mathf.Ceil((effect.Def.Power * 0.5f) +
                                (float)_rng.NextDouble() * effect.Def.Power * 0.5f), effect.Owner?.Squads.FirstOrDefault(), true);
                    }
                    break;
            }
        }

        internal void TickCataclysmImpact(ActiveSpellEffect effect)
        {
            Vector2 point = new Vector2((float)_rng.NextDouble() * OriginalRules.NativeStageWidth,
                (float)_rng.NextDouble() * OriginalRules.NativeStageHeight * OriginalRules.WorldYScale);
            float damage = 4f + (float)_rng.NextDouble() * 4f;
            foreach (CombatUnit unit in Squads.SelectMany(squad => squad.Units).Where(unit => !unit.IsDead).ToList())
            {
                if (Vector2.Distance(unit.Pos, point) > effect.Def.Radius) continue;
                float applied = unit.Squad.Faction == effect.Faction ? damage * 0.5f : damage;
                CombatSystem.ApplyDamage(unit, applied, effect.Owner?.Squads.FirstOrDefault(), true);
            }
            foreach (MapNode node in Nodes.Where(value => value.Building != null && value.Building.Hp > 0))
            {
                if (Vector2.Distance(node.Pos, point) <= effect.Def.Radius)
                    CombatSystem.ApplyBuildingDamage(node.Building, damage * 0.5f, effect.Owner?.Squads.FirstOrDefault());
            }
            _events.Add(new BattleEvent
            {
                Kind = "CataclysmImpact",
                ActorId = effect.Owner?.Id ?? -1,
                TargetId = effect.ImpactIndex,
                Value = damage,
                Position = point,
            });
        }

        internal IEnumerable<CombatUnit> AlliedUnits(Faction faction, Vector2 center, float radiusSquared)
        {
            return Squads.Where(squad => !squad.IsDead && squad.Faction == faction)
                .SelectMany(squad => squad.Units)
                .Where(unit => !unit.IsDead && Vector2.SqrMagnitude(unit.Pos - center) <= radiusSquared);
        }

        internal IEnumerable<CombatUnit> HostileUnits(Faction faction, Vector2 center, float radiusSquared, bool excludeBandits)
        {
            return Squads.Where(squad => !squad.IsDead && squad.Faction != faction &&
                                         (!excludeBandits || squad.Faction != Faction.Bandits))
                .SelectMany(squad => squad.Units)
                .Where(unit => !unit.IsDead && Vector2.SqrMagnitude(unit.Pos - center) <= radiusSquared);
        }

        internal bool TryCastSpell(PlayerState player, GameConfig.takeover.Spell spell,
            MapNode target, out string reason)
        {
            return TryCastSpell(player, spell, target?.Pos ?? Vector2.zero, target, out reason);
        }

        internal bool TryCastSpell(PlayerState player, GameConfig.takeover.Spell spell,
            Vector2 position, MapNode target, out string reason)
        {
            reason = null;
            if (player == null || spell == null) { reason = "无效法术"; return false; }
            if (OriginalRules.FromConfigFaction(spell.Faction) != player.Faction) { reason = "阵营不匹配"; return false; }
            if (!OriginalRules.IsSpellUnlocked(player, spell.Id)) { reason = "法术尚未解锁"; return false; }

            bool ultimate = spell.SpellType == 2;
            if (ultimate)
            {
                if (player.Ultimate + 0.0001f < player.UltimateMax) { reason = "终极技能尚未充满"; return false; }
                if (!player.IsHuman && !AiSystem.CanAiCastUltimate(player)) { reason = "终极技能条件不满足"; return false; }
                player.Ultimate = 0f;
            }
            else
            {
                int cost = Mathf.RoundToInt(spell.Cost * player.ModMul(35));
                if (player.Mana < cost) { reason = "法力不足"; return false; }
                player.Mana -= cost;
            }

            CastSpellEffect(player, spell, position, target);
            _events.Add(new BattleEvent
            {
                Kind = ultimate ? "UltimateCast" : "SpellCast",
                ActorId = player.Id,
                TargetId = target?.Id ?? -1,
                Value = spell.Id,
                Position = position,
            });
            return true;
        }

        internal void CastSpellEffect(PlayerState player, GameConfig.takeover.Spell spell, Vector2 position, MapNode target)
        {
            int originalId = OriginalRules.OriginalSpellIndex(spell.Id);
            switch (originalId)
            {
                case 0:
                    foreach (CombatUnit unit in HostileUnits(player.Faction, position,
                                 spell.Radius * spell.Radius, false))
                    {
                        float percent = spell.Power * 0.5f + (float)_rng.NextDouble() * spell.Power * 0.5f;
                        percent *= player.ModMul(10);
                        CombatSystem.ApplyDamage(unit, Mathf.Ceil(unit.MaxHp * percent / 100f), null, true);
                    }
                    break;
                case 1:
                case 2:
                case 6:
                case 10:
                case 14:
                    ActiveSpells.Add(new ActiveSpellEffect
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
                    ApplySpellEffect(ActiveSpells[ActiveSpells.Count - 1]);
                    break;
                case 3:
                    SpawnSpecialUnit(player, "Avatar", position);
                    break;
                case 4:
                    ActiveSpells.Add(new ActiveSpellEffect
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
                    ActiveSpells.Add(new ActiveSpellEffect
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
                    ActiveSpells.Add(new ActiveSpellEffect
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
                    ActiveSpells.Add(new ActiveSpellEffect
                    {
                        Def = spell,
                        Owner = player,
                        Faction = player.Faction,
                        Center = position,
                        Remaining = spell.Time,
                        TickTimer = 0f,
                        Kind = SpellEffectKind.FrostEnchantment,
                    });
                    ApplySpellEffect(ActiveSpells[ActiveSpells.Count - 1]);
                    break;
                case 11:
                    ApplyGrandTransformation(player);
                    break;
                case 12:
                    foreach (CombatUnit unit in HostileUnits(player.Faction, position,
                                 spell.Radius * spell.Radius, true))
                    {
                        if (unit.MagicImmunitySeconds > 0f || HasFearLock(unit.Squad)) continue;
                        unit.HeroicSeconds = 0f;
                        unit.FearedSeconds = Mathf.Max(unit.FearedSeconds, 10f);
                        unit.MoraleCurrent = 0f;
                    }
                    break;
                case 13:
                    bool archer = _rng.NextDouble() > 0.5d;
                    SpawnSpecialUnit(player, archer ? "Undying Archers" : "Undying Warriors", target?.Pos ?? position);
                    break;
                case 15:
                    CallToTheGrave(player);
                    break;
            }
        }

        internal void SpawnSpecialUnit(PlayerState player, string unitName, Vector2 position)
        {
            GameConfig.takeover.Unit def = Cfg.TbUnit.DataList.FirstOrDefault(unit =>
                unit.Name == unitName && OriginalRules.FromConfigFaction(unit.Faction) == player.Faction);
            if (def == null && unitName == "Avatar")
                def = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Avatar");
            if (def == null) return;
            MapNode node = MapSystem.NearestKeyNode(position);
            EconomySystem.SpawnSquad(player, def, position, node, 0, false);
        }

        internal void ApplyGrandTransformation(PlayerState player)
        {
            bool kill = true;
            foreach (CombatUnit unit in Squads.Where(squad => squad.Faction != player.Faction &&
                                                              squad.Faction != Faction.Bandits)
                         .SelectMany(squad => squad.Units).ToList())
            {
                if (unit.IsDead || !kill) { kill = !kill; continue; }
                kill = !kill;
                CombatSystem.ApplyDamage(unit, unit.Hp, null, true);
                if (!unit.IsDead) continue;
                if (_rng.NextDouble() > 0.5d) player.Mana += 20f;
                else player.Gold += 20f;
            }
        }

        internal void CallToTheGrave(PlayerState player)
        {
            List<Squad> candidates = Squads.Where(squad => !squad.IsDead &&
                                                           squad.Faction != player.Faction &&
                                                           squad.Faction != Faction.Bandits)
                .ToList();
            for (int i = 0; i < 2 && candidates.Count > 0; i++)
            {
                Squad source = candidates[_rng.Next(candidates.Count)];
                int tier = Mathf.Clamp(1 + player.Mods[BattleEconomySystem.TierModifier(source.Def.UnitType)], 1, 3);
                GameConfig.takeover.Unit undead = Cfg.TbUnit.DataList.FirstOrDefault(unit =>
                    OriginalRules.FromConfigFaction(unit.Faction) == player.Faction &&
                    unit.UnitType == source.Def.UnitType && unit.Tier == tier);
                if (undead == null) break;
                Vector2 position = source.Pos;
                int xp = (int)source.Xp;
                source.Owner.Squads.Remove(source);
                Squads.Remove(source);
                MapNode home = player.OwnedNodes.FirstOrDefault(node => node.Building != null);
                Squad created = EconomySystem.SpawnSquad(player, undead, position, home, xp, false);
                MapNode targetNode = MapSystem.FindNearestHostileNode(home ?? created.AtNode, player);
                if (targetNode != null) MapSystem.OrderMove(created, targetNode.Pos, targetNode);
                candidates.Remove(source);
            }
        }

        internal List<GameConfig.takeover.Spell> AvailableSpells(PlayerState player)
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
