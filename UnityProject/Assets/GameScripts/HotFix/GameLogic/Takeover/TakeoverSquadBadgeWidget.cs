using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverSquadBadgeWidget : UIWidget
    {
        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);
        private static readonly string[] StatusPriority =
        {
            "takeover_status_frozen",
            "takeover_status_poison",
            "takeover_status_charge",
            "takeover_status_shield",
            "takeover_status_fear",
        };

        private const int MaxVisibleStatus = 3;
        private const float StatusIconRightX = 13f;
        private const float StatusIconStep = 15f;
        private const float StatusIconY = 15f;

        private readonly Dictionary<string, Image> _statusIcons = new Dictionary<string, Image>();
        private readonly List<string> _statusBuffer = new List<string>(5);
        private RectTransform _rect;
        private Image _hpFill;
        private Image _ring;
        private Image _portrait;

        public Squad Squad { get; private set; }
        public RectTransform RectTransform => _rect;
        public bool IsValid => _rect != null && _hpFill != null && _ring != null && _portrait != null;

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

        public void Bind(Squad squad)
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

        public void SetPosition(Vector2 position)
        {
            _rect.anchoredPosition = position;
        }

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

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera uiCamera)
        {
            return _rect != null &&
                   RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, uiCamera);
        }

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
                CombatUnit unit = Squad.Units[i];
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
