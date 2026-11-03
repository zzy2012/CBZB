using System;
using TEngine;
using UnityEngine;

namespace GameLogic.UI
{
    /// <summary>
    /// 音效/音乐开关。属于本机设置，用 PlayerPrefs 持久化，不写进远征档案。
    /// </summary>
    public static class UIAudioSettings
    {
        private const string SoundKey = "takeover.audio.sound";
        private const string MusicKey = "takeover.audio.music";

        private static bool _loaded;
        private static bool _soundEnabled = true;
        private static bool _musicEnabled = true;
        private static float _lastSound = 1f;
        private static float _lastMusic = 1f;

        public static bool SoundEnabled => _soundEnabled;
        public static bool MusicEnabled => _musicEnabled;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _soundEnabled = PlayerPrefs.GetInt(SoundKey, 1) != 0;
            _musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) != 0;
            Apply();
        }

        public static bool ToggleSound()
        {
            Load();
            _soundEnabled = !_soundEnabled;
            PlayerPrefs.SetInt(SoundKey, _soundEnabled ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            return _soundEnabled;
        }

        public static bool ToggleMusic()
        {
            Load();
            _musicEnabled = !_musicEnabled;
            PlayerPrefs.SetInt(MusicKey, _musicEnabled ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            return _musicEnabled;
        }

        private static void Apply()
        {
            try
            {
                IAudioModule audio = GameModule.Audio;
                if (audio == null) return;

                float sound = audio.SoundVolume;
                if (sound > 0.01f) _lastSound = sound;
                float music = audio.MusicVolume;
                if (music > 0.01f) _lastMusic = music;

                audio.SoundVolume = _soundEnabled ? Mathf.Clamp(_lastSound, 0.01f, 1f) : 0.0001f;
                audio.MusicVolume = _musicEnabled ? Mathf.Clamp(_lastMusic, 0.01f, 1f) : 0.0001f;
            }
            catch (Exception exception)
            {
                Log.Warning($"[TakeoverUI] 设置音量失败: {exception.Message}");
            }
        }
    }
}
