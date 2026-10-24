# TEngine 学习指南（基于当前仓库实读）

> 本笔记不是 README 的复述，而是**按代码验证过**的用法说明。
> 阅读顺序建议：§1 跑起来 → §2 心智模型 → §3 UI → §4 事件 → §5 其它常用 API → §6 三个练习 → §7 坑。
> 官方文字文档在 `Books/`，AI 精炼文档在 `UnityProject/.claude/skills/tengine-dev/references/`（本笔记与它冲突处以代码为准，差异见 §7）。

---

## 0. 一句话理解

TEngine = 一套 **"模块系统 + 流程驱动 + 全平台热更"** 的 Unity 工程骨架：

| 组成 | 提供者 | 你该怎么用 |
|------|--------|-----------|
| 模块容器、事件、流程、FSM、计时器、音频、场景、调试器 | TEngine 自研（`TEngine.Runtime`） | `GameModule.XXX` 直接调用，不要自己 new |
| 资源打包/加载/引用计数 | YooAsset | 只操作 `GameModule.Resource`，禁止 `Resources.Load` |
| 代码热更 | HybridCLR | 业务代码放 `GameScripts/HotFix/`，主包不热更 |
| 异步 | UniTask | 一律 `async UniTask`，禁止 Coroutine |
| 配置表 | Luban | Excel → C# + bytes，`ConfigSystem.Instance.Tables` |
| UI | TEngine 自研（在**热更区** GameLogic） | `[Window]` + `UIWindow` + 代码生成 |

**最重要的一点**：UI 框架（`UIModule`/`UIWindow`/`UIWidget`）**不在** `TEngine.Runtime` 里，而是在热更程序集 `GameLogic` 下（`Assets/GameScripts/HotFix/GameLogic/Module/UIModule/`）。所以 UI 代码天生可热更，而框架核心不行。

---

## 1. 先跑起来（5 分钟）

### 环境
- Unity **2021.3.45f1**（`UnityProject/ProjectSettings/ProjectVersion.txt`；README 写 2021.3.20f1c1，以工程为准）
- **不需要 Odin**：README 说必须要 `Assets/Plugins/Sirenix`，但仓库里那 3 处 `Sirenix.OdinInspector` 引用全部包在 `#if ODIN_INSPECTOR` 里，工程没定义这个宏，所以能正常编译
- 打开目录：`UnityProject/`（不是仓库根目录）

### 如果一打开就报 "Project has invalid dependencies"
说明 `Packages/manifest.json` 被换成了 **Unity 6 的清单**，和 2021.3 不兼容。典型两条：

| 报错 | 原因 | 正确写法（2021.3） |
|------|------|------------------|
| `com.unity.ugui@2.0.0 cannot be found` | ugui 2.0.0 是 Unity 6 才有的（TMP 被并入 ugui） | `"com.unity.ugui": "1.0.0"` |
| `com.unity.modules.accessibility cannot be found` | 该模块 2023+ 才有 | 整行删掉 |

Unity 6 的清单还会**弄丢 `com.unity.textmeshpro`**（因为 6 把它塞进了 ugui），所以必须补回 `"com.unity.textmeshpro": "3.0.6"`，否则 `#if TextMeshPro` 那 16 处代码会报 CS0246。
改完 `manifest.json` 后重启 Unity 让它重新解析（**不要**直接删 `Library`，会触发 hybridclr 从 gitee 重新拉包）。

### 运行步骤
1. Unity 打开 `UnityProject/`
2. 打开场景 `Assets/Scenes/main.unity` —— 场景里只有三样东西：`Main Camera`，以及两个 **Prefab 实例** `GameEntry`、`UIRoot`（都在 `Assets/TEngine/Settings/Prefab/`）
3. 工具栏右侧的下拉 `EditorPlayMode` 选 `EditorSimulateMode`（默认值，直接从 Asset 目录读资源，不打 AB）
4. 点 Play

**成功标志**：Console 出现

```
======= 看到此条日志代表你成功运行了热更新代码 =======
======= Entrance GameApp =======
======= StartGameLogic =======
```

随后 `BattleMainUI` 被打开（`GameApp.StartGameLogic()` 里写死的）。

### 这一下背后发生了什么

