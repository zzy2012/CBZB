using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal sealed class BattleEconomySystem : BattleSimSystem
    {
        internal BattleEconomySystem(BattleSim sim) : base(sim) { }

        internal GameConfig.takeover.Unit FindUnit(Faction faction, UnitKind kind, int tier)
        {
            int configFaction = OriginalRules.ToConfigFaction(faction);
            return Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Faction == configFaction &&
                                                              unit.UnitType == (int)kind &&
                                                              unit.Tier == tier);
        }

        internal bool TryBuildFirstType(PlayerState player, int buildingId)
        {
            MapNode spot = Nodes.FirstOrDefault(node => node.Kind == NodeKind.BuildSpot &&
                                                        node.Building == null &&
                                                        IsLocControlled(player, node.Loc));
            return spot != null && TryBuild(player, spot, buildingId, out _);
        }

        internal bool CanBuy(PlayerState player, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            reason = null;
            if (player == null || def == null || at?.Building == null) { reason = "无效招募目标"; return false; }
            if (!IsKeyBuilding(at.Building)) { reason = "附属建筑不能招募"; return false; }
            if (at.Building.Owner != player.Faction || !at.Building.IsActive) { reason = "不是己方可用建筑"; return false; }
            if (!AllowedAt(player, at.Building, def)) { reason = "该建筑不能生产此兵种"; return false; }
            if (player.Gold < UnitCost(player, def)) { reason = "金币不足"; return false; }
            if (player.FreeSupply < def.Upkeep) { reason = "补给不足"; return false; }
            return true;
        }

        internal static bool AllowedAt(BuildingState building, GameConfig.takeover.Unit def)
        {
            if (building == null || def == null) return false;
            var buildingDef = Cfg.BuildingById(building.Kind);
            return buildingDef != null && buildingDef.AllowedTypes.Contains(def.UnitType) && def.Tier == 1;
        }

        internal static bool AllowedAt(PlayerState player, BuildingState building, GameConfig.takeover.Unit def)
        {
            if (building == null || def == null) return false;
            var buildingDef = Cfg.BuildingById(building.Kind);
            if (buildingDef == null || !buildingDef.AllowedTypes.Contains(def.UnitType) ||
                def.UnitType > (player?.UnitTypeLimit ?? 4))
                return false;
            int selectedTier = Mathf.Clamp(1 + (player?.Mods[TierModifier(def.UnitType)] ?? 0), 1, 3);
            return def.Tier == selectedTier;
        }

        internal Squad BuyUnit(PlayerState player, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            if (!CanBuy(player, def, at, out reason)) return null;
            player.Gold -= UnitCost(player, def);
            int initialXp = at.Building.Level == 0 ? 0 : at.Building.Level == 1 ? 25 : 100;
            Squad squad = SpawnSquad(player, def, at.Pos, at, initialXp, true);
            if (LevelAssetId == 1 && player.IsHuman && def.Name == "Guards" && !_tutorialGuardPurchased)
            {
                _tutorialGuardPurchased = true;
                if (_tutorialEnemySquad != null)
                    MapSystem.OrderMove(_tutorialEnemySquad, new Vector2(340f, 290f * OriginalRules.WorldYScale), null);
            }
            return squad;
        }

        internal static int UnitCost(PlayerState player, GameConfig.takeover.Unit def)
        {
            float multiplier = OriginalRules.UsesUndeadRecruitDiscount(def) ? player.ModMul(28) : 1f;
            return Mathf.RoundToInt(def.Cost * multiplier);
        }

        internal Squad SpawnSquad(PlayerState owner, GameConfig.takeover.Unit def, Vector2 position,
            MapNode home, int initialXp, bool checkSupply)
        {
            if (checkSupply && owner.FreeSupply < def.Upkeep) return null;
            AbilityMask abilities = OriginalRules.ParseAbilities(def.Abilities);
            int count = def.UnitsCount + ((abilities & AbilityMask.Undead) != 0 && def.Name == "Immortals"
                ? owner.Mods[23]
                : 0);
            var squad = new Squad
            {
                Id = Sim.AllocateSquadId(),
                Owner = owner,
                Def = def,
                Abilities = abilities,
                Profile = OriginalRules.UnitProfile(def),
                HomeNode = home,
                AtNode = home,
                TargetPosition = position,
                FormationCenter = position,
                Xp = initialXp * owner.ModMul(37),
            };
            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPosition = position + FormationOffset(i, count);
                var unit = new CombatUnit
                {
                    Id = Sim.AllocateUnitId(),
                    Index = i,
                    Squad = squad,
                    Pos = spawnPosition,
                    MaxHp = UnitHp(owner, def),
                    BaseMaxHp = UnitHp(owner, def),
                    AttackTimer = 0f,
                    MoraleBase = Mathf.Round(def.Morale * owner.ModMul(15)) * 10f,
                    ChargeAvailable = (abilities & AbilityMask.Charge) != 0,
                };
                unit.Hp = unit.MaxHp;
                unit.MoraleCurrent = unit.MoraleBase;
                squad.Units.Add(unit);
            }
            squad.LevelIndex = LevelForXp(squad.Xp);
            ApplyLevel(squad);
            owner.Squads.Add(squad);
            Squads.Add(squad);
            _events.Add(new BattleEvent
            {
                Kind = "SquadSpawned",
                ActorId = owner.Id,
                TargetId = squad.Id,
                Value = def.Id,
                Position = position,
            });
            return squad;
        }

        internal static float UnitHp(PlayerState player, GameConfig.takeover.Unit def)
        {
            float multiplier = 1f;
            if (def.UnitType == (int)UnitKind.Infantry) multiplier *= player.ModMul(16);
            if (def.UnitType == (int)UnitKind.Cavalry) multiplier *= player.ModMul(39);
            return Mathf.Ceil(def.Hp * multiplier);
        }

        internal static void ApplyLevel(Squad squad)
        {
            foreach (CombatUnit unit in squad.Units)
            {
                float hpRatio = unit.MaxHp <= 0f ? 1f : unit.Hp / unit.MaxHp;
                unit.MaxHp = Mathf.Ceil(unit.BaseMaxHp * (1f + squad.LevelIndex * 0.1f));
                unit.Hp = unit.MaxHp * hpRatio;
            }
        }

        internal void AddSquadXp(Squad squad, float amount)
        {
            if (squad == null || squad.IsDead) return;
            squad.Xp += amount * squad.Owner.ModMul(37);
            int level = LevelForXp(squad.Xp);
            if (level <= squad.LevelIndex) return;
            squad.LevelIndex = level;
            ApplyLevel(squad);
        }

        internal bool TryUpgrade(PlayerState player, MapNode at)
        {
            if (at?.Building == null || at.Building.Owner != player.Faction || at.Building.Level >= BuildingState.MaxLevel)
                return false;
            if (!IsKeyBuilding(at.Building)) return false;
            var def = Cfg.BuildingById(at.Building.Kind);
            if (def == null) return false;
            int cost = at.Building.Level == 0 ? def.UpgradeCost1 : def.UpgradeCost2;
            if (player.Gold < cost) return false;
            player.Gold -= cost;
            int before = at.Building.MaxHp;
            at.Building.Level++;
            at.Building.Hp += Mathf.Max(0, at.Building.MaxHp - before);
            return true;
        }

        internal bool CanPlace(PlayerState player, MapNode spot, int buildingId, out string reason)
        {
            reason = null;
            var def = Cfg.BuildingById(buildingId);
            if (player == null || spot == null || spot.Kind != NodeKind.BuildSpot || spot.Building != null || def == null)
            {
                reason = "不是可建造空位";
                return false;
            }
            if (!_humanBuildingsUnlocked.Contains(buildingId) && player.IsHuman)
            {
                reason = "该建筑尚未解锁";
                return false;
            }
            if (!IsLocControlled(player, spot.Loc))
            {
                reason = "不在己方控制范围";
                return false;
            }
            int cost = Mathf.RoundToInt(def.BuildCost * player.ModMul(31));
            if (player.Gold < cost)
            {
                reason = "金币不足";
                return false;
            }
            return true;
        }

        internal bool TryBuild(PlayerState player, MapNode spot, int buildingId, out string reason)
        {
            if (!CanPlace(player, spot, buildingId, out reason)) return false;
            var def = Cfg.BuildingById(buildingId);
            player.Gold -= Mathf.RoundToInt(def.BuildCost * player.ModMul(31));
            spot.Building = new BuildingState
            {
                Node = spot,
                Owner = player.Faction,
                Kind = def.Id,
                BaseHp = def.Hp,
                Hp = def.Hp,
                GoldPerSec = def.Gold,
                ManaPerSec = def.Mana,
                Supply = def.Supply,
                UltimatePerSec = def.Ultimate,
            };
            if (!player.OwnedNodes.Contains(spot)) player.OwnedNodes.Add(spot);
            if (LevelAssetId == 2 && player.IsHuman && def.Id == 5)
                _humanBuildingsUnlocked.UnionWith(new[] { 6, 7 });
            return true;
        }

        internal static int TierModifier(int unitType)
        {
            switch ((UnitKind)unitType)
            {
                case UnitKind.Infantry: return 0;
                case UnitKind.Cavalry: return 1;
                case UnitKind.Archers: return 2;
                case UnitKind.Siege: return 3;
                case UnitKind.Magic: return 4;
                default: return 0;
            }
        }
    }
}
