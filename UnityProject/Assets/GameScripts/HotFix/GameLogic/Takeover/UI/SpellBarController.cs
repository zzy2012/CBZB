using System.Collections.Generic;
using System.Text;
using GameConfig.takeover;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class SpellBarController
    {
        private readonly TakeoverRunner _runner;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder(128);
        private RectTransform _root;
        private string _signature;

        public SpellBarController(TakeoverRunner runner) { _runner = runner; }

        public void Bind(Transform host)
        {
            _root = TakeoverUiFactory.CreateRow(host, "SpellBar", 6f);
            TakeoverUiFactory.Anchor(_root, new Vector2(0.68f, 0.04f),
                new Vector2(0.98f, 0.18f), Vector2.zero, Vector2.zero, new Vector2(1f, 0f));
            _root.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (_root == null) return;
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null)
            {
                _root.gameObject.SetActive(false);
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
            _root.gameObject.SetActive(spells.Count > 0);
        }

        private void Rebuild(List<Spell> spells, PlayerState human)
        {
            for (int i = _root.childCount - 1; i >= 0; i--)
                Object.Destroy(_root.GetChild(i).gameObject);
            _buttons.Clear();
            foreach (Spell spell in spells)
            {
                Spell captured = spell;
                int cost = SpellCost(spell, human);
                bool ultimate = spell.SpellType == 2;
                Button button = TakeoverUiFactory.CreateButton(_root, $"Spell{spell.Id}",
                    $"{spell.CnName}\n{(ultimate ? "终极" : cost.ToString())}",
                    () => _runner.BeginSpellTarget(captured), 13,
                    new Color(0.15f, 0.2f, 0.18f, 0.96f));
                button.interactable = CanCast(spell, human, cost);
                TakeoverUiFactory.SetPreferredWidth(button, 94f);
                TakeoverUiFactory.SetPreferredHeight(button, 66f);
                _buttons.Add(button);
            }
        }

        private void UpdateEnabled(List<Spell> spells, PlayerState human)
        {
            int count = Mathf.Min(spells.Count, _buttons.Count);
            for (int i = 0; i < count; i++)
                _buttons[i].interactable = CanCast(spells[i], human, SpellCost(spells[i], human));
        }

        private static int SpellCost(Spell spell, PlayerState human) =>
            Mathf.RoundToInt(spell.Cost * human.ModMul(35));

        private static bool CanCast(Spell spell, PlayerState human, int cost) =>
            spell.SpellType == 2
                ? human.Ultimate + 0.0001f >= human.UltimateMax
                : human.Mana >= cost;

        private string BuildSignature(List<Spell> spells, PlayerState human)
        {
            _signatureBuilder.Clear();
            foreach (Spell spell in spells)
                _signatureBuilder.Append(spell.Id).Append(':').Append(spell.SpellType).Append(':')
                    .Append(SpellCost(spell, human)).Append(';');
            return _signatureBuilder.ToString();
        }
    }
}
