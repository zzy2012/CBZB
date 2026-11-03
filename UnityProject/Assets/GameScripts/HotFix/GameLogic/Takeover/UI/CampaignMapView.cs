using System.Linq;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>战役地图：8 个关卡节点 + 底部等级/徽章/金币。</summary>
    public sealed class CampaignMapView : UIView
    {
        private TakeoverRunner _runner;
        private VisualElement _nodes;

        protected override string UxmlLocation => "TakeoverCampaignMapView";

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            Require("faction-heading", "mission-nodes", "exit-button", "skills-button",
                "medal-label", "level-label", "xp-label", "xp-fill", "gold-label");

            Label heading = Q<Label>("faction-heading");
            if (heading != null)
                heading.text = $"{TakeoverUiContent.FactionName(Campaign.SelectedFaction)}战役";

            Button exit = Q<Button>("exit-button");
            if (exit != null)
            {
                exit.text = "离开";
                exit.clicked += () => _runner?.ShowTitle();
            }

            Button skills = Q<Button>("skills-button");
            if (skills != null)
            {
                skills.text = "技能";
                skills.clicked += () => _runner?.ShowSkillTree();
            }

            VisualElement background = Q<VisualElement>("map-background");
            if (background != null)
            {
                UISpriteCache.ApplyBackgroundAsync(background, "takeover_map_1").Forget();
                UISpriteCache.SetTint(background, new Color(0.35f, 0.30f, 0.24f, 1f));
            }

            _nodes = Q<VisualElement>("mission-nodes");
            BuildNodes();
            RefreshFooter();
        }

        private void BuildNodes()
        {
            if (_nodes == null) return;
            _nodes.Clear();

            var levels = Campaign.CurrentLevels;
            for (int i = 0; i < levels.Count; i++)
            {
                int levelId = levels[i];
                int bestStars = Campaign.Data.Records
                    .FirstOrDefault(record => record.AssetId == levelId)?.BestStars ?? 0;
                bool unlocked = Campaign.IsUnlocked(levelId);
                bool cleared = bestStars > 0;

                var node = new Button { text = string.Empty };
                node.AddToClassList("mission-node");
                if (cleared) node.AddToClassList("is-done");
                else if (unlocked) node.AddToClassList("is-open");
                else node.AddToClassList("is-locked");

                Vector2 position = TakeoverUiContent.NodePosition(i);
                node.style.position = Position.Absolute;
                node.style.left = new StyleLength(Length.Percent(position.x));
                node.style.top = new StyleLength(Length.Percent(position.y));

                var icon = new VisualElement();
                icon.AddToClassList("mission-node-icon");
                UISpriteCache.ApplyBackgroundAsync(icon, "takeover_building_2").Forget();
                UISpriteCache.SetTint(icon,
                    cleared ? new Color(0.66f, 1f, 0.52f, 0.95f) :
                    unlocked ? new Color(1f, 0.86f, 0.46f, 0.95f) :
                    new Color(0.45f, 0.45f, 0.45f, 0.8f));
                node.Add(icon);

                var number = new Label(levelId.ToString());
                number.AddToClassList("mission-node-number");
                node.Add(number);

                var label = new Label(cleared ? "GREAT" : unlocked ? "NEW" : "LOCKED");
                label.AddToClassList("mission-node-label");
                node.Add(label);

                int captured = levelId;
                node.clicked += () => OnNodeClicked(captured, unlocked);
                _nodes.Add(node);
            }

            UIToolkitTheme.Apply(_nodes);
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

        private void RefreshFooter()
        {
            Label medal = Q<Label>("medal-label");
            if (medal != null) medal.text = $"徽章 {Campaign.Medals}/{Campaign.MedalCap}";

            Label level = Q<Label>("level-label");
            if (level != null) level.text = Campaign.PlayerLevel.ToString();

            Label xp = Q<Label>("xp-label");
            if (xp != null) xp.text = $"{Campaign.XpToNextLevel} / {Campaign.XpPerLevel} XP";

            VisualElement fill = Q<VisualElement>("xp-fill");
            if (fill != null)
            {
                float ratio = Campaign.XpPerLevel <= 0
                    ? 0f
                    : Mathf.Clamp01(Campaign.XpToNextLevel / (float)Campaign.XpPerLevel);
                fill.style.width = new StyleLength(Length.Percent(ratio * 100f));
            }

            Label gold = Q<Label>("gold-label");
            if (gold != null) gold.text = UIFormat.Number(Campaign.Gold);
        }
    }
}
