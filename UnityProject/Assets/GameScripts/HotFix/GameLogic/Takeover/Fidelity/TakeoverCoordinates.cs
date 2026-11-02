using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 在 Flash 模拟中加倍的 Y 轴坐标与 Unity 左下原点显示坐标之间进行转换。
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
