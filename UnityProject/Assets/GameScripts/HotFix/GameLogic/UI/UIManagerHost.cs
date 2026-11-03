using UnityEngine;

namespace GameLogic.UI
{
    /// <summary>挂在 UIManager 创建的常驻 GameObject 上，驱动每帧 UI Tick 与退出清理。</summary>
    public sealed class UIManagerHost : MonoBehaviour
    {
        private void Update()
        {
            UIManager.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            UIManager.OnHostDestroyed();
        }
    }
}
