using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>选择挑战阵营。第 2/3 章素材未导入，解锁后点击只提示。</summary>
    public sealed class FactionSelectView : UIView
    {
        private TakeoverRunner _runner;
        private VisualElement _grid;

        protected override string UxmlLocation => "TakeoverFactionView";

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            Require("faction-grid", "continue-button", "exit-button", "ribbon");

            Label ribbon = Q<Label>("ribbon");
            if (ribbon != null) ribbon.text = "选择你要挑战的战役";

            Button exit = Q<Button>("exit-button");
            if (exit != null)
            {
                exit.text = "离开";
                exit.clicked += () => _runner?.ShowTitle();
            }

            Button continueButton = Q<Button>("continue-button");
            if (continueButton != null)
            {
                continueButton.text = "继续";
                continueButton.clicked += OnContinueClicked;
            }

            _grid = Q<VisualElement>("faction-grid");
            Refresh();
        }

        private void OnContinueClicked()
        {
            Faction faction = Campaign.SelectedFaction;
            if (!Campaign.FactionPlayable(faction))
            {
                UIManager.Toast("第 2/3 章内容尚未导入，暂不可挑战。");
                return;
            }
            _runner?.ContinueFromFactionSelect();
        }

        private void Refresh()
        {
            if (_grid == null) return;
            _grid.Clear();

            Faction selected = Campaign.FactionPlayable(Campaign.SelectedFaction)
                ? Campaign.SelectedFaction
                : Faction.Westaria;

            foreach (FactionCardData data in TakeoverUiContent.FactionCards)
            {
                bool unlocked = Campaign.FactionUnlocked(data.Faction);
                bool playable = Campaign.FactionPlayable(data.Faction);
                bool isSelected = unlocked && data.Faction == selected;

                var card = new Button { text = string.Empty };
                card.AddToClassList("faction-card");
                card.style.backgroundColor = data.DarkColor;
                if (isSelected) card.AddToClassList("is-selected");
                if (!unlocked || !playable) card.AddToClassList("is-locked");

                var portrait = new VisualElement();
                portrait.AddToClassList("faction-portrait");
                UISpriteCache.ApplyBackgroundAsync(
                    portrait, TakeoverUiContent.FactionPortraitLocation(data.Faction)).Forget();
                card.Add(portrait);

                var number = new Label(data.Rank.ToString());
                number.AddToClassList("faction-number");
                card.Add(number);

                var name = new Label(unlocked ? data.ShortName : "???");
                name.AddToClassList("faction-name");
                card.Add(name);

                var description = new Label(
                    unlocked ? data.Description : data.LockedDescription);
                description.AddToClassList("faction-description");
                card.Add(description);

                if (unlocked && !playable)
                {
                    var badge = new Label("内容尚未导入");
                    badge.AddToClassList("faction-lock-badge");
                    card.Add(badge);
                }

                Faction faction = data.Faction;
                card.clicked += () => OnFactionClicked(faction, unlocked, playable);
                _grid.Add(card);
            }

            UIFontProvider.ApplyToTree(Root);
            UIToolkitTheme.Apply(Root);
        }

        private void OnFactionClicked(Faction faction, bool unlocked, bool playable)
        {
            if (!unlocked)
            {
                UIManager.Toast("完成第一战役的前两关即可解锁该阵营。");
                return;
            }
            if (!playable)
            {
                UIManager.Toast("第 2/3 章内容尚未导入，暂不可挑战。");
                return;
            }

            Campaign.SetSelectedFaction(faction);
            Refresh();
        }
    }
}
