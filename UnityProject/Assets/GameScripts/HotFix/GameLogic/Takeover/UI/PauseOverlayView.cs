using GameLogic.UI;

namespace GameLogic.Takeover.UI
{
    /// <summary>远征暂停遮罩。</summary>
    public sealed class PauseOverlayView : MessageOverlayView
    {
        protected override OverlaySpec BuildSpec(object args)
        {
            var spec = new OverlaySpec
            {
                Title = "远征暂停",
                Body = "战场已暂停。你可以继续战斗、重新开始本关，或返回战役地图。",
            };
            spec.Buttons.Add(new OverlayButtonSpec("继续", true, () =>
            {
                UIManager.Close<PauseOverlayView>();
                Runner?.SetPaused(false);
            }));
            spec.Buttons.Add(new OverlayButtonSpec("重开本关", false, () =>
            {
                UIManager.Close<PauseOverlayView>();
                Runner?.NewGame();
            }));
            spec.Buttons.Add(new OverlayButtonSpec("返回地图", false, () =>
            {
                UIManager.Close<PauseOverlayView>();
                Runner?.ReturnToCampaignMap();
            }));
            return spec;
        }

        protected override void OnOverlayOpened()
        {
            Runner?.SetPaused(true);
        }
    }
}
