# UI Toolkit 重构（城邦争霸）

本次把 `Takeover` 的 uGUI + TEngine `UIWindow/UIWidget + Prefab` 表现层整体替换为 **UI Toolkit（UXML + USS + C#）**，并新增一个简单的 `UIManager`。旧 UI 脚本与 Prefab 已废弃；TEngine 的 `UIModule` 仅保留给 `LogUI`/Launcher，不再被 `Takeover` 使用。

## 目录

| 位置 | 内容 |
|---|---|
| `GameLogic/UI/` | `UIManager`、`UIView`、`UIToolkitAssets`、`UIFontProvider`、`UISpriteCache`、`UIFormat`、`UIAudioSettings`、`ToastView` |
| `GameLogic/Takeover/UI/` | 标题、存档、阵营、地图、关卡详情、技能（教令）、战斗 HUD、战斗菜单、GM、暂停/设置/结算、音频开关、战斗动作计划、头顶徽标 |
| `Assets/AssetRaw/UI/Toolkit/` | 所有 UXML/USS（YooAsset `UI` 收集器按文件名寻址） |
| `Assets/Resources/UI/TakeoverRuntimeTheme.tss` | 运行时 `PanelSettings` 使用的基础主题 |

## 运行时引导

1. `TakeoverRunner.Awake` → `UIManager.InitializeAsync()`。
2. `UIManager` 创建常驻 `UIDocument` + 运行时 `PanelSettings`（参考分辨率 1366×768、`ScaleWithScreenSize`、`MatchWidthOrHeight`、`sortingOrder=100`），主题从 `Resources/UI/TakeoverRuntimeTheme.tss` 读取。
3. 依次建立 `Screen / Persistent / Popup / Overlay / Toast` 五个层；`Screen` 层同时只保留一个界面，`Popup/Overlay` 各最多一个。
4. 中文字体必须是一个**带 Font Data 的项目字体资源**（`Assets/Resources/UI/Toolkit/Fonts/TakeoverCjk.ttf`）。UI Toolkit 不支持 `Font.CreateDynamicFontFromOSFont` 的动态字体（会报 Include Font Data 错误）；编辑器菜单 **TEngine → Takeover → 安装中文字体（从系统复制）** 会从系统复制 simhei/msyh 并设置 `includeFontData=true`。
5. 鼠标是否落在 UI 上用 `IPanel.Pick(RuntimePanelUtils.ScreenToPanel(...))` 判断；战斗 HUD 根节点与 Toast/徽标层 `pickingMode=Ignore`，空白区域仍可点世界。

## 界面与 index.html 的对应

| index.html | Unity 实现 |
|---|---|
| 标题 / 开始远征 / 音效音乐 | `TitleView` + 常驻 `AudioControlsView` |
| 3 个远征档案 | `SaveSlotsView`；槽 1 兼容旧单档 `takeover-campaign.json` |
| 3 阵营 + 通关 2 关解锁 | `FactionSelectView`；第 2/3 章素材未导入，解锁后点击只提示 |
| 战役地图 + 关卡详情 | `CampaignMapView` + `MissionDetailView`；8 关，GREAT/NEW/LOCKED 来自真实存档 |
| 技能树 | `SkillTreeView`；对应现有教令树（西利亚 9 条 × 3 档），技能点=教令点 |
| 战斗 HUD / 兵种菜单 / 升级 | `BattleHudView` + `BattleMenuController` + `TakeoverActionPlanner`，按钮接真实的招募/升级/建造命令 |
| 教程提示条 | `TutorialController`；4 条文案，“继续提示”推进并写入存档 |
| 暂停 / 设置 / 结算 | `PauseOverlayView` / `SettingsOverlayView` / `ResultOverlayView`（共用 `TakeoverMessageOverlayView.uxml`） |
| GM 面板 | `GmPanelController`；保留金币+1000、法力+500、胜负、7 档速度，并追加 AI 托管 |
| Toast | `ToastView`；顶部居中 2.3 秒 |

## 存档（v3）

```csharp
public sealed class CampaignSave
{
    public int Version = 3;
    public Faction SelectedFaction = Faction.Westaria;
    public int LastLevel, Xp, EdictPoints;
    public int Medals;      // 胜利次数，上限 = 当前战役关卡数（当前 8）
    public int Gold;        // 胜利累计展示值：+1000 + 250*stars
    public int Honor;       // 城堡升级 +25
    public int TutorialStep;
    public List<int> Unlocked, EdictTiers;
    public List<LevelRecord> Records;
}
```