```
GameEntry.Awake()                                   // Assets/GameScripts/GameEntry.cs
 ├ ModuleSystem.GetModule<IUpdateDriver>()          // 逐个把模块创建出来（反射）
 ├ ModuleSystem.GetModule<IResourceModule>()
 ├ ModuleSystem.GetModule<IDebuggerModule>()
 ├ ModuleSystem.GetModule<IFsmModule>()
 └ Settings.ProcedureSetting.StartProcedure()

Procedure 链（主包，不可热更；顺序写在 Assets/TEngine/Settings/ProcedureSetting.asset）
 ProcedureLaunch → ProcedureSplash → ProcedureInitResources → ProcedureInitPackage
 → ProcedureCreateDownloader → ProcedureDownloadFile → ProcedurePreload
 → ProcedureLoadAssembly → ProcedureStartGame

ProcedureLoadAssembly
 ├ 加载热更 dll（有 ENABLE_HYBRIDCLR 时从 AssetRaw/DLL 读 .bytes）
 └ 反射调用 GameApp.Entrance(object[])
      ├ GameEventHelper.Init()                 ← 必须最先，否则接口事件全哑
      ├ 保存热更程序集列表
      ├ Utility.Unity.AddDestroyListener(Release)
      └ StartGameLogic() → GameModule.UI.ShowUIAsync<BattleMainUI>()
```

### 关键：当前仓库**没开** HybridCLR

`ProjectSettings.asset` 的 `scriptingDefineSymbols` 只有 `ENABLE_LOG;TextMeshPro`，**没有** `ENABLE_HYBRIDCLR` / `ENABLE_OBFUZ`。
所以现在跑的是"普通模式"：`GameLogic` 作为普通程序集编译进工程，`ProcedureLoadAssembly` 走 `GetMainLogicAssembly()` 分支，按名字 `GameLogic.dll` 从 `AppDomain` 里找到程序集再反射调 `Entrance`。
**日常开发就保持这个模式**，改了代码直接 Play 即可，不需要转表/打 AB/生成 dll。

---

## 2. 心智模型

### 2.1 四层依赖（只能向下）

```
业务层   GameLogic（热更）      GameProto（热更，Luban 生成）
   ↓
主包层   Assembly-CSharp：GameEntry.cs + Procedure/   |   Launcher（启动器 UI）
   ↓
框架层   TEngine.Runtime（模块 / 事件 / 资源封装 / 流程 / FSM / 音频 / 场景 / 计时 / 本地化）
   ↓
基础层   YooAsset · HybridCLR · UniTask · Luban · Odin
```

| 程序集 | 路径 | 热更 | 职责 |
|--------|------|------|------|
| `TEngine.Runtime` | `Assets/TEngine/Runtime/` | 否 | 框架核心 |
| `TEngine.Editor` | `Assets/TEngine/Editor/` | 否 | 编辑器工具（打包、Luban、HybridCLR、图集） |
| `Launcher` | `Assets/Launcher/` | 否 | 启动加载界面（`LauncherMgr`） |
| `Assembly-CSharp` | `Assets/GameScripts/`（`GameEntry.cs`、`Procedure/`） | 否 | 启动入口 + 主包流程 |
| `GameProto` | `Assets/GameScripts/HotFix/GameProto/` | 是 | Luban 生成代码 |
| `GameLogic` | `Assets/GameScripts/HotFix/GameLogic/` | 是 | **你的主战场**：UI、系统、事件定义 |

> 注意：README 里画的 `GameScripts/Main` 程序集**不存在**（`GameEntry.cs` 和 `Procedure/` 没有 asmdef，属于 `Assembly-CSharp`）。

### 2.2 三个入口，别搞混

| 入口 | 文件 | 什么时候改它 |
|------|------|-------------|
| `GameEntry.Awake()` | `Assets/GameScripts/GameEntry.cs` | 几乎不改。只做模块预热 + 启动流程 |
| Procedure 链 | `Assets/GameScripts/Procedure/Procedure*.cs` | 改"启动/热更/下载/加载代码"节奏时改 |
| `GameApp.Entrance()` | `Assets/GameScripts/HotFix/GameLogic/GameApp.cs` | **日常主入口**：游戏启动后要初始化什么，写这里 |

`GameApp` 是 `partial class`，扩展入口逻辑就再开一个 `GameApp_XXX.cs` 写 `public partial class GameApp`。

### 2.3 三种"模块"，分清

**① 框架模块**（`ModuleSystem` 管）
- 通过**接口**取：`ModuleSystem.GetModule<ITimerModule>()`
- 命名约定：接口 `ITimerModule` → 实现 `TEngine.TimerModule`（同命名空间、去掉 `I`），靠反射 `Type.GetType` 找到
- 首次 `GetModule` 时懒创建，`OnInit()` 立即调用；实现了 `IUpdateModule` 的会被 `RootModule.Update()` 每帧驱动
- 现有：`IResourceModule` `ITimerModule` `IAudioModule` `ISceneModule` `IFsmModule` `IProcedureModule` `ILocalizationModule` `IObjectPoolModule` `IDebuggerModule` `IUpdateDriver`

**② 门面 `GameModule`**（热更区的便利层，`GameModule.cs`）
```csharp
GameModule.Base          // RootModule（FindObjectOfType）
GameModule.Resource      // IResourceModule
GameModule.UI            // UIModule（单例，注意不是接口）
GameModule.Timer / Scene / Audio / Fsm / Procedure / Localization / Debugger
```
> 业务代码**只用 `GameModule.XXX`**，不要反复 `ModuleSystem.GetModule<T>()`。

