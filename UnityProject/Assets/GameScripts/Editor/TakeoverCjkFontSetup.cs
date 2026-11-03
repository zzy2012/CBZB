using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 从系统字体安装一个带 Font Data 的中文字体到 Resources，供 UI Toolkit 使用。
/// UI Toolkit 无法使用 Font.CreateDynamicFontFromOSFont 的动态字体（会报 Include Font Data 错误），
/// 因此必须把 TTF/TTC 作为项目资源导入。
///
/// 注意：从 Windows 系统字体目录复制字体仅用于本地开发验证；
/// 正式发布前请替换为有授权的字体（例如思源黑体 Noto Sans SC）。
/// </summary>
public static class TakeoverCjkFontSetup
{
    private const string FontFolder = "Assets/Resources/UI/Toolkit/Fonts";
    private const string FontBaseName = "TakeoverCjk";

    private static readonly string[] Candidates =
    {
        "simhei.ttf",
        "msyh.ttc",
        "msyh.ttf",
        "simsun.ttc",
        "Deng.ttf",
        "DengXian.ttf",
        "NotoSansSC-Regular.otf",
        "NotoSansCJKsc-Regular.otf",
    };

    public static string FontPath => $"{FontFolder}/{FontBaseName}.ttf";

    [InitializeOnLoadMethod]
    private static void AutoInstallOnLoad()
    {
        if (FindInstalledFontPath() != null) return;
        TryInstall(false);
    }

    [MenuItem("TEngine/Takeover/安装中文字体（从系统复制）")]
    public static void InstallFromMenu()
    {
        TryInstall(true);
    }

    private static string FindInstalledFontPath()
    {
        if (!Directory.Exists(FontFolder)) return null;
        string[] files = Directory.GetFiles(FontFolder, FontBaseName + ".*");
        return files.Length > 0 ? files[0].Replace('\\', '/') : null;
    }

    private static void TryInstall(bool verbose)
    {
        string existing = FindInstalledFontPath();
        if (existing != null)
        {
            if (verbose) Debug.Log($"[TakeoverUI] 中文字体已存在: {existing}");
            return;
        }

        string fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (string.IsNullOrEmpty(fontsFolder) || !Directory.Exists(fontsFolder))
        {
            Debug.LogError("[TakeoverUI] 找不到 Windows 字体目录，请手动把中文 TTF 放到 " + FontFolder);
            return;
        }

        foreach (string candidate in Candidates)
        {
            string source = Path.Combine(fontsFolder, candidate);
            if (!File.Exists(source)) continue;

            try
            {
                Directory.CreateDirectory(FontFolder);
                string destination = $"{FontFolder}/{FontBaseName}{Path.GetExtension(source)}";
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(destination) as TrueTypeFontImporter;
                if (importer != null)
                {
                    importer.includeFontData = true;
                    importer.fontTextureCase = FontTextureCase.Dynamic;
                    importer.SaveAndReimport();
                }

                AssetDatabase.Refresh();

                if (AssetDatabase.LoadAssetAtPath<Font>(destination) == null)
                {
                    Debug.LogWarning($"[TakeoverUI] {candidate} 导入后不是可用字体（可能是 TTC 不支持），继续尝试下一个。");
                    continue;
                }

                Debug.Log($"[TakeoverUI] 已安装中文字体: {source} -> {destination}\n" +
                          "仅用于本地开发；发布前请替换为有授权的字体。");
                return;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[TakeoverUI] 安装系统字体 {candidate} 失败: {exception.Message}");
            }
        }

        Debug.LogError("[TakeoverUI] 未找到可复制的中文系统字体（尝试了 simhei/msyh/simsun），" +
                       "请手动把中文 TTF 放到 " + FontFolder);
    }
}
