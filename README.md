# MajRadar

MajRadar is a Unity-free radar analyser and fitted-constant estimator for Simai
charts. It consumes MajSimai's typed `SimaiChart`, preserves chart-relative
timing, computes seven fixed raw features, applies the frozen regression model,
and maps the public radar axes.

## Runtime API

```csharp
var runtime = new RadarRuntime();
RadarResult result = runtime.Analyze(existingSimaiChart, cancellationToken);
```

Extended `K` Slides need gameplay geometry supplied by the host:

```csharp
var runtime = new RadarRuntime(new PlayExtendedSlideBarCountProvider());
```

The provider receives a normalized SlideCode such as `1P6K7`. Ordinary Slides,
feature parameters, regression coefficients, and score mapping are built in.

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
