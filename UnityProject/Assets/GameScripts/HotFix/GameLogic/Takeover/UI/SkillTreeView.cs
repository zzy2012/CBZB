using System.Collections.Generic;
using GameConfig.takeover;
using GameLogic.UI;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>教令（技能）树：按当前阵营展示 9 条教令，点击购买下一档。</summary>
    public sealed class SkillTreeView : UIView
    {
        private TakeoverRunner _runner;
        private VisualElement _grid;

        protected override string UxmlLocation => "TakeoverSkillTreeView";

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            Require("skill-grid", "points-label", "reset-button", "ok-button");

            Button reset = Q<Button>("reset-button");
            if (reset != null)
            {
                reset.text = "重选";
                reset.clicked += OnResetClicked;
            }

            Button ok = Q<Button>("ok-button");
            if (ok != null)
            {
                ok.text = "OK";
                ok.clicked += () => _runner?.ShowCampaignMap();
            }

            _grid = Q<VisualElement>("skill-grid");
            Refresh();
        }

        private void Refresh()
        {
            if (_grid == null) return;
            _grid.Clear();

            Label points = Q<Label>("points-label");
            if (points != null)
                points.text = $"已获得技能点：{Campaign.EdictPoints}";

            IReadOnlyList<int> ids = TakeoverEdicts.EdictIdsOf(Campaign.SelectedFaction);
            foreach (int edictId in ids)
            {
                _grid.Add(CreateCard(edictId));
            }

            if (ids.Count == 0)
            {
                var empty = new Label("该阵营暂无教令数据");
                empty.AddToClassList("muted");
                _grid.Add(empty);
            }

            UIFontProvider.ApplyToTree(Root);
            UIToolkitTheme.Apply(Root);
        }

        private Button CreateCard(int edictId)
        {
            int tier = Campaign.EdictTier(edictId);
            bool maxed = tier >= TakeoverEdicts.MaxTier;
            bool available = Campaign.CanBuyEdict(edictId);
            int cost = TakeoverEdicts.NextTierCost(edictId);
            Edict next = maxed ? null : TakeoverEdicts.TierRow(edictId, tier + 1);
            Edict current = tier > 0 ? TakeoverEdicts.TierRow(edictId, tier) : null;

            var card = new Button { text = string.Empty };
            card.AddToClassList("skill-card");
            if (available) card.AddToClassList("is-available");
            else card.AddToClassList("is-locked");

            var icon = new Label(TakeoverUiContent.EdictIcon(edictId));
            icon.AddToClassList("skill-icon");
            card.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("skill-info");

            var title = new Label(next?.CnName ?? current?.CnName ?? $"教令 {edictId}");
            title.AddToClassList("skill-name");
            info.Add(title);

            var rank = new Label($"Lv.{tier}/{TakeoverEdicts.MaxTier}");
            rank.AddToClassList("skill-rank");
            info.Add(rank);

            for (int t = 1; t <= TakeoverEdicts.MaxTier; t++)
            {
                Edict row = TakeoverEdicts.TierRow(edictId, t);
                if (row == null) continue;
                var effect = new Label($"{t}　{row.CnDesc}");
                effect.AddToClassList("skill-effect");
                if (t <= tier) effect.AddToClassList("is-owned");
                info.Add(effect);
            }

            var costLabel = new Label(maxed ? "已满级" : $"{cost} 点");
            costLabel.AddToClassList("skill-cost");
            info.Add(costLabel);
            card.Add(info);

            card.clicked += () => OnBuyClicked(edictId);
            return card;
        }

        private void OnBuyClicked(int edictId)
        {
            if (Campaign.EdictTier(edictId) >= TakeoverEdicts.MaxTier)
            {
                UIManager.Toast("已经满级。");
                return;
            }

            string error = Campaign.TryBuyEdict(edictId);
            if (error != null)
            {
                UIManager.Toast(error);
                return;
            }

            int tier = Campaign.EdictTier(edictId);
            Edict row = TakeoverEdicts.TierRow(edictId, tier);
            UIManager.Toast($"已解锁 {row?.CnName ?? "教令"} 第 {tier} 档");
            Refresh();
        }

        private void OnResetClicked()
        {
            Campaign.ResetEdicts();
            Refresh();
            UIManager.Toast("已重置技能配置。");
        }
    }
}
