using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal sealed class BattleAiSystem : BattleSimSystem
    {
        /// <summary>进攻所需的最低部队战力（人类托管和敌方 AI 共用）。</summary>
        private const float AttackPowerThreshold = 250f;

        /// <summary>留作升级建筑的金币缓冲，避免把兵买光后无法升级。</summary>
        private const int UpgradeGoldReserve = 250;

        /// <summary>一次 AI 心跳最多连续买几个兵，GM 加钱后能明显加快暴兵。</summary>
        private const int MaxPurchasesPerTick = 6;

        /// <summary>每拍最多补几个农场（补给是硬上限，金币无限时靠它堆兵）。</summary>
        private const int MaxFarmsPerTick = 2;

        /// <summary>补给低于这个值就继续造农场，留一点招募余量。</summary>
        private const int SupplyComfort = 4;

        /// <summary>进攻时要求己方战力至少是目标守军的多少倍。</summary>
        private const float AttackPowerMargin = 1.25f;

        /// <summary>全局最多留几支部队驻防（只统计被威胁的据点）。</summary>
        private const int MaxGarrisonSquads = 2;

        internal BattleAiSystem(BattleSim sim) : base(sim) { }

        internal void TickAiSecond(PlayerState player)
        {
            EnsureEconomy(player);
            BuyArmy(player);
            UpgradeFrontline(player);
            CommandArmy(player);
            CastAiSpells(player);
            TryAiUltimate(player);
        }

        // ---------------- 经济 ----------------

        /// <summary>
        /// 经济建筑：补给是硬上限（农场每个只 +1 补给），所以金币够就尽量先把农场铺满，
        /// 再补法力/金币建筑。以前只在补给耗尽时造 1 个农场，无限金币也堆不出兵。
        /// </summary>
        internal void EnsureEconomy(PlayerState player)
        {
            int farmCost = Cfg.BuildingById(5)?.BuildCost ?? 150;
            for (int i = 0; i < MaxFarmsPerTick && player.FreeSupply < SupplyComfort && player.Gold >= farmCost; i++)
            {
                if (!EconomySystem.TryBuildFirstType(player, 5)) break;
            }

            int buildingCount = Mathf.Max(1, player.ActiveBuildings);
            if (player.ManaIncome < Mathf.RoundToInt(buildingCount / 3f))
                EconomySystem.TryBuildFirstType(player, 7);
            if (player.BaseGoldIncome < 2 && buildingCount > player.BaseGoldIncome)
                EconomySystem.TryBuildFirstType(player, 6);
        }

        // ---------------- 造兵 ----------------

        /// <summary>能买就买，按「战斗力/价格」挑最好的兵种，优先放在离敌人最近的己方据点。</summary>
        internal void BuyArmy(PlayerState player)
        {
            for (int purchase = 0; purchase < MaxPurchasesPerTick; purchase++)
            {
                if (player.Gold < BattleSim.AiBuyThreshold) return;

                MapNode bestNode = null;
                GameConfig.takeover.Unit bestUnit = null;
                float bestScore = float.NegativeInfinity;
                foreach (MapNode node in player.OwnedNodes.Where(value =>
                             IsKeyBuilding(value.Building) && value.Building.IsActive))
                {
                    float frontier = FrontierBonus(node, player);
                    foreach (GameConfig.takeover.Unit unit in Cfg.TbUnit.DataList)
                    {
                        if (OriginalRules.FromConfigFaction(unit.Faction) != player.Faction) continue;
                        if (!BattleEconomySystem.AllowedAt(player, node.Building, unit)) continue;
                        int cost = BattleEconomySystem.UnitCost(player, unit);
                        if (player.Gold < cost) continue;
                        if (player.FreeSupply < unit.Upkeep) continue;
                        float score = UnitScore(player, unit) + frontier;
                        if (score <= bestScore) continue;
                        bestScore = score;
                        bestNode = node;
                        bestUnit = unit;
                    }
                }

                if (bestNode == null || bestUnit == null) return;
                Squad bought = EconomySystem.BuyUnit(player, bestUnit, bestNode, out _);
                if (bought == null) return;
                // 新兵先去集结地，不要一个一个往敌人堆里送
                MapNode push = StagingNode(player, PickAttackTarget(player));
                if (push != null) MapSystem.OrderMove(bought, push);
            }
        }

        /// <summary>集结地：己方据点里离目标最近的那个。</summary>
        private MapNode StagingNode(PlayerState player, MapNode target)
        {
            if (target == null) return null;
            return player.OwnedNodes
                .Where(value => value != target)
                .OrderBy(value => Vector2.Distance(value.Pos, target.Pos))
                .FirstOrDefault();
        }

        /// <summary>目标据点附近守军的战力（用来判断打得过再打）。</summary>
        internal float DefenderPower(MapNode node)
        {
            if (node == null) return 0f;
            return Squads.Where(squad => !squad.IsDead && squad.HomeNode == node &&
                                          Vector2.Distance(squad.Pos, node.Pos) <= 120f)
                .Sum(SquadPower);
        }

        /// <summary>战斗力 / 价格，兼顾远程和血量；高 Tier 略微加权。</summary>
        internal float UnitScore(PlayerState player, GameConfig.takeover.Unit unit)
        {
            float dps = (unit.DmgMin + unit.DmgMax) * 0.5f * Mathf.Max(1, unit.UnitsCount) /
                        Mathf.Max(0.2f, unit.AttackSpeed);
            float tank = unit.Hp * Mathf.Max(1, unit.UnitsCount);
            float value = dps * 2f + tank * 0.15f + unit.Range * 0.3f + unit.Tier * 8f;
            int cost = Mathf.Max(1, BattleEconomySystem.UnitCost(player, unit));
            return value / cost + (float)_rng.NextDouble() * 0.02f;
        }

        /// <summary>据点威胁度：附近敌军越多、附近敌方据点越近、守军越少，分越高。</summary>
        internal float BuildingThreat(MapNode node)
        {
            if (node?.Building == null) return 0f;
            float score = Squads.Count(squad => !squad.IsDead && squad.Faction != node.Building.Owner &&
                                                  Vector2.Distance(squad.Pos, node.Pos) <= 220f) * 20f;
            int defenders = Squads.Count(squad => !squad.IsDead && squad.Faction == node.Building.Owner &&
                                                    squad.HomeNode == node &&
                                                    Vector2.Distance(squad.Pos, node.Pos) <= 60f);
            foreach (MapNode other in Nodes.Where(value => IsKeyBuilding(value.Building) &&
                                                           value.Building.IsActive &&
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

        /// <summary>离敌方关键建筑越近的据点加成越高，兵才会往前面堆。</summary>
        private float FrontierBonus(MapNode node, PlayerState player)
        {
            float best = float.MaxValue;
            foreach (MapNode other in Nodes.Where(value =>
                         IsKeyBuilding(value.Building) && value.Building.IsActive &&
                         value.Building.Owner != player.Faction))
            {
                best = Mathf.Min(best, Vector2.Distance(node.Pos, other.Pos));
            }
            return best < float.MaxValue ? 200f / Mathf.Max(50f, best) : 0f;
        }

        // ---------------- 升级 ----------------

        /// <summary>有余钱就升级前线关键建筑：等级越高血量/产出越高，也更抗打。</summary>
        internal void UpgradeFrontline(PlayerState player)
        {
            MapNode best = null;
            float bestScore = float.NegativeInfinity;
            foreach (MapNode node in player.OwnedNodes.Where(value =>
                         IsKeyBuilding(value.Building) && value.Building.IsActive &&
                         value.Building.Level < BuildingState.MaxLevel))
            {
                GameConfig.takeover.Building def = Cfg.BuildingById(node.Building.Kind);
                int cost = node.Building.Level == 0
                    ? def?.UpgradeCost1 ?? 350
                    : def?.UpgradeCost2 ?? 500;
                if (player.Gold < cost + UpgradeGoldReserve) continue;
                float score = BuildingThreat(node) - cost * 0.01f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = node;
            }
            if (best != null) EconomySystem.TryUpgrade(player, best);
        }

        // ---------------- 指挥 ----------------

        /// <summary>
        /// 集中兵力：战力够了就打敌方最薄弱的关键建筑；战力不够就先把富余守军推到前线据点集合，
        /// 同时把最近的无命令部队调回被围攻的据点。
        /// </summary>
        internal void CommandArmy(PlayerState player)
        {
            MapNode underAttack = player.OwnedNodes
                .Where(value => IsKeyBuilding(value.Building) && value.Building.IsActive)
                .OrderByDescending(BuildingThreat)
                .FirstOrDefault(node => BuildingThreat(node) > 40f);

            List<Squad> idle = player.Squads
                .Where(squad => !squad.IsDead && !squad.HasMoveOrder)
                .ToList();

            if (underAttack != null)
            {
                Squad defender = idle
                    .Where(squad => squad.HomeNode != underAttack)
                    .OrderBy(squad => Vector2.Distance(squad.Pos, underAttack.Pos))
                    .FirstOrDefault();
                if (defender != null)
                {
                    MapSystem.OrderMove(defender, underAttack);
                    idle.Remove(defender);
                }
            }

            int enemyKeys = Nodes.Count(node => IsKeyBuilding(node.Building) &&
                                                node.Building.IsActive &&
                                                node.Building.Owner != player.Faction);
            // 收尾阶段（敌方只剩 1-2 个关键建筑）不再留驻军、阈值也放宽，避免「围而不打」
            bool endgame = enemyKeys > 0 && enemyKeys <= 2;

            List<Squad> available = endgame
                ? idle.ToList()
                : idle.Where(squad => !OnGarrisonDuty(squad)).ToList();
            float power = available.Sum(SquadPower);
            MapNode target = PickAttackTarget(player);
            if (target == null) return;

            // 空血建筑只差走上去接收，不需要凑战力：随便派一支过去占领
            if (target.Building != null && target.Building.Hp <= 0f)
            {
                Squad receiver = available.FirstOrDefault() ?? idle.FirstOrDefault();
                if (receiver != null) MapSystem.OrderMove(receiver, target);
                return;
            }

            // 进攻判定：兵力够、或者有 2 支以上部队且接近阈值，就直接压上去
            float defenderPower = DefenderPower(target);
            float threshold = Mathf.Max(AttackPowerThreshold, defenderPower * AttackPowerMargin);
            if (endgame) threshold *= 0.5f;
            bool strongEnough = power >= threshold ||
                                (available.Count >= 2 && power >= threshold * 0.6f);
            if (!strongEnough && idle.Count > available.Count)
            {
                // 兵力压在阈值附近时，连驻军也抽出来（每个据点至少留 1 支）
                List<Squad> extra = idle.Where(squad => !available.Contains(squad) &&
                                                        CanPullGarrison(squad)).ToList();
                if (power + extra.Sum(SquadPower) >= threshold)
                {
                    available.AddRange(extra);
                    strongEnough = true;
                }
            }

            if (strongEnough && available.Count > 0)
            {
                foreach (Squad squad in available) MapSystem.OrderMove(squad, target);
                return;
            }

            // 战力不足：把富余部队推到最靠近目标的己方据点集合
            MapNode staging = player.OwnedNodes
                .Where(value => value != target)
                .OrderBy(value => Vector2.Distance(value.Pos, target.Pos))
                .FirstOrDefault();
            if (staging == null) return;
            foreach (Squad squad in available) MapSystem.OrderMove(squad, staging);
        }

        /// <summary>
        /// 只有「真的被威胁」的据点才留驻军，而且全局最多留 <see cref="MaxGarrisonSquads"/> 支。
        /// 之前按「每个关键建筑留 1 支」来算，占的地盘越多就被钉得越多，
        /// 到收尾阶段所有部队都成了驻军，于是出现「还剩一个城堡却围而不打」。
        /// </summary>
        private bool OnGarrisonDuty(Squad squad)
        {
            MapNode home = squad.HomeNode;
            if (home?.Building == null || !IsKeyBuilding(home.Building)) return false;
            if (Vector2.Distance(squad.Pos, home.Pos) > 80f) return false;
            if (BuildingThreat(home) <= 40f) return false;
            return GarrisonDutyCount() <= MaxGarrisonSquads;
        }

        /// <summary>当前停在自家关键建筑旁的部队数量（所有己方部队）。</summary>
        private int GarrisonDutyCount()
        {
            int count = 0;
            for (int i = 0; i < Squads.Count; i++)
            {
                Squad squad = Squads[i];
                if (squad.IsDead) continue;
                MapNode home = squad.HomeNode;
                if (home?.Building == null || !IsKeyBuilding(home.Building)) continue;
                if (Vector2.Distance(squad.Pos, home.Pos) > 80f) continue;
                count++;
            }
            return count;
        }

        /// <summary>该据点守军超过 1 支时，可以抽走多余的那支去进攻。</summary>
        private bool CanPullGarrison(Squad squad)
        {
            MapNode home = squad.HomeNode;
            if (home?.Building == null || !IsKeyBuilding(home.Building)) return false;
            if (Vector2.Distance(squad.Pos, home.Pos) > 80f) return false;
            int defenders = Squads.Count(other => !other.IsDead && other.HomeNode == home &&
                                                   Vector2.Distance(other.Pos, home.Pos) <= 80f);
            return defenders > 1;
        }

        /// <summary>挑敌方关键建筑里「距离近 + 守军少」的那个；血量为 0 的建筑优先级最高（只差走上去接收）。</summary>
        internal MapNode PickAttackTarget(PlayerState player)
        {
            MapNode best = null;
            float bestScore = float.NegativeInfinity;
            MapNode from = player.OwnedNodes.FirstOrDefault();
            foreach (MapNode node in Nodes.Where(value =>
                         IsKeyBuilding(value.Building) &&
                         value.Building.Owner != player.Faction))
            {
                int defenders = Squads.Count(squad => !squad.IsDead && squad.HomeNode == node &&
                                                       Vector2.Distance(squad.Pos, node.Pos) <= 80f);
                float distance = from != null ? Vector2.Distance(from.Pos, node.Pos) : 0f;
                float score = 400f / Mathf.Max(60f, distance) - defenders * 0.6f - node.Building.Level * 0.4f;
                // 已经打空血但还没被接收的建筑：必须派人上去占领，否则永远赢不了
                if (node.Building.Hp <= 0f) score += 100f;
                // 补给吃紧时优先抢能解锁可建造点的区段（否则金币再多也造不出农场）
                if (player.FreeSupply <= 1)
                {
                    score += Nodes.Count(spot => spot.Kind == NodeKind.BuildSpot &&
                                                 spot.Building == null && spot.Loc == node.Loc) * 0.8f;
                }
                if (score <= bestScore) continue;
                bestScore = score;
                best = node;
            }
            return best;
        }

        /// <summary>粗略战力：存活人数 ×（每兵血量权重 + 每兵输出）。</summary>
        internal float SquadPower(Squad squad)
        {
            int alive = squad.AliveSoldiers;
            if (alive <= 0 || squad.Def == null) return 0f;
            float dps = (squad.Def.DmgMin + squad.Def.DmgMax) * 0.5f;
            return alive * (squad.HpPerSoldier * 0.15f + dps * 1.5f + squad.Def.Range * 0.2f);
        }

        // ---------------- 法术 ----------------

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
