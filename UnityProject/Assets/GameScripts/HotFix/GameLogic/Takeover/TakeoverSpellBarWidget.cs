using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Takeover
{
    public sealed class TakeoverSpellBarWidget : TakeoverActionListWidget
    {
        private readonly List<TakeoverActionPlan> _plan = new List<TakeoverActionPlan>(8);
        private TakeoverRunner _runner;
        private BattleSimulation _lastSimulation;

        public void SetRunner(TakeoverRunner runner)
        {
            _runner = runner;
            Refresh();
        }

        public void Refresh()
        {
            _plan.Clear();
            if (_runner == null || _runner.Simulation == null)
            {
                RefreshActionButtons(_plan, "none");
                return;
            }

            BattleSimulation simulation = _runner.Simulation;
            if (_lastSimulation != simulation)
            {
                _lastSimulation = simulation;
                InvalidateActionButtons();
            }

            var human = simulation.Human;
            var spells = simulation.State.AvailableSpells(human);
            for (int i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                int cost = Mathf.RoundToInt(spell.Cost * human.ModMul(35));
                _plan.Add(new TakeoverActionPlan
                {
                    Name = $"Spell{spell.Id}",
                    Label = $"{spell.CnName}\n{(spell.SpellType == 2 ? "终极" : spell.Cost.ToString())}",
                    Position = new Vector2(-126f + i * 82f, 0f),
                    Size = new Vector2(76f, 40f),
                    FontSize = 11,
                    Click = () => _runner.BeginSpellTarget(spell),
                    Enabled = () => _runner.Simulation != null &&
                        (spell.SpellType == 2
                            ? _runner.Simulation.Human.Ultimate + 0.0001f >=
                              _runner.Simulation.Human.UltimateMax
                            : _runner.Simulation.Human.Mana >= cost),
                });
            }

            RefreshActionButtons(_plan, $"spell:{simulation.LevelAssetId}");
        }
    }
}
