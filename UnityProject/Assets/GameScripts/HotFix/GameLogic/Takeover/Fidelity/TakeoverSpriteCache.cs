using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 将世界场景精灵加载转交给 TEngine 共享资源缓存。
    /// </summary>
    public static class TakeoverSpriteCache
    {
        public static void Apply(SpriteRenderer renderer, string location,
            Action<SpriteRenderer> callback = null)
        {
            if (renderer == null || string.IsNullOrEmpty(location)) return;
            renderer.SetSprite(location, callback, renderer.GetCancellationTokenOnDestroy());
        }
    }
}
