using System.Linq;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.Top, location: "TakeoverUIWindow")]
    public sealed class SaveSlotsView : UIView
    {
        private TakeoverRunner _runner;

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            ClearContent();
            RectTransform panel = TakeoverUiFactory.CreatePanel(Root, "SaveSlotsPanel",
                new Vector2(820f, 620f), TakeoverUiFactory.Panel, true);
            RectTransform column = TakeoverUiFactory.CreateColumn(panel, "SaveSlotsContent", 14f);
            TakeoverUiFactory.Anchor(column, Vector2.zero, Vector2.one,
                new Vector2(-72f, -56f), Vector2.zero, new Vector2(0.5f, 0.5f));

            RectTransform heading = TakeoverUiFactory.CreateRow(column, "Heading");
            TakeoverUiFactory.CreateText(heading, "Title", "远征档案", 32,
                TakeoverUiFactory.Accent, TextAnchor.MiddleLeft);
            Button close = TakeoverUiFactory.CreateButton(heading, "Close", "×",
                () => UIManager.Close<SaveSlotsView>(), 24);
            TakeoverUiFactory.SetPreferredWidth(close, 48f);
            TakeoverUiFactory.SetPreferredHeight(close, 42f);
            TakeoverUiFactory.SetPreferredHeight(heading, 48f);

            RectTransform list = TakeoverUiFactory.CreateColumn(column, "SlotList", 12f);
            for (int slot = 0; slot < Campaign.SlotCount; slot++)
                AddSlot(list, slot);
        }

        private void AddSlot(Transform parent, int slot)
        {
            bool hasSave = CampaignSaveService.TryProbe(slot, out CampaignSave save, out string warning);
            string title = hasSave
                ? $"远征档案 {slot + 1} · {TakeoverUiContent.FactionName(save.SelectedFaction)}"
                : $"远征档案 {slot + 1}";
            string detail = hasSave
                ? $"等级 {Campaign.BasePlayerLevel + save.Xp / Campaign.XpPerLevel}　进度 " +
                  $"{save.Records.Count(record => record.BestStars > 0)}/" +
                  $"{TakeoverFactions.LevelsOf(save.SelectedFaction).Count}　" +
                  $"{save.Medals}/{TakeoverFactions.LevelsOf(save.SelectedFaction).Count} 徽章"
                : "空档案 · 点击开始新战役";
            if (!string.IsNullOrEmpty(warning)) detail += $"\n{warning}";

            Button card = TakeoverUiFactory.CreateButton(parent, $"SaveSlot{slot + 1}",
                $"{title}\n{detail}\n{(hasSave ? "继续 ›" : "＋")}",
                () =>
                {
                    UIManager.Close<SaveSlotsView>();
                    _runner?.EnterCampaignSlot(slot);
                }, 18, new Color(0.15f, 0.16f, 0.12f, 1f));
            card.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            TakeoverUiFactory.SetPreferredHeight(card, 108f);
        }
    }
}