**③ 业务系统**（`Singleton<T>` + `SingletonSystem`）
```csharp
public class WalletSystem : Singleton<WalletSystem>, IUpdate
{
    protected override void OnInit() { }      // 首次访问 Instance 时调用
    public void OnUpdate() { }               // 被 UpdateDriver 每帧驱动
    protected override void OnRelease() { }
}
// 使用
WalletSystem.Instance.AddGold(100);
```
`UIModule` 本身就是这个模式的范例（`Singleton<UIModule>, IUpdate`）。
注意：构造函数是 `protected`，**必须**通过 `Instance` 创建，否则编辑器会报"请必须通过 Instance 方法来实例化"。
`IUpdate / IFixedUpdate / ILateUpdate / IDrawGizmos` 都在 `SingletonSystem.cs` 里。

---

## 3. UI 框架（最常用，先吃透这一节）

### 3.1 层级 `UILayer`

| 值 | 名称 | 用途 |
|----|------|------|
| 0 | Bottom | 世界空间 UI、背景 |
| 1 | UI | 普通主界面 |
| 2 | Top | 弹窗、全屏遮罩 |
| 3 | Tips | Toast、飘字 |
| 4 | System | 加载中、异常提示 |

层级只影响 `sortingOrder`（`layer * 2000 + 序号 * 100`），不影响生死。

### 3.2 `[Window]` 特性（真实签名）

```csharp
public WindowAttribute(UILayer windowLayer, string location = "", bool fullScreen = false, int hideTimeToClose = 10)
public WindowAttribute(UILayer windowLayer, bool fromResources, bool fullScreen = false, int hideTimeToClose = 10)
public WindowAttribute(UILayer windowLayer, bool fromResources, string location, bool fullScreen = false, int hideTimeToClose = 10)
```

```csharp
[Window(UILayer.UI, location: "ShopUI")]                       // 最常用
[Window(UILayer.Top, location: "LoginUI", fullScreen: true)]   // 全屏挡板
[Window(UILayer.UI)]                                           // location 省略 = 用类名
```

- `location` = YooAsset 定位地址（`AssetRaw/UI/` 下的**文件名，不含扩展名**）
- `fullScreen: true` 会把它**下面**的窗口全部设为不可见（`UIModule.OnSetWindowVisible`）
- `hideTimeToClose` 是 **int**（默认 10 秒）：`HideUI` 后到点自动 `CloseUI`

### 3.3 生命周期（`UIBase` → `UIWindow`）

```
ShowUIAsync<T>()
  └ 加载 Prefab（UIModule.Resource.LoadGameObjectAsync）
      └ InternalCreate()   ← 只在"首次"执行一次
           Inject()              依赖注入（UIBase.Injector 静态委托）
           ScriptGenerator()     绑定节点（代码生成产物就写这里）
           BindMemberProperty()  预留钩子，一般不写
           RegisterEvent()       注册事件（AddUIEvent，销毁时自动清理）
           OnCreate()            一次性初始化
      └ InternalRefresh()  ← 每次 Show 都执行
           OnRefresh()           用 UserData 刷新显示

每帧（仅 Visible 时）：InternalUpdate() → OnUpdate()

HideUI<T>()  → Visible=false，起 HideTimeToClose 定时器
CloseUI<T>() → InternalDestroy()
                 RemoveAllUIEvent()     先清事件
                 子 Widget.OnDestroy()  先死子
                 OnDestroy()            再死自己
                 Destroy(gameObject)
```

要点：
- **没有 `OnClose`**，销毁回调是 `OnDestroy()`
- 同一个窗口重复 `ShowUI` 不会重建，只会 `Pop → Push` 再 `OnRefresh`
- `UserData` / `UserDatas` 就是 `ShowUIAsync<T>(a, b)` 传进来的可变参数

### 3.4 打开 / 关闭 API（`UIModule`）

```csharp
GameModule.UI.ShowUIAsync<ShopUI>();                  // 异步，fire and forget
GameModule.UI.ShowUI<ShopUI>();                       // 同步（资源必须已加载）
var shop = await GameModule.UI.ShowUIAsyncAwait<ShopUI>(itemId);   // 等实例
GameModule.UI.ShowUIAsync<ItemDetailUI>(itemId, extraData);        // 带 UserData

GameModule.UI.CloseUI<ShopUI>();          // 关闭并销毁
GameModule.UI.HideUI<ShopUI>();           // 隐藏，超时自动关闭
GameModule.UI.CloseAll();
GameModule.UI.CloseAllWithOut<ShopUI>();

GameModule.UI.HasWindow<ShopUI>();
GameModule.UI.IsAnyLoading();
GameModule.UI.GetTopWindow();
```

### 3.5 Prefab 的硬性要求（**最容易踩**）

