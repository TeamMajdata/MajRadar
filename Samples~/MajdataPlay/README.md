# MajdataPlay integration

Copy `PlayExtendedSlideBarCountProvider.cs` into the Play assembly and construct
one reusable runtime instance:

```csharp
private readonly RadarRuntime _radar =
    new(new PlayExtendedSlideBarCountProvider());
```

Pass Play's existing `SimaiChart` to `_radar.Analyze(chart, token)`. Play remains
responsible for selection caching, cancellation ownership, logging, and UI.
