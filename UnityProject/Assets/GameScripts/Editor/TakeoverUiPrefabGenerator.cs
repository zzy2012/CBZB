using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TakeoverUiPrefabGenerator
{
    private const string PrefabPath = "Assets/AssetRaw/UI/TakeoverUIWindow.prefab";

    [MenuItem("TEngine/Takeover/生成 UGUI 窗口模板")]
    public static void Generate()
    {
        string directory = Path.GetDirectoryName(PrefabPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var root = new GameObject("TakeoverUIWindow",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.overrideSorting = true;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject prefab;
        try
        {
            prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        if (prefab == null)
        {
            Debug.LogError($"[TakeoverUI] UGUI 窗口模板生成失败: {PrefabPath}");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"[TakeoverUI] UGUI 窗口模板写入后无法加载: {PrefabPath}");
            return;
        }
        Debug.Log($"[TakeoverUI] UGUI 窗口模板已生成: {PrefabPath}");
    }
}
