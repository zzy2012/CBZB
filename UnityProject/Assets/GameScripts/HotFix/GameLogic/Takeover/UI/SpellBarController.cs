using System.Collections.Generic;
using System.Text;
using GameConfig.takeover;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>战斗法术栏：列出当前可用法术，点击进入选点施法。</summary>
    public sealed class SpellBarController
    {
        private readonly TakeoverRunner _runner;
        private VisualElement _list;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Spell> _spells = new List<Spell>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder(128);
        private string _signature;

        public SpellBarController(TakeoverRunner runner)
        {
            _runner = runner;
        }

        public void Bind(VisualElement list)
        {
            _list = list;
            if (_list != null) _list.pickingMode = PickingMode.Position;
        }

        public void Refresh()
        {
            if (_list == null) return;
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null)
            {
                _list.style.display = DisplayStyle.None;
                return;
            }

            PlayerState human = simulation.Human;
            List<Spell> spells = simulation.State.AvailableSpells(human);
            string signature = BuildSignature(spells, human);
            if (signature != _signature)
            {
                _signature = signature;
                Rebuild(spells, human);
            }
            else
            {
                UpdateEnabled(spells, human);
            }

            _list.style.display = spells.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Rebuild(List<Spell> spells, PlayerState human)
        {
            _list.Clear();
            _buttons.Clear();
            _spells.Clear();

            foreach (Spell spell in spells)
            {
                Spell captured = spell;
                int cost = SpellCost(spell, human);
                bool ultimate = spell.SpellType == 2;
                var button = new Button
                {
                    text = $"{spell.CnName}\n{(ultimate ? "终极" : cost.ToString())}",
                };
                button.AddToClassList("spell-button");
                button.SetEnabled(CanCast(spell, human, cost));
                button.clicked += () => _runner.BeginSpellTarget(captured);
                _list.Add(button);
                _buttons.Add(button);
                _spells.Add(spell);
            }

            UIToolkitTheme.Apply(_list);
        }

        private void UpdateEnabled(List<Spell> spells, PlayerState human)
        {
            int count = Mathf.Min(spells.Count, _buttons.Count);
            for (int i = 0; i < count; i++)
            {
                int cost = SpellCost(spells[i], human);
                _buttons[i].SetEnabled(CanCast(spells[i], human, cost));
            }
        }

        private static int SpellCost(Spell spell, PlayerState human)
        {
            return Mathf.RoundToInt(spell.Cost * human.ModMul(35));
        }

        private static bool CanCast(Spell spell, PlayerState human, int cost)
        {
            if (spell.SpellType == 2)
                return human.Ultimate + 0.0001f >= human.UltimateMax;
            return human.Mana >= cost;
        }

        private string BuildSignature(List<Spell> spells, PlayerState human)
        {
            _signatureBuilder.Clear();
            foreach (Spell spell in spells)
            {
                _signatureBuilder.Append(spell.Id).Append(':')
                    .Append(spell.SpellType).Append(':')
                    .Append(SpellCost(spell, human)).Append(';');
            }
            return _signatureBuilder.ToString();
        }
    }
}
