using System.Collections.Generic;
using GameConfig.takeover;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 关卡选择界面里的教令面板（临时 UI：纯色底板 + 复用 TakeoverUIActionButton 列表）。
    /// 数据来自 edict.xlsx（takeover.TbEdict），已购档位存在 CampaignSave。
    /// </summary>
    public sealed class TakeoverEdictPanelWidget : TakeoverActionListWidget
    {
        private const float RowTop = 150f;
        private const float RowSpacing = 36f;
        private static readonly Vector2 RowSize = new Vector2(490f, 34f);

        private readonly List<TakeoverActionPlan> _plan = new List<TakeoverActionPlan>(TakeoverEdicts.EdictsPerFaction);
        private Text _title;
        private Text _points;
        private Text _hint;
        private RectTransform _list;
        private Button _close;
        private Font _font;

        protected override Transform ActionButtonParent => _list != null ? _list : transform;

        protected override void ScriptGenerator()
        {
            _title = Required<Text>("m_text_EdictTitle");
            _points = Required<Text>("m_text_EdictPoints");
            _hint = Required<Text>("m_text_EdictHint");
            _list = Required<RectTransform>("m_rect_EdictList");
            _close = Required<Button>("m_btn_EdictClose");
        }

        protected override void RegisterEvent()
        {
            if (_close != null) _close.onClick.AddListener(() => SetVisible(false));
        }

        public void SetFont(Font font)
        {
            _font = font;
            if (_font != null) TakeoverUIResources.ApplyFont(gameObject, _font);
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        /// <summary>按当前阵营 + 已购档位重建列表。</summary>
        public void Refresh()
        {
            Faction faction = TakeoverEdicts.CurrentFaction();
            if (_title != null) _title.text = $"教令 · {TakeoverEdicts.FactionDisplayName(faction)}";
            if (_points != null) _points.text = $"教令点：{Campaign.EdictPoints}";

            _plan.Clear();
            IReadOnlyList<int> ids = TakeoverEdicts.EdictIdsOf(faction);
            if (ids.Count == 0)
            {
                if (_hint != null) _hint.text = "该阵营暂无教令数据";
                RefreshActionButtons(_plan, $"edict:{faction}:empty", stateTint: true);
                return;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                int edictId = ids[i];
                int tier = Campaign.EdictTier(edictId);
                bool maxed = tier >= TakeoverEdicts.MaxTier;
                int cost = TakeoverEdicts.NextTierCost(edictId);
                Edict next = maxed ? null : TakeoverEdicts.TierRow(edictId, tier + 1);
                Edict current = tier > 0 ? TakeoverEdicts.TierRow(edictId, tier) : null;
                string name = (next ?? current)?.CnName ?? $"教令 {edictId}";
                string desc = maxed
                    ? (current?.CnDesc ?? "已满级")
                    : (next?.CnDesc ?? string.Empty);
                string costText = maxed ? "已满级" : $"{cost} 点";

                _plan.Add(new TakeoverActionPlan
                {
                    Name = $"Edict{edictId}",
                    Label = $"{name}  {tier}/{TakeoverEdicts.MaxTier}   {costText}\n{desc}",
                    Position = new Vector2(0f, RowTop - i * RowSpacing),
                    Size = RowSize,
                    FontSize = 10,
                    Click = () => Buy(edictId),
                    Enabled = () => Campaign.CanBuyEdict(edictId),
                });
            }

            if (_hint != null)
                _hint.text = $"每档花费 = 教令点（首通 +2，首次三星 +2）";
            RefreshActionButtons(_plan, $"edict:{faction}:{Campaign.EdictPoints}", stateTint: true);
            if (_font != null) TakeoverUIResources.ApplyFont(gameObject, _font);
        }

        private void Buy(int edictId)
        {
            string error = Campaign.TryBuyEdict(edictId);
            if (error != null)
            {
                TakeoverToastUI.Show(error, 2.5f);
                return;
            }
            int tier = Campaign.EdictTier(edictId);
            Edict row = TakeoverEdicts.TierRow(edictId, tier);
            TakeoverToastUI.Show($"已解锁 {row?.CnName} 第 {tier} 档", 2.5f);
            Refresh();
        }

        private T Required<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new System.InvalidOperationException(
                    $"TakeoverEdictPanelWidget prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
