using System.Linq;
using Cysharp.Threading.Tasks;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.UI, location: "TakeoverUIWindow", fullScreen: true)]
    public sealed class CampaignMapView : UIView
    {
        private TakeoverRunner _runner;

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            ClearContent();
            Image backdrop = TakeoverUiFactory.CreateImage(Root, "CampaignBackdrop",
                new Color(0.10f, 0.09f, 0.07f, 1f));
            TakeoverUiFactory.Stretch(backdrop.rectTransform);
            UISpriteCache.ApplyAsync(backdrop, "takeover_map_1",
                gameObject.GetCancellationTokenOnDestroy()).Forget();
            UISpriteCache.SetTint(backdrop, new Color(0.45f, 0.41f, 0.34f, 0.55f));

            RectTransform header = TakeoverUiFactory.CreateRow(Root, "Header", 14f);
            TakeoverUiFactory.Anchor(header, new Vector2(0.04f, 0.88f),
                new Vector2(0.96f, 0.98f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            TakeoverUiFactory.CreateText(header, "FactionHeading",
                $"{TakeoverUiContent.FactionName(Campaign.SelectedFaction)}战役", 30,
                TakeoverUiFactory.Accent, TextAnchor.MiddleLeft);
            Button skills = TakeoverUiFactory.CreateButton(header, "Skills", "技能",
                () => _runner?.ShowSkillTree());
            Button exit = TakeoverUiFactory.CreateButton(header, "Exit", "离开",
                () => _runner?.ShowTitle());
            TakeoverUiFactory.SetPreferredWidth(skills, 130f);
            TakeoverUiFactory.SetPreferredWidth(exit, 130f);
            TakeoverUiFactory.SetPreferredHeight(skills, 44f);
            TakeoverUiFactory.SetPreferredHeight(exit, 44f);

            RectTransform map = TakeoverUiFactory.CreateImage(Root, "MissionMap",
                new Color(0.08f, 0.09f, 0.07f, 0.55f)).rectTransform;
            TakeoverUiFactory.Anchor(map, new Vector2(0.06f, 0.19f),
                new Vector2(0.94f, 0.85f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            AddNodes(map);
            AddFooter();
        }

        private void AddNodes(RectTransform map)
        {
            var levels = Campaign.CurrentLevels;
            for (int i = 0; i < levels.Count; i++)
            {
                int levelId = levels[i];
                int bestStars = Campaign.Data.Records
                    .FirstOrDefault(record => record.AssetId == levelId)?.BestStars ?? 0;
                bool unlocked = Campaign.IsUnlocked(levelId);
                bool cleared = bestStars > 0;
                var button = new GameObject($"Mission{levelId}", typeof(RectTransform),
                    typeof(Image), typeof(Button));
                button.transform.SetParent(map, false);
                RectTransform rect = (RectTransform)button.transform;
                Vector2 position = TakeoverUiContent.NodePosition(i) / 100f;
                TakeoverUiFactory.Anchor(rect, position, position, new Vector2(102f, 70f),
                    Vector2.zero, new Vector2(0.5f, 0.5f));
                Image image = button.GetComponent<Image>();
                image.color = cleared ? new Color(0.24f, 0.36f, 0.19f, 0.98f) :
                    unlocked ? new Color(0.39f, 0.31f, 0.16f, 0.98f) :
                    new Color(0.22f, 0.23f, 0.21f, 0.92f);
                Button click = button.GetComponent<Button>();
                click.targetGraphic = image;
                click.onClick.AddListener(() => OnNodeClicked(levelId, unlocked));
                Text label = TakeoverUiFactory.CreateText(button.transform, "MissionLabel",
                    $"第 {levelId} 关\n{(cleared ? UIFormat.Stars(bestStars) : unlocked ? "出征" : "锁定")}",
                    15, Color.white, TextAnchor.MiddleCenter);
                TakeoverUiFactory.Stretch(label.rectTransform);
            }
        }

        private void AddFooter()
        {
            RectTransform footer = TakeoverUiFactory.CreateRow(Root, "CampaignFooter", 26f);
            TakeoverUiFactory.Anchor(footer, new Vector2(0.04f, 0.04f),
                new Vector2(0.96f, 0.15f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            TakeoverUiFactory.CreateText(footer, "Medals",
                $"徽章 {Campaign.Medals}/{Campaign.MedalCap}", 16);
            TakeoverUiFactory.CreateText(footer, "PlayerLevel", $"等级 {Campaign.PlayerLevel}", 16);
            TakeoverUiFactory.CreateText(footer, "Xp",
                $"{Campaign.XpToNextLevel} / {Campaign.XpPerLevel} XP", 16);
            TakeoverUiFactory.CreateText(footer, "Gold",
                $"金币 {UIFormat.Number(Campaign.Gold)}", 16);
        }

        private void OnNodeClicked(int levelId, bool unlocked)
        {
            if (!unlocked)
            {
                UIManager.Toast("先完成前置关卡，再向新的城邦进军。");
                return;
            }
            _runner?.OpenMissionDetail(levelId);
        }
    }
}
