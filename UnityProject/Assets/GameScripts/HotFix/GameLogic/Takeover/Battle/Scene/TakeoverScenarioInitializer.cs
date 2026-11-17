using System.Collections.Generic;
using System.Linq;

namespace GameLogic.Takeover
{
    /// <summary>创建参与方并初始化关卡开场状态。</summary>
    internal sealed class TakeoverScenarioInitializer : BattleSimSystemBase
    {
        /// <summary>创建并关联当前战斗模拟的场景初始化器。</summary>
        internal TakeoverScenarioInitializer(TakeoverScene sim) : base(sim) { }

        /// <summary>创建人类和电脑参与方并应用开场修正项。</summary>
        internal PlayerStateEntity InitializePlayers(Faction humanFaction, Faction enemyFaction, int[] humanMods)
        {
            var human = new PlayerStateEntity
            {
                Id = 0,
                Name = "Player",
                Faction = humanFaction,
                IsHuman = true,
                AiEnabled = false,
            };
            Sim.Players.Add(human);
            Sim.AddEntity(BattleEntityType.Player, human);
            ApplyModifiers(human, humanMods);
            OriginalRules.ApplyBaselineEdicts(human);

            IReadOnlyList<EnemyPlayerSetup> setups = OriginalRules.EnemyPlayers(Sim.LevelAssetId);
            if (setups.Count == 0)
            {
                setups = new[]
                {
                    new EnemyPlayerSetup
                    {
                        Faction = enemyFaction,
                        AiEnabled = true,
                    },
                };
            }
            foreach (EnemyPlayerSetup setup in setups)
            {
                var player = new PlayerStateEntity
                {
                    Id = Sim.Players.Count,
                    Name = setup.Faction + " AI " + Sim.Players.Count,
                    Faction = setup.Faction,
                    AiEnabled = setup.AiEnabled,
                    Gold = setup.StartGold,
                    Mana = setup.StartMana,
                };
                foreach (ModifierValue modifier in setup.Modifiers)
                {
                    if (modifier.Id >= 0 && modifier.Id < player.Mods.Length)
                        player.Mods[modifier.Id] += modifier.Value;
                }
                player.UltimateMax = 200f * player.ModMul(12);
                Sim.Players.Add(player);
                Sim.AddEntity(BattleEntityType.Player, player);
            }

            human.UltimateMax = 200f * human.ModMul(12);
            return human;
        }

        /// <summary>设置开场资源、刷新领地并初始化关卡专属内容。</summary>
        internal void Initialize()
        {
            GameConfig.takeover.Level level =
                Cfg.TbLevel.DataList.FirstOrDefault(value => value.AssetId == Sim.LevelAssetId);
            Sim.Human.Gold = (level?.StartGold ?? OriginalRules.PlayerStartGold) + Sim.Human.Mods[8];
            Sim.Human.Mana = level?.StartMana ?? OriginalRules.PlayerStartMana;
            Sim.MapSystem.RefreshOwnership();
            InitializeScenario();
            Sim.MapSystem.RefreshOwnership();
        }

        /// <summary>将初始修正项应用到指定参与方。</summary>
        private static void ApplyModifiers(PlayerStateEntity player, int[] modifiers)
        {
            if (player == null || modifiers == null) return;
            for (int i = 0; i < player.Mods.Length && i < modifiers.Length; i++)
                player.Mods[i] = modifiers[i];
        }

        /// <summary>根据关卡编号初始化对应开场状态。</summary>
        private void InitializeScenario()
        {
            Sim.HumanBuildingsUnlocked.UnionWith(Cfg.BuildableBuildings.Select(building => building.Id));
            if (Sim.LevelAssetId == 1) InitializeLevelOne();
            else if (Sim.LevelAssetId == 2) InitializeLevelTwo();
            else SpawnScenarioOgre();
        }

        /// <summary>初始化第一关的开场状态和初始部队。</summary>
        private void InitializeLevelOne()
        {
            PlayerStateEntity empire = Sim.PlayerOf(Faction.Empire);
            if (empire == null) return;
            GameConfig.takeover.Unit undeadWarrior =
                Sim.EconomySystem.FindUnit(Faction.Empire, UnitKind.Infantry, 1);
            if (undeadWarrior == null) return;
            empire.Gold = undeadWarrior.Cost * 2f;
            foreach (MapNodeEntity node in empire.OwnedNodes.Where(value => value.Building != null))
            {
                Sim.EconomySystem.SpawnSquad(empire, undeadWarrior, node.Pos, node, 0, false);
                empire.Gold -= undeadWarrior.Cost;
            }
        }

        /// <summary>初始化第二关的特殊场景和初始状态。</summary>
        private void InitializeLevelTwo()
        {
            Sim.Human.Gold += 300f;
            PlayerStateEntity khaganate = Sim.PlayerOf(Faction.Khaganate);
            if (khaganate == null) return;

            GameConfig.takeover.Building farm =
                Cfg.BuildableBuildings.Single(building => building.Supply > 0);
            GameConfig.takeover.Building mine =
                Cfg.BuildableBuildings.Single(building => building.Gold > 0);
            GameConfig.takeover.Building obelisk =
                Cfg.BuildableBuildings.Single(building => building.Mana > 0);
            Sim.EconomySystem.TryBuildFirstType(Sim.Human, mine.Id);
            Sim.EconomySystem.TryBuildFirstType(Sim.Human, obelisk.Id);
            Sim.HumanBuildingsUnlocked.Clear();
            Sim.HumanBuildingsUnlocked.Add(farm.Id);

            GameConfig.takeover.Unit warrior =
                Sim.EconomySystem.FindUnit(Faction.Khaganate, UnitKind.Infantry, 1);
            GameConfig.takeover.Unit hunter =
                Sim.EconomySystem.FindUnit(Faction.Khaganate, UnitKind.Archers, 1);
            if (warrior == null) return;
            int villageCost = farm.BuildCost;
            int mineCost = mine.BuildCost;
            khaganate.Gold = warrior.Cost * 3f + (hunter?.Cost ?? 0) + villageCost + mineCost;
            Sim.EconomySystem.TryBuildFirstType(khaganate, farm.Id);
            foreach (MapNodeEntity node in khaganate.OwnedNodes.Where(value => value.Building != null))
            {
                if (node.Kind == NodeKind.MageTower)
                {
                    Sim.EconomySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                    khaganate.Gold -= warrior.Cost;
                    continue;
                }
                node.Building.Catch = 3;
                Sim.EconomySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                Sim.EconomySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                Sim.EconomySystem.SpawnSquad(khaganate, warrior, node.Pos, node, 0, false);
                khaganate.Gold -= warrior.Cost * 3f;
                if (hunter != null)
                {
                    Sim.EconomySystem.SpawnSquad(khaganate, hunter, node.Pos, node, 0, false);
                    khaganate.Gold -= hunter.Cost;
                }
            }
            SpawnScenarioOgre();
        }

        /// <summary>在当前场景创建食人魔部队。</summary>
        private void SpawnScenarioOgre()
        {
            MapNodeEntity ogreNode = Sim.Nodes.FirstOrDefault(node => node.Kind == NodeKind.Ogre);
            if (ogreNode == null) return;
            PlayerStateEntity bandits = Sim.MapSystem.EnsurePlayer(Faction.Bandits);
            GameConfig.takeover.Unit ogre = Cfg.TbUnit.DataList.FirstOrDefault(unit => unit.Name == "Ogre");
            if (ogre != null)
                Sim.EconomySystem.SpawnSquad(bandits, ogre, ogreNode.Pos, ogreNode, 0, false);
        }
    }
}
