# Fallback extended-Slide geometry

`new RadarRuntime()` and `new MajSimaiChartAdapter()` select
`DefaultExtendedSlideBarCountProvider.Instance` when the optional provider is
null. An explicitly supplied provider is authoritative, including its exceptions
and non-positive results. Standard Slides continue using the existing lookup
table and never call the extended-Slide provider.

## Source and maintenance

The geometry snapshot is adapted from TeamMajdata/MajdataPlay commit
`acd295d3f878f53ecd412d150936a465ed2e3d28` (GPL-3.0):

https://github.com/TeamMajdata/MajdataPlay/tree/acd295d3f878f53ecd412d150936a465ed2e3d28/Assets/Scripts/Scenes/Game/Misc/Parsing

`Runtime/MajSimaiAdapter/FallbackGeometry/` retains the pure `System.Numerics`
path parser, circles, path constructor and segments. Namespaces and top-level
visibility are changed to keep these implementation types internal. No Unity
types, assets, rendering or judgement-area lookup are needed.

The provider follows `SlideDataBuilder.BuildArrowData(path).Length - 2` but only
counts placements: it does not allocate the arrow coordinate array. It preserves
the exact segment transition order, floating-point operations, default spacing,
`SmoothAlign` and `ForceAlign` rules. A total-length/divisor approximation would
change counts. Each call owns its path because alignment mutates segment spacing.
Invalid or non-advancing geometry throws and is converted into the existing
structured adaptation error at the public analysis boundary.

The provider is synchronous, like the host-provider interface. Cancellation does
not interrupt a call midway; hosts should bound input sizes and analysis
concurrency. The fallback adds no process-wide result cache.

Persisted analysis cache keys should include the MajRadar and MajSimai package
versions plus `DefaultExtendedSlideBarCountProvider.GeometryVersion`. When
updating this snapshot, change the geometry version and regenerate the reference
fixtures after reviewing the upstream change. An injected host provider should
use its own geometry version instead.

## Independent reference tests

`Tests~/MajRadar.Tests/Fixtures/PlaySlideBarCounts.tsv` contains 5,757 positive
counts generated using the unmodified Play parser and arrow builder at the
commit above. Cases cover all eight starting/ending positions, A/B/C nodes,
clockwise/counterclockwise orbits, repeated circles, tangent transfers and
transitions to/from the outer circle. They exercise both alignment markers.

Run `dotnet test MajRadar.slnx` to compare the fallback against every reference
count and check default selection, host precedence, host failures and concurrent
path ownership. The ordinary-chart regression tests run alongside them.

To regenerate with the pinned Play sources, run the separate reference tool:

```sh
dotnet run --project Tests~/PlayGeometryReference/PlayGeometryReference.csproj \
  -p:PlayParsingSource=/absolute/path/to/MajdataPlay/Assets/Scripts/Scenes/Game/Misc/Parsing \
  -- Tests~/MajRadar.Tests/Fixtures/PlaySlideBarCounts.tsv
```

The tool checks source hashes and compiles the original Play files. It has no
reference to MajRadar, keeping the oracle independent of the implementation
under test. Updating to another upstream snapshot requires reviewing and
updating those hashes as well as `GeometryVersion`.

MajSimai 2.2.3 is required for end-to-end parsing: version 2.2.2 misclassifies
some extended paths containing A/B/C, even when the geometry provider is correct.
CI checks out the `2.2.3` release tag (`refs/tags/2.2.3`), aligned with the
NuGet dependency in `MajRadar.csproj`. This tag includes the same parser fix.