`UIWindow.Handle_Completed` 里会 `GetComponent<Canvas>()`，**找不到就 throw**。所以：

1. UI Prefab 根节点必须有 **`Canvas`** + **`GraphicRaycaster`**
2. 根节点是 `RectTransform`
3. Prefab 放在 `Assets/AssetRaw/UI/` 下（`AssetBundleCollectorSetting` 已按文件名寻址，`PackRule = PackSeparately`）
4. 文件名与 `[Window]` 的 `location` 一致

`UIRoot` 结构（`Assets/TEngine/Settings/Prefab/UIRoot.prefab`）：`UIRoot > UICanvas(Canvas) / UICamera / EventSystem`。窗口实例都挂到 `UIRoot/UICanvas` 下。

### 3.6 节点命名 → 自动绑定

`ScriptGeneratorSetting.asset`（`Assets/Editor/UIScriptGenerator/ScriptGeneratorSetting.asset`）里定义前缀，代码生成器遍历子节点按前缀产出字段：

| 前缀 | 生成类型 | 前缀 | 生成类型 |
|------|---------|------|---------|
| `m_go` | GameObject | `m_btn` | Button |
| `m_item` | **Widget（不再向下遍历）** | `m_toggle` | Toggle |
| `m_tf` | Transform | `m_group` | ToggleGroup |
| `m_rect` | RectTransform | `m_input` | InputField |
| `m_text` | Text | `m_tmpInput` | TMP_InputField |
| `m_richText` | RichTextItem | `m_tmpDropdown` | TMP_Dropdown |
| `m_img` | Image | `m_tmp` | TextMeshProUGUI |
| `m_rimg` | RawImage | `m_slider` | Slider |
| `m_scroll` | ScrollRect | `m_canvasGroup` | CanvasGroup |
| `m_scrollBar` | Scrollbar | `m_curve` | AnimationCurve |
| `m_grid` / `m_hlay` / `m_vlay` | LayoutGroup | | |

前缀是**按配置表顺序匹配**（`m_scrollBar` 排在 `m_scroll` 之前、`m_tmpInput` 排在 `m_tmp` 之前），所以新加前缀时要注意顺序。不需要绑定的节点不加前缀即可。

> ⚠️ `references/naming-rules.md` 里还列了 `m_canvas_`、`m_dropdown_`，但本仓库的 `ScriptGeneratorSetting.asset` **没有配置这两条规则**（只到 23 项：go/item/tf/rect/text/richText/btn/img/rimg/scrollBar/scroll/input/grid/hlay/vlay/slider/group/curve/canvasGroup/toggle/tmpInput/tmpDropdown/tmp）。写了也不会生成绑定。

### 3.7 代码生成（本仓库已切换为 UIBindComponent 模式）

`ScriptGeneratorSetting.asset` 里 `useBindComponent: 1`、`CodeStyle: 1`（字段前缀 `m_`），所以推荐流程是：

1. 选中 UI Prefab 根节点 → 添加/选中 **`UIBindComponent`** 组件
2. Inspector 顶部按钮：
   - **重新绑定组件**：清空并重新遍历子节点，按前缀把 `RectTransform`/组件收集进 `m_components` 列表
   - **生成脚本** / **生成UniTask脚本**：写文件
     - `Assets/GameScripts/HotFix/GameLogic/UI/Gen/{类名}_Gen.g.cs`（只读，自动生成，别手改）
     - 若勾选"生成实现类"：`.../GameLogic/UI/{类名}.cs`（业务实现，已存在则跳过）
   - **生成标准版绑定代码**：只生成到**剪贴板**，手粘（老习惯）
3. 生成的绑定代码长这样：

```csharp
private UIBindComponent m_bindComponent;
protected override void ScriptGenerator()
{
    m_bindComponent = gameObject.GetComponent<UIBindComponent>();
    if (m_bindComponent == null) { Log.Error($"根物体: {gameObject.name} 缺少组件 UIBindComponent"); return; }
    m_btnStart = m_bindComponent.GetComponent<Button>(0);   // 索引顺序 = 收集顺序
    m_txtTitle = m_bindComponent.GetComponent<Text>(1);
}
```

> 字段命名 = 去掉节点前缀 + 按 `CodeStyle` 加字段前缀。本仓库 `CodeStyle: 1`（`m_` 前缀），所以生成 `m_btnStart`；
> `BattleMainUI.cs` 里的 `_rectContainer` 是 `CodeStyle: 0`（`_` 前缀）时代生成的。两种都合法，别被混用搞糊涂。

**注意**：`BattleMainUI.cs` 是**旧风格**产物（`FindChildComponent<RectTransform>("m_rectContainer")` 直接按路径找），说明它是在切换模式前生成的。两种风格都能跑，但**不要对老 UI 点"重新绑定组件"**，否则字段会变成索引式，老代码要跟着改。

