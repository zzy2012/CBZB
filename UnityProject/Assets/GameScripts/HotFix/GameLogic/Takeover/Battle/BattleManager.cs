using System;
using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class BattleManager : IDisposable
    {
        public const float FixedStep = 1f / 30f;
        private const float MaxTickSeconds = 40f;
        private const int MaxStepsPerTick = 1500;

        private Battle _battle;
        private float _accumulator;

        public Battle Battle => _battle;

        public void CreateBattle(Battle battle)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));
            DestroyBattle();
            _battle = battle;
            _battle.OnInit();
        }

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

        public void DestroyBattle()
        {
            _battle?.OnDestroy();
            _battle = null;
            _accumulator = 0f;
        }

        public void Dispose()
        {
            DestroyBattle();
        }
    }
}
