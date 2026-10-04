# MajRadar

[中文](#中文) | [English](#english)

<a id="中文"></a>

## 中文

MajRadar 是一个独立的的Simai谱面雷达分析和拟合定数估计组件。通过分析`SimaiChart`格式的已解析谱面计算固定七维特征产生雷达轴和拟合定数。

### 调用

推荐直接复制 `Samples~/MajdataPlay` 中的静态薄调用层。

```csharp
ChartRadarSnapshot snapshot = await ChartRadarService.AnalyzeAsync(
    simaiChart,
    cancellationToken);
```

静态 facade 只持有无单谱状态的 Runtime；显示当前谱面的组件仍负责 token、selection
generation 和缓存。sample 返回 `ChartRadarSnapshot`

固定维度可以通过 enum 直接读取，同时保留原有字符串字典接口：

```csharp
double? noteScore = snapshot.GetScore(RadarOutputDimension.Note);
double? trickyRaw = snapshot.GetRawValue(RadarOutputDimension.SlideTricky);

// 动态 UI 和现有代码仍可直接使用字符串 key。
double? sameNoteScore = snapshot.Scores["note"];
```

`RadarOutputDimension` 只包含公开的六个雷达轴和 `FittedConstant`，不包含仅用于拟合的
`slide_cumulate`。拟合定数仍优先从 `snapshot.FittedConstant` 读取。

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

只有一段 inote 文本时，可以使用：

```csharp
RadarResult result = await _runtime.ParseAndAnalyzeAsync(
    inote,
    cancellationToken);
```

### Slidecode 依赖

未注入 provider 时，MajRadar 使用内置的 `DefaultExtendedSlideBarCountProvider`，
它来自固定版本的 Play 纯 .NET 几何实现，后端无需安装 Unity。
Play 仍应显式注入自己的 provider，以游戏当前使用的几何为准：

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
provider只接受SlideCode，例如 `1P6K7`，并且必须返回正数 arrow/bar count。
provider 抛出的异常或非正结果会变成结构化适配失败，不会再次尝试备用实现。
只有构造时未提供 provider（或传入 `null`）才选择内置实现；普通 Slide 不受影响。
备用实现无共享的单谱状态，可并发调用。数据库缓存版本应包含
`DefaultExtendedSlideBarCountProvider.GeometryVersion`；宿主 provider 使用自己的版本标识。
几何来源和对照测试见 [备用几何说明](Documentation~/FallbackGeometry.md)。

NuGet 依赖最低 MajSimai 2.2.3，其中修复了 `1A3P9K5` 等带 A/B/C 路径的
Slidecode 被误识别为 Touch 的问题。

### 取消与选歌状态

取消采用合作式机制，并作为结果数据返回：

```csharp
if (result.IsCancelled)
    return;
```

- `AnalyzeAsync`会在下一个检查点返回
  `Status == "cancelled"` 的 `RadarResult`。
- MajSimai 没有提供解析中途取消，因此 `ParseAndAnalyzeAsync` 只能parser 返回后立即再次检查。
- 同步provider应调用中途不会被取消。

应为当前选歌持有一个 `CancellationTokenSource`。歌曲或难度变化时，取消上一份
token、启动新分析，并丢弃过期的结果。

### 结果处理方式

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

- `Analysis.Features` 包含模型使用的固定七维，包括内部维度 `slide_cumulate`。
- `RawValues` 和 `Scores` 始终保持公开形状：六个雷达轴加
  `fitted_constant`；不可用项为 `null`。
- 只有七维 raw 全部成功时才生成 `FittedConstant` 和映射后的 score。
- 拟合输出在模块内部限位；上下限由 `RegressionBetaModel` 的
  `MinimumFittedConstant` / `MaximumFittedConstant` 常量配置，当前为 0～18。
- `partial` 会保留已完成的特征，但不会生成拟合定数。


### .NET 与 NuGet

```sh
dotnet test MajRadar.slnx
dotnet pack MajRadar.csproj -c Release
```

NuGet包以 `netstandard2.1` 为目标，包含MajSimai包依赖。


---

<a id="english"></a>

## English

MajRadar is a Unity-free radar analyser and fitted-constant estimator for Simai
charts. It consumes MajSimai's typed `SimaiChart`, preserves chart-relative
timing, computes seven fixed raw features, applies the frozen regression model,
and maps the public radar axes.

### Calling the runtime

MajdataPlay should copy the static integration facade from
`Samples~/MajdataPlay`. The sample wires the single
`PlayExtendedSlideBarCountProvider` directly into one long-lived `RadarRuntime`;
callers do not hold a service instance and do not need factories, options, or
per-request dependency selection:

```csharp
ChartRadarSnapshot snapshot = await ChartRadarService.AnalyzeAsync(
    simaiChart,
    cancellationToken);
```

The static facade retains only the Runtime, which has no per-chart state. The
component that owns the current chart still owns its token, selection generation,
and cache. The sample returns a lightweight snapshot so UI caches do not retain
the full adapted event list and seven-dimension analysis graph.

Known dimensions can be read through the enum while the existing string-keyed
dictionaries remain available:

```csharp
double? noteScore = snapshot.GetScore(RadarOutputDimension.Note);
double? trickyRaw = snapshot.GetRawValue(RadarOutputDimension.SlideTricky);

// Existing and dynamic UI code can keep using literal keys.
double? sameNoteScore = snapshot.Scores["note"];
```

`RadarOutputDimension` contains the six public radar axes and
`FittedConstant`; it intentionally excludes the internal regression-only
`slide_cumulate`. Prefer `snapshot.FittedConstant` when reading the fitted value.

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

When no provider is supplied, extended `K` Slides use the built-in
`DefaultExtendedSlideBarCountProvider`, a pinned, Unity-free copy of Play's
geometry. Play should still inject its own provider so its current gameplay
geometry remains authoritative:

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
structured adaptation failure; they never trigger a retry through the fallback.
The fallback is selected only when the constructor receives no provider or
`null`. It creates a fresh path per call and can be shared concurrently.
Include `DefaultExtendedSlideBarCountProvider.GeometryVersion` in persistent
cache identities, or your host provider's own version when overriding it.
See [fallback geometry](Documentation~/FallbackGeometry.md) for provenance and
the focused geometry and provider regression tests.

NuGet requires MajSimai 2.2.3 or newer. That release fixes Slidecodes containing
A/B/C path commands, such as `1A3P9K5`, being misclassified as Touch notes.

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
- Predictions are clamped inside the module using `MinimumFittedConstant` and
  `MaximumFittedConstant` in `RegressionBetaModel` (currently 0 and 18).
- `partial` preserves completed feature results but does not produce a fitted
  constant.
- `fitted_constant` is identity-mapped and is not on the radar axes' 0-250
  scale.

### MajdataPlay source integration

MajdataPlay should pin MajRadar as a Git submodule beside its existing MajSimai
submodule. Unity compiles `Runtime/` through `MajRadar.asmdef`, which references
the single existing `MajSimai` assembly. Do not install the MajRadar NuGet package
into the same Unity project.

MajRadar is code-only and ignores Unity-generated `*.meta` files inside the
submodule. The Play repository should still commit the
`Assets/Plugins/MajRadar.meta` file for the submodule directory itself. Revisit
this policy if the package later adds prefabs, ScriptableObjects, or other assets
that require stable GUIDs.

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
