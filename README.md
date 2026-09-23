# MajRadar

MajRadar is a Unity-free radar analyser and fitted-constant estimator for Simai
charts. It consumes MajSimai's typed `SimaiChart`, preserves chart-relative
timing, computes seven fixed raw features, applies the frozen regression model,
and maps the public radar axes.

## Runtime lifetime

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

## Calling the runtime

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

## Extended Slide dependency

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

## Cancellation and selection ownership

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

## Result contract

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

## MajdataPlay source integration

MajdataPlay should pin MajRadar as a Git submodule beside its existing MajSimai
submodule. Unity compiles `Runtime/` through `MajRadar.asmdef`, which references
the single existing `MajSimai` assembly. Do not install the MajRadar NuGet package
into the same Unity project.

## .NET and NuGet

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
