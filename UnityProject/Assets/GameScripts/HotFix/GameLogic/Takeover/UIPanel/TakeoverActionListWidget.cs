using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
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
        private const string ActionButtonPrefabLocation = "TakeoverUIActionButton";

        private readonly List<TakeoverActionButtonWidget> _buttons =
            new List<TakeoverActionButtonWidget>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder(256);
        private string _lastSignature;
        private int _reconcileVersion;

        protected virtual Transform ActionButtonParent => transform;

        protected void RefreshActionButtons(
            IReadOnlyList<TakeoverActionPlan> plans, string context, bool stateTint = false)
        {
            string signature = BuildSignature(plans, context, stateTint);
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                int version = ++_reconcileVersion;
                var snapshot = new TakeoverActionPlan[plans.Count];
                for (int i = 0; i < plans.Count; i++)
                    snapshot[i] = plans[i];
                ReconcileActionButtonsAsync(snapshot, version, stateTint).Forget();
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
            _reconcileVersion++;
        }

        protected override void OnDestroy()
        {
            _reconcileVersion++;
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

        private async UniTask ReconcileActionButtonsAsync(
            TakeoverActionPlan[] plans, int version, bool stateTint)
        {
            Transform parent = ActionButtonParent;
            if (parent == null)
                throw new InvalidOperationException($"{GetType().Name} has no action button parent.");

            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].Visible = false;

            for (int i = 0; i < plans.Length; i++)
            {
                if (version != _reconcileVersion) return;
                if (i >= _buttons.Count)
                {
                    TakeoverActionButtonWidget button =
                        await CreateWidgetByPathAsync<TakeoverActionButtonWidget>(
                            parent, ActionButtonPrefabLocation, false);
                    if (button == null)
                        throw new InvalidOperationException(
                            $"Failed to create action button widget: {ActionButtonPrefabLocation}");
                    if (version != _reconcileVersion)
                    {
                        button.Destroy();
                        return;
                    }
                    _buttons.Add(button);
                }
            }

            if (version != _reconcileVersion) return;
            for (int i = _buttons.Count - 1; i >= plans.Length; i--)
            {
                TakeoverActionButtonWidget button = _buttons[i];
                _buttons.RemoveAt(i);
                button.Destroy();
            }

            for (int i = 0; i < plans.Length; i++)
            {
                if (version != _reconcileVersion) return;
                _buttons[i].Configure(plans[i], stateTint);
                _buttons[i].Visible = true;
            }
        }
    }
}