> 生成器依赖节点名。改名 = 重新生成 = 老代码可能要改，所以节点名先定好再写逻辑。

### 3.8 `UIWidget`（列表项 / 子组件）

```csharp
public class ItemWidget : UIWidget
{
    protected override void ScriptGenerator() { /* 绑节点 */ }
    protected override void OnRefresh() { /* 填数据 */ }
}

// 窗口内创建
_item = CreateWidget<ItemWidget>("m_item_Slot");              // 复用场景里已有的节点
_item = await CreateWidgetByPathAsync<ItemWidget>(trans, "ItemWidget"); // 动态加载 prefab
AdjustIconNum<ItemWidget>(_icons, 10, parentTrans);           // 批量增删
```

- Widget 生命周期与 Window 一致，但 `CreateImp` 里会**直接调一次 `OnRefresh()`**
- 销毁入口分三种：`Destroy()`（自己主动销毁）、`OnDestroyWidget()`（框架回收，别手调）、`OnDestroy()`（**业务清理写这里**，框架会在回收前调用）
- 父窗口销毁时先销毁子 Widget，**别在窗口 `OnDestroy` 里访问子 Widget**

---

## 4. 事件系统

### 4.1 两种用法

**接口事件（推荐，类型安全）**——三步：

```csharp
// 1. 定义（放 GameLogic/IEvent/，必须 [EventInterface]）
[EventInterface(EEventGroup.GroupLogic)]
public interface IWalletEvent
{
    void OnGoldChanged(int gold);
}

// 2. 发送（任何地方）
GameEvent.Get<IWalletEvent>().OnGoldChanged(100);

// 3. 监听（UI 里用 AddUIEvent，自动清理）
protected override void RegisterEvent()
    => AddUIEvent<int>(IWalletEvent_Event.OnGoldChanged, OnGoldChanged);
```

- `GameEventHelper` / `IWalletEvent_Event` 都是 **Roslyn SourceGenerator 生成**的（`Assets/TEngine/Runtime/Core/GameEvent/SourceGenerator.dll`），仓库里没有 `.cs` 源文件，**不要手写**
- `IWalletEvent_Event` 这种"接口名 + `_Event`"的类名是文档给的约定；第一次用时在 IDE 里搜一下 `IWalletEvent`，跳进生成文件确认成员名（不同版本生成器可能微调）
- 前提：`GameEventHelper.Init()` 已在 `GameApp.Entrance` 里最先调用（已有，别删）

**int / string 事件**：`GameEvent.Send(1001)` + `GameEvent.AddEventListener(1001, cb)`，简单但无编译期检查，适合临时通知。

### 4.2 谁来清理监听

| 场景 | 写法 | 清理 |
|------|------|------|
| `UIWindow` / `UIWidget` 内 | `AddUIEvent(...)` | 窗口销毁时 `RemoveAllUIEvent()` **自动**清理 |
| 普通类（System/Manager） | `GameEventMgr _mgr = new(); _mgr.AddEvent(...)` | 手动 `_mgr.Clear()` |
| 临时 | `GameEvent.AddEventListener/RemoveEventListener` | 手动成对调用 |

`AddUIEvent` 支持 0~4 个泛型参数；`GameEvent.Send` 支持到 6 个。

### 4.3 事件定义规范
- 命名：`On` + 过去式 + 名词（`OnGoldChanged`、`OnBattleEnded`）
- 接口：`I` + 名词（`IWalletEvent`）
- 手写 int 常量容易撞号，能用接口事件就别手写

---

## 5. 其它常用 API

### 5.1 资源（`GameModule.Resource`）

**寻址**：location = 文件名（不含路径、不含扩展名）。`Assets/AssetRaw/UI/ShopUI.prefab` → `"ShopUI"`。

| 资源 | 用什么 | 要手动释放吗 |
|------|--------|-------------|
| Sprite / 图集子图 | `image.SetSprite("icon_x")` / `SetSubSprite("Atlas", "子图")` | 否（自带缓存池） |
| 要实例化的 Prefab | `await GameModule.Resource.LoadGameObjectAsync("Hero", parent)` | 否，`Destroy(go)` 即可 |
| TextAsset / ScriptableObject / AudioClip | `await GameModule.Resource.LoadAssetAsync<T>(loc)` | **是**，用完 `UnloadAsset(asset)` |
| Scene | `await GameModule.Scene.LoadSceneAsync("Battle")` | — |

```csharp
_imgIcon.SetSprite("item_icon_001");
_imgIcon.SetSprite("item_icon_001", setNativeSize: true, callback: img => { });
_imgIcon.SetSubSprite("ItemAtlas", "item_sword_01");   // 图集子图，无回调重载

var go = await GameModule.Resource.LoadGameObjectAsync("HeroPrefab", parent);
Destroy(go);                                            // 框架自动减引用

var cfg = await GameModule.Resource.LoadAssetAsync<TextAsset>("level_data");
GameModule.Resource.UnloadAsset(cfg);                   // 必须配对

// 切场景后整理
GameModule.Resource.UnloadUnusedAssets();
GameModule.Resource.ForceUnloadUnusedAssets(performGCCollect: true);
```

