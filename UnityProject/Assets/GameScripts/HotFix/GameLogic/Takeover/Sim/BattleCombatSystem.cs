using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal sealed class BattleCombatSystem : BattleSimSystem
    {
        internal BattleCombatSystem(BattleSim sim) : base(sim) { }

        internal void TickSquads(float dt)
        {
            for (int squadIndex = 0; squadIndex < Squads.Count; squadIndex++)
            {
                Squad squad = Squads[squadIndex];
                if (squad.IsDead) continue;
                bool squadEngaged = false;
                foreach (CombatUnit unit in squad.Units)
                {
                    if (unit.IsDead) continue;
                    TickUnitStatuses(unit, dt);
                    TickProjectile(unit, dt);
                    if (unit.IsDead || unit.FrozenSeconds > 0f) continue;

                    CombatUnit targetUnit = FindTargetUnit(unit);
                    MapNode targetBuilding = FindTargetBuilding(unit, squad);
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
                    foreach (CombatUnit unit in squad.Units)
                    {
                        if (unit.IsDead) continue;
                        unit.Hp = Mathf.Min(unit.MaxHp,
                            unit.Hp + unit.MaxHp * squad.Owner.Mods[40] / 100f * dt);
                    }
                }
                ResolveUnitSeparation(squad);
                TryCollectChest(squad);
                if (squad.TargetNode?.Building != null && squad.TargetNode.Building.Hp <= 0 &&
                    squad.Units.Where(unit => !unit.IsDead).Any(unit =>
                        Vector2.Distance(unit.Pos, squad.TargetNode.Pos) <= 42f))
                {
                    MapSystem.Capture(squad.TargetNode, squad);
                }
            }

            ApplyDivinePresence(dt);
            for (int i = Squads.Count - 1; i >= 0; i--)
            {
                if (!Squads[i].IsDead) continue;
                KillSquad(Squads[i]);
            }
        }

        internal void ApplyDivinePresence(float dt)
        {
            foreach (Squad source in Squads)
            {
                if (source.IsDead) continue;
                if (!OriginalRules.TryAbility(source.Def.Abilities, "DivinePresence", out int power) || power <= 0)
                    continue;
                foreach (Squad allySquad in Squads)
                {
                    if (allySquad.IsDead || allySquad.Faction != source.Faction) continue;
                    foreach (CombatUnit ally in allySquad.Units)
                    {
                        if (ally.IsDead || HasMoraleLock(allySquad) ||
                            Vector2.Distance(ally.Pos, source.Pos) > 100f)
                            continue;
                        ally.MoraleCurrent = Mathf.Min(ally.MoraleBase,
                            ally.MoraleCurrent + power * dt * 5f);
                    }
                }
            }
        }

        internal void TickUnitStatuses(CombatUnit unit, float dt)
        {
            unit.FrozenSeconds = Mathf.Max(0f, unit.FrozenSeconds - dt);
            unit.DashSeconds = Mathf.Max(0f, unit.DashSeconds - dt);
            unit.ChargeSpeedSeconds = Mathf.Max(0f, unit.ChargeSpeedSeconds - dt);
            unit.SlowSeconds = Mathf.Max(0f, unit.SlowSeconds - dt);
            unit.DamageResistSeconds = Mathf.Max(0f, unit.DamageResistSeconds - dt);
            unit.DamageBuffSeconds = Mathf.Max(0f, unit.DamageBuffSeconds - dt);
            unit.MaxDamageSeconds = Mathf.Max(0f, unit.MaxDamageSeconds - dt);
            unit.MagicImmunitySeconds = Mathf.Max(0f, unit.MagicImmunitySeconds - dt);
            unit.HeroicSeconds = Mathf.Max(0f, unit.HeroicSeconds - dt);
            unit.FearedSeconds = Mathf.Max(0f, unit.FearedSeconds - dt);
            if (unit.HeroicSeconds > 0f) unit.MoraleState = MoraleState.Heroic;
            else if (unit.FearedSeconds > 0f) unit.MoraleState = MoraleState.Feared;
            else if (unit.MoraleCurrent <= unit.MoraleBase * 0.35f) unit.MoraleState = MoraleState.Low;
            else if (unit.MoraleCurrent >= unit.MoraleBase * 0.65f) unit.MoraleState = MoraleState.High;
            else unit.MoraleState = MoraleState.Neutral;

            if (unit.PoisonSeconds > 0f)
            {
                unit.PoisonSeconds = Mathf.Max(0f, unit.PoisonSeconds - dt);
                unit.PoisonTick -= dt;
                if (unit.PoisonTick <= 0f)
                {
                    unit.PoisonTick = 3f;
                    ApplyDamage(unit, unit.PoisonDamage, null, false);
                }
            }
        }

        internal void TickProjectile(CombatUnit unit, float dt)
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
                CombatUnit target = FindUnitById(unitId);
                if (target != null && !target.IsDead)
                {
                    ApplyDamage(target, damage, unit.Squad, false);
                    ApplyAttackAbilities(unit, target, damage);
                }
            }
            else if (nodeId >= 0)
            {
                MapNode node = FindNode(nodeId);
                if (node?.Building != null && node.Building.Hp > 0)
                    ApplyBuildingDamage(node.Building, damage, unit.Squad);
            }
            _events.Add(new BattleEvent
            {
                Kind = "ProjectileHit",
                ActorId = unit.Id,
                TargetId = Mathf.Max(unitId, nodeId),
                Value = damage,
                Position = point,
            });
        }

        internal void AdvanceSquad(Squad squad, float dt)
        {
            if (squad.MoveDelaySeconds > 0f)
            {
                squad.MoveDelaySeconds = Mathf.Max(0f, squad.MoveDelaySeconds - dt);
                return;
            }
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
                CombatUnit unit = squad.Units[i];
                if (unit.IsDead) continue;
                Vector2 formationOffset = FormationOffset(i, squad.Units.Count, facing);
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
                unit.Pos += unitDelta / unitDistance * Mathf.Min(unitDistance, unitSpeed * dt);
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

        internal void FinishMove(Squad squad)
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

        internal void RepairAtHome(Squad squad, float dt)
        {
            if (squad.HomeNode?.Building == null || squad.HomeNode.Building.Owner != squad.Faction) return;
            if (squad.Units.Where(unit => !unit.IsDead)
                .All(unit => Vector2.Distance(unit.Pos, squad.HomeNode.Pos) > 60f))
                return;
            foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead && value.Hp < value.MaxHp))
            {
                if (HasMechanism(squad)) continue;
                float repair = unit.MaxHp / 4f + 1f;
                unit.Hp = Mathf.Min(unit.MaxHp, unit.Hp + repair * dt * 2f);
            }
        }

        internal void TryCollectChest(Squad squad)
        {
            foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead))
            {
                MapNode node = Nodes.FirstOrDefault(value => value.Kind == NodeKind.Chest &&
                                                             !value.ChestCollected &&
                                                             Vector2.Distance(value.Pos, unit.Pos) <= 30f);
                if (node == null) continue;
                node.ChestCollected = true;
                squad.Owner.Gold += 500f;
                _events.Add(new BattleEvent
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

        internal CombatUnit FindTargetUnit(CombatUnit unit)
        {
            CombatUnit best = null;
            float bestDistance = float.MaxValue;
            foreach (Squad squad in Squads)
            {
                if (squad.IsDead || squad.Faction == unit.Squad.Faction) continue;
                foreach (CombatUnit candidate in squad.Units)
                {
                    if (candidate.IsDead) continue;
                    float distance = Vector2.Distance(unit.Pos, candidate.Pos);
                    float reach = Mathf.Max(90f, unit.Squad.Def.Range) + UnitRadius(candidate.Squad);
                    if (distance > reach || distance >= bestDistance) continue;
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        internal CombatUnit FindNearestHostile(CombatUnit unit, Squad self)
        {
            CombatUnit best = null;
            float bestDistance = 160f * 160f;
            foreach (Squad squad in Squads)
            {
                if (squad.Faction == self.Faction) continue;
                foreach (CombatUnit candidate in squad.Units)
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

        internal MapNode FindTargetBuilding(CombatUnit unit, Squad squad)
        {
            if (!IsKeyBuilding(squad.TargetNode?.Building) || squad.TargetNode.Building.Owner == squad.Faction ||
                squad.TargetNode.Building.Hp <= 0)
                return null;
            if (Vector2.Distance(unit.Pos, squad.TargetNode.Pos) >
                Mathf.Max(90f, squad.Def.Range) + 50f)
                return null;
            return squad.TargetNode;
        }

        internal void AttackUnit(CombatUnit attacker, CombatUnit target, float dt)
        {
            if (LevelAssetId == 2 && target.Squad == _ogreSquad && !_ogreEngaged &&
                attacker.Squad.Owner.IsHuman)
            {
                _ogreEngaged = true;
                Human.SpellsEnabled = true;
                _events.Add(new BattleEvent
                {
                    Kind = "OgreFightStarted",
                    ActorId = attacker.Id,
                    TargetId = target.Id,
                    Position = target.Pos,
                });
            }
            attacker.AttackTimer -= dt;
            if (attacker.AttackTimer > 0f) return;
            float slowMultiplier = attacker.SlowSeconds > 0f ? 2f : 1f;
            attacker.AttackTimer = Mathf.Max(0.1f, attacker.Squad.Def.AttackSpeed * 0.1f * slowMultiplier);
            EconomySystem.AddSquadXp(attacker.Squad,
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
                attacker.ProjectileRemaining = attacker.Squad.Profile.ProjectileTravel / 10f * (0.5f + (float)_rng.NextDouble());
            }
            else
            {
                ApplyDamage(target, damage, attacker.Squad, false);
                ApplyAttackAbilities(attacker, target, damage);
            }
        }

        internal void AttackBuilding(CombatUnit attacker, MapNode node, float dt)
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
                attacker.ProjectileRemaining = attacker.Squad.Profile.ProjectileTravel / 10f * (0.5f + (float)_rng.NextDouble());
            }
            else
            {
                ApplyBuildingDamage(node.Building, damage, attacker.Squad);
            }
            if (node.Building != null) node.Building.LastAttackerUnitId = attacker.Id;
        }

        internal float RollDamage(CombatUnit attacker, CombatUnit target)
        {
            GameConfig.takeover.Unit def = attacker.Squad.Def;
            float roll = attacker.MaxDamageSeconds > 0f
                ? def.DmgMax
                : Mathf.Lerp(def.DmgMin, def.DmgMax, (float)_rng.NextDouble());
            float levelMultiplier = 1f + LevelTier(attacker.Squad) * 0.1f;
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
                    Vector2.Distance(attacker.Pos, target.Pos) <= UnitRadius(target.Squad) + 10f &&
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
                _rng.NextDouble() < attacker.Squad.Owner.Mods[32] / 100d)
                roll *= 2f;

            if (target == null && def.UnitType != (int)UnitKind.Siege)
            {
                // 建筑沿用单位的逐个判定方式；攻城修正已在上方应用。
            }
            return Mathf.Max(1f, roll);
        }

        internal void ApplyAttackAbilities(CombatUnit attacker, CombatUnit target, float damage, bool allowSplash = true)
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
            if (_rng.NextDouble() < attacker.Squad.Owner.Mods[33] / 100d)
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
                foreach (Squad squad in Squads)
                {
                    if (squad.Faction == attacker.Squad.Faction || squad.IsDead) continue;
                    foreach (CombatUnit unit in squad.Units)
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

        internal void ApplyDamage(CombatUnit target, float damage, Squad source, bool magic)
        {
            if (target == null || target.IsDead) return;
            if (magic && target.MagicImmunitySeconds > 0f) return;
            if (target.DamageResistSeconds > 0f) damage *= 1f - target.DamageResist / 100f;
            target.Hp = Mathf.Max(0f, target.Hp - damage);
            if (!HasMoraleLock(target.Squad))
                target.MoraleCurrent = Mathf.Max(0f, target.MoraleCurrent - damage * 0.5f);
            if (!target.IsDead)
            {
                _events.Add(new BattleEvent
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
            _events.Add(new BattleEvent
            {
                Kind = "UnitKilled",
                ActorId = source?.Id ?? -1,
                TargetId = target.Id,
                Value = damage,
                Position = target.Pos,
            });
        }

        internal void ApplyBuildingDamage(BuildingState building, float damage, Squad source)
        {
            if (building == null || building.Hp <= 0) return;
            building.Hp = Mathf.Max(0, building.Hp - Mathf.RoundToInt(damage));
            _events.Add(new BattleEvent
            {
                Kind = building.Hp <= 0 ? "BuildingDestroyed" : "BuildingDamaged",
                ActorId = source?.Id ?? -1,
                TargetId = building.Node.Id,
                Value = damage,
                Position = building.Node.Pos,
            });
            if (building.Hp <= 0)
                MapSystem.RefreshOwnership();
        }

        internal void TickBuildings(float dt)
        {
            foreach (MapNode node in Nodes.Where(value => value.Building != null))
            {
                BuildingState building = node.Building;
                if (building.Hp <= 0 || building.LastAttackerUnitId < 0) continue;
                CombatUnit attacker = FindUnitById(building.LastAttackerUnitId);
                if (attacker == null || attacker.IsDead) continue;
                PlayerState owner = PlayerOf(building.Owner);
                if (owner == null) continue;
                foreach (Squad defender in owner.Squads.Where(value => !value.IsDead && value.HomeNode == node))
                {
                    if (defender.Path == null && defender.TargetNode != node)
                        MapSystem.OrderMove(defender, attacker.Pos, null);
                }
                building.LastAttackerUnitId = -1;
            }
        }

        internal void KillSquad(Squad squad)
        {
            if (squad == _ogreSquad && !_ogreKilled)
            {
                _ogreKilled = true;
                PlayerState khaganate = PlayerOf(Faction.Khaganate);
                if (khaganate != null)
                {
                    khaganate.AiEnabled = true;
                    khaganate.Gold = 0f;
                }
                _events.Add(new BattleEvent
                {
                    Kind = "OgreDefeated",
                    ActorId = -1,
                    TargetId = squad.Id,
                    Position = squad.Pos,
                });
            }
            if (squad == _tutorialEnemySquad && !_tutorialEnemyReplaced)
            {
                _tutorialEnemyReplaced = true;
                PlayerState empire = PlayerOf(Faction.Empire);
                GameConfig.takeover.Unit replacement = EconomySystem.FindUnit(Faction.Empire, UnitKind.Infantry, 1);
                if (empire != null && replacement != null)
                {
                    empire.Gold += replacement.Cost;
                    EconomySystem.SpawnSquad(empire, replacement, _tutorialTown.Pos, _tutorialTown, 0, false);
                    empire.Gold -= replacement.Cost;
                    empire.AiEnabled = true;
                    Human.UnitTypeLimit = 1;
                    _events.Add(new BattleEvent
                    {
                        Kind = "TutorialEnemyReinforced",
                        ActorId = empire.Id,
                        TargetId = _tutorialTown.Id,
                        Position = _tutorialTown.Pos,
                    });
                }
            }
            squad.Owner.Squads.Remove(squad);
            Squads.Remove(squad);
            if (squad.Owner != null && squad.Owner.Mods[26] > 0)
                squad.Owner.Ultimate = Mathf.Min(squad.Owner.UltimateMax,
                    squad.Owner.Ultimate + squad.Owner.UltimateMax * squad.Owner.Mods[26] / 100f);
        }

        internal void ResolveUnitSeparation(Squad squad)
        {
            for (int i = 0; i < squad.Units.Count; i++)
            {
                CombatUnit a = squad.Units[i];
                if (a.IsDead) continue;
                for (int j = i + 1; j < squad.Units.Count; j++)
                {
                    CombatUnit b = squad.Units[j];
                    if (b.IsDead) continue;
                    Vector2 delta = b.Pos - a.Pos;
                    float distance = delta.magnitude;
                    float minimum = UnitRadius(squad) * 1.6f;
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
