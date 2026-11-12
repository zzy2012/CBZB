using System.Linq;
using GameLogic.UI;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    public sealed class MissionDetailArgs
    {
        public TakeoverRunner Runner;
        public int LevelId;
    }

    /// <summary>关卡详情弹窗。</summary>
    public sealed class MissionDetailView : UIView
    {
        private MissionDetailArgs _args;

        protected override string UxmlLocation => "TakeoverMissionDetailView";

        protected override void OnCreate(object args)
        {
            _args = args as MissionDetailArgs;
            if (_args == null)
            {
                UIManager.Close<MissionDetailView>();
                return;
            }

            Require("mission-title", "mission-reward", "mission-description",
                "mission-enemy", "mission-stars", "start-battle-button", "close-button");

            int levelId = _args.LevelId;
            Label title = Q<Label>("mission-title");
            if (title != null) title.text = TakeoverUiContent.LevelTitle(levelId);

            Label reward = Q<Label>("mission-reward");
            if (reward != null) reward.text = $"XP {UIFormat.Number(TakeoverUiContent.LevelXp(levelId))}";

            Label enemy = Q<Label>("mission-enemy");
            if (enemy != null) enemy.text = $"VS {TakeoverUiContent.EnemyFactionNames(levelId)}";

            Label description = Q<Label>("mission-description");
            if (description != null) description.text = TakeoverUiContent.LevelDescription(levelId);

            int bestStars = Campaign.Data.Records
                .FirstOrDefault(record => record.AssetId == levelId)?.BestStars ?? 0;
            LevelRecord record = Campaign.Data.Records.FirstOrDefault(value => value.AssetId == levelId);
            Label stars = Q<Label>("mission-stars");
            if (stars != null)
            {
                stars.text = bestStars > 0
                    ? $"最佳 {UIFormat.Stars(bestStars)}　{UIFormat.Time(record?.BestSeconds ?? 0f)}"
                    : "尚未通关";
            }

            Button start = Q<Button>("start-battle-button");
            if (start != null)
            {
                start.text = "开始战斗";
                start.clicked += () =>
                {
                    UIManager.Close<MissionDetailView>();
                    _args?.Runner?.StartLevel(levelId);
                };
            }

            Button close = Q<Button>("close-button");
            if (close != null)
            {
                close.text = "×";
                close.clicked += () => UIManager.Close<MissionDetailView>();
            }
        }
    }
}
