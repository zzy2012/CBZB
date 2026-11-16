using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace GameLogic.Takeover
{
    /// <summary>描述界面操作按钮的内容、布局及交互。</summary>
    public struct TakeoverActionPlan
    {
        /// <summary>操作按钮对象的名称。</summary>
        public string Name;
        /// <summary>按钮显示的文本。</summary>
        public string Label;
        /// <summary>按钮的界面位置。</summary>
        public Vector2 Position;
        /// <summary>按钮的界面尺寸。</summary>
        public Vector2 Size;
        /// <summary>按钮文本的字号。</summary>
        public int FontSize;
        /// <summary>按钮被点击时执行的回调。</summary>
        public UnityAction Click;
        /// <summary>决定按钮是否可交互的条件回调。</summary>
        public Func<bool> Enabled;
    }

    /// <summary>提供动态操作按钮列表的界面基类。</summary>
    public abstract class TakeoverActionListWidget : UIWidget
    {

        /// <summary>当前操作列表中的按钮组件。</summary>
        private readonly List<TakeoverActionButtonWidget> _buttons =
            new List<TakeoverActionButtonWidget>();
        /// <summary>用于构造操作列表状态签名的字符串缓冲区。</summary>
        private readonly StringBuilder _signatureBuilder = new StringBuilder(256);
        /// <summary>上次绑定的操作列表状态签名。</summary>
        private string _lastSignature;

        /// <summary>动态操作按钮的父级变换。</summary>
        protected virtual Transform ActionButtonParent => transform;

        /// <summary>根据操作计划更新按钮集合及其可交互状态。</summary>
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

        /// <summary>使缓存的操作列表签名失效以触发重建。</summary>
        protected void InvalidateActionButtons()
        {
            _lastSignature = null;

        }

        /// <summary>根据操作计划构造用于检测变更的签名。</summary>
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

        /// <summary>将操作计划绑定到对应按钮组件。</summary>
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