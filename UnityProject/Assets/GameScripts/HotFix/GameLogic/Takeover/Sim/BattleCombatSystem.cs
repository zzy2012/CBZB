using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>处理士兵和建筑之间的战斗。</summary>
    internal sealed class BattleCombatSystem : BattleSimSystemBase
    {
        /// <summary>创建并关联当前战斗模拟的战斗系统。</summary>
        internal BattleCombatSystem(TakeoverScene sim) : base(sim) { }

        /// <summary>推进部队和士兵的战斗、移动及状态更新。</summary>
        internal void TickSquads(float dt)
        {
            for (int squadIndex = 0; squadIndex < Sim.Squads.Count; squadIndex++)
            {
                SquadEntity squad = Sim.Squads[squadIndex];
                if (squad.IsDead) continue;
                bool squadEngaged = false;
                foreach (CombatUnitEntity unit in squad.Units)
                {
                    if (unit.IsDead) continue;
                    TickUnitStatuses(unit, dt);
                    TickProjectile(unit, dt);
                    if (unit.IsDead || unit.FrozenSeconds > 0f) continue;

                    CombatUnitEntity targetUnit = FindTargetUnit(unit);
                    MapNodeEntity targetBuilding = FindTargetBuilding(unit, squad);
                    unit.TargetUnitId = targetUnit?.Id ?? -1;
                    unit.TargetBuildingNodeId = targetBuilding?.Id ?? -1;

                    if (targetUnit != null)
                    {
                        squadEngaged = true;
                        unit.Action = squad.Def.Range > 0 ? UnitAction.Ranged : UnitAction.Melee;
                        unit.Pursuing = false;
                        AttackUnit(unit, targetUnit, dt);
                    }
                    else if (targetBuilding != null)
                    {
                        squadEngaged = true;
                        unit.Action = squad.Def.Range > 0 ? UnitAction.Ranged : UnitAction.Melee;
                        AttackBuilding(unit, targetBuilding, dt);
                    }
                    else
                    {
                        unit.Action = squad.HasMoveOrder ? UnitAction.Moving : UnitAction.Stand;
                    }
                }

                if (!squadEngaged) AdvanceSquad(squad, dt);
                if (squad.Def.Name == "Giantbots" && squad.Owner.Mods[40] > 0)
                {
                    foreach (CombatUnitEntity unit in squad.Units)
                    {
                        if (unit.IsDead) continue;
                        unit.Hp = Mathf.Min(unit.MaxHp,
                            unit.Hp + unit.MaxHp * squad.Owner.Mods[40] / 100f * dt);
                    }
                }
                ResolveUnitSeparation(squad);
                TryCollectChest(squad);
                // 接收半径要和 FinishMove 的 AtNode 判定（70）一致：
                // 原来 42 会导致部队在 42~70 之间停下却永远接收不了空血建筑。
                if (squad.TargetNode?.Building != null && squad.TargetNode.Building.Hp <= 0 &&
                    squad.Units.Where(unit => !unit.IsDead).Any(unit =>
                        Vector2.Distance(unit.Pos, squad.TargetNode.Pos) <= 70f))
                {
                    Sim.MapSystem.Capture(squad.TargetNode, squad);
                }
            }

            ApplyDivinePresence(dt);
            for (int i = Sim.Squads.Count - 1; i >= 0; i--)
            {
                if (!Sim.Squads[i].IsDead) continue;
                KillSquad(Sim.Squads[i]);
            }
        }

        /// <summary>应用 Divine Presence 能力对周围单位的影响。</summary>
        internal void ApplyDivinePresence(float dt)
        {
            foreach (SquadEntity source in Sim.Squads)
            {
                if (source.IsDead) continue;
                if (!OriginalRules.TryAbility(source.Def.Abilities, "DivinePresence", out int power) || power <= 0)
                    continue;
                foreach (SquadEntity allySquad in Sim.Squads)
                {
                    if (allySquad.IsDead || allySquad.Faction != source.Faction) continue;
                    foreach (CombatUnitEntity ally in allySquad.Units)
                    {
                        if (ally.IsDead || TakeoverScene.HasMoraleLock(allySquad) ||
                            Vector2.Distance(ally.Pos, source.Pos) > 100f)
                            continue;
                        ally.MoraleCurrent = Mathf.Min(ally.MoraleBase,
                            ally.MoraleCurrent + power * dt * 5f);
                    }
                }
            }
        }

        /// <summary>更新部队中士兵的持续状态效果。</summary>
        internal void TickUnitStatuses(CombatUnitEntity unit, float dt)
        {
            unit.Status.Tick(dt, this);
        }

        /// <summary>推进投射物飞行并在命中时结算攻击。</summary>
        internal void TickProjectile(CombatUnitEntity unit, float dt)
        {
            if (unit.ProjectileTargetUnitId < 0 && unit.ProjectileTargetNodeId < 0) return;
            unit.ProjectileRemaining -= dt;
            if (unit.ProjectileRemaining > 0f) return;

            int unitId = unit.ProjectileTargetUnitId;
            int nodeId = unit.ProjectileTargetNodeId;
            float damage = unit.ProjectileDamage;
            Vector2 point = unit.ProjectilePoint;
            unit.ProjectileTargetUnitId = -1;
            unit.ProjectileTargetNodeId = -1;
            unit.ProjectileRemaining = 0f;
            unit.ProjectileDamage = 0f;
            if (unitId >= 0)
            {
                CombatUnitEntity target = Sim.FindUnitById(unitId);
                if (target != null && !target.IsDead)
                {
                    ApplyDamage(target, damage, unit.Squad, false);
                    ApplyAttackAbilities(unit, target, damage);
                }
            }
            else if (nodeId >= 0)
            {
                MapNodeEntity node = Sim.FindNode(nodeId);
                if (node?.Building != null && node.Building.Hp > 0)
                    ApplyBuildingDamage(node.Building, damage, unit.Squad);
            }
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = "ProjectileHit",
                ActorId = unit.Id,
                TargetId = Mathf.Max(unitId, nodeId),
                Value = damage,
                Position = point,
            });
        }

        /// <summary>推进指定部队的移动、目标选择和攻击行为。</summary>
        internal void AdvanceSquad(SquadEntity squad, float dt)
        {
            if (squad.Movement.ConsumeMoveDelay(dt)) return;
            if (!squad.HasMoveOrder || squad.Path == null || squad.Path.Count == 0)
            {
                RepairAtHome(squad, dt);
                return;
            }

            Vector2 center = squad.FormationCenter;
            Vector2 waypoint = squad.Path[Mathf.Clamp(squad.PathIndex, 0, squad.Path.Count - 1)];
            Vector2 delta = waypoint - center;
            float distance = delta.magnitude;
            float speed = squad.Speed;
            float movement = speed * dt;
            Vector2 direction = distance < 0.001f ? Vector2.right : delta / distance;
            Vector2 newCenter = distance <= movement || distance < 3f
                ? waypoint
                : center + direction * movement;
            squad.FormationCenter = newCenter;
            float facing = Mathf.Atan2(direction.y, direction.x);
            bool formationArrived = true;
            for (int i = 0; i < squad.Units.Count; i++)
            {
                CombatUnitEntity unit = squad.Units[i];
                if (unit.IsDead) continue;
                Vector2 formationOffset = Sim.FormationOffset(i, squad.Units.Count, facing);
                Vector2 desired = newCenter + formationOffset;
                Vector2 unitDelta = desired - unit.Pos;
                float unitDistance = unitDelta.magnitude;
                if (unitDistance <= 0.001f) continue;
                float unitSpeed = squad.Speed;
                if ((squad.Abilities & AbilityMask.HorrificDash) != 0 &&
                    !unit.Pursuing && FindNearestHostile(unit, squad) != null)
                {
                    unit.Pursuing = true;
                    unit.DashSeconds = 0.5f;
                }
                if ((squad.Abilities & AbilityMask.Charge) != 0 &&
                    unit.ChargeAvailable && !unit.Pursuing && FindNearestHostile(unit, squad) != null)
                {
                    unit.Pursuing = true;
                    unit.ChargeSpeedSeconds = 1.5f;
                }
                if (unit.DashSeconds > 0f) unitSpeed *= 15f;
                if (unit.ChargeSpeedSeconds > 0f) unitSpeed *= 1.75f;
                squad.Movement.MoveUnitToward(unit, desired, unitSpeed, dt);
                unit.DashSeconds = Mathf.Max(0f, unit.DashSeconds - dt);
                unit.ChargeSpeedSeconds = Mathf.Max(0f, unit.ChargeSpeedSeconds - dt);
                if (Vector2.Distance(unit.Pos, desired) > 2f) formationArrived = false;
            }
            squad.AtNode = null;
            if (distance <= movement || distance < 3f)
            {
                bool finalWaypoint = squad.PathIndex >= squad.Path.Count - 1;
                if (!finalWaypoint)
                {
                    squad.PathIndex++;
                }
                else if (formationArrived)
                {
                    FinishMove(squad);
                }
            }
        }

        /// <summary>完成部队移动并更新其到达节点状态。</summary>
        internal void FinishMove(SquadEntity squad)
        {
            squad.HasMoveOrder = false;
            squad.Path = null;
            squad.PathIndex = 0;
            if (squad.TargetNode != null &&
                squad.Units.Where(unit => !unit.IsDead).Any(unit =>
                    Vector2.Distance(unit.Pos, squad.TargetNode.Pos) <= 70f))
            {
                squad.AtNode = squad.TargetNode;
                if (squad.TargetNode.Building?.Owner == squad.Faction)
                    squad.HomeNode = squad.TargetNode;
            }
        }

        /// <summary>处理部队在己方驻地的士兵恢复。</summary>
        internal void RepairAtHome(SquadEntity squad, float dt)
        {
            if (squad.HomeNode?.Building == null || squad.HomeNode.Building.Owner != squad.Faction) return;
            if (squad.Units.Where(unit => !unit.IsDead)
                .All(unit => Vector2.Distance(unit.Pos, squad.HomeNode.Pos) > 60f))
                return;
            foreach (CombatUnitEntity unit in squad.Units.Where(value => !value.IsDead && value.Hp < value.MaxHp))
            {
                if (TakeoverScene.HasMechanism(squad)) continue;
                float repair = unit.MaxHp / 4f + 1f;
                unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + repair * dt * 2f);
            }
        }

        /// <summary>尝试由部队收集所在节点的宝箱。</summary>
        internal void TryCollectChest(SquadEntity squad)
        {
            foreach (CombatUnitEntity unit in squad.Units.Where(value => !value.IsDead))
            {
                MapNodeEntity node = Sim.Nodes.FirstOrDefault(value => value.Kind == NodeKind.Chest &&
                                                             !value.ChestCollected &&
                                                             Vector2.Distance(value.Pos, unit.Pos) <= 30f);
                if (node == null) continue;
                node.ChestCollected = true;
                squad.Owner.Gold += 500f;
                Sim.EventBuffer.Add(new BattleEvent
                {
                    Kind = "ChestCollected",
                    ActorId = squad.Id,
                    TargetId = node.Id,
                    Value = 500f,
                    Position = node.Pos,
                });
                return;
            }
        }

        /// <summary>为攻击者查找当前目标敌方士兵。</summary>
        internal CombatUnitEntity FindTargetUnit(CombatUnitEntity unit)
        {
            CombatUnitEntity best = null;
            float bestDistance = float.MaxValue;
            foreach (SquadEntity squad in Sim.Squads)
            {
                if (squad.IsDead || squad.Faction == unit.Squad.Faction) continue;
                foreach (CombatUnitEntity candidate in squad.Units)
                {
                    if (candidate.IsDead) continue;
                    float distance = Vector2.Distance(unit.Pos, candidate.Pos);
                    float reach = Mathf.Max(90f, unit.Squad.Def.Range) + TakeoverScene.UnitRadius(candidate.Squad);
                    if (distance > reach || distance >= bestDistance) continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>查找距离部队最近的敌对部队。</summary>
        internal CombatUnitEntity FindNearestHostile(CombatUnitEntity unit, SquadEntity self)
        {
            CombatUnitEntity best = null;
            float bestDistance = 160f * 160f;
            foreach (SquadEntity squad in Sim.Squads)
            {
                if (squad.Faction == self.Faction) continue;
                foreach (CombatUnitEntity candidate in squad.Units)
                {
                    if (candidate.IsDead) continue;
                    float distance = Vector2.SqrMagnitude(candidate.Pos - unit.Pos);
                    if (distance > bestDistance) continue;
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>为攻击者查找可攻击的敌方建筑目标。</summary>
        internal MapNodeEntity FindTargetBuilding(CombatUnitEntity unit, SquadEntity squad)
        {
            if (!TakeoverScene.IsKeyBuilding(squad.TargetNode?.Building) || squad.TargetNode.Building.Owner == squad.Faction ||
                squad.TargetNode.Building.Hp <= 0)
                return null;
            if (Vector2.Distance(unit.Pos, squad.TargetNode.Pos) >
                Mathf.Max(90f, squad.Def.Range) + 50f)
                return null;
            return squad.TargetNode;
        }

        /// <summary>执行士兵对敌方士兵的攻击并结算效果。</summary>
        internal void AttackUnit(CombatUnitEntity attacker, CombatUnitEntity target, float dt)
        {
            attacker.AttackTimer -= dt;
            if (attacker.AttackTimer > 0f) return;
            float slowMultiplier = attacker.SlowSeconds > 0f ? 2f : 1f;
            attacker.AttackTimer = Mathf.Max(0.1f, attacker.Squad.Def.AttackSpeed * 0.1f * slowMultiplier);
            Sim.EconomySystem.AddSquadXp(attacker.Squad,
                Mathf.Max(0.01f, attacker.Squad.Def.AttackSpeed * 0.1f /
                                 Mathf.Max(1, attacker.Squad.MaxSoldiers)));
            float damage = RollDamage(attacker, target);
            if (attacker.Squad.Def.Range > 0)
            {
                attacker.ProjectileTargetUnitId = target.Id;
                attacker.ProjectileTargetNodeId = -1;
                attacker.ProjectileOrigin = attacker.Pos;
                attacker.ProjectilePoint = target.Pos;
                attacker.ProjectileDamage = damage;
                attacker.ProjectileRemaining = attacker.Squad.Profile.ProjectileTravel / 10f * (0.5f + (float)Sim.RandomSource.NextDouble());
            }
            else
            {
                ApplyDamage(target, damage, attacker.Squad, false);
                ApplyAttackAbilities(attacker, target, damage);
            }
        }

        /// <summary>执行士兵对敌方建筑的攻击。</summary>
        internal void AttackBuilding(CombatUnitEntity attacker, MapNodeEntity node, float dt)
        {
            attacker.AttackTimer -= dt;
            if (attacker.AttackTimer > 0f) return;
            float slowMultiplier = attacker.SlowSeconds > 0f ? 2f : 1f;
            attacker.AttackTimer = Mathf.Max(0.1f, attacker.Squad.Def.AttackSpeed * 0.1f * slowMultiplier);
            float damage = RollDamage(attacker, null);
            if (attacker.Squad.Def.Range > 0)
            {
                attacker.ProjectileTargetUnitId = -1;
                attacker.ProjectileTargetNodeId = node.Id;
                attacker.ProjectileOrigin = attacker.Pos;
                attacker.ProjectilePoint = node.Pos;
                attacker.ProjectileDamage = damage;
                attacker.ProjectileRemaining = attacker.Squad.Profile.ProjectileTravel / 10f * (0.5f + (float)Sim.RandomSource.NextDouble());
            }
            else
            {
                ApplyBuildingDamage(node.Building, damage, attacker.Squad);
            }
            if (node.Building != null) node.Building.LastAttackerUnitId = attacker.Id;
        }

        /// <summary>依据攻击属性和战斗修正计算攻击伤害。</summary>
        internal float RollDamage(CombatUnitEntity attacker, CombatUnitEntity target)
        {
            GameConfig.takeover.Unit def = attacker.Squad.Def;
            float roll = attacker.MaxDamageSeconds > 0f
                ? def.DmgMax
                : Mathf.Lerp(def.DmgMin, def.DmgMax, (float)Sim.RandomSource.NextDouble());
            float levelMultiplier = 1f + TakeoverScene.LevelTier(attacker.Squad) * 0.1f;
            roll *= levelMultiplier;
            if (def.UnitType == (int)UnitKind.Archers || def.UnitType == (int)UnitKind.Siege)
                roll *= attacker.Squad.Owner.ModMul(13);
            if (attacker.MoraleState == MoraleState.Heroic) roll *= 1.5f;
            else if (attacker.MoraleState == MoraleState.Feared) roll *= 0.5f;
            if (attacker.DamageBuffSeconds > 0f) roll *= 1f + attacker.DamageBuffPercent / 100f;
            if (target != null && target.DamageResistSeconds > 0f) roll *= 1f - target.DamageResist / 100f;

            AbilityMask attackerAbilities = attacker.Squad.Abilities;
            if ((attackerAbilities & AbilityMask.Charge) != 0 && attacker.ChargeAvailable)
            {
                OriginalRules.TryAbility(def.Abilities, "Charge", out int charge);
                int chargeModifier = attacker.Squad.Owner.Mods[7];
                roll += Mathf.Floor(roll * (charge * 10 + chargeModifier) / 100f);
                attacker.ChargeAvailable = false;
            }
            if (target != null && target.Squad.Def.UnitType == (int)UnitKind.Cavalry &&
                (attackerAbilities & AbilityMask.LongSpear) != 0)
            {
                OriginalRules.TryAbility(def.Abilities, "LongSpear", out int spear);
                roll *= 1f + spear / 100f;
            }
            if (target != null)
            {
                AbilityMask targetAbilities = target.Squad.Abilities;
                if ((targetAbilities & AbilityMask.ShieldWall) != 0 &&
                    def.Range > 0)
                {
                    OriginalRules.TryAbility(target.Squad.Def.Abilities, "ShieldWall", out int shield);
                    roll -= Mathf.RoundToInt(roll * shield / 100f);
                }
                if (def.Range > 0 &&
                    Vector2.Distance(attacker.Pos, target.Pos) <= TakeoverScene.UnitRadius(target.Squad) + 10f &&
                    (attackerAbilities & AbilityMask.MartialArtists) == 0)
                {
                    roll *= Mathf.Max(0f, (100f - def.MeleePenalty) / 100f);
                }
            }

            if (target == null)
            {
                roll *= 1f + attacker.Squad.Owner.Mods[18] / 100f;
                if ((attackerAbilities & AbilityMask.SiegeWeapon) != 0)
                {
                    OriginalRules.TryAbility(def.Abilities, "SiegeWeapon", out int siege);
                    roll *= 1f + siege / 100f;
                }
            }
            if ((attackerAbilities & AbilityMask.Mechanism) != 0 &&
                Sim.RandomSource.NextDouble() < attacker.Squad.Owner.Mods[32] / 100d)
                roll *= 2f;

            if (target == null && def.UnitType != (int)UnitKind.Siege)
            {
                // 建筑沿用单位的逐个判定方式；攻城修正已在上方应用。
            }
            return Mathf.Max(1f, roll);
        }

        /// <summary>应用攻击者能力及其附带的战斗效果。</summary>
        internal void ApplyAttackAbilities(CombatUnitEntity attacker, CombatUnitEntity target, float damage, bool allowSplash = true)
        {
            AbilityMask abilities = attacker.Squad.Abilities;
            AbilityMask targetAbilities = target.Squad.Abilities;
            if ((abilities & AbilityMask.Poisonous) != 0 &&
                (targetAbilities & (AbilityMask.PoisonImmunity | AbilityMask.Undead | AbilityMask.Mechanism)) == 0)
            {
                OriginalRules.TryAbility(attacker.Squad.Def.Abilities, "Poisonous", out int poison);
                target.PoisonDamage = poison + attacker.Squad.Owner.Mods[24];
                target.PoisonSeconds = 3f;
                target.PoisonTick = 3f;
            }
            if ((abilities & (AbilityMask.FreezeArrow | AbilityMask.FreezeBlast)) != 0)
            {
                target.FrozenSeconds = Mathf.Max(target.FrozenSeconds, 3f + attacker.Squad.Owner.Mods[30]);
            }
            if (Sim.RandomSource.NextDouble() < attacker.Squad.Owner.Mods[33] / 100d)
                target.FrozenSeconds = Mathf.Max(target.FrozenSeconds, 3f + attacker.Squad.Owner.Mods[30]);
            if ((abilities & AbilityMask.Intimidiation) != 0 &&
                (targetAbilities & (AbilityMask.Discipline | (AbilityMask.Mechanism | AbilityMask.Undead))) == 0)
            {
                OriginalRules.TryAbility(attacker.Squad.Def.Abilities, "Intimidiation", out int intimidation);
                target.MoraleCurrent = Mathf.Max(0f, target.MoraleCurrent - intimidation);
            }
            if (allowSplash && (abilities & AbilityMask.SplashDamage) != 0)
            {
                OriginalRules.TryAbility(attacker.Squad.Def.Abilities, "SplashDamage", out int radius);
                foreach (SquadEntity squad in Sim.Squads)
                {
                    if (squad.Faction == attacker.Squad.Faction || squad.IsDead) continue;
                    foreach (CombatUnitEntity unit in squad.Units)
                    {
                        if (unit == target || unit.IsDead) continue;
                        if (Vector2.Distance(unit.Pos, target.Pos) <= radius)
                        {
                            ApplyDamage(unit, damage, attacker.Squad, false);
                            ApplyAttackAbilities(attacker, unit, damage, false);
                        }
                    }
                }
            }
        }

        /// <summary>结算单位受到的伤害并处理濒死状态。</summary>
        internal void ApplyDamage(CombatUnitEntity target, float damage, SquadEntity source, bool magic)
        {
            if (target == null || target.IsDead) return;
            if (magic && target.MagicImmunitySeconds > 0f) return;
            if (target.DamageResistSeconds > 0f) damage *= 1f - target.DamageResist / 100f;
            target.Hp = Mathf.Max(0f, target.Hp - damage);
            if (!TakeoverScene.HasMoraleLock(target.Squad))
                target.MoraleCurrent = Mathf.Max(0f, target.MoraleCurrent - damage * 0.5f);
            if (!target.IsDead)
            {
                Sim.EventBuffer.Add(new BattleEvent
                {
                    Kind = "UnitDamaged",
                    ActorId = source?.Id ?? -1,
                    TargetId = target.Id,
                    Value = damage,
                    Position = target.Pos,
                });
                return;
            }
            target.Hp = 0f;
            target.Action = UnitAction.Dead;
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = "UnitKilled",
                ActorId = source?.Id ?? -1,
                TargetId = target.Id,
                Value = damage,
                Position = target.Pos,
            });
        }

        /// <summary>对建筑结算伤害并处理占领状态变化。</summary>
        internal void ApplyBuildingDamage(BuildingState building, float damage, SquadEntity source)
        {
            if (building == null || building.Hp <= 0) return;
            building.Hp = Mathf.Max(0, building.Hp - Mathf.RoundToInt(damage));
            Sim.EventBuffer.Add(new BattleEvent
            {
                Kind = building.Hp <= 0 ? "BuildingDestroyed" : "BuildingDamaged",
                ActorId = source?.Id ?? -1,
                TargetId = building.Node.Id,
                Value = damage,
                Position = building.Node.Pos,
            });
            if (building.Hp <= 0)
                Sim.MapSystem.RefreshOwnership();
        }

        /// <summary>更新战斗中的建筑生命、占领和修复状态。</summary>
        internal void TickBuildings(float dt)
        {
            foreach (MapNodeEntity node in Sim.Nodes.Where(value => value.Building != null))
            {
                BuildingState building = node.Building;
                if (building.Hp <= 0 || building.LastAttackerUnitId < 0) continue;
                CombatUnitEntity attacker = Sim.FindUnitById(building.LastAttackerUnitId);
                if (attacker == null || attacker.IsDead) continue;
                PlayerStateEntity owner = Sim.PlayerOf(building.Owner);
                if (owner == null) continue;
                foreach (SquadEntity defender in owner.Squads.Where(value => !value.IsDead && value.HomeNode == node))
                {
                    if (defender.Path == null && defender.TargetNode != node)
                        Sim.MapSystem.OrderMove(defender, attacker.Pos, null);
                }
                building.LastAttackerUnitId = -1;
            }
        }

        /// <summary>清理全灭部队并结算击杀后的效果。</summary>
        internal void KillSquad(SquadEntity squad)
        {
            squad.Owner.Squads.Remove(squad);
            Sim.Squads.Remove(squad);
            Sim.RemoveSquadEntities(squad);
            if (squad.Owner != null && squad.Owner.Mods[26] > 0)
                squad.Owner.Ultimate = Mathf.Min(squad.Owner.UltimateMax,
                    squad.Owner.Ultimate + squad.Owner.UltimateMax * squad.Owner.Mods[26] / 100f);
        }

        /// <summary>分离重叠士兵以避免单位位置互相穿插。</summary>
        internal void ResolveUnitSeparation(SquadEntity squad)
        {
            for (int i = 0; i < squad.Units.Count; i++)
            {
                CombatUnitEntity a = squad.Units[i];
                if (a.IsDead) continue;
                for (int j = i + 1; j < squad.Units.Count; j++)
                {
                    CombatUnitEntity b = squad.Units[j];
                    if (b.IsDead) continue;
                    Vector2 delta = b.Pos - a.Pos;
                    float distance = delta.magnitude;
                    float minimum = TakeoverScene.UnitRadius(squad) * 1.6f;
                    if (distance >= minimum) continue;
                    if (distance < 0.001f) delta = Vector2.right;
                    else delta /= distance;
                    float push = (minimum - distance) * 0.5f;
                    a.Pos -= delta * push;
                    b.Pos += delta * push;
                }
            }
        }
    }
}
