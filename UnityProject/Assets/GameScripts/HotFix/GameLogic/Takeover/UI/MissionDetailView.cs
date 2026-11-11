using System.Linq;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class MissionDetailArgs
    {
        public TakeoverRunner Runner;
        public int LevelId;
    }

    [Window(UILayer.Top, location: "TakeoverUIWindow")]
    public sealed class MissionDetailView : UIView
    {
        private MissionDetailArgs _args;

        protected override void OnCreate(object args)
        {
            _args = args as MissionDetailArgs;
            if (_args == null)
            {
                UIManager.Close<MissionDetailView>();
                return;
            }

            ClearContent();
            RectTransform panel = TakeoverUiFactory.CreateColumn(Root, "MissionDetailPanel", 14f);
            TakeoverUiFactory.SetCentered(panel, new Vector2(580f, 460f));
            panel.GetComponent<RectTransform>().gameObject.AddComponent<Image>().color =
                TakeoverUiFactory.Panel;
            int levelId = _args.LevelId;
            LevelRecord record = Campaign.Data.Records.FirstOrDefault(value => value.AssetId == levelId);

            TakeoverUiFactory.CreateText(panel, "MissionTitle",
                TakeoverUiContent.LevelTitle(levelId), 28, TakeoverUiFactory.Accent,
                TextAnchor.MiddleCenter);
            TakeoverUiFactory.CreateText(panel, "Reward",
                $"XP {UIFormat.Number(TakeoverUiContent.LevelXp(levelId))}", 20,
                Color.white, TextAnchor.MiddleCenter);
            TakeoverUiFactory.CreateText(panel, "Enemy",
                $"VS {TakeoverUiContent.EnemyFactionNames(levelId)}", 18,
                Color.white, TextAnchor.MiddleCenter);
            TakeoverUiFactory.CreateText(panel, "Description",
                TakeoverUiContent.LevelDescription(levelId), 16,
                new Color(0.84f, 0.84f, 0.78f, 1f), TextAnchor.MiddleCenter);
            int bestStars = record?.BestStars ?? 0;
            TakeoverUiFactory.CreateText(panel, "Stars",
                bestStars > 0
                    ? $"最佳 {UIFormat.Stars(bestStars)}　{UIFormat.Time(record.BestSeconds)}"
                    : "尚未通关", 18, TakeoverUiFactory.Accent, TextAnchor.MiddleCenter);

            RectTransform actions = TakeoverUiFactory.CreateRow(panel, "Actions", 12f);
            Button start = TakeoverUiFactory.CreateButton(actions, "StartBattle", "开始战斗",
                () =>
                {
                    UIManager.Close<MissionDetailView>();
                    _args?.Runner?.StartLevel(levelId);
                }, 19, new Color(0.39f, 0.31f, 0.16f, 1f));
            Button close = TakeoverUiFactory.CreateButton(actions, "Close", "返回",
                () => UIManager.Close<MissionDetailView>());
            TakeoverUiFactory.SetPreferredWidth(start, 220f);
            TakeoverUiFactory.SetPreferredWidth(close, 160f);
            TakeoverUiFactory.SetPreferredHeight(start, 50f);
            TakeoverUiFactory.SetPreferredHeight(close, 50f);
        }
    }
}
