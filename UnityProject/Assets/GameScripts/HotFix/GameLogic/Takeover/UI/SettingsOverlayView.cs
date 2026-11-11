using GameLogic;
using GameLogic.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.Top, location: "TakeoverUIWindow")]
    public sealed class SettingsOverlayView : MessageOverlayView
    {
        private bool _wasPaused;

        protected override OverlaySpec BuildSpec(object args)
        {
            var spec = new OverlaySpec
            {
                Title = "战场设置",
                Body = "音效与音乐可在右上角单独开关。教程提示可通过“继续提示”逐条查看。",
            };
            spec.Buttons.Add(new OverlayButtonSpec("返回战场", true, () =>
            {
                UIManager.Close<SettingsOverlayView>();
                if (!_wasPaused) Runner?.SetPaused(false);
            }));
            return spec;
        }

        protected override void OnOverlayOpened()
        {
            _wasPaused = Runner != null && Runner.Paused;
            Runner?.SetPaused(true);
        }
    }
}
