namespace GameLogic.UI
{
    /// <summary>
    /// UI Toolkit 的层。数值即绘制顺序，越大约靠前。
    /// </summary>
    public enum UILayerKind
    {
        Screen = 0,
        Persistent = 10,
        Popup = 20,
        Overlay = 30,
        Toast = 40,
    }
}
