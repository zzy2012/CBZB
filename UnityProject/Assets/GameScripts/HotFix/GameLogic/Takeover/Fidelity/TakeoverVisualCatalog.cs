using System.Text;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 管理与外部 SWF 资源提取流程共用的 YooAsset 资源地址。
    /// 原始资源缺失时保留当前回退精灵，以便在导入原始图集前仍可验证加载流程。
    /// </summary>
    public static class TakeoverVisualCatalog
    {
        public static string UnitLocation(GameConfig.takeover.Unit unit) =>
            $"takeover_unit_{unit.Id}_" + Slug(unit.Name);

        public static string BuildingLocation(int buildingId)
            => BuildingLocation(buildingId, Faction.Westaria);

        public static string BuildingLocation(int buildingId, Faction owner)
        {
            if (buildingId < 1 || buildingId > 7) return string.Empty;

            string color = ColorName(owner);
            string factionLocation = $"takeover_building_{buildingId}_{color}";
            if (GameModule.Resource.CheckLocationValid(factionLocation))
                return factionLocation;

            switch (buildingId)
            {
                case 1: return "takeover_building_town";
                case 2: return "takeover_building_castle";
                case 3: return "takeover_building_camp";
                case 4: return "takeover_building_magetower";
                case 5: return "takeover_building_farm";
                case 6: return "takeover_building_mine";
                case 7: return "takeover_building_obelisk";
                default: return string.Empty;
            }
        }

        public static string NodeLocation(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Empty: return "takeover_node_empty";
                case NodeKind.BuildSpot: return "takeover_node_buildspot";
                case NodeKind.Chest: return "takeover_node_chest";
                case NodeKind.Ogre: return "takeover_node_ogre";
                default: return string.Empty;
            }
        }

        public static void Bind(SpriteRenderer renderer, GameConfig.takeover.Unit unit)
        {
            Bind(renderer, UnitLocation(unit));
        }

        public static void Bind(SpriteRenderer renderer, MapNode node)
        {
            Bind(renderer, LocationFor(node));
        }

        public static string LocationFor(MapNode node)
        {
            if (node == null) return string.Empty;
            return node.Building != null
                ? BuildingLocation(node.Building.Kind, node.Building.Owner)
                : NodeLocation(node.Kind);
        }

        public static bool Bind(SpriteRenderer renderer, string location)
        {
            if (renderer == null || string.IsNullOrEmpty(location)) return false;
            if (!GameModule.Resource.CheckLocationValid(location)) return false;
            TakeoverSpriteCache.Apply(renderer, location);
            return true;
        }

        public static void BindMapBackground(SpriteRenderer renderer, int levelId, Vector2 targetSize)
        {
            if (renderer == null) return;
            string location = $"takeover_map_{levelId}";
            if (!GameModule.Resource.CheckLocationValid(location)) return;
            TakeoverSpriteCache.Apply(renderer, location, spriteRenderer =>
            {
                if (spriteRenderer.sprite == null) return;
                Vector2 source = spriteRenderer.sprite.bounds.size;
                if (source.x <= 0f || source.y <= 0f) return;
                spriteRenderer.transform.localScale = new Vector3(
                    targetSize.x / source.x, targetSize.y / source.y, 1f);
            });
        }

        public static string[] UnitAnimationLocations(GameConfig.takeover.Unit unit)
        {
            string root = UnitLocation(unit);
            return new[] { root + "_f0", root + "_f1", root + "_f2" };
        }

        private static string ColorName(Faction faction)
        {
            switch (faction)
            {
                case Faction.Westaria: return "green";
                case Faction.Khaganate: return "red";
                case Faction.Cult: return "blue";
                case Faction.Empire: return "purple";
                default: return "gray";
            }
        }

        private static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            var builder = new StringBuilder(value.Length);
            bool previousUnderscore = false;
            foreach (char raw in value.Trim().ToLowerInvariant())
            {
                char c = char.IsLetterOrDigit(raw) ? raw : '_';
                if (c == '_')
                {
                    if (previousUnderscore) continue;
                    previousUnderscore = true;
                }
                else
                {
                    previousUnderscore = false;
                }
                builder.Append(c);
            }
            return builder.ToString().Trim('_');
        }
    }
}
