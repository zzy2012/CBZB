using GameLogic.UI;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>右上角常驻的音效/音乐开关（对应 index.html 的 top-controls）。</summary>
    public sealed class AudioControlsView : UIView
    {
        private Button _soundButton;
        private Button _musicButton;

        protected override string UxmlLocation => "TakeoverAudioControlsView";

        protected override void OnCreate(object args)
        {
            _soundButton = Q<Button>("sound-button");
            _musicButton = Q<Button>("music-button");

            if (_soundButton != null)
            {
                _soundButton.text = "SFX";
                _soundButton.clicked += OnSoundClicked;
            }
            if (_musicButton != null)
            {
                _musicButton.text = "BGM";
                _musicButton.clicked += OnMusicClicked;
            }

            UIAudioSettings.Load();
            Refresh();
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
            SetState(_soundButton, UIAudioSettings.SoundEnabled);
            SetState(_musicButton, UIAudioSettings.MusicEnabled);
        }

        private static void SetState(Button button, bool enabled)
        {
            if (button == null) return;
            if (enabled) button.RemoveFromClassList("is-off");
            else button.AddToClassList("is-off");
        }
    }
}
