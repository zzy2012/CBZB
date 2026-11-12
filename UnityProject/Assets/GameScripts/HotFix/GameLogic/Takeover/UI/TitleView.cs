using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>标题界面：标题、BETA、工作室标记、版本与“开始远征”。</summary>
    public sealed class TitleView : UIView
    {
        private TakeoverRunner _runner;

        protected override string UxmlLocation => "TakeoverTitleView";

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            Require("title-screen", "title-label", "beta-label", "start-button", "version-label", "studio-label");

            Label title = Q<Label>("title-label");
            if (title != null) title.text = TakeoverUiContent.GameTitle;

            Label beta = Q<Label>("beta-label");
            if (beta != null) beta.text = "BETA";

            Label studio = Q<Label>("studio-label");
            if (studio != null) studio.text = $"{TakeoverUiContent.StudioMark}\n{TakeoverUiContent.StudioSubMark}";

            Label version = Q<Label>("version-label");
            if (version != null) version.text = TakeoverUiContent.VersionText;

            Button start = Q<Button>("start-button");
            if (start != null)
            {
                start.text = "开始远征";
                start.clicked += () => _runner?.OpenSaveSlots();
            }

            VisualElement art = Q<VisualElement>("title-art");
            if (art != null)
            {
                UISpriteCache.ApplyBackgroundAsync(art, "takeover_building_2").Forget();
                UISpriteCache.SetTint(art, new Color(1f, 0.82f, 0.52f, 0.16f));
            }
        }
    }
}