**禁止**：`Resources.Load()`、`LoadAssetAsync<Sprite>()`、`LoadAssetAsync<GameObject>() + Instantiate`、`StartCoroutine`。

### 5.2 计时器

```csharp
// 回调签名是 TimerHandler(object[] args)，不是无参 Action！
private int _timerId;
_timerId = GameModule.Timer.AddTimer(OnTick, 1f, isLoop: true);
_timerId = GameModule.Timer.AddTimer(_ => { /* 闭包也行 */ }, 3f, isLoop: false, isUnscaled: true);
_timerId = GameModule.Timer.AddTimer(OnTickWithArg, 1f, true, false, someObject); // args 传参避免闭包

private void OnTick(object[] args) { }

GameModule.Timer.Stop(id);
GameModule.Timer.Resume(id);
GameModule.Timer.Restart(id);
GameModule.Timer.GetLeftTime(id);
GameModule.Timer.IsRunning(id);
GameModule.Timer.RemoveTimer(id);   // 销毁时必须调，否则回调持有已死对象
```

### 5.3 音频 / 场景 / FSM

```csharp
GameModule.Audio.Play(AudioType.Music, "MainTheme", bLoop: true);
GameModule.Audio.Play(AudioType.Sound, "Click");
GameModule.Audio.Stop(AudioType.Music, fadeout: true);
GameModule.Audio.MusicVolume = 0.8f;
GameModule.Audio.MusicEnable = true;

var scene = await GameModule.Scene.LoadSceneAsync("Battle", LoadSceneMode.Additive,
    progressCallBack: p => { });
await GameModule.Scene.UnloadAsync("Battle");

public class IdleState : FsmState<Player>
{
    protected override void OnEnter(IFsm<Player> fsm) { }
    protected override void OnUpdate(IFsm<Player> fsm, float elapse, float real) { }
    protected override void OnLeave(IFsm<Player> fsm, bool isShutdown) { }
}
var fsm = GameModule.Fsm.CreateFsm("PlayerFsm", player, new IdleState(), new RunState());
fsm.Start<IdleState>();
fsm.SetData<int>("Hp", 100);
GameModule.Fsm.DestroyFsm<Player>("PlayerFsm");
```

### 5.4 日志 / 内存池

```csharp
// Debug 是 [Conditional("ENABLE_LOG")] 的：没定义 ENABLE_LOG 的包里会被整段剥离
// 本工程 Standalone/Android/iPhone/WebGL 都定义了 ENABLE_LOG，所以编辑器与真机都能看到
Log.Debug("调试");  Log.Info("");  Log.Warning("");  Log.Error("");  Log.Fatal("");

class DamageInfo : IMemory { public int Damage; public void Clear() => Damage = 0; }
var info = MemoryPool.Acquire<DamageInfo>();
MemoryPool.Release(info);   // Release 后不可再用，不可重复 Release
```

### 5.5 配置表（Luban）

现状：`Configs/GameConfig/Datas/` 有模板 Excel，但 **`GameProto` 下还没有生成代码**（只有 `LubanLib/`），`Tools/Luban/` 里也只有 `about.txt`（**没有 Luban.dll**）。所以要用配置表得先补工具链：

```bat
:: 1) 准备 Luban：把 Luban.dll 放到 Tools/Luban/（about.txt: "使用 luban-next，请前往 Tools/build-luban 编译或自行导入"）
:: 2) 在 Configs/GameConfig/ 下跑
gen_code_bin_to_project.bat      :: 或 Unity 菜单 TEngine/Luban/转表 &X
```

脚本会：
1. 把 `CustomTemplate/ConfigSystem.cs`、`ExternalTypeUtil.cs` 拷到 `GameProto/`
2. 生成代码到 `Assets/GameScripts/HotFix/GameProto/GameConfig/`
3. 生成数据到 `Assets/AssetRaw/Configs/bytes/`

用起来：

```csharp
var tables = ConfigSystem.Instance.Tables;      // 首次访问懒加载
var item = tables.TbItem.Get(1001);
foreach (var row in tables.TbItem.DataList) { }
```

配置的 **bytes 必须打上 `PRELOAD` 标签**，`ProcedurePreload` 才会在启动时预加载（`_resourceModule.GetAssetInfos("PRELOAD")`）。当前仓库**没有任何资源带这个标签**，所以预加载是空的——加配置表时记得在 `AssetBundleCollectorSetting` / Asset 的标签里补。

---

## 6. 三个练习（按顺序做，做完就掌握 80%）

