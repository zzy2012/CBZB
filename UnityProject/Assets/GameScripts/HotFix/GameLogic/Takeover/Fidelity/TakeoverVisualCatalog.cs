using System.Linq;
using System.Text;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 管理与外部 SWF 资源提取流程共用的 YooAsset 资源地址。
    /// 建筑贴图来自 Luban 表 building.xlsx 的 sprite 列（默认底图，各阵营共用），
    /// 阵营差异由 TakeoverRunner 的颜色叠加区分；其余类别仍按命名约定拼地址。
    /// </summary>
    public static class TakeoverVisualCatalog
    {
        public static string UnitLocation(GameConfig.takeover.Unit unit) =>
            $"takeover_unit_{unit.Id}_" + Slug(unit.Name);

        /// <summary>建筑默认贴图：读 building.xlsx 的 sprite 列，空串表示该建筑没有配置贴图。</summary>
        public static string BuildingLocation(int buildingId) =>
            Cfg.BuildingById(buildingId)?.Sprite ?? string.Empty;

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
                ? BuildingLocation(node.Building.Kind)
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

        /// <summary>
        /// 地图贴图相对舞台坐标的修正量（level.xlsx 的 map_offset_x/y）。
        /// 地图 PNG 是从导出位图居中裁切来的，和舞台坐标差一个固定平移；
        /// 这个值表示“舞台坐标 + 偏移 = 贴图像素”，所以渲染时要对 x 取反、y 同号。
        /// </summary>
        public static Vector2 MapOffset(int levelAssetId)
        {
            GameConfig.takeover.Level level =
                Cfg.TbLevel.DataList.FirstOrDefault(value => value.AssetId == levelAssetId);
            return level == null ? Vector2.zero : new Vector2(level.MapOffsetX, level.MapOffsetY);
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
