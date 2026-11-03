using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    internal abstract class BattleSimSystem
    {
        protected BattleSimSystem(BattleSim sim)
        {
            Sim = sim;
        }

        protected BattleSim Sim { get; }
        protected List<MapNode> Nodes => Sim.Nodes;
        protected Dictionary<string, MapNode> NodeByKey => Sim.NodeByKey;
        protected List<MapEdge> Edges => Sim.Edges;
        protected List<Squad> Squads => Sim.Squads;
        protected List<PlayerState> Players => Sim.Players;
        protected List<ActiveSpellEffect> ActiveSpells => Sim.ActiveSpells;
        protected PlayerState Human => Sim.Human;
        protected PlayerState Enemy => Sim.Enemy;
        protected int LevelAssetId => Sim.LevelAssetId;
        protected float Time
        {
            get => Sim.Time;
            set => Sim.Time = value;
        }

        protected System.Random _rng => Sim.RandomSource;
        protected List<BattleEvent> _events => Sim.EventBuffer;
        protected HashSet<int> _humanBuildingsUnlocked => Sim.HumanBuildingsUnlocked;

        protected MapNode _tutorialTown
        {
            get => Sim.TutorialTown;
            set => Sim.TutorialTown = value;
        }

        protected Squad _tutorialEnemySquad
        {
            get => Sim.TutorialEnemySquad;
            set => Sim.TutorialEnemySquad = value;
        }

        protected bool _tutorialGuardPurchased
        {
            get => Sim.TutorialGuardPurchased;
            set => Sim.TutorialGuardPurchased = value;
        }

        protected bool _tutorialEnemyReplaced
        {
            get => Sim.TutorialEnemyReplaced;
            set => Sim.TutorialEnemyReplaced = value;
        }

        protected bool _tutorialTownCaptured
        {
            get => Sim.TutorialTownCaptured;
            set => Sim.TutorialTownCaptured = value;
        }

        protected Squad _ogreSquad
        {
            get => Sim.OgreSquad;
            set => Sim.OgreSquad = value;
        }

        protected bool _ogreEngaged
        {
            get => Sim.OgreEngaged;
            set => Sim.OgreEngaged = value;
        }

        protected bool _ogreKilled
        {
            get => Sim.OgreKilled;
            set => Sim.OgreKilled = value;
        }

        protected bool _l2MagicTowerCaptured
        {
            get => Sim.L2MagicTowerCaptured;
            set => Sim.L2MagicTowerCaptured = value;
        }

        protected BattleMapSystem MapSystem => Sim.MapSystem;
        protected BattleEconomySystem EconomySystem => Sim.EconomySystem;
        protected BattleCombatSystem CombatSystem => Sim.CombatSystem;
        protected BattleSpellSystem SpellSystem => Sim.SpellSystem;
        protected BattleAiSystem AiSystem => Sim.AiSystem;

        protected MapNode FindNode(int id) => Sim.FindNode(id);
        protected PlayerState PlayerOf(Faction faction) => Sim.PlayerOf(faction);
        protected CombatUnit FindUnitById(int id) => Sim.FindUnitById(id);

        protected static bool IsKeyBuilding(BuildingState building) => BattleSim.IsKeyBuilding(building);
        protected static bool IsLocControlled(PlayerState player, int location) =>
            BattleSim.IsLocControlled(player, location);
        protected static bool HasMechanism(Squad squad) => BattleSim.HasMechanism(squad);
        protected static bool HasFearLock(Squad squad) => BattleSim.HasFearLock(squad);
        protected static bool HasMoraleLock(Squad squad) => BattleSim.HasMoraleLock(squad);
        protected static float UnitRadius(Squad squad) => BattleSim.UnitRadius(squad);
        protected static Vector2 FormationOffset(int index, int count, float rotation = 0f) =>
            BattleSim.FormationOffset(index, count, rotation);
        protected static int LevelForXp(float xp) => BattleSim.LevelForXp(xp);
        protected static int LevelTier(Squad squad) => BattleSim.LevelTier(squad);
    }
}
