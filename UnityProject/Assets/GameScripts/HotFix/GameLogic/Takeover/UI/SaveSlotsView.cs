using System.Linq;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>三个远征档案槽。</summary>
    public sealed class SaveSlotsView : UIView
    {
        private TakeoverRunner _runner;

        protected override string UxmlLocation => "TakeoverSaveSlotsView";

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            Require("slot-list", "close-button");

            Button close = Q<Button>("close-button");
            if (close != null)
            {
                close.text = "×";
                close.clicked += () => UIManager.Close<SaveSlotsView>();
            }

            VisualElement list = Q<VisualElement>("slot-list");
            if (list == null) return;
            list.Clear();

            for (int slot = 0; slot < Campaign.SlotCount; slot++)
                list.Add(CreateSlotCard(slot));
        }

        private Button CreateSlotCard(int slot)
        {
            bool hasSave = CampaignSaveService.TryProbe(slot, out CampaignSave save, out string warning);

            var card = new Button { text = string.Empty };
            card.AddToClassList("save-card");

            var lines = new VisualElement();
            lines.AddToClassList("save-card-copy");

            var title = new Label();
            title.AddToClassList("save-card-title");
            title.text = hasSave
                ? $"远征档案 {slot + 1} · {TakeoverUiContent.FactionName(save.SelectedFaction)}"
                : $"远征档案 {slot + 1}";

            var detail = new Label();
            detail.AddToClassList("save-card-detail");
            if (hasSave)
            {
                int cleared = save.Records.Count(record => record.BestStars > 0);
                int level = Campaign.BasePlayerLevel + save.Xp / Campaign.XpPerLevel;
                detail.text = $"等级 {level}　进度 {cleared}/{TakeoverFactions.LevelsOf(save.SelectedFaction).Count}" +
                              $"　{save.Medals}/{TakeoverFactions.LevelsOf(save.SelectedFaction).Count} 徽章";
                if (!string.IsNullOrEmpty(warning))
                {
                    var warningLabel = new Label(warning);
                    warningLabel.AddToClassList("save-card-warning");
                    lines.Add(warningLabel);
                }
            }
            else
            {
                detail.text = "空档案 · 点击开始新战役";
            }

            lines.Add(title);
            lines.Add(detail);

            var action = new Label();
            action.AddToClassList("save-card-action");
            action.text = hasSave ? "继续 ›" : "＋";

            card.Add(lines);
            card.Add(action);

            card.clicked += () =>
            {
                UIManager.Close<SaveSlotsView>();
                _runner?.EnterCampaignSlot(slot);
            };
            return card;
        }
    }
}
