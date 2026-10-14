# MajRadar

[算法细节](#Algorithm)

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

<a id="Algorithm"></a>

## 特征计算方法

下面是每个维度对应的计算方式以及映射方式。

### 物量加权

Note 和 Peak 共用物量的加权逻辑

- Tap：`1`
- 长度不超过半拍的 Hold：`1`
- 长 Hold：`2`
- Slide：SlidePrefab的BarCount /64，向上取整。

- Touch：同拍相邻 Touch 合并为一个连通分量；相差不超过 {16}的相邻 Touch 也可以跨时间合并。

无头 Slide 会保留并参与 Note、Peak，但不算作SlideGroup。

### Note

将谱面按固定1.5 s划分窗口，空谱返回 `0`。每个窗口的密度为：`density_i = workload_i / 1.5`

设所有窗口密度的均值为 `μ`，总体标准差为 `σ`：

```text
NoteRaw = μ × (1 + 0.3 × σ / μ)
        = μ + 0.3σ
```

### Peak

以 `0.5 s` 为步长扫描候选位置。对候选起点 `t`，计算三个相邻 `1.5 s` 窗口的加权密度：

`Peak(t) = 0.2D_{t-1} + 0.6D_{t} + 0.2D_{t+1}`

候选按强度排序，最多选择三个相距至少 `4.5 s` 的峰值：

```text
PeakRaw = 0.5 × P1 + 0.3 × P2 + 0.2 × P3
```

Peak中连续单键扫键进行对数衰减，对第四个及之后的扫键权重乘上`1 / log2(n-2)`，避免简单连续扫键无限抬高 Peak。此外Touch 组权重降为0.5。

### Sweep

Sweep 分为四个阶段：

1. 识别单手或双手 Sweep 主干；
2. 将相邻 Sweep 连接为 family；
3. 用DP寻找平均位移最低的双手动作分配；
4. 聚合最强的多个peak window。

识别规则包括：

- 每拍最多三个外键；
- 普通单位移动间隔不超过{12}；
- 允许连续八分，以及减速到八分；
- 长 Hold 可以连接跨两个键位的移动，避免锁手圈被打断；
- 支持单键、双键、宽度变化、换手和折返；

每个 Sweep batch 的基础负荷为：

```text
noteWeight = normalCount + 0.3 × exCount

speedFactor = sqrt((1 / 12) / unitIntervalSeconds)

base = noteWeight × speedFactor
```

连续单手简单 Sweep 在第 16 个 attack 后开始衰减：

```text
decay = excessRank^(-1/2)
```

双手动态规划按以下优先级寻找动作分配：

1. 最少过快跳跃；
2. 最少空闲手接管；
3. 最短总环形键位距离；
4. 最短主动移动距离。

将不在处理键的手定义为空闲，计算它处理接下来的note之前需要走的距离。空闲手移动压力为：

```text
idlePressure =
    distance × sqrt(max(1, distance / (elapsedSeconds × 10)))
```

动作负荷为：

```text
motion =
    0.5 × idlePressure
  + 1.0 × freeHandTakeover
  + 2.0 × fastJumpViolation
```

重复规律动作的 motion 乘以 `0.1`。同方向连接和同方向换手分别提供局部 `0.2` bonus。

最终选择top3不重合2 s窗口：

```
windowValue = baseDensity + motionDensity

weight_k = k^(-1/2)
```

最终结果：

```text
SweepRaw =
    Σ(weight_k × windowValue_k)
    / Σweight_k
```

### SlideTricky

SlideTricky 衡量 Slide头打下到启动，星星运动之间中插入其他操作所造成的错位压力，为了避免放手星星处理配置造成的异常情况，星星运动的计算时间取min(星星运动时间，一拍四分音符）

对物件计算的主要加权规则：

- 与 Slide 目标同位置的按键×1.5
- 启动拍上的总物量x0.5，最高2.
- Touch 组权重1.5，一次错位最多只算两组
- 一次错位最多计算16个物件

普通按键的速度因子由中位时间间隔计算：

```text
ratio = (30 / medianGapSeconds) / 180
```

```text
speed =
    sqrt(ratio)       , ratio < 1
    min(1.5, ratio)   , ratio >= 1
```

多Slide增益为：

```text
concurrency = Σsqrt(pathCount)

multiplier =
    1 + 0.15 × max(0, concurrency - 1)
```

每次错位的负荷为：

```text
clusterLoad =
    (internalLoad + launchLoad)
    × objectCapFactor
    × multiplier
```

最终取负荷最高Top5，做对数衰减：

```text
weight_k = 1 / log2(k + 1)
```

```text
SlideTrickyRaw =
    Σ(weight_k × clusterLoad_k)
    / Σweight_k
```

### SlideSequence

SlideSequence 衡量连续星星阵列的节奏、长度和是否存在双押。slide头之间间距<=四分音即可合并为一组。

序列要求相邻有效节点至少间隔八分，避免扫星星的巨大加成，并通过动态规划依次优先选择：

1. 更长的序列；
2. 更多直接连接；
3. 更高速度；
4. 更高并发。

节奏项：

```text
cadence =
    average(0.5 / intervalSeconds)
```

并发项：

```text
concurrency =
    average(max(0, Σsqrt(pathCount) - 1))
```

section 强度：

```text
sectionIntensity =
    cadence × lengthFactor(sequenceLength)
  + 0.5 × concurrency
```

长度在短序列阶段线性增长，超过 16 个节点后转为平方根增长，避免超长序列无限线性放大。

最终取top5连接段落：

```text
weight_k = 1 / log2(k + 1)
```

```text
SlideSequenceRaw =
    Σ(weight_k × sectionIntensity_k)
    / Σweight_k
```

### Jack

Jack 在八个外键上分别寻找纵连。

序列允许一次出现4个偏移纵连位置的tap（类AMABIE配置，纵连轴交互）；偏移之前必须有两个及以上的同键纵连才会将他们前后组合为纵连。
基础强度为：

```text
strength =
    normalAnchors //纵连物量
  + 0.3 × exAnchors 
  + 1.5 × (
        normalInterruptions //外部飞键
      + 0.3 × exInterruptions
    )
```
根据首尾键计算等效十六分 BPM，以180bpm为基准：

```text
bpm =
    15 × (anchorTimeCount - 1)
    / spanSeconds

speedFactor = (bpm / 180)^1.5
```

单个序列的速度加权强度：

```text
weightedStrength = strength × speedFactor
```

最终取Top5：

```text
JackRaw =
    Σ(
        weightedStrength_k
        × 1.3
        × 0.645^(k - 1)
    )
```

### SlideCumulate

SlideCumulate 衡量连续 Slide 阵列中逐步累积的错位压力。只参与拟合不在对外雷达中展示。

设错位内部累计物件数为 `n`，内部总负荷为：

```text
internalTotal(n) =
    n + 0.35 × log2(n!)
```

每个 section 的强度为：

```text
sectionIntensity =
    0.8 × mean(onsetLoad)
  + 0.2 × RMS(onsetLoad)
```

有效长度函数为：

```text
effectiveLength(n) = n
```

当 `n <= 5`；超过 5 后使用对数压缩：

```text
effectiveLength(n) =
    5 × (
        1 + log_5(
            1 + (n - 5) / 5
        )
    )
```

最终结果按整谱时长归一化：

```text
SlideCumulateRaw =
    Σ(
        effectiveLength(sectionLength)
        × sectionIntensity
    )
    / (chartDurationSeconds / 0.5)
```

### Fitted Constant

七个维度按以下固定顺序输入模型：

```text
note
peak
sweep
slide_tricky
slide_sequence
jack
slide_cumulate
```
使用 center 和 scale 标准化：`z_i = (x_i - center_i) / scale_i`

随后进行二次回归：

```
FittedConstant =
    intercept
  + Σ(a_i × z_i)
  + Σ(b_ij × z_i × z_j) ,
0 <= i <= j < 7
```

参数作为常量hardcode。


### Raw 到 Score 的映射

七个 raw 维度各自具有固定的四个 raw 锚点，使用线性插值，在官谱分布上拟合了整个分布，每维度最高记作 `T4Maximum` 

```text
T1 → 50
T2 → 100
T3 → 150
T4 → 200

score(x) =
    S_i
  + (S_(i+1) - S_i)
    × (x - T_i)
    / (T_(i+1) - T_i)
```

从 `T4` 到该维度的 `T4Maximum` 保持 200 分平台：

```text
T4 < x <= T4Maximum
→ score = 200
```

为了兼容自制的难度，允许超越官谱的最高数值，留有200～250的headroom。超过 `T4Maximum` 后使用log渐进。在最初采用同样斜率避免跳变，然后对数衰减渐进到250。
```text
score(x) =
    200
  + headroom × (
        1 - exp(
            -initialSlope
            × (x - T4Maximum)
            / headroom
        )
    )
```
