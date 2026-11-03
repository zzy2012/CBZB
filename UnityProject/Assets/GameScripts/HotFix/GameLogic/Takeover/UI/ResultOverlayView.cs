using GameLogic.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class ResultOverlayArgs : OverlayArgs
    {
        public bool Victory;
        public float Seconds;
        public int Stars;
        public CampaignReward Reward;
    }

    /// <summary>战斗结算遮罩。</summary>
    public sealed class ResultOverlayView : MessageOverlayView
    {
        protected override OverlaySpec BuildSpec(object args)
        {
            var result = args as ResultOverlayArgs;
            bool victory = result != null && result.Victory;
            var spec = new OverlaySpec
            {
                Title = victory ? "战斗胜利" : "战斗失败",
                Body = BuildBody(result, victory),
            };
            spec.Buttons.Add(new OverlayButtonSpec("返回战役地图", true, () =>
            {
                UIManager.Close<ResultOverlayView>();
                Runner?.ReturnToCampaignMap();
            }));
            return spec;
        }

        private static string BuildBody(ResultOverlayArgs result, bool victory)
        {
            if (result == null) return "战果已记录到当前远征档案。";
            string time = UIFormat.Time(result.Seconds);
            if (!victory)
                return $"用时 {time}\n重新部署后再次挑战。";

            CampaignReward reward = result.Reward;
            string body = $"用时 {time}　{UIFormat.Stars(result.Stars)}\n" +
                          $"获得 XP {UIFormat.Number(reward.AwardedXp)}　金币 +{UIFormat.Number(reward.Gold)}";
            if (reward.Medals > 0) body += "　徽章 +1";
            if (reward.EdictPoints > 0) body += $"　教令点 +{reward.EdictPoints}";
            if (reward.UnlockedNext) body += "\n已解锁下一关。";
            return body;
        }
    }
}
