using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    public readonly struct ModifierValue
    {
        public readonly int Id;
        public readonly int Value;

        public ModifierValue(int id, int value)
        {
            Id = id;
            Value = value;
        }
    }

    /// <summary>
    /// Values recovered from LevelInfo.§_-5S§ in Takeover 1.0.4. These are
    /// deliberately kept separate from editable edict data. The first-chapter
    /// build applies only the original baseline edict and per-level AI bonuses.
    /// </summary>
    public static class OriginalRules
    {
        public const string SourceVersion = "4399 Takeover 1.0.4";
        public const float WorldYScale = 2f;
        public const int NativeStageWidth = 800;
        public const int NativeStageHeight = 600;
        public const int PlayerStartGold = 250;
        public const int PlayerStartMana = 100;
        public const int AiBuyThreshold = 180;
        public const float LogicStep = 1f / 30f;
        public const float SpellRetrySeconds = 2f;

        private static readonly IReadOnlyDictionary<int, EnemyPlayerSetup[]> EnemyPlayersByLevel =
            new Dictionary<int, EnemyPlayerSetup[]>
            {
                [1] = E(P(Faction.Empire, 0, 0)),
                [2] = E(P(Faction.Khaganate, 0, 0)),
                [3] = E(P(Faction.Empire, 0, 0, (41, 1))),
                [4] = E(P(Faction.Empire, 0, 0, (43, 1), (41, 1), (2, 1)),
                    P(Faction.Cult, 0, 0, (29, 1), (2, 1), (0, 1))),
                [5] = E(P(Faction.Empire, 0, 0, (43, 1), (41, 1), (2, 1), (0, 1)),
                    P(Faction.Khaganate, 0, 0, (17, 1), (20, 1), (1, 1))),
                [6] = E(P(Faction.Khaganate, 0, 0, (17, 1), (20, 1), (1, 1), (0, 1))),
                [7] = E(P(Faction.Empire, 0, 0, (43, 1), (41, 1), (2, 1), (0, 1),
                        (25, 1), (4, 1), (3, 1)),
                    P(Faction.Cult, 0, 0, (29, 1), (2, 1), (0, 1), (34, 1), (4, 1))),
                [8] = E(P(Faction.Empire, 0, 0, (43, 1), (41, 1), (2, 1), (0, 1),
                    (25, 1), (4, 1), (3, 1), (1, 1))),
            };

        public static IReadOnlyList<ModifierValue> EnemyModifiers(int levelId)
        {
            return EnemyPlayers(levelId).SelectMany(player => player.Modifiers.Select(value => value)).ToArray();
        }

        public static IReadOnlyList<EnemyPlayerSetup> EnemyPlayers(int levelId)
        {
            return EnemyPlayersByLevel.TryGetValue(levelId, out var values)
                ? values
                : Array.Empty<EnemyPlayerSetup>();
        }

        /// <summary>Config tables during the asset migration used UI order 0=Westaria..3=Empire.</summary>
        public static Faction FromConfigFaction(int value)
        {
            switch (value)
            {
                case 0: return Faction.Westaria;
                case 1: return Faction.Khaganate;
                case 2: return Faction.Cult;
                case 3: return Faction.Empire;
                default: return Faction.Bandits;
            }
        }

        public static int ToConfigFaction(Faction value)
        {
            switch (value)
            {
                case Faction.Westaria: return 0;
                case Faction.Khaganate: return 1;
                case Faction.Cult: return 2;
                case Faction.Empire: return 3;
                default: return 4;
            }
        }

        public static AbilityMask ParseAbilities(string abilities)
        {
            AbilityMask result = AbilityMask.None;
            if (string.IsNullOrWhiteSpace(abilities)) return result;
            foreach (string raw in abilities.Split(';'))
            {
                string name = raw.Trim().Split(' ')[0];
                switch (name)
                {
                    case "ShieldWall": result |= AbilityMask.ShieldWall; break;
                    case "Charge": result |= AbilityMask.Charge; break;
                    case "SplashDamage": result |= AbilityMask.SplashDamage; break;
                    case "SiegeWeapon": result |= AbilityMask.SiegeWeapon; break;
                    case "DivinePresence": result |= AbilityMask.DivinePresence; break;
                    case "Poisonous": result |= AbilityMask.Poisonous; break;
                    case "Intimidiation": result |= AbilityMask.Intimidiation; break;
                    case "Discipline": result |= AbilityMask.Discipline; break;
                    case "Mechanism": result |= AbilityMask.Mechanism; break;
                    case "MartialArtists": result |= AbilityMask.MartialArtists; break;
                    case "Undead": result |= AbilityMask.Undead; break;
                    case "HorrificDash": result |= AbilityMask.HorrificDash; break;
                    case "PoisonImmunity": result |= AbilityMask.PoisonImmunity; break;
                    case "LongSpear": result |= AbilityMask.LongSpear; break;
                    case "Explosive": result |= AbilityMask.Explosive; break;
                    case "FreezeArrow": result |= AbilityMask.FreezeArrow; break;
                    case "FreezeBlast": result |= AbilityMask.FreezeBlast; break;
                }
            }
            return result;
        }

        public static UnitCombatProfile UnitProfile(GameConfig.takeover.Unit def)
        {
            var profile = new UnitCombatProfile
            {
                AnimationSpeed = 0.75f,
                ProjectileHeight = 0,
                ShowProjectileRadius = 0,
                BaseWalkSpeed = 11f,
                ProjectileTravel = def.UnitType == (int)UnitKind.Archers ? 10f : 5f,
                Abilities = ParseAbilities(def.Abilities),
            };

            switch (def.Name)
            {
                case "Gunsmiths":
                    profile.AnimationSpeed = 0.05f;
                    profile.ProjectileHeight = 25;
                    profile.ShowProjectileRadius = 25;
                    profile.ProjectileTravel = 3f;
                    break;
                case "Gunbots":
                    profile.AnimationSpeed = 0.15f;
                    profile.ProjectileHeight = 25;
                    profile.ShowProjectileRadius = 10;
                    profile.ProjectileTravel = 3f;
                    break;
                case "Giantbots":
                    profile.AnimationSpeed = 0.15f;
                    profile.ProjectileHeight = 55;
                    profile.ShowProjectileRadius = 15;
                    profile.ProjectileTravel = 3f;
                    break;
                case "Trebuchet":
                    profile.AnimationSpeed = 0.2f;
                    profile.ProjectileHeight = 100;
                    profile.ProjectileTravel = 10f;
                    break;
                case "Poison Catapult":
                    profile.AnimationSpeed = 0.3f;
                    profile.ProjectileHeight = 50;
                    profile.ProjectileTravel = 10f;
                    break;
                case "Acolytes":
                    profile.AnimationSpeed = 0.5f;
                    profile.ProjectileHeight = 44;
                    break;
                case "Zealots":
                    profile.AnimationSpeed = 0.33f;
                    profile.ProjectileHeight = 50;
                    break;
                case "Wizards":
                    profile.ProjectileHeight = 55;
                    break;
                case "Ogre":
                case "Avatar":
                    profile.BaseWalkSpeed = 20f;
                    break;
            }
            if (def.UnitType == (int)UnitKind.Cavalry) profile.BaseWalkSpeed = 15.5f;
            return profile;
        }

        public static bool UsesUndeadRecruitDiscount(GameConfig.takeover.Unit def) =>
            def != null && (def.Name == "Immortals" || def.Name == "Ancient Raiders");

        public static void ApplyBaselineEdicts(PlayerState player)
        {
            switch (player.Faction)
            {
                case Faction.Westaria: player.Mods[5] = 1; break;
                case Faction.Khaganate: player.Mods[17] = 1; break;
                case Faction.Cult: player.Mods[29] = 1; break;
            }
        }

        public static bool IsSpellUnlocked(PlayerState player, int spellId)
        {
            if (player == null || !player.SpellsEnabled) return false;
            spellId = OriginalSpellIndex(spellId);
            switch ((Faction)player.Faction)
            {
                case Faction.Westaria:
                    if (spellId == 3) return true;
                    if (spellId == 0) return player.Mods[9] > 0;
                    if (spellId == 1) return player.Mods[5] > 0;
                    if (spellId == 2) return player.Mods[14] > 0;
                    break;
                case Faction.Khaganate:
                    if (spellId == 7) return true;
                    if (spellId == 4) return player.Mods[20] > 0;
                    if (spellId == 5) return player.Mods[25] > 0;
                    if (spellId == 6) return player.Mods[17] > 0;
                    break;
                case Faction.Cult:
                    if (spellId == 11) return true;
                    if (spellId == 8) return player.Mods[29] > 0;
                    if (spellId == 9) return player.Mods[34] > 0;
                    if (spellId == 10) return player.Mods[36] > 0;
                    break;
                case Faction.Empire:
                    if (spellId == 15) return true;
                    if (spellId == 12) return player.Mods[41] > 0;
                    if (spellId == 13) return player.Mods[42] > 0;
                    if (spellId == 14) return player.Mods[43] > 0;
                    break;
            }
            return false;
        }

        /// <summary>The editable migration table uses 1-based IDs; raw code uses IDs 0..15.</summary>
        public static int OriginalSpellIndex(int configSpellId) => Mathf.Clamp(configSpellId - 1, 0, 15);

        /// <summary>Returns false for malformed tokens and leaves value at zero.</summary>
        public static bool TryAbility(string abilities, string abilityName, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(abilities) || string.IsNullOrWhiteSpace(abilityName))
                return false;

            foreach (var raw in abilities.Split(';'))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;
                int split = token.IndexOf(' ');
                string name = split < 0 ? token : token.Substring(0, split);
                if (!string.Equals(name, abilityName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (split < 0 || !int.TryParse(token.Substring(split + 1).Trim(), out value))
                    value = 0;
                return true;
            }

            return false;
        }

        public static bool HasAbility(string abilities, string abilityName)
        {
            return TryAbility(abilities, abilityName, out _);
        }

        private static EnemyPlayerSetup[] E(params EnemyPlayerSetup[] values)
        {
            return values;
        }

        private static EnemyPlayerSetup P(Faction faction, int gold, int mana,
            params (int id, int value)[] mods)
        {
            var setup = new EnemyPlayerSetup
            {
                Faction = faction,
                StartGold = gold,
                StartMana = mana,
            };
            foreach (var modifier in mods)
                setup.Modifiers.Add(new ModifierValue(modifier.id, modifier.value));
            return setup;
        }
    }
}
