# MajdataPlay integration

MajdataPlay should pin this repository as `Assets/Plugins/MajRadar` and keep its
existing `Assets/Plugins/MajSimai` pin. `MajRadar.asmdef` references that one
MajSimai assembly; the NuGet package is not installed into the Unity project.

Extended `K` Slides are resolved through `IExtendedSlideBarCountProvider`. The
sample provider calls Play's existing `SlideCodeParser` and `SlideDataBuilder`,
so MajRadar does not copy or depend on gameplay geometry.

The sample `ChartRadarService` is a static Play-only facade. It owns one
thread-safe `RadarRuntime` with the single supported provider, while each UI
caller continues to own cancellation, selection generation, and caching.
