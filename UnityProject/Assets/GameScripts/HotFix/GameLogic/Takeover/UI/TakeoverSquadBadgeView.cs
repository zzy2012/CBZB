using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class TakeoverSquadBadgeView
    {
        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);

        private readonly Image _ring;
        private readonly Image _portrait;
        private readonly Image _hpFill;
        private readonly List<Image> _statusIcons = new List<Image>(3);
        private readonly string[] _statusLocations = new string[3];
        private readonly CancellationToken _token;
        private Squad _squad;

        public RectTransform Root { get; }

        public TakeoverSquadBadgeView(Transform parent, Squad squad, Action pointerDown,
            CancellationToken token)
        {
            _token = token;
            var go = new GameObject($"SquadBadge_{squad.Id}", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(parent, false);
            Root = (RectTransform)go.transform;
            TakeoverUiFactory.SetCentered(Root, new Vector2(44f, 44f));
            Image target = go.GetComponent<Image>();
            target.color = new Color(1f, 1f, 1f, 0.01f);
            target.raycastTarget = true;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            go.AddComponent<TakeoverSquadBadgePointer>().Bind(pointerDown);

            _ring = target;
            Outline outline = go.GetComponent<Outline>();
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = false;

            _portrait = TakeoverUiFactory.CreateImage(go.transform, "Portrait", Color.white);
            TakeoverUiFactory.Anchor(_portrait.rectTransform, new Vector2(0.12f, 0.12f),
                new Vector2(0.88f, 0.88f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _portrait.preserveAspect = true;
            _portrait.raycastTarget = false;
            _hpFill = TakeoverUiFactory.CreateImage(go.transform, "Health",
                HpHigh, false);
            TakeoverUiFactory.Anchor(_hpFill.rectTransform, new Vector2(0.08f, 0.02f),
                new Vector2(0.92f, 0.12f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;

            RectTransform status = TakeoverUiFactory.CreateRow(go.transform, "Statuses", 1f);
            TakeoverUiFactory.Anchor(status, new Vector2(0.04f, 0.78f),
                new Vector2(0.96f, 1.12f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 3; i++)
            {
                Image icon = TakeoverUiFactory.CreateImage(status, $"Status{i}", Color.white, false);
                icon.gameObject.SetActive(false);
                TakeoverUiFactory.SetPreferredWidth(icon, 11f);
                TakeoverUiFactory.SetPreferredHeight(icon, 11f);
                _statusIcons.Add(icon);
            }
            Refresh(squad, false);
        }

        public void Refresh(Squad squad, bool selected)
        {
            if (_squad != squad)
            {
                _squad = squad;
                UISpriteCache.ApplyAsync(_portrait, TakeoverVisualCatalog.UnitLocation(squad.Def), _token).Forget();
            }
            float maxHp = squad.HpPerSoldier * squad.MaxSoldiers;
            float ratio = maxHp <= 0f ? 0f : Mathf.Clamp01(squad.Hp / maxHp);
            _hpFill.fillAmount = ratio;
            _hpFill.color = ratio > 0.6f ? HpHigh : ratio > 0.3f ? HpMid : HpLow;
            Color ringColor = selected ? RingSelected : squad.Owner.IsHuman ? RingHuman : RingEnemy;
            Outline outline = Root.GetComponent<Outline>();
            outline.effectColor = ringColor;
            _ring.color = new Color(ringColor.r, ringColor.g, ringColor.b, 0.01f);
            RefreshStatuses();
        }

        public void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }

        private void RefreshStatuses()
        {
            bool frozen = false, poison = false, charge = false, shield = false, fear = false;
            foreach (CombatUnit unit in _squad.Units)
            {
                if (unit.IsDead) continue;
                frozen |= unit.FrozenSeconds > 0f;
                poison |= unit.PoisonSeconds > 0f;
                charge |= unit.ChargeSpeedSeconds > 0f || unit.DashSeconds > 0f;
                shield |= unit.DamageResistSeconds > 0f;
                fear |= unit.FearedSeconds > 0f;
            }

            var active = new List<string>(5);
            if (frozen) active.Add("takeover_status_frozen");
            if (poison) active.Add("takeover_status_poison");
            if (charge) active.Add("takeover_status_charge");
            if (shield) active.Add("takeover_status_shield");
            if (fear) active.Add("takeover_status_fear");

            for (int i = 0; i < _statusIcons.Count; i++)
            {
                Image icon = _statusIcons[i];
                string location = i < active.Count ? active[i] : null;
                icon.gameObject.SetActive(!string.IsNullOrEmpty(location));
                if (string.IsNullOrEmpty(location) || _statusLocations[i] == location) continue;
                _statusLocations[i] = location;
                UISpriteCache.ApplyAsync(icon, location, _token).Forget();
            }
        }
    }
}