- 三槽文件：`Application.persistentDataPath/takeover-campaign-slot-{1..3}.json`；槽 1 读不到新文件时回退旧 `takeover-campaign.json`。
- 等级：`11 + floor(Xp / 1500)`；地图 XP 条显示 `Xp % 1500 / 1500`。
- 教令点：首通 +2、首次三星 +2（沿用原规则）；“重选”按已购档位 × 每档 `cost` 全额退还。
- 阵营：`Westaria = 1..8`、`Khaganate = 11..18`、`Cult = 21..28`；当前只有 `Westaria` 可玩（缺第 2/3 章地图底图与 waypoint）。

## 输入与头顶徽标

- 世界点击、缩放、平移不再依赖 uGUI `EventSystem`；统一用 `UIManager.IsPointerOverUI()` 判断。
- 小队徽标是 UI Toolkit 元素，每帧把世界坐标经 `WorldToScreenPoint → ScreenToPanel` 定位；`PointerDown` 调用 `TakeoverRunner.BeginBadgeDrag(squad)`，后续拖动/松开沿用 8px 阈值与真实寻路下令。
- 建筑徽标显示阵营环、等级与血条；`ShowSquadBadges/ShowBuildingBadges` 调试开关仍有效。

## 校验

`TakeoverValidation.RunBatch` 现在包含 `TakeoverUiValidation`：

- 主题、UXML、USS 能否导入；每个界面必需 name 节点是否齐全；UXML 无 `?` 占位文案。
- 地图节点/徽标用到的 Sprite 是否存在。
- 存档 v3 JSON 往返、等级公式、非法阵营回退、教令退款、三章关卡映射。
- `TakeoverValidation.RunPlaySmoke`：等 UIManager 初始化 → 槽 1 重置 → `StartLevel(1)` → `BattleHudView` 打开且模拟推进 >0.5s → GM 判胜 → `ResultOverlayView` 打开 → PASS。

## 实现备注

- UXML 共 12 个（每个界面一个；暂停/设置/结算共用 `TakeoverMessageOverlayView`）；USS 3 个：`TakeoverCommonStyles`、`TakeoverScreenStyles`、`TakeoverBattleStyles`。
- 动态内容（存档卡、阵营卡、关卡节点、教令卡、战斗按钮、GM 速度按钮、头顶徽标）由 C# 创建 UI Toolkit 元素并套 USS class，避免 UXML 自定义元素与热更程序集的脚本引用问题。
- 中文字体用 `FontDefinition.FromFont` + 内联 `unityFontDefinition` 写到每个 TextElement（含动态创建的卡片/徽标），优先于主题默认字体；字体来自 Resources 里的 TTF/TTC，而不是 OS 动态字体。
- 主题固定放在 `Assets/Resources/UI/TakeoverRuntimeTheme.tss`，运行时 `PanelSettings` 用代码创建并指定参考分辨率 1366×768。
- 旧 Prefab 与 `Takeover/UIPanel/` 通过菜单 **TEngine → Takeover → 清理旧 uGUI 资源** 删除；旧 `.cs` 已只剩占位注释。

## 已知限制

- 第 2/3 章仅保留界面与存档结构；`OriginalRules.EnemyPlayersByLevel` 与地图底图/waypoint 只有 1–8，需补齐后把 `TakeoverFactions.IsPlayable` 打开。
- 关卡名/描述、教令图标为 `TakeoverUiContent` 中的占位文案。
- 中文字体由编辑器菜单从 Windows 系统字体复制到 `Assets/Resources/UI/Toolkit/Fonts/TakeoverCjk.*`，**仅用于本地开发**；发布前请替换为有授权的字体（如思源黑体）。
- USS 按 Unity 2022.3 支持范围手写：无 CSS 渐变、`box-shadow`、彩色 emoji；需要更强视觉时补九宫格贴图。
- 旧 Prefab 仍留在 `Assets/AssetRaw/UI/`（当前环境无法执行 shell 删除命令），已不被代码/校验引用；在 Unity 中执行菜单 **TEngine → Takeover → 清理旧 uGUI 资源** 可一次性删除旧 Prefab 与 `Takeover/UIPanel/`。
