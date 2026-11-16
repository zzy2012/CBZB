using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>提供战斗调试控制面板。</summary>
    public sealed class TakeoverGmPanelWidget : UIWidget
    {
        /// <summary>GM 面板提供的模拟速度选项。</summary>
        private static readonly float[] GmSpeeds = { 1f, 2f, 4f, 8f, 16f, 32f, 64f };

        /// <summary>调试速度按钮的文本组件集合。</summary>
        private readonly List<Text> _speedLabels = new List<Text>(7);
        /// <summary>调试面板控制的战斗运行器。</summary>
        private TakeoverRunner _runner;
        /// <summary>展开或收起调试面板的按钮。</summary>
        private Button _toggle;
        /// <summary>发放金币的调试按钮。</summary>
        private Button _gold;
        /// <summary>发放魔法值的调试按钮。</summary>
        private Button _mana;
        /// <summary>结束战斗并判定胜利的调试按钮。</summary>
        private Button _win;
        /// <summary>结束战斗并判定失败的调试按钮。</summary>
        private Button _lose;
        /// <summary>可展开或收起的调试内容区域。</summary>
        private GameObject _content;
        /// <summary>调试内容区域当前是否展开。</summary>
        private bool _expanded;

        /// <summary>生成 GM 调试面板所需的界面组件。</summary>
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

        /// <summary>注册 GM 调试按钮的点击事件。</summary>
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

        /// <summary>初始化 GM 面板及其调试控件。</summary>
        protected override void OnCreate()
        {
            SetRunner((OwnerWindow as TakeoverBattleUI)?.Runner);
            SetGmExpanded(false);
        }

        /// <summary>设置调试面板使用的战斗运行器。</summary>
        public void SetRunner(TakeoverRunner runner)
        {
            _runner = runner;
        }

        /// <summary>设置调试面板的展开状态。</summary>
        public void SetGmExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_content != null && _content.activeSelf != expanded)
                _content.SetActive(expanded);
        }

        /// <summary>刷新调试面板显示的战斗速度。</summary>
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

        /// <summary>查找并返回指定的必需界面组件。</summary>
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
