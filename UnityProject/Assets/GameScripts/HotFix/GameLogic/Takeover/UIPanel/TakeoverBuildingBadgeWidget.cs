using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>建筑头顶徽标：阵营色环 + 等级数字 + 血条（节点结构来自 TakeoverBattleUI 预制体）。</summary>
    public sealed class TakeoverBuildingBadgeWidget : UIWidget
    {
        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);

        private RectTransform _rect;
        private Image _ring;
        private Image _hpFill;
        private Text _level;

        public MapNode Node { get; private set; }
        public RectTransform RectTransform => _rect;
        public bool IsValid => _rect != null && _ring != null && _hpFill != null && _level != null;

        protected override void ScriptGenerator()
        {
            _rect = rectTransform;
            _ring = Required<Image>("m_img_Ring");
            _hpFill = Required<Image>("m_img_HpFill");
            _level = Required<Text>("m_text_Level");
        }

        public void Bind(MapNode node)
        {
            Node = node;
            gameObject.name = $"m_item_BuildingBadge_{node.Id}";
        }

        public void SetPosition(Vector2 position)
        {
            _rect.anchoredPosition = position;
        }

        public void RefreshVisuals(bool selected, bool humanOwned, float scale)
        {
            if (!IsValid || Node == null || Node.Building == null) return;
            BuildingState building = Node.Building;
            float ratio = building.MaxHp <= 0 ? 0f : Mathf.Clamp01((float)building.Hp / building.MaxHp);
            _hpFill.fillAmount = ratio;
            _hpFill.color = ratio > 0.6f ? HpHigh : ratio > 0.3f ? HpMid : HpLow;
            // BuildingState.Level 是 0 基的升级次数，界面上显示 1..3 级
            _level.text = (building.Level + 1).ToString();
            _ring.color = selected ? RingSelected : humanOwned ? RingHuman : RingEnemy;
            float finalScale = scale * (selected ? 1.12f : 1f);
            _rect.localScale = new Vector3(finalScale, finalScale, 1f);
        }

        private T Required<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new System.InvalidOperationException(
                    $"Building badge prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
