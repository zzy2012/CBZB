using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>排队显示短暂提示消息的窗口。</summary>
    [Window(UILayer.Tips, location: "TakeoverToastUI", fullScreen: false)]
    public sealed class TakeoverToastUI : UIWindow
    {
        /// <summary>提示消息的文本组件。</summary>
        private Text _toast;
        /// <summary>提示消息容器的矩形变换组件。</summary>
        private RectTransform _toastRect;
        /// <summary>提示消息的背景图像。</summary>
        private GameObject _toastPanel;
        /// <summary>提示窗口持有的字体资源。</summary>
        private Font _font;
        /// <summary>提示消息自动关闭计时器的标识。</summary>
        private int _timerId;

        /// <summary>排队显示指定文本和持续时间的提示消息。</summary>
        public static void Show(string message, float seconds = 2.5f)
        {
            if (string.IsNullOrEmpty(message)) return;
            GameModule.UI.ShowUIAsync<TakeoverToastUI>(new ToastRequest(message, seconds));
        }

        /// <summary>生成提示窗口所需的界面组件。</summary>
        protected override void ScriptGenerator()
        {
            _toastPanel = FindRequired<RectTransform>(
                "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel").gameObject;
            _toastRect = _toastPanel.GetComponent<RectTransform>();
            _toast = FindRequired<Text>(
                "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel/m_text_ToastText");
        }

        /// <summary>初始化提示窗口的文本和背景组件。</summary>
        protected override void OnCreate()
        {
            LoadFontAsync().Forget();
        }

        /// <summary>刷新提示窗口当前显示的消息。</summary>
        protected override void OnRefresh()
        {
            if (!(UserData is ToastRequest request))
                throw new InvalidOperationException("TakeoverToastUI requires a toast request.");

            _toast.text = request.Message;
            FitToastToText();
            _toastPanel.SetActive(true);
            RestartTimer(request.Seconds);
        }

        /// <summary>销毁提示窗口并清理计时状态。</summary>
        protected override void OnDestroy()
        {
            if (_timerId != 0)
            {
                GameModule.Timer.RemoveTimer(_timerId);
                _timerId = 0;
            }
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        /// <summary>重启提示窗口自动关闭计时器。</summary>
        private void RestartTimer(float seconds)
        {
            if (_timerId != 0)
                GameModule.Timer.RemoveTimer(_timerId);
            _timerId = GameModule.Timer.AddTimer(
                CloseAfterDelay, Mathf.Max(0.05f, seconds), isLoop: false, isUnscaled: true);
        }

        /// <summary>等待指定时间后关闭提示窗口。</summary>
        private void CloseAfterDelay(object[] args)
        {
            _timerId = 0;
            GameModule.UI.CloseUI<TakeoverToastUI>();
        }

        /// <summary>根据提示文本调整容器尺寸。</summary>
        private void FitToastToText()
        {
            if (_toastRect == null || _toast == null) return;
            const float maxWidth = 700f;
            const float horizontalPadding = 40f;

            _toast.horizontalOverflow = HorizontalWrapMode.Overflow;
            float contentWidth = _toast.preferredWidth;
            float width = Mathf.Clamp(contentWidth + horizontalPadding, 220f, maxWidth);

            bool needsWrap = contentWidth + horizontalPadding > maxWidth;
            _toast.horizontalOverflow = needsWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            _toastRect.sizeDelta = new Vector2(width, 52f);
            float height = Mathf.Clamp(_toast.preferredHeight + 14f, 34f, 120f);
            _toastRect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>异步加载字体并应用到提示窗口。</summary>
        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
            FitToastToText();
        }

        /// <summary>查找指定类型的必需界面组件。</summary>
        private T FindRequired<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverToastUI prefab is missing {typeof(T).Name}: {path}");
            return component;
        }

        /// <summary>保存一条待显示的提示消息及持续时间。</summary>
        private sealed class ToastRequest
        {
            /// <summary>提示消息内容。</summary>
            public readonly string Message;
            /// <summary>提示消息的显示时长，单位为秒。</summary>
            public readonly float Seconds;

            /// <summary>创建包含消息和显示时长的提示请求。</summary>
            public ToastRequest(string message, float seconds)
            {
                Message = message;
                Seconds = seconds;
            }
        }
    }
}
