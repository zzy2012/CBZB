using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>建筑头顶徽标：阵营色环 + 等级数字 + 血条（节点结构来自 TakeoverBattleUI 预制体）。</summary>
    public sealed class TakeoverBuildingBadgeWidget : UIWidget
    {
        /// <summary>建筑生命值较高时使用的颜色。</summary>
        private static readonly Color HpHigh = new Color(0.42f, 0.80f, 0.42f, 1f);
        /// <summary>建筑生命值中等时使用的颜色。</summary>
        private static readonly Color HpMid = new Color(0.88f, 0.78f, 0.30f, 1f);
        /// <summary>建筑生命值较低时使用的颜色。</summary>
        private static readonly Color HpLow = new Color(0.85f, 0.32f, 0.26f, 1f);
        /// <summary>人类阵营建筑的标记边框颜色。</summary>
        private static readonly Color RingHuman = new Color(0.35f, 0.75f, 0.40f, 0.95f);
        /// <summary>敌方建筑的标记边框颜色。</summary>
        private static readonly Color RingEnemy = new Color(0.85f, 0.35f, 0.32f, 0.95f);
        /// <summary>选中建筑的标记边框颜色。</summary>
        private static readonly Color RingSelected = new Color(1f, 0.85f, 0.35f, 0.98f);

        /// <summary>建筑标记的矩形变换组件。</summary>
        private RectTransform _rect;
        /// <summary>建筑标记的边框图像。</summary>
        private Image _ring;
        /// <summary>建筑生命条的填充图像。</summary>
        private Image _hpFill;
        /// <summary>显示建筑等级的文本组件。</summary>
        private Text _level;

        /// <summary>关联的地图节点。</summary>
        public MapNodeEntity Node { get; private set; }
        /// <summary>标记使用的界面矩形变换。</summary>
        public RectTransform RectTransform => _rect;
        /// <summary>显示标记所需的界面组件是否齐全。</summary>
        public bool IsValid => _rect != null && _ring != null && _hpFill != null && _level != null;

        /// <summary>生成建筑标记所需的界面组件。</summary>
        protected override void ScriptGenerator()
        {
            _rect = rectTransform;
            _ring = Required<Image>("m_img_Ring");
            _hpFill = Required<Image>("m_img_HpFill");
            _level = Required<Text>("m_text_Level");
        }

        /// <summary>将建筑标记绑定到地图节点。</summary>
        public void Bind(MapNodeEntity node)
        {
            Node = node;
            gameObject.name = $"m_item_BuildingBadge_{node.Id}";
        }

        /// <summary>设置建筑标记的界面位置。</summary>
        public void SetPosition(Vector2 position)
        {
            _rect.anchoredPosition = position;
        }

        /// <summary>根据选中状态、归属和缩放刷新建筑标记。</summary>
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

        /// <summary>查找并返回指定的必需界面组件。</summary>
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
