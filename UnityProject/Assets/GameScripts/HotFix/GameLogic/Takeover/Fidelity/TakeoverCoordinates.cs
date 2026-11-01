using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// Conversion between the doubled Flash simulation Y axis and Unity's
    /// bottom-left display space.
    /// </summary>
    public static class TakeoverCoordinates
    {
        public static Vector2 LogicalToDisplay(Vector2 logical)
        {
            return new Vector2(
                logical.x,
                OriginalRules.NativeStageHeight - logical.y / OriginalRules.WorldYScale);
        }

        public static Vector2 LogicalToMapPixel(Vector2 logical)
        {
            return new Vector2(logical.x, logical.y / OriginalRules.WorldYScale);
        }
    }
}
