using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>处理资源、招募和建筑经济规则。</summary>
    internal sealed class BattleEconomySystem : BattleSimSystemBase
    {
        /// <summary>创建并关联当前战斗模拟的经济系统。</summary>
        internal BattleEconomySystem(TakeoverScene sim) : base(sim) { }

        /// <summary>按配置标识查找单位定义。</summary>
        internal GameConfig.takeover.Unit FindUnit(Faction faction, UnitKind kind, int tier)
        {
            int configFaction = OriginalRules.ToConfigFaction(faction);
            return Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Faction == configFaction &&
                                                              unit.UnitType == (int)kind &&
                                                              unit.Tier == tier);
        }

        /// <summary>尝试为参与方建造首个可用建筑类型。</summary>
        internal bool TryBuildFirstType(PlayerStateEntity player, int buildingId)
        {
            MapNodeEntity spot = Sim.Nodes.FirstOrDefault(node => node.Kind == NodeKind.BuildSpot &&
                                                        node.Building == null &&
                                                        TakeoverScene.IsLocControlled(player, node.Loc));
            return spot != null && TryBuild(player, spot, buildingId, out _);
        }

        /// <summary>检查单位招募条件并返回失败原因。</summary>
        internal bool CanBuy(PlayerStateEntity player, GameConfig.takeover.Unit def, MapNodeEntity at, out string reason)
        {
            reason = null;
            if (player == null || def == null || at?.Building == null) { reason = "无效招募目标"; return false; }
            if (!TakeoverScene.IsKeyBuilding(at.Building)) { reason = "附属建筑不能招募"; return false; }
            if (at.Building.Owner != player.Faction || !at.Building.IsActive) { reason = "不是己方可用建筑"; return false; }
            if (!AllowedAt(player, at.Building, def)) { reason = "该建筑不能生产此兵种"; return false; }
            if (player.Gold < UnitCost(player, def)) { reason = "金币不足"; return false; }
            if (player.FreeSupply < def.Upkeep) { reason = "补给不足"; return false; }
            return true;
        }

        /// <summary>判断当前建筑是否允许招募指定单位。</summary>
        internal static bool AllowedAt(BuildingState building, GameConfig.takeover.Unit def)
        {
            if (building == null || def == null) return false;
            var buildingDef = Cfg.BuildingById(building.Kind);
            return buildingDef != null && buildingDef.AllowedTypes.Contains(def.UnitType) && def.Tier == 1;
        }

        /// <summary>判断当前建筑是否允许招募指定单位。</summary>
        internal static bool AllowedAt(PlayerStateEntity player, BuildingState building, GameConfig.takeover.Unit def)
        {
            if (building == null || def == null) return false;
            var buildingDef = Cfg.BuildingById(building.Kind);
            if (buildingDef == null || !buildingDef.AllowedTypes.Contains(def.UnitType) ||
                def.UnitType > (player?.UnitTypeLimit ?? 4))
                return false;
            int selectedTier = Mathf.Clamp(1 + (player?.Mods[TierModifier(def.UnitType)] ?? 0), 1, 3);
            return def.Tier == selectedTier;
        }

        /// <summary>扣除招募资源并创建指定单位部队。</summary>
        internal SquadEntity BuyUnit(PlayerStateEntity player, GameConfig.takeover.Unit def, MapNodeEntity at, out string reason)
        {
            if (!CanBuy(player, def, at, out reason)) return null;
            player.Gold -= UnitCost(player, def);
            int initialXp = at.Building.Level == 0 ? 0 : at.Building.Level == 1 ? 25 : 100;
            SquadEntity squad = SpawnSquad(player, def, at.Pos, at, initialXp, true);
            return squad;
        }

        /// <summary>计算考虑折扣修正后的单位招募费用。</summary>
        internal static int UnitCost(PlayerStateEntity player, GameConfig.takeover.Unit def)
        {
            float multiplier = OriginalRules.UsesUndeadRecruitDiscount(def) ? player.ModMul(28) : 1f;
            return Mathf.RoundToInt(def.Cost * multiplier);
        }

        /// <summary>按单位定义在指定节点创建部队和士兵。</summary>
        internal SquadEntity SpawnSquad(PlayerStateEntity owner, GameConfig.takeover.Unit def, Vector2 position,
            MapNodeEntity home, int initialXp, bool checkSupply)
        {
            if (checkSupply && owner.FreeSupply < def.Upkeep) return null;
            AbilityMask abilities = OriginalRules.ParseAbilities(def.Abilities);
            int count = def.UnitsCount + ((abilities & AbilityMask.Undead) != 0 && def.Name == "Immortals"
                ? owner.Mods[23]
                : 0);
            var squad = new SquadEntity
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
                Vector2 spawnPosition = position + Sim.FormationSystem.FormationOffset(i, count);
                var unit = new CombatUnitEntity
                {
                    Id = Sim.AllocateUnitId(),
                    Index = i,
                    Squad = squad,
                    Pos = spawnPosition,
                    MaxHp = UnitHp(owner, def),
                    BaseMaxHp = UnitHp(owner, def),
                    AttackTimer = 0f,
                    MoraleBase = Mathf.Round(def.Morale * owner.ModMul(15)) * 10f,
                    ChargeAvailable = (abilities & AbilityMask.Charge) != 0,//
                };
                unit.Hp = unit.MaxHp;
                unit.MoraleCurrent = unit.MoraleBase;
                squad.Units.Add(unit);
                Sim.AddEntity(BattleEntityType.Unit, unit);
            }
            squad.LevelIndex = TakeoverScene.LevelForXp(squad.Xp);
            ApplyLevel(squad);
            owner.Squads.Add(squad);
            Sim.Squads.Add(squad);
            Sim.AddEntity(BattleEntityType.Squad, squad);
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = "SquadSpawned",
                ActorId = owner.Id,
                TargetId = squad.Id,
                Value = def.Id,
                Position = position,
            });
            return squad;
        }

        /// <summary>计算单位当前等级下的生命值。</summary>
        internal static float UnitHp(PlayerStateEntity player, GameConfig.takeover.Unit def)
        {
            float multiplier = 1f;
            if (def.UnitType == (int)UnitKind.Infantry) multiplier *= player.ModMul(16);
            if (def.UnitType == (int)UnitKind.Cavalry) multiplier *= player.ModMul(39);
            return Mathf.Ceil(def.Hp * multiplier);
        }

        /// <summary>按部队等级更新士兵属性。</summary>
        internal static void ApplyLevel(SquadEntity squad)
        {
            foreach (CombatUnitEntity unit in squad.Units)
            {
                float hpRatio = unit.MaxHp <= 0f ? 1f : unit.Hp / unit.MaxHp;
                unit.MaxHp = Mathf.Ceil(unit.BaseMaxHp * (1f + squad.LevelIndex * 0.1f));
                unit.Hp = unit.MaxHp * hpRatio;
            }
        }

        /// <summary>增加部队经验并应用升级效果。</summary>
        internal void AddSquadXp(SquadEntity squad, float amount)
        {
            if (squad == null || squad.IsDead) return;
            squad.Xp += amount * squad.Owner.ModMul(37);
            int level = TakeoverScene.LevelForXp(squad.Xp);
            if (level <= squad.LevelIndex) return;
            squad.LevelIndex = level;
            ApplyLevel(squad);
        }

        /// <summary>检查条件并尝试升级节点上的建筑。</summary>
        internal bool TryUpgrade(PlayerStateEntity player, MapNodeEntity at)
        {
            if (at?.Building == null || at.Building.Owner != player.Faction || at.Building.Level >= BuildingState.MaxLevel)
                return false;
            if (!TakeoverScene.IsKeyBuilding(at.Building)) return false;
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

        /// <summary>检查建筑能否由参与方建造在指定节点。</summary>
        internal bool CanPlace(PlayerStateEntity player, MapNodeEntity spot, int buildingId, out string reason)
        {
            reason = null;
            var def = Cfg.BuildingById(buildingId);
            if (player == null || spot == null || spot.Kind != NodeKind.BuildSpot || spot.Building != null || def == null)
            {
                reason = "不是可建造空位";
                return false;
            }
            if (!Sim.HumanBuildingsUnlocked.Contains(buildingId) && player.IsHuman)
            {
                reason = "该建筑尚未解锁";
                return false;
            }
            if (!TakeoverScene.IsLocControlled(player, spot.Loc))
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

        /// <summary>扣除资源并在指定节点建造建筑。</summary>
        internal bool TryBuild(PlayerStateEntity player, MapNodeEntity spot, int buildingId, out string reason)
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
            if (Sim.LevelAssetId == 2 && player.IsHuman && def.Id == Cfg.SupplyBuilding.Id)
                Sim.HumanBuildingsUnlocked.UnionWith(
                    Cfg.BuildableBuildings.Select(building => building.Id));
            return true;
        }

        /// <summary>获取指定政令修正项当前生效的等级值。</summary>
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
