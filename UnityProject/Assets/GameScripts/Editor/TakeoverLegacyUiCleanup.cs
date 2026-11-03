using UnityEditor;
using UnityEngine;

/// <summary>
/// 一次性清理旧的 uGUI 预制体与 UIPanel 脚本。
/// 当前部署环境无法执行 shell 删除命令，所以提供编辑器菜单，执行后 Unity 会一并删掉 .meta。
/// </summary>
public static class TakeoverLegacyUiCleanup
{
    private static readonly string[] LegacyAssets =
    {
        "Assets/AssetRaw/UI/TakeoverBattleUI.prefab",
        "Assets/AssetRaw/UI/TakeoverLevelSelectUI.prefab",
        "Assets/AssetRaw/UI/TakeoverResultUI.prefab",
        "Assets/AssetRaw/UI/TakeoverToastUI.prefab",
        "Assets/AssetRaw/UI/TakeoverUIActionButton.prefab",
        "Assets/GameScripts/HotFix/GameLogic/Takeover/UIPanel",
    };

    [MenuItem("TEngine/Takeover/清理旧 uGUI 资源")]
    public static void Cleanup()
    {
        int removed = 0;
        foreach (string path in LegacyAssets)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) == null &&
                !AssetDatabase.IsValidFolder(path))
                continue;

            if (AssetDatabase.DeleteAsset(path)) removed++;
            else Debug.LogWarning($"[Takeover] 删除失败: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Takeover] 旧 UI 资源清理完成，删除 {removed} 项。");
    }
}
