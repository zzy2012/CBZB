using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>用于播放原始精灵序列的简易动画器，在 SWF 时间轴还原为完整 Animator Controller 前使用。</summary>
    public sealed class TakeoverUnitAnimator : MonoBehaviour
    {
        /// <summary>单位动画每帧的播放时长，单位为秒。</summary>
        private const float FrameSeconds = 0.14f;
        /// <summary>用于显示当前单位动画帧的精灵渲染器。</summary>
        private SpriteRenderer _renderer;
        /// <summary>单位动画各帧对应的精灵资源位置。</summary>
        private string[] _locations;
        /// <summary>当前显示的动画帧索引。</summary>
        private int _frame;
        /// <summary>距离切换到下一动画帧的累计时间。</summary>
        private float _timer;

        /// <summary>使用渲染器、单位配置和部队标识初始化动画。</summary>
        public void Initialize(SpriteRenderer renderer, GameConfig.takeover.Unit unit, int squadId)
        {
            _renderer = renderer;
            _renderer.enabled = false;
            _locations = TakeoverVisualCatalog.UnitAnimationLocations(unit);
            _frame = Mathf.Abs(squadId) % Mathf.Max(1, _locations.Length);
            _timer = 0.5f + FrameSeconds * (_frame + 1);
        }

        /// <summary>累计动画时间并在需要时切换单位动画帧。</summary>
        private void Update()
        {
            if (_renderer == null || _locations == null || _locations.Length == 0) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer += FrameSeconds;
            _frame = (_frame + 1) % _locations.Length;
            Apply();
        }

        /// <summary>将当前动画帧对应的精灵应用到渲染器。</summary>
        private void Apply()
        {
            string location = _locations[_frame];
            if (!GameModule.Resource.CheckLocationValid(location)) return;
            TakeoverSpriteCache.Apply(_renderer, location);
            _renderer.enabled = true;
        }
    }
}
