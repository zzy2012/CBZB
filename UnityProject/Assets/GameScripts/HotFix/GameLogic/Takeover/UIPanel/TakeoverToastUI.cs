using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    [Window(UILayer.Tips, location: "TakeoverToastUI", fullScreen: false)]
    public sealed class TakeoverToastUI : UIWindow
    {
        private Text _toast;
        private RectTransform _toastRect;
        private GameObject _toastPanel;
        private Font _font;
        private int _timerId;

        public static void Show(string message, float seconds = 2.5f)
        {
            if (string.IsNullOrEmpty(message)) return;
            GameModule.UI.ShowUIAsync<TakeoverToastUI>(new ToastRequest(message, seconds));
        }

        protected override void ScriptGenerator()
        {
            _toastPanel = FindRequired<RectTransform>(
                "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel").gameObject;
            _toastRect = _toastPanel.GetComponent<RectTransform>();
            _toast = FindRequired<Text>(
                "m_rect_TakeoverRuntimeUI/m_rect_ToastPanel/m_text_ToastText");
        }

        protected override void OnCreate()
        {
            LoadFontAsync().Forget();
        }

        protected override void OnRefresh()
        {
            if (!(UserData is ToastRequest request))
                throw new InvalidOperationException("TakeoverToastUI requires a toast request.");

            _toast.text = request.Message;
            FitToastToText();
            _toastPanel.SetActive(true);
            RestartTimer(request.Seconds);
        }

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

        private void RestartTimer(float seconds)
        {
            if (_timerId != 0)
                GameModule.Timer.RemoveTimer(_timerId);
            _timerId = GameModule.Timer.AddTimer(
                CloseAfterDelay, Mathf.Max(0.05f, seconds), isLoop: false, isUnscaled: true);
        }

        private void CloseAfterDelay(object[] args)
        {
            _timerId = 0;
            GameModule.UI.CloseUI<TakeoverToastUI>();
        }

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

        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
            FitToastToText();
        }

        private T FindRequired<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverToastUI prefab is missing {typeof(T).Name}: {path}");
            return component;
        }

        private sealed class ToastRequest
        {
            public readonly string Message;
            public readonly float Seconds;

            public ToastRequest(string message, float seconds)
            {
                Message = message;
                Seconds = seconds;
            }
        }
    }
}
