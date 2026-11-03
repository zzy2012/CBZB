using System.Globalization;
using UnityEngine;

namespace GameLogic.UI
{
    /// <summary>界面数字/时间格式，保证和 index.html 的 1,234 / 05:30 口径一致。</summary>
    public static class UIFormat
    {
        public static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        public static string Number(float value) => Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture);

        public static string Time(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        public static string Stars(int stars)
        {
            return new string('★', Mathf.Clamp(stars, 0, 3));
        }
    }
}
