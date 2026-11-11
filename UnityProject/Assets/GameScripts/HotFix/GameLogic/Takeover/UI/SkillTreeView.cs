using GameConfig.takeover;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.UI, location: "TakeoverUIWindow", fullScreen: true)]
    public sealed class SkillTreeView : UIView
    {
        private TakeoverRunner _runner;
        private RectTransform _list;

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            ClearContent();
            Image backdrop = TakeoverUiFactory.CreateImage(Root, "SkillBackdrop",
                new Color(0.06f, 0.07f, 0.06f, 1f));
            TakeoverUiFactory.Stretch(backdrop.rectTransform);

            RectTransform panel = TakeoverUiFactory.CreateColumn(Root, "SkillPanel", 12f);
            TakeoverUiFactory.Anchor(panel, new Vector2(0.08f, 0.06f),
                new Vector2(0.92f, 0.94f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            RectTransform heading = TakeoverUiFactory.CreateRow(panel, "Heading", 12f);
            TakeoverUiFactory.CreateText(heading, "Title", "教令树", 30,
                TakeoverUiFactory.Accent);
            TakeoverUiFactory.CreateText(heading, "Points", $"技能点：{Campaign.EdictPoints}", 18,
                Color.white);
            Button reset = TakeoverUiFactory.CreateButton(heading, "Reset", "重选", OnResetClicked);
            Button ok = TakeoverUiFactory.CreateButton(heading, "Ok", "完成",
                () => _runner?.ShowCampaignMap());
            TakeoverUiFactory.SetPreferredWidth(reset, 110f);
            TakeoverUiFactory.SetPreferredWidth(ok, 110f);
            TakeoverUiFactory.SetPreferredHeight(heading, 46f);

            RectTransform scroll = TakeoverUiFactory.CreateScrollView(panel, "EdictScroll", out _list);
            LayoutElement scrollLayout = scroll.gameObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            Refresh();
        }

        private void Refresh()
        {
            if (_list == null) return;
            for (int i = _list.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_list.GetChild(i).gameObject);

            foreach (int edictId in TakeoverEdicts.EdictIdsOf(Campaign.SelectedFaction))
                AddEdict(edictId);

            if (_list.childCount == 0)
                TakeoverUiFactory.CreateText(_list, "NoEdicts", "该阵营暂无教令数据", 18);
        }

        private void AddEdict(int edictId)
        {
            int tier = Campaign.EdictTier(edictId);
            bool maxed = tier >= TakeoverEdicts.MaxTier;
            bool available = Campaign.CanBuyEdict(edictId);
            int cost = TakeoverEdicts.NextTierCost(edictId);
            Edict next = maxed ? null : TakeoverEdicts.TierRow(edictId, tier + 1);
            Edict current = tier > 0 ? TakeoverEdicts.TierRow(edictId, tier) : null;

            Button card = TakeoverUiFactory.CreateButton(_list, $"Edict{edictId}",
                BuildEdictText(edictId, tier, next, current, maxed, cost),
                () => OnBuyClicked(edictId), 15,
                available ? new Color(0.18f, 0.21f, 0.15f, 1f) :
                    new Color(0.14f, 0.15f, 0.13f, 1f));
            card.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            TakeoverUiFactory.SetPreferredHeight(card, 118f);
            card.interactable = !maxed;
        }

        private static string BuildEdictText(int id, int tier, Edict next, Edict current,
            bool maxed, int cost)
        {
            string title = next?.CnName ?? current?.CnName ?? $"教令 {id}";
            string text = $"{TakeoverUiContent.EdictIcon(id)}　{title}　Lv.{tier}/{TakeoverEdicts.MaxTier}";
            for (int t = 1; t <= TakeoverEdicts.MaxTier; t++)
            {
                Edict row = TakeoverEdicts.TierRow(id, t);
                if (row != null) text += $"\n{t}　{row.CnDesc}";
            }
            return text + (maxed ? "\n已满级" : $"\n下一档：{cost} 点");
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
