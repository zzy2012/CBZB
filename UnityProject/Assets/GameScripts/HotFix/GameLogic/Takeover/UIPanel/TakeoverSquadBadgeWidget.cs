using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>显示部队头像、生命和选中状态的界面标记。</summary>
    public sealed class TakeoverSquadBadgeWidget : UIWidget
    {
        /// <summary>部队生命值较高时使用的颜色。</summary>
        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        /// <summary>部队生命值中等时使用的颜色。</summary>
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        /// <summary>部队生命值较低时使用的颜色。</summary>
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        /// <summary>人类阵营部队的标记边框颜色。</summary>
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        /// <summary>敌方部队的标记边框颜色。</summary>
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        /// <summary>选中部队的标记边框颜色。</summary>
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);
        /// <summary>状态图标的显示优先级顺序。</summary>
        private static readonly string[] StatusPriority =
        {
            "takeover_status_frozen",
            "takeover_status_poison",
            "takeover_status_charge",
            "takeover_status_shield",
            "takeover_status_fear",
        };

        /// <summary>部队标记上同时显示的状态图标数量上限。</summary>
        private const int MaxVisibleStatus = 3;
        /// <summary>状态图标列的右侧横坐标。</summary>
        private const float StatusIconRightX = 13f;
        /// <summary>相邻状态图标的横向间距。</summary>
        private const float StatusIconStep = 15f;
        /// <summary>状态图标的纵向坐标。</summary>
        private const float StatusIconY = 15f;

        /// <summary>显示部队状态效果的图标集合。</summary>
        private readonly Dictionary<string, Image> _statusIcons = new Dictionary<string, Image>();
        /// <summary>刷新状态图标时使用的临时缓冲区。</summary>
        private readonly List<string> _statusBuffer = new List<string>(5);
        /// <summary>部队标记的矩形变换组件。</summary>
        private RectTransform _rect;
        /// <summary>部队生命条的填充图像。</summary>
        private Image _hpFill;
        /// <summary>部队标记的边框图像。</summary>
        private Image _ring;
        /// <summary>部队单位头像图像。</summary>
        private Image _portrait;

        /// <summary>所属或绑定的部队。</summary>
        public SquadEntity Squad { get; private set; }
        /// <summary>标记使用的界面矩形变换。</summary>
        public RectTransform RectTransform => _rect;
        /// <summary>显示标记所需的界面组件是否齐全。</summary>
        public bool IsValid => _rect != null && _hpFill != null && _ring != null && _portrait != null;

        /// <summary>生成部队标记所需的界面组件。</summary>
        protected override void ScriptGenerator()
        {
            _rect = rectTransform;
            _ring = Required<Image>("m_img_Ring");
            _hpFill = Required<Image>("m_img_HpFill");
            _portrait = Required<Image>("m_img_Portrait");
            _statusIcons.Clear();
            _statusIcons.Add(StatusPriority[0], Required<Image>("m_img_StatusFrozen"));
            _statusIcons.Add(StatusPriority[1], Required<Image>("m_img_StatusPoison"));
            _statusIcons.Add(StatusPriority[2], Required<Image>("m_img_StatusCharge"));
            _statusIcons.Add(StatusPriority[3], Required<Image>("m_img_StatusShield"));
            _statusIcons.Add(StatusPriority[4], Required<Image>("m_img_StatusFear"));
        }

        /// <summary>将部队标记绑定到指定部队。</summary>
        public void Bind(SquadEntity squad)
        {
            Squad = squad;
            gameObject.name = $"m_item_SquadBadge_{squad.Id}";
            string location = TakeoverVisualCatalog.UnitLocation(squad.Def);
            if (!string.IsNullOrEmpty(location))
            {
                _portrait.SetSprite(location, false, null,
                    _portrait.GetCancellationTokenOnDestroy());
            }
        }

        /// <summary>设置部队标记的界面位置。</summary>
        public void SetPosition(Vector2 position)
        {
            _rect.anchoredPosition = position;
        }

        /// <summary>根据部队状态、选中状态和缩放刷新标记。</summary>
        public void RefreshVisuals(bool selected, float scale)
        {
            if (!IsValid || Squad == null) return;

            float maxHp = Squad.HpPerSoldier * Squad.MaxSoldiers;
            float ratio = maxHp <= 0f ? 0f : Mathf.Clamp01(Squad.Hp / maxHp);
            _hpFill.fillAmount = ratio;
            _hpFill.color = ratio > 0.6f ? HpHigh : ratio > 0.3f ? HpMid : HpLow;
            _ring.color = selected ? RingSelected : Squad.Owner.IsHuman ? RingHuman : RingEnemy;
            float selectedScale = scale * (selected ? 1.12f : 1f);
            _rect.localScale = new Vector3(selectedScale, selectedScale, 1f);
            RefreshStatusIcons();
        }

        /// <summary>判断屏幕坐标是否位于部队标记内。</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint, Camera uiCamera)
        {
            return _rect != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, uiCamera);
        }

        /// <summary>按优先级刷新部队状态效果图标。</summary>
        private void RefreshStatusIcons()
        {
            _statusBuffer.Clear();
            bool frozen = false;
            bool poison = false;
            bool charge = false;
            bool shield = false;
            bool fear = false;
            for (int i = 0; i < Squad.Units.Count; i++)
            {
                CombatUnitEntity unit = Squad.Units[i];
                if (unit.IsDead) continue;
                if (unit.FrozenSeconds > 0f) frozen = true;
                if (unit.PoisonSeconds > 0f) poison = true;
                if (unit.ChargeSpeedSeconds > 0f || unit.DashSeconds > 0f) charge = true;
                if (unit.DamageResistSeconds > 0f) shield = true;
                if (unit.FearedSeconds > 0f) fear = true;
            }

            if (frozen) _statusBuffer.Add(StatusPriority[0]);
            if (poison) _statusBuffer.Add(StatusPriority[1]);
            if (charge) _statusBuffer.Add(StatusPriority[2]);
            if (shield) _statusBuffer.Add(StatusPriority[3]);
            if (fear) _statusBuffer.Add(StatusPriority[4]);

            foreach (Image icon in _statusIcons.Values)
            {
                if (icon.gameObject.activeSelf)
                    icon.gameObject.SetActive(false);
            }
            for (int i = 0; i < _statusBuffer.Count && i < MaxVisibleStatus; i++)
            {
                Image icon = _statusIcons[_statusBuffer[i]];
                icon.gameObject.SetActive(true);
                var iconRect = icon.transform as RectTransform;
                if (iconRect != null)
                {
                    iconRect.anchoredPosition = new Vector2(
                        StatusIconRightX - StatusIconStep * i, StatusIconY);
                }
            }
        }

        /// <summary>查找并返回指定的必需界面组件。</summary>
        private T Required<T>(string path) where T : UnityEngine.Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new System.InvalidOperationException(
                    $"Squad badge prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