### 练习 A：热身 —— 改 `BattleMainUI`
1. 打开 `Assets/AssetRaw/UI/BattleMainUI.prefab`，改某个 `m_text_xxx` 的文案
2. 新建一个按钮节点 `m_btnTest`、一个文本 `m_textTitle`
3. 在 `BattleMainUI.ScriptGenerator()` 里**手写**两行绑定（该文件是老风格，手写最快）：
   ```csharp
   _btnTest   = FindChildComponent<Button>("m_btnTest");
   _textTitle = FindChildComponent<Text>("m_textTitle");
   ```
4. 在 `OnCreate()` 里 `_btnTest.onClick.AddListener(() => Log.Info("click"));`
5. Play，点按钮看 Console

验证：会了"改 UI + 绑节点 + 加回调"。

### 练习 B：新窗口 + 事件 + 资源图标
1. `Assets/AssetRaw/UI/` 下复制一个 prefab 改名 `ShopUI`（根节点保留 Canvas/GraphicRaycaster）
2. 新文件 `GameLogic/UI/ShopUI/ShopUI.cs`：

```csharp
using TEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.UI, location: "ShopUI")]
    public class ShopUI : UIWindow
    {
        private Text _txtGold;
        private Image _imgIcon;

        protected override void ScriptGenerator()
        {
            _txtGold = FindChildComponent<Text>("m_textGold");
            _imgIcon = FindChildComponent<Image>("m_imgIcon");
        }

        protected override void RegisterEvent()
        {
            AddUIEvent<int>(IWalletEvent_Event.OnGoldChanged, OnGoldChanged);
        }

        protected override void OnRefresh()
        {
            _txtGold.text = WalletSystem.Instance.Gold.ToString();
            _imgIcon.SetSprite("item_icon_001");     // 图集/单图都走 SetSprite
        }

        private void OnGoldChanged(int gold) => _txtGold.text = gold.ToString();
    }
}
```

3. 在 `GameApp.StartGameLogic()` 里改成 `GameModule.UI.ShowUIAsync<ShopUI>();`
4. Play

验证：会了"新窗口 + 生命周期 + 事件 + Sprite"。

### 练习 C：业务系统（Singleton + 事件发送 + Timer）
```csharp
// GameLogic/System/WalletSystem.cs
using TEngine;

namespace GameLogic
{
    public class WalletSystem : Singleton<WalletSystem>, IUpdate
    {
        public int Gold { get; private set; }

        public void AddGold(int v)
        {
            Gold += v;
            GameEvent.Get<IWalletEvent>().OnGoldChanged(Gold);   // 通知所有 UI
        }

        public void OnUpdate() { /* 每帧逻辑，尽量少用 */ }
    }
}
```
- 触发：在某按钮回调里 `WalletSystem.Instance.AddGold(100);`
- 验证：`ShopUI` 里的金币文本自己变了 —— 这就是"UI 不互相引用，只认事件"

---

## 7. 坑 / 文档与代码不一致（已逐条核对源码）

| # | 现象 | 真相 | 依据 |
|---|------|------|------|
| 1 | 文档示例 `[Window(layer: UILayer.Top, fullScreen: true, hideTimeToClose: 10f)]` | **编译不过**。参数名是 `windowLayer`，`hideTimeToClose` 是 **int** | `WindowAttribute.cs:45` |
| 2 | 文档示例 `GameModule.Timer.AddTimer(OnTick, 3f)`，`OnTick()` 无参 | **编译不过**。回调类型是 `TimerHandler(object[] args)` | `TimerModule.cs:6`、`ITimerModule.cs:14` |
| 3 | 文档说 `OnUpdate` 不 override 就不会被调用 | `UIWindow` 的 `OnUpdate` **每帧都会调**（`UIModule.OnUpdate` 忽略返回值）；`UIWidget` 才会因为返回 false 被移出父级更新列表 | `UIModule.cs:712`、`UIWidget.cs:71` |
| 4 | 文档说 `UIWindow` 靠 `_hasOverrideUpdate` 控制 | 该标记只在**同一帧内**决定返回值，不影响 Window 是否被调用 | `UIBase.cs:179`、`UIWindow.cs:411` |
| 5 | README 说 Unity 2021.3.20f1c1 | 工程是 **2021.3.45f1** | `ProjectVersion.txt` |
| 6 | README 说顶部 EditorMode + Launcher 按钮 | 实际是工具栏右侧 `EditorPlayMode` 下拉；`GameEntry`/`UIRoot` 已内置在 `main.unity` | `EditorPlayMode.cs`、`main.unity` |
| 7 | 以为启动器 UI 也走 YooAsset | 启动器 UI 走 **`Resources.Load`**，prefab 在 `Assets/Launcher/Resources/UIWindow/`（`LoadUpdateUI`、`LoadTipsUI`）；只有业务 UI 才走 YooAsset。两套寻址别混 | `LauncherMgr.cs:13,42` |
| 8 | 文档说 `GameScripts/Main` 程序集 | **不存在**，`GameEntry.cs` + `Procedure/` 属于 `Assembly-CSharp` | 无 asmdef |
| 9 | 文档说 `GameApp_RegisterSystem.cs` | **不存在**，用 `partial class GameApp` 扩展 | `GameApp.cs` |
| 10 | 以为 `GameProto` 里有 `ConfigSystem` | 仓库里**没有**，要跑 `gen_code_bin_to_project.bat` 才会从 `CustomTemplate` 拷过来 | 文件缺失 |
| 11 | `ENABLE_HYBRIDCLR` 以为默认开 | 默认**没开**（只有 `ENABLE_LOG;TextMeshPro`），出包前需菜单 `HybridCLR/Define Symbols/Enable HybridCLR` | `ProjectSettings.asset:768` |
| 12 | UI Prefab 只放个 Image | 根节点没 `Canvas` 会在 `Handle_Completed` 里 **抛异常** | `UIWindow.cs:484` |
| 13 | README 说必须买 Odin（`Assets/Plugins/Sirenix`） | 已解耦：3 处引用都在 `#if ODIN_INSPECTOR` 内，未定义宏时不编译那几行 | `ResourceExtComponent.cs:8` 等 |

