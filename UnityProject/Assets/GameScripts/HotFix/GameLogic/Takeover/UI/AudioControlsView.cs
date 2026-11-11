using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.Top, location: "TakeoverUIWindow")]
    public sealed class AudioControlsView : UIView
    {
        private Button _soundButton;
        private Button _musicButton;

        protected override void OnCreate(object args)
        {
            ClearContent();
            RectTransform controls = TakeoverUiFactory.CreateRow(Root, "AudioControls", 6f);
            TakeoverUiFactory.Anchor(controls, new Vector2(0.82f, 0.92f),
                new Vector2(0.98f, 0.99f), Vector2.zero, Vector2.zero, new Vector2(1f, 1f));
            _soundButton = AddButton(controls, "Sound", OnSoundClicked);
            _musicButton = AddButton(controls, "Music", OnMusicClicked);
            UIAudioSettings.Load();
            Refresh();
        }

        private Button AddButton(Transform parent, string name, System.Action action)
        {
            Button button = TakeoverUiFactory.CreateButton(parent, name, name, action, 12,
                new Color(0.18f, 0.19f, 0.16f, 0.95f));
            TakeoverUiFactory.SetPreferredWidth(button, 76f);
            TakeoverUiFactory.SetPreferredHeight(button, 32f);
            return button;
        }

        private void OnSoundClicked()
        {
            bool enabled = UIAudioSettings.ToggleSound();
            Refresh();
            UIManager.Toast(enabled ? "音效已开启" : "音效已关闭");
        }

        private void OnMusicClicked()
        {
            bool enabled = UIAudioSettings.ToggleMusic();
            Refresh();
            UIManager.Toast(enabled ? "音乐已开启" : "音乐已关闭");
        }

        private void Refresh()
        {
            SetState(_soundButton, "SFX", UIAudioSettings.SoundEnabled);
            SetState(_musicButton, "BGM", UIAudioSettings.MusicEnabled);
        }

        private static void SetState(Button button, string name, bool enabled)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.text = enabled ? name : $"{name} OFF";
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = enabled ? new Color(0.18f, 0.25f, 0.17f, 0.95f) :
                    new Color(0.22f, 0.13f, 0.12f, 0.95f);
        }
    }
}
