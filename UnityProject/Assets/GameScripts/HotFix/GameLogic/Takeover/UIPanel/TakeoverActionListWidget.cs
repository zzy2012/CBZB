using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace GameLogic.Takeover
{
    public struct TakeoverActionPlan
    {
        public string Name;
        public string Label;
        public Vector2 Position;
        public Vector2 Size;
        public int FontSize;
        public UnityAction Click;
        public Func<bool> Enabled;
    }

    public abstract class TakeoverActionListWidget : UIWidget
    {

        private readonly List<TakeoverActionButtonWidget> _buttons =
            new List<TakeoverActionButtonWidget>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder(256);
        private string _lastSignature;

        protected virtual Transform ActionButtonParent => transform;

        protected void RefreshActionButtons(
            IReadOnlyList<TakeoverActionPlan> plans, string context, bool stateTint = false)
        {
            string signature = BuildSignature(plans, context, stateTint);
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                BindSlots(plans, stateTint);
                return;
            }

            int count = Math.Min(_buttons.Count, plans.Count);
            for (int i = 0; i < count; i++)
            {
                TakeoverActionPlan plan = plans[i];
                _buttons[i].SetInteractable(plan.Enabled == null || plan.Enabled());
            }
        }

        protected void InvalidateActionButtons()
        {
            _lastSignature = null;

        }

        private string BuildSignature(
            IReadOnlyList<TakeoverActionPlan> plans, string context, bool stateTint)
        {
            _signatureBuilder.Clear();
            _signatureBuilder.Append(context).Append('#').Append(stateTint ? '1' : '0').Append(';');
            for (int i = 0; i < plans.Count; i++)
            {
                TakeoverActionPlan plan = plans[i];
                _signatureBuilder.Append(plan.Name).Append('|')
                    .Append(plan.Label).Append('|')
                    .Append(plan.Position.x).Append(',').Append(plan.Position.y).Append('|')
                    .Append(plan.Size.x).Append(',').Append(plan.Size.y).Append('|')
                    .Append(plan.FontSize).Append(';');
            }
            return _signatureBuilder.ToString();
        }

        private void BindSlots(IReadOnlyList<TakeoverActionPlan> plans, bool stateTint)
        {
            if (_buttons.Count == 0)
            {
                Transform parent = ActionButtonParent;
                for (int index = 0; index < parent.childCount; index++)
                {
                    Transform slot = parent.GetChild(index);
                    if (slot.name.StartsWith("ActionSlot", StringComparison.Ordinal))
                        _buttons.Add(CreateWidget<TakeoverActionButtonWidget>(slot.gameObject, false));
                }
            }

            if (plans.Count > _buttons.Count)
                throw new InvalidOperationException($"{GetType().Name} needs {plans.Count} action slots.");

            for (int index = 0; index < _buttons.Count; index++)
            {
                bool visible = index < plans.Count;
                if (visible)
                    _buttons[index].Configure(plans[index], stateTint);
                _buttons[index].Visible = visible;
            }
        }
    }
}