namespace GameLogic.UI
{
    /// <summary>
    /// Game UI navigation layers. Higher values are drawn in front of lower values.
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
