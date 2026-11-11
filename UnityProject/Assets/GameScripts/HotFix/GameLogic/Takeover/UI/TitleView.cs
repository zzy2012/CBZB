using Cysharp.Threading.Tasks;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.UI, location: "TakeoverUIWindow", fullScreen: true)]
    public sealed class TitleView : UIView
    {
        private TakeoverRunner _runner;

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            ClearContent();

            Image backdrop = TakeoverUiFactory.CreateImage(Root, "TitleBackdrop",
                new Color(0.055f, 0.065f, 0.055f, 1f));
            TakeoverUiFactory.Stretch(backdrop.rectTransform);
            Image art = TakeoverUiFactory.CreateImage(Root, "TitleArt",
                new Color(0.72f, 0.56f, 0.28f, 0.22f));
            TakeoverUiFactory.Anchor(art.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(560f, 500f),
                new Vector2(0f, 70f), new Vector2(0.5f, 0.5f));
            UISpriteCache.ApplyAsync(art, "takeover_building_2",
                gameObject.GetCancellationTokenOnDestroy()).Forget();

            RectTransform panel = TakeoverUiFactory.CreateColumn(Root, "TitleContent", 18f);
            TakeoverUiFactory.SetCentered(panel, new Vector2(700f, 600f));
            TakeoverUiFactory.CreateText(panel, "GameTitle", TakeoverUiContent.GameTitle,
                58, TakeoverUiFactory.Accent, TextAnchor.MiddleCenter);
            TakeoverUiFactory.CreateText(panel, "BetaTag", "BETA  ·  IRON OAK", 18,
                new Color(0.78f, 0.72f, 0.58f, 1f), TextAnchor.MiddleCenter);
            TakeoverUiFactory.CreateText(panel, "StudioMark",
                $"{TakeoverUiContent.StudioMark}\n{TakeoverUiContent.StudioSubMark}",
                20, Color.white, TextAnchor.MiddleCenter);

            Button start = TakeoverUiFactory.CreateButton(panel, "StartButton", "开始远征",
                () => _runner?.OpenSaveSlots(), 24, new Color(0.39f, 0.31f, 0.16f, 1f));
            TakeoverUiFactory.SetPreferredWidth(start, 300f);
            TakeoverUiFactory.SetPreferredHeight(start, 62f);
            TakeoverUiFactory.CreateText(panel, "VersionLabel", TakeoverUiContent.VersionText,
                15, new Color(0.75f, 0.73f, 0.66f, 1f), TextAnchor.MiddleCenter);
        }
    }
}
