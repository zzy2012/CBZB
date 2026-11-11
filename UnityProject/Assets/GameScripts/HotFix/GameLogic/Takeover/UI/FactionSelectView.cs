using Cysharp.Threading.Tasks;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.UI, location: "TakeoverUIWindow", fullScreen: true)]
    public sealed class FactionSelectView : UIView
    {
        private TakeoverRunner _runner;
        private RectTransform _cards;

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            ClearContent();
            Image backdrop = TakeoverUiFactory.CreateImage(Root, "Backdrop",
                new Color(0.07f, 0.08f, 0.065f, 1f));
            TakeoverUiFactory.Stretch(backdrop.rectTransform);

            RectTransform layout = TakeoverUiFactory.CreateColumn(Root, "FactionLayout", 20f);
            TakeoverUiFactory.Anchor(layout, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            TakeoverUiFactory.CreateText(layout, "Heading", "选择你要挑战的战役", 32,
                TakeoverUiFactory.Accent, TextAnchor.MiddleCenter);
            _cards = TakeoverUiFactory.CreateRow(layout, "FactionCards", 20f);
            TakeoverUiFactory.SetPreferredHeight(_cards, 440f);

            RectTransform actions = TakeoverUiFactory.CreateRow(layout, "Actions", 16f);
            Button exit = TakeoverUiFactory.CreateButton(actions, "Exit", "离开",
                () => _runner?.ShowTitle());
            Button next = TakeoverUiFactory.CreateButton(actions, "Continue", "继续",
                OnContinueClicked, 20, new Color(0.39f, 0.31f, 0.16f, 1f));
            TakeoverUiFactory.SetPreferredWidth(exit, 180f);
            TakeoverUiFactory.SetPreferredWidth(next, 220f);
            TakeoverUiFactory.SetPreferredHeight(exit, 48f);
            TakeoverUiFactory.SetPreferredHeight(next, 48f);
            Refresh();
        }

        private void Refresh()
        {
            if (_cards == null) return;
            for (int i = _cards.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_cards.GetChild(i).gameObject);

            foreach (FactionCardData data in TakeoverUiContent.FactionCards)
            {
                bool unlocked = Campaign.FactionUnlocked(data.Faction);
                bool playable = Campaign.FactionPlayable(data.Faction);
                bool selected = unlocked && data.Faction == Campaign.SelectedFaction;
                var card = new GameObject($"Faction{data.Rank}", typeof(RectTransform),
                    typeof(Image), typeof(Button), typeof(VerticalLayoutGroup), typeof(LayoutElement));
                card.transform.SetParent(_cards, false);
                Image image = card.GetComponent<Image>();
                image.color = selected ? data.DarkColor * 1.15f : data.DarkColor;
                Button button = card.GetComponent<Button>();
                button.targetGraphic = image;
                Faction faction = data.Faction;
                button.onClick.AddListener(() => SelectFaction(faction, unlocked, playable));

                VerticalLayoutGroup group = card.GetComponent<VerticalLayoutGroup>();
                group.padding = new RectOffset(14, 14, 14, 14);
                group.spacing = 10f;
                group.childControlWidth = true;
                group.childControlHeight = true;
                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;
                LayoutElement element = card.GetComponent<LayoutElement>();
                element.flexibleWidth = 1f;
                element.preferredHeight = 420f;

                var portrait = TakeoverUiFactory.CreateImage(card.transform, "Portrait",
                    new Color(1f, 1f, 1f, 0.9f));
                TakeoverUiFactory.SetPreferredHeight(portrait, 215f);
                UISpriteCache.ApplyAsync(portrait,
                    TakeoverUiContent.FactionPortraitLocation(data.Faction),
                    gameObject.GetCancellationTokenOnDestroy()).Forget();
                TakeoverUiFactory.CreateText(card.transform, "Rank", $"—  {data.Rank:00}  —",
                    16, TakeoverUiFactory.Accent, TextAnchor.MiddleCenter);
                TakeoverUiFactory.CreateText(card.transform, "Name", unlocked ? data.ShortName : "???",
                    23, Color.white, TextAnchor.MiddleCenter);
                TakeoverUiFactory.CreateText(card.transform, "Description",
                    unlocked ? data.Description : data.LockedDescription, 16,
                    new Color(0.88f, 0.86f, 0.78f, 1f), TextAnchor.MiddleCenter);
                if (unlocked && !playable)
                    TakeoverUiFactory.CreateText(card.transform, "Unavailable", "内容尚未导入",
                        15, new Color(1f, 0.65f, 0.42f, 1f), TextAnchor.MiddleCenter);
            }
        }

        private void SelectFaction(Faction faction, bool unlocked, bool playable)
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

        private void OnContinueClicked()
        {
            if (!Campaign.FactionPlayable(Campaign.SelectedFaction))
            {
                UIManager.Toast("第 2/3 章内容尚未导入，暂不可挑战。");
                return;
            }
            _runner?.ContinueFromFactionSelect();
        }
    }
}
