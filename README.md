# MajRadar

[中文](#中文) | [English](#english)

<a id="中文"></a>

## 中文

MajRadar 是一个不依赖 Unity 的 Simai 谱面雷达分析和拟合定数估计组件。
它接收 MajSimai 输出的类型化 `SimaiChart`，保留谱面相对时间，计算固定七维
raw 特征，运行冻结的回归模型，并映射对外展示的雷达轴。

### Runtime 生命周期

建议由宿主的雷达服务创建一个 `RadarRuntime`，并在应用生命周期内跨选歌复用。
`RadarRuntime` 不缓存谱面或结果，也不保留单次分析状态。它是普通对象而不是全局
静态单例，便于测试和其他宿主注入不同的扩展 Slide 实现。

```csharp
private readonly RadarRuntime _runtime =
    new(new PlayExtendedSlideBarCountProvider());
```

满足以下条件时，同一 Runtime 可以并发分析多张谱面：

- 注入的 `IExtendedSlideBarCountProvider` 无状态且线程安全；
- 分析期间调用方不修改传入的 `SimaiChart` 或 `RadarChartInput`。

`AnalyzeAsync` 在线程池运行同步分析。与它一起使用的 provider 不能访问仅限 Unity
主线程的对象。Play provider 每次创建局部 Slide 路径，并且只调用纯几何计算，符合此约束。

### 调用 Runtime

已有 MajSimai 解析结果时，优先复用现成的谱面：

```csharp
RadarResult result = runtime.Analyze(existingSimaiChart, cancellationToken);
```

需要保持 Unity 主线程响应时，使用异步包装：

```csharp
RadarResult result = await _runtime.AnalyzeAsync(
    existingSimaiChart,
    cancellationToken);
```

独立调用方只有一段 inote 文本时，可以使用：

```csharp
RadarResult result = await _runtime.ParseAndAnalyzeAsync(
    inote,
    cancellationToken);
```

`ParseAndAnalyzeAsync` 只是便利边界，不是歌曲加载器。文件、歌曲 metadata、谱面类型、
曲绘和音频 offset 仍由调用方管理。

### Extended Slide 依赖

扩展 `K` Slide 的游戏几何由宿主提供。MajRadar 只保留一个窄接口，不引用
MajdataPlay：

```csharp
internal sealed class PlayExtendedSlideBarCountProvider
    : IExtendedSlideBarCountProvider
{
    public int ResolveBarCount(string slideCode)
    {
        var path = SlideCodeParser.Parse(slideCode);
        return SlideDataBuilder.BuildArrowData(path).Length - 2;
    }
}
```

provider 接收规范化后的 SlideCode，例如 `1P6K7`，并且必须返回正数 arrow/bar count。
provider 抛出的异常或非正结果会变成结构化适配失败。未提供 provider 时，普通谱面仍可
正常分析；遇到 `K` Slide 会返回错误，而不是猜测几何。

普通 Slide、特征参数、回归系数和 score 映射均内置，不参与依赖注入。

### 取消与选歌状态

取消采用合作式机制，并作为结果数据返回：

```csharp
if (result.IsCancelled)
    return;
```

- `Analyze` 在适配过程、各特征维度之间，以及高复杂度 Sweep 的候选、family、手部
  动态规划和选择循环中检查 token。
- 其他维度在特征或有界 section 边界检查。
- `AnalyzeAsync` 不会强制终止工作线程；它会在下一个检查点返回
  `Status == "cancelled"` 的 `RadarResult`。
- MajSimai 没有提供解析中途取消，因此 `ParseAndAnalyzeAsync` 会在解析前检查一次，
  并在 parser 返回后立即再次检查。
- 同步 provider 应保持短小且有界，调用中途不会被取消。

宿主应为当前选歌持有一个 `CancellationTokenSource`。歌曲或难度变化时，取消上一份
token、启动新分析，并丢弃 selection generation 已经过期的结果。仅取消还不够，因为旧任务
可能恰好在新选歌后完成。

### 结果契约

```csharp
if (result.IsSuccess)
{
    foreach (var dimension in result.DimensionOrder)
        Render(dimension, result.Scores[dimension]);
}
else if (!result.IsCancelled)
{
    LogErrors(result.Errors);
}
```

- `Analysis.Features` 包含模型使用的固定七维 raw，包括内部维度 `slide_cumulate`。
- `RawValues` 和 `Scores` 始终保持固定公开形状：六个雷达轴加
  `fitted_constant`；不可用项为 `null`。
- 只有七维 raw 全部成功时才生成 `FittedConstant` 和映射后的 score。
- `partial` 会保留已完成的特征，但不会生成拟合定数。
- `fitted_constant` 使用恒等映射，不属于雷达轴的 0–250 标度。

### MajdataPlay 源码接入

MajdataPlay 应在现有 MajSimai submodule 旁固定 MajRadar submodule。Unity 通过
`MajRadar.asmdef` 编译 `Runtime/`，该程序集引用项目内唯一的 `MajSimai` 程序集。
同一个 Unity 项目中不要再安装 MajRadar NuGet 包。

### .NET 与 NuGet

```sh
dotnet test MajRadar.slnx
dotnet pack MajRadar.csproj -c Release
```

NuGet 包以 `netstandard2.1` 为目标，并声明一个 MajSimai 包依赖。Pull Request 会执行
构建与测试；main 分支 push 发布唯一的 CI 预发行版本，`v*` tag 发布正式版本。

兼容性测试可以直接引用与 Play pin 一致的 MajSimai 源码项目：

```sh
dotnet test MajRadar.slnx \
  -p:MajSimaiProject=/absolute/path/to/MajSimai.csproj
```

歌曲发现、metadata、音频 offset、visualizer、训练和实验管线均明确留在 Runtime 组件之外。

---

<a id="english"></a>

## English

MajRadar is a Unity-free radar analyser and fitted-constant estimator for Simai
charts. It consumes MajSimai's typed `SimaiChart`, preserves chart-relative
timing, computes seven fixed raw features, applies the frozen regression model,
and maps the public radar axes.

### Runtime lifetime

Create one `RadarRuntime` for the lifetime of the host radar service and reuse
it across chart selections. `RadarRuntime` does not cache charts or results and
does not retain per-analysis state. It is intentionally a normal object rather
than a global singleton so tests and other hosts can supply different extended
Slide implementations.

```csharp
private readonly RadarRuntime _runtime =
    new(new PlayExtendedSlideBarCountProvider());
```

The same runtime may analyze multiple charts concurrently when both conditions
below hold:

- the injected `IExtendedSlideBarCountProvider` is stateless and thread-safe;
- callers do not mutate a supplied `SimaiChart` or `RadarChartInput` while it is
  being analyzed.

`AnalyzeAsync` executes the synchronous analysis on the thread pool. A provider
used with it must not access Unity main-thread-only objects. Play's provider is
suitable because it creates a local Slide path and uses only pure geometry.

### Calling the runtime

Reuse an existing chart parsed by MajSimai whenever possible:

```csharp
RadarResult result = runtime.Analyze(existingSimaiChart, cancellationToken);
```

Use the asynchronous wrapper when the caller must keep the Unity main thread
responsive:

```csharp
RadarResult result = await _runtime.AnalyzeAsync(
    existingSimaiChart,
    cancellationToken);
```

Standalone callers that only have one inote string may use:

```csharp
RadarResult result = await _runtime.ParseAndAnalyzeAsync(
    inote,
    cancellationToken);
```

`ParseAndAnalyzeAsync` is a convenience boundary, not a song loader. The caller
continues to own files, song metadata, chart type, artwork, and audio offsets.

### Extended Slide dependency

Extended `K` Slides need gameplay geometry supplied by the host. MajRadar keeps
the dependency narrow and does not reference MajdataPlay:

```csharp
internal sealed class PlayExtendedSlideBarCountProvider
    : IExtendedSlideBarCountProvider
{
    public int ResolveBarCount(string slideCode)
    {
        var path = SlideCodeParser.Parse(slideCode);
        return SlideDataBuilder.BuildArrowData(path).Length - 2;
    }
}
```

The provider receives a normalized SlideCode such as `1P6K7`. It must return a
positive arrow/bar count. Provider exceptions and non-positive results become a
structured adaptation failure. Without a provider, ordinary charts still work,
while a chart containing a `K` Slide returns an error instead of guessing.

Ordinary Slides, feature parameters, regression coefficients, and score mapping
are built in and are not dependency-injected.

### Cancellation and selection ownership

Cancellation is cooperative and returned as data:

```csharp
if (result.IsCancelled)
    return;
```

- `Analyze` checks the token during adaptation, between feature dimensions, and
  throughout the high-complexity Sweep candidate, family, hand-motion, and
  selection loops.
- Other dimensions check at their feature or bounded section boundaries.
- `AnalyzeAsync` does not forcibly abort its worker thread; it returns a
  `RadarResult` with `Status == "cancelled"` after the next observation point.
- MajSimai does not expose cancellation for an in-progress parse. Therefore
  `ParseAndAnalyzeAsync` checks immediately before parsing and again when the
  parser returns.
- The synchronous provider method is expected to be short and bounded; it is not
  interrupted in the middle of a call.

The host owns one `CancellationTokenSource` per current selection. On a song or
difficulty change, cancel the previous source, start a new analysis, and discard
any result whose selection generation is no longer current. Cancellation alone
does not replace the generation check because a completed older task may race a
new selection.

### Result contract

```csharp
if (result.IsSuccess)
{
    foreach (var dimension in result.DimensionOrder)
        Render(dimension, result.Scores[dimension]);
}
else if (!result.IsCancelled)
{
    LogErrors(result.Errors);
}
```

- `Analysis.Features` contains all seven fixed model inputs, including internal
  `slide_cumulate`.
- `RawValues` and `Scores` always use the shape-stable public order: six radar
  axes plus `fitted_constant`; unavailable entries are `null`.
- `FittedConstant` and mapped scores are produced only when all seven raw
  features succeed.
- `partial` preserves completed feature results but does not produce a fitted
  constant.
- `fitted_constant` is identity-mapped and is not on the radar axes' 0-250
  scale.

### MajdataPlay source integration

MajdataPlay should pin MajRadar as a Git submodule beside its existing MajSimai
submodule. Unity compiles `Runtime/` through `MajRadar.asmdef`, which references
the single existing `MajSimai` assembly. Do not install the MajRadar NuGet package
into the same Unity project.

### .NET and NuGet

```sh
dotnet test MajRadar.slnx
dotnet pack MajRadar.csproj -c Release
```

The NuGet package targets `netstandard2.1` and declares one MajSimai package
dependency. Pull requests build and test; main-branch pushes publish unique CI
prereleases, while `v*` tags publish stable versions.

Compatibility tests can compile against a source checkout matching Play's pin:

```sh
dotnet test MajRadar.slnx \
  -p:MajSimaiProject=/absolute/path/to/MajSimai.csproj
```

Song discovery, metadata, audio offsets, visualizers, training, and experimental
pipelines intentionally remain outside this runtime component.
