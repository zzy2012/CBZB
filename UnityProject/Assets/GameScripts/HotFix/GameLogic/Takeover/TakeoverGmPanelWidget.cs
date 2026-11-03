using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverGmPanelWidget : UIWidget
    {
        private static readonly float[] GmSpeeds = { 1f, 2f, 4f, 8f, 16f, 32f, 64f };

        private readonly List<Text> _speedLabels = new List<Text>(7);
        private TakeoverRunner _runner;
        private Button _toggle;
        private Button _gold;
        private Button _mana;
        private Button _win;
        private Button _lose;
        private GameObject _content;
        private bool _expanded;

        protected override void ScriptGenerator()
        {
            _content = Required<RectTransform>("m_rect_GmContent").gameObject;
            _toggle = Required<Button>("m_btn_GmToggle");
            Required<Text>("m_btn_GmToggle/m_text_Label");
            _gold = Required<Button>("m_rect_GmContent/m_btn_GmGold");
            _mana = Required<Button>("m_rect_GmContent/m_btn_GmMana");
            _win = Required<Button>("m_rect_GmContent/m_btn_GmWin");
            _lose = Required<Button>("m_rect_GmContent/m_btn_GmLose");
            Required<Text>("m_rect_GmContent/m_text_GmTitle");
            Required<Text>("m_rect_GmContent/m_text_GmHint");

            _speedLabels.Clear();
            for (int i = 0; i < GmSpeeds.Length; i++)
            {
                string path = $"m_rect_GmContent/m_btn_GmSpeed{GmSpeeds[i]:0}";
                Required<Button>(path);
                _speedLabels.Add(Required<Text>(path + "/m_text_Label"));
            }
        }

        protected override void RegisterEvent()
        {
            _toggle.onClick.AddListener(() => SetGmExpanded(!_expanded));
            _gold.onClick.AddListener(() => _runner?.GrantResources(1000, 0));
            _mana.onClick.AddListener(() => _runner?.GrantResources(0, 500));
            _win.onClick.AddListener(() => _runner?.GmEndBattle(true));
            _lose.onClick.AddListener(() => _runner?.GmEndBattle(false));
            for (int i = 0; i < GmSpeeds.Length; i++)
            {
                float speed = GmSpeeds[i];
                Required<Button>($"m_rect_GmContent/m_btn_GmSpeed{speed:0}")
                    .onClick.AddListener(() => _runner?.SetSpeed(speed));
            }
        }

        protected override void OnCreate()
        {
            SetRunner((OwnerWindow as TakeoverBattleUI)?.Runner);
            SetGmExpanded(false);
        }

        public void SetRunner(TakeoverRunner runner)
        {
            _runner = runner;
        }

        public void SetGmExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_content != null && _content.activeSelf != expanded)
                _content.SetActive(expanded);
        }

        public void RefreshSpeed(float speed)
        {
            for (int i = 0; i < _speedLabels.Count; i++)
            {
                string label = $"×{GmSpeeds[i]:0}";
                _speedLabels[i].text = Mathf.Approximately(speed, GmSpeeds[i])
                    ? $"{label}*"
                    : label;
            }
        }

        private T Required<T>(string path) where T : UnityEngine.Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverGmPanelWidget prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
