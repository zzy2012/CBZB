using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal sealed class BattleAiSystem : BattleSimSystem
    {
        internal BattleAiSystem(BattleSim sim) : base(sim) { }

        internal void TickAiSecond(PlayerState player)
        {
            SortieDefenders(player);
            BuildAiSupplies(player);
            if (player.FreeSupply > 0 && player.Gold >= BattleSim.AiBuyThreshold)
                BuyAiUnit(player);
            CastAiSpells(player);
            TryAiUltimate(player);
        }

        internal void SortieDefenders(PlayerState player)
        {
            foreach (MapNode node in player.OwnedNodes.Where(value =>
                         IsKeyBuilding(value.Building) && value.Building.IsActive).ToList())
            {
                List<Squad> defenders = player.Squads
                    .Where(squad => !squad.IsDead && squad.HomeNode == node && !squad.HasMoveOrder &&
                                    Vector2.Distance(squad.Pos, node.Pos) <= 60f)
                    .ToList();
                if (defenders.Count <= node.Building.Catch) continue;
                MapNode target = MapSystem.FindNearestHostileNode(node, player);
                if (target != null) MapSystem.OrderMove(defenders[0], target.Pos, target);
            }
        }

        internal void BuildAiSupplies(PlayerState player)
        {
            int buildingCount = player.ActiveBuildings;
            if (player.SupplyCap - player.SupplyUsed <= 0 && player.Gold >= (Cfg.BuildingById(5)?.BuildCost ?? 150))
                EconomySystem.TryBuildFirstType(player, 5);
            if (player.ManaIncome < Mathf.RoundToInt(buildingCount / 3f))
                EconomySystem.TryBuildFirstType(player, 7);
            if (player.BaseGoldIncome < 2 && buildingCount > player.BaseGoldIncome)
                EconomySystem.TryBuildFirstType(player, 6);
        }

        internal void BuyAiUnit(PlayerState player)
        {
            MapNode bestNode = null;
            float bestScore = -999999f;
            List<GameConfig.takeover.Unit> bestCandidates = null;
            foreach (MapNode node in player.OwnedNodes.Where(value =>
                         IsKeyBuilding(value.Building) && value.Building.IsActive))
            {
                float score = BuildingThreat(node);
                score += (float)(0.5d - _rng.NextDouble()) * score / 3f;
                var candidates = Cfg.TbUnit.DataList.Where(unit =>
                        OriginalRules.FromConfigFaction(unit.Faction) == player.Faction &&
                        unit.UnitType <= player.LivingSquads &&
                        BattleEconomySystem.AllowedAt(player, node.Building, unit) &&
                        player.Gold >= BattleEconomySystem.UnitCost(player, unit) &&
                        player.FreeSupply >= unit.Upkeep)
                    .ToList();
                if (candidates.Count == 0 || score < bestScore) continue;
                bestNode = node;
                bestScore = score;
                bestCandidates = candidates;
            }
            if (bestNode != null && bestCandidates.Count > 0)
                EconomySystem.BuyUnit(player, bestCandidates[_rng.Next(bestCandidates.Count)], bestNode, out _);
        }

        internal float BuildingThreat(MapNode node)
        {
            float score = Squads.Count(squad => !squad.IsDead && squad.Faction != node.Building.Owner &&
                                                  Vector2.Distance(squad.Pos, node.Pos) <= 220f) * 20f;
            int defenders = Squads.Count(squad => !squad.IsDead && squad.Faction == node.Building.Owner &&
                                                    squad.HomeNode == node &&
                                                    Vector2.Distance(squad.Pos, node.Pos) <= 60f);
            foreach (MapNode other in Nodes.Where(value => IsKeyBuilding(value.Building) && value.Building.IsActive &&
                                                           value.Building.Owner != node.Building.Owner))
            {
                float distance = Mathf.Max(1f, Vector2.Distance(node.Pos, other.Pos));
                int enemyDefenders = Squads.Count(squad => !squad.IsDead &&
                                                           squad.Faction == other.Building.Owner &&
                                                           squad.HomeNode == other);
                score += 10000f / distance + 10f * (enemyDefenders - defenders);
            }
            return score;
        }

        internal void CastAiSpells(PlayerState player)
        {
            if (!player.SpellsEnabled || player.SpellCooldown > 0f) return;
            foreach (GameConfig.takeover.Spell spell in SpellSystem.AvailableSpells(player)
                         .Where(value => value.SpellType != 2)
                         .OrderBy(value => value.Id))
            {
                int cost = Mathf.RoundToInt(spell.Cost * player.ModMul(35));
                if (cost > player.Mana) continue;
                Vector2 position = FindAiSpellTarget(player, spell);
                if (!HasAiSpellTarget(player, spell, position)) continue;
                if (SpellSystem.TryCastSpell(player, spell, position, MapSystem.NearestKeyNode(position), out _))
                {
                    player.SpellCooldown = OriginalRules.SpellRetrySeconds;
                    break;
                }
            }
        }

        internal Vector2 FindAiSpellTarget(PlayerState player, GameConfig.takeover.Spell spell)
        {
            int id = OriginalRules.OriginalSpellIndex(spell.Id);
            bool beneficial = id == 1 || id == 2 || id == 6 || id == 9 || id == 10 || id == 14;
            IEnumerable<CombatUnit> units = beneficial
                ? Squads.Where(squad => !squad.IsDead && squad.Faction == player.Faction).SelectMany(squad => squad.Units)
                : Squads.Where(squad => !squad.IsDead && squad.Faction != player.Faction &&
                                         squad.Faction != Faction.Bandits).SelectMany(squad => squad.Units);
            CombatUnit best = units.Where(unit => !unit.IsDead)
                .OrderByDescending(unit => beneficial ? 1f - unit.HpRatio : 1f)
                .ThenBy(unit => Vector2.Distance(unit.Pos, player.OwnedNodes.FirstOrDefault()?.Pos ?? Vector2.zero))
                .FirstOrDefault();
            if (best != null) return best.Pos + FormationOffset(0, 1);
            return player.OwnedNodes.FirstOrDefault()?.Pos ??
                   new Vector2(OriginalRules.NativeStageWidth * 0.5f, OriginalRules.NativeStageHeight);
        }

        internal bool HasAiSpellTarget(PlayerState player, GameConfig.takeover.Spell spell, Vector2 position)
        {
            int id = OriginalRules.OriginalSpellIndex(spell.Id);
            bool beneficial = id == 1 || id == 2 || id == 6 || id == 9 || id == 10 || id == 14;
            if (id == 3 || id == 5 || id == 7 || id == 11 || id == 13 || id == 15) return true;
            return Squads.Any(squad => !squad.IsDead &&
                                       (beneficial ? squad.Faction == player.Faction : squad.Faction != player.Faction) &&
                                       Vector2.Distance(squad.Pos, position) <= spell.Radius);
        }

        internal void TryAiUltimate(PlayerState player)
        {
            if (player.Ultimate + 0.0001f < player.UltimateMax || !CanAiCastUltimate(player)) return;
            GameConfig.takeover.Spell ultimate = SpellSystem.AvailableSpells(player).FirstOrDefault(spell => spell.SpellType == 2);
            if (ultimate == null) return;
            Vector2 position = FindAiSpellTarget(player, ultimate);
            SpellSystem.TryCastSpell(player, ultimate, position, MapSystem.NearestKeyNode(position), out _);
        }

        internal bool CanAiCastUltimate(PlayerState player)
        {
            int own = Squads.Count(squad => !squad.IsDead && squad.Faction == player.Faction);
            int enemies = Squads.Count(squad => !squad.IsDead && squad.Faction != player.Faction &&
                                                squad.Faction != Faction.Bandits);
            switch (player.Faction)
            {
                case Faction.Empire:
                    return enemies > 5;
                case Faction.Westaria:
                    return Squads.Count(squad => !squad.IsDead && squad.Faction == player.Faction &&
                                                 !HasMechanism(squad)) >= 3;
                case Faction.Khaganate:
                    return true;
                case Faction.Cult:
                    return enemies > 3 && own < enemies;
                default:
                    return false;
            }
        }
    }
}
