# MajdataPlay integration

Copy both C# files into `Assets/Scripts/Utils/ChartRadar/`. The service wires the
single Play geometry implementation directly into one reusable `RadarRuntime`.
Callers only hold the service:

```csharp
private readonly ChartRadarService _chartRadarService = new();

var snapshot = await _chartRadarService.AnalyzeAsync(chart, cancellationToken);
```

Play remains responsible for selection generation, cancellation ownership,
logging, UI updates, and caching the lightweight `ChartRadarSnapshot`.
