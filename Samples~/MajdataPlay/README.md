# MajdataPlay integration

Copy both C# files into `Assets/Scripts/Utils/ChartRadar/`. The static facade
wires the single Play geometry implementation directly into one reusable
`RadarRuntime`; callers do not hold a service instance:

```csharp
var snapshot = await ChartRadarService.AnalyzeAsync(chart, cancellationToken);
```

Play remains responsible for selection generation, cancellation ownership,
logging, UI updates, and caching the lightweight `ChartRadarSnapshot`.
