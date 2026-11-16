using System;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>管理战斗实例，并按固定时间步推进战斗。</summary>
    public sealed class BattleManager : IDisposable
    {
        /// <summary>战斗模拟的固定时间步长，单位为秒。</summary>
        public const float FixedStep = 1f / 30f;
        /// <summary>单次更新允许累积的最大实际时间，单位为秒。</summary>
        private const float MaxTickSeconds = 40f;
        /// <summary>单次更新允许执行的最大固定步数。</summary>
        private const int MaxStepsPerTick = 1500;

        /// <summary>由管理器驱动的当前战斗实例。</summary>
        private Battle _battle;
        /// <summary>等待转换为固定步更新的时间累积值。</summary>
        private float _accumulator;

        /// <summary>当前战斗实例。</summary>
        public Battle Battle => _battle;

        /// <summary>销毁现有战斗并初始化指定战斗实例。</summary>
        public void CreateBattle(Battle battle)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            DestroyBattle();
            _battle = battle;
            _battle.OnInit();
        }

        /// <summary>按实际时间累积固定步长，并在每步前调用可选回调。</summary>
        public void Update(float realDeltaSeconds, Action beforeFixedStep = null)
        {
            if (_battle == null || realDeltaSeconds <= 0f) return;
            float budget = Mathf.Min(realDeltaSeconds, MaxTickSeconds);
            _accumulator += budget;
            int maxSteps = Mathf.Clamp(Mathf.CeilToInt(budget / FixedStep) + 2, 1, MaxStepsPerTick);
            int steps = 0;
            while (_accumulator >= FixedStep && steps < maxSteps)
            {
                beforeFixedStep?.Invoke();
                _battle.OnUpdate(FixedStep);
                _accumulator -= FixedStep;
                steps++;
            }
            if (steps >= maxSteps) _accumulator = 0f;
        }

        /// <summary>销毁当前战斗并清空时间累积。</summary>
        public void DestroyBattle()
        {
            _battle?.OnDestroy();
            _battle = null;
            _accumulator = 0f;
        }

        /// <summary>销毁当前战斗并释放管理器持有的战斗状态。</summary>
        public void Dispose()
        {
            DestroyBattle();
        }
    }
}
