using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class TakeoverBuildingBadgeView
    {
        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);

        private readonly Outline _outline;
        private readonly Image _hpFill;
        private readonly Text _level;

        public RectTransform Root { get; }

        public TakeoverBuildingBadgeView(Transform parent)
        {
            var go = new GameObject("BuildingBadge", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            Root = (RectTransform)go.transform;
            TakeoverUiFactory.SetCentered(Root, new Vector2(62f, 30f));
            Image background = go.GetComponent<Image>();
            background.color = new Color(0.045f, 0.05f, 0.04f, 0.9f);
            background.raycastTarget = false;
            _outline = go.GetComponent<Outline>();
            _outline.effectDistance = new Vector2(2f, 2f);

            _level = TakeoverUiFactory.CreateText(go.transform, "Level", string.Empty,
                13, Color.white, TextAnchor.MiddleCenter);
            TakeoverUiFactory.Anchor(_level.rectTransform, new Vector2(0f, 0.35f),
                new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _hpFill = TakeoverUiFactory.CreateImage(go.transform, "Health", HpHigh, false);
            TakeoverUiFactory.Anchor(_hpFill.rectTransform, new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.25f), Vector2.zero, Vector2.zero, new Vector2(0f, 0.5f));
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
        }

        public void Refresh(MapNode node, bool selected, bool humanOwned)
        {
            BuildingState building = node.Building;
            if (building == null) return;
            float ratio = building.MaxHp <= 0 ? 0f :
                Mathf.Clamp01((float)building.Hp / building.MaxHp);
            _hpFill.fillAmount = ratio;
            _hpFill.color = ratio > 0.6f ? HpHigh : ratio > 0.3f ? HpMid : HpLow;
            _level.text = $"{node.Key}  Lv.{building.Level + 1}";
            _outline.effectColor = selected ? RingSelected : humanOwned ? RingHuman : RingEnemy;
        }

        public void Destroy()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
