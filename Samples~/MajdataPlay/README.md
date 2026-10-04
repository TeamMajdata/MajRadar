# MajdataPlay integration

Copy both C# files into `Assets/Scripts/Utils/ChartRadar/`. The static facade
wires the single Play geometry implementation directly into one reusable
`RadarRuntime`; callers do not hold a service instance:

```csharp
var snapshot = await ChartRadarService.AnalyzeAsync(chart, cancellationToken);

var noteScore = snapshot.GetScore(RadarOutputDimension.Note);
var trickyRaw = snapshot.GetRawValue(RadarOutputDimension.SlideTricky);
```

Play remains responsible for selection generation, cancellation ownership,
logging, UI updates, and caching the lightweight `ChartRadarSnapshot`.

The explicitly injected Play provider takes precedence over MajRadar's built-in
fallback. A host without Play geometry can use `new RadarRuntime()` directly;
the fallback is selected only when no provider is supplied. Exceptions or
non-positive results from an injected provider remain structured failures.

Existing string access remains available for dynamic UI code:

```csharp
var noteScore = snapshot.Scores["note"];
```