### 编码红线（养成习惯）
1. 异步一律 `UniTask`（`UniTaskVoid` 方法调用方 `.Forget()`），禁 `Coroutine`/`Task`
2. 模块只走 `GameModule.XXX`
3. `LoadAssetAsync` ↔ `UnloadAsset` 必须配对；GameObject 用 `LoadGameObjectAsync` + `Destroy`
4. `GameScripts/HotFix/` 才是热更区，别把业务写到主包
5. 模块间用 `GameEvent`，UI 内部用 `AddUIEvent`
6. 不用 `Resources.Load` / `FindObjectOfType` / `Update` 里 new 对象

---

## 8. 打包 / 热更（出包时才需要）

顺序（README + `Books/1-快速开始.md`）：

```
1. HybridCLR/Install...                         安装（升级 HybridCLR 后需重跑）
2. HybridCLR/Define Symbols/Enable HybridCLR    打开 ENABLE_HYBRIDCLR
3. HybridCLR/Generate/All                       必须执行，不可跳过
4. HybridCLR/Build/BuildAssets And CopyTo AssemblyPath   生成热更 dll → AssetRaw/DLL
5. YooAsset/AssetBundle Builder                  打 AB
6. Build Settings → Build And Run
```

相关源码：
- dll 生成/拷贝：`Assets/TEngine/Editor/HybridCLR/BuildDLLCommand.cs`
- 资源打包窗口：`Assets/TEngine/Editor/ReleaseTools/BuildPipelineWindow.cs`
- 热更地址/程序集名：`UpdateSetting`（菜单 `TEngine/Settings/TEngine UpdateSettings`）里的 `LogicMainDllName`、`HotUpdateAssemblies`、`AOTMetaAssemblies`
- 下载流程：`ProcedureInitResources` → `ProcedureCreateDownloader` → `ProcedureDownloadFile`

**AOT 泛型坑**：热更代码里首次用到 `List<自定义类型>`、`UniTask<自定义类型>` 等组合时若报 `ExecutionEngineException`，在 `HybridCLR/Generate/AOT Generic References` 补上后重新出包。

---

## 9. 速查表

```
入口        GameEntry.Awake → Procedure 链 → GameApp.Entrance(object[])
主战场      Assets/GameScripts/HotFix/GameLogic/
加 UI       AssetRaw/UI/Xxx.prefab(带 Canvas) + GameLogic/UI/Xxx/Xxx.cs([Window]) + ShowUIAsync<Xxx>()
模块        GameModule.Resource / UI / Timer / Scene / Audio / Fsm / Procedure / Localization / Debugger
系统        class XxxSystem : Singleton<XxxSystem>, IUpdate
事件        定义 [EventInterface] 接口 → GameEvent.Get<T>().OnXxx() → AddUIEvent<T>(T_Event.OnXxx, cb)
资源        SetSprite / LoadGameObjectAsync+Destroy / LoadAssetAsync+UnloadAsset
配置        ConfigSystem.Instance.Tables.TbXxx
日志        Log.Debug/Info/Warning/Error/Fatal
查文档      Books/*.md（给人看）  .claude/skills/tengine-dev/references/*.md（给 AI 看）
```

---

### 附：给 AI 协作的提示

仓库里有一套 AI 工作流（`UnityProject/CLAUDE.md` + `.claude/skills/tengine-dev/`）：
- 任务分 L1~L4 等级，L2 以上先查 `references/` 再写代码
- references 与代码冲突时**以代码为准**，并把冲突记录到 `.claude/memory/problem_YYYY-MM-DD.md`
- 建议让 AI 在改 UI/资源/事件前先读对应 reference，能显著减少 API 用错
