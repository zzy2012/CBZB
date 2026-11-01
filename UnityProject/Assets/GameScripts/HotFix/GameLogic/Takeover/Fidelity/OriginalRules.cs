using System;
using System.Collections.Generic;
using System.Linq;

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
    /// deliberately kept separate from the editable player edict data so the
    /// first-chapter build can reproduce enemy bonuses while edicts remain off.
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

        private static readonly IReadOnlyDictionary<int, ModifierValue[]> EnemyModifiersByLevel =
            new Dictionary<int, ModifierValue[]>
            {
                [1] = Array.Empty<ModifierValue>(),
                [2] = M((17, 0)),
                [3] = M((41, 1)),
                [4] = M((43, 1), (41, 1), (2, 1), (29, 1), (0, 1), (2, 1)),
                [5] = M((43, 1), (41, 1), (2, 1), (0, 1), (17, 1), (1, 1), (20, 1)),
                [6] = M((17, 1), (1, 1), (20, 1), (0, 1)),
                [7] = M((43, 1), (41, 1), (2, 1), (0, 1), (25, 1), (4, 1), (3, 1),
                    (29, 1), (0, 1), (2, 1), (34, 1), (4, 1)),
                [8] = M((43, 1), (41, 1), (2, 1), (0, 1), (25, 1), (4, 1), (3, 1), (1, 1)),
            };

        public static IReadOnlyList<ModifierValue> EnemyModifiers(int levelId)
        {
            return EnemyModifiersByLevel.TryGetValue(levelId, out var values)
                ? values
                : Array.Empty<ModifierValue>();
        }

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

        private static ModifierValue[] M(params (int id, int value)[] values)
        {
            return values.Select(v => new ModifierValue(v.id, v.value)).ToArray();
        }
    }
}
