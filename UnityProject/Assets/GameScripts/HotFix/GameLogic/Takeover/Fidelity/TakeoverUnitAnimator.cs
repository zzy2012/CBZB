using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>用于播放原始精灵序列的简易动画器，在 SWF 时间轴还原为完整 Animator Controller 前使用。</summary>
    public sealed class TakeoverUnitAnimator : MonoBehaviour
    {
        private const float FrameSeconds = 0.14f;
        private SpriteRenderer _renderer;
        private string[] _locations;
        private int _frame;
        private float _timer;

        public void Initialize(SpriteRenderer renderer, GameConfig.takeover.Unit unit, int squadId)
        {
            _renderer = renderer;
            _renderer.enabled = false;
            _locations = TakeoverVisualCatalog.UnitAnimationLocations(unit);
            _frame = Mathf.Abs(squadId) % Mathf.Max(1, _locations.Length);
            _timer = 0.5f + FrameSeconds * (_frame + 1);
        }

        private void Update()
        {
            if (_renderer == null || _locations == null || _locations.Length == 0) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer += FrameSeconds;
            _frame = (_frame + 1) % _locations.Length;
            Apply();
        }

        private void Apply()
        {
            string location = _locations[_frame];
            if (!GameModule.Resource.CheckLocationValid(location)) return;
            TakeoverSpriteCache.Apply(_renderer, location);
            _renderer.enabled = true;
        }
    }
}
