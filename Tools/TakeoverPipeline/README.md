# 城邦争霸 1.0.4 外部流水线

这些脚本用于外部 Windows/CI 环境。素材提取需要 Java 和 FFDec；Windows Player 构建还需要 Windows Build Support、PowerShell、.NET 和 Luban。

## 1. 锁定并提取原版资源

```powershell
.\extract_takeover_1_0_4.ps1 `
  -GameDataRoot "E:\GameData\城邦争霸_Takeover" `
  -FfdecJar "E:\GameData\FFDec\ffdec-cli.jar" `
  -JavaExe "java.exe"
```

脚本同时验证正式版 1.0.4 的 20,934,803 字节大小和 SHA-256，然后通过 FFDec CLI 导出脚本、图片、声音、字体和二进制资源。导出内容位于仓库外的 `GameDataRoot`，避免把 20 MB SWF 和完整反编译中间产物提交进 Git。

提取后必须完成：

- 先运行 `python Tools/TakeoverPipeline/import_chapter_assets.py`，从 SymbolClass 和首章数据生成本机使用的原版资源。
- 运行 `python Tools/TakeoverPipeline/generate_asset_manifest.py` 生成固定 location 清单。
- 按原版 SymbolClass / 类名建立 `original_symbol -> Unity location` 映射。
- 将地图、建筑、单位、HUD、图标、音频和字体导入 `Assets/AssetRaw`。
- 以原版 SWF 舞台尺寸设置场景和 CanvasScaler，并保留原版 Y 轴 `×2` 等距比例。
- 对照 `Docs/城邦争霸数值/` 和反编译源码核对规则，未确认项不得用近似值替代。

## 2. 生成配置并构建 Windows x64

```powershell
.\build_windows.ps1 `
  -UnityExe "C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe" `
  -UnityProject "X:\work\CBZB\UnityProject" `
  -DotnetExe "dotnet.exe"
```

流程固定为：

1. Luban 重新生成 C# 和 bytes。
2. `TakeoverValidation.RunBatch` 校验首章数据与规则。
3. `TakeoverValidation.RunScenarioSoakBatch` 扫描 L1-L8。
4. `TEngine.ReleaseTools.AutomationBuild` 生成 YooAsset、热更 DLL 和 Windows x64 Player。
5. 检查 `UnityProject/Build/Windows/Release_Windows.exe` 和符号文件。

任一工具、配置、校验或资源缺失时脚本直接失败，不执行降级构建。
