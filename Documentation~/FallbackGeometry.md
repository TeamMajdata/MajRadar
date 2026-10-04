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
updating this snapshot, change the geometry version and review the geometry
regressions against the upstream change. An injected host provider should
use its own geometry version instead.

## Focused regression tests

`DefaultExtendedSlideBarCountProviderTests` keeps 13 representative Play counts
covering A/B/C nodes, clockwise/counterclockwise orbits, repeated circles,
tangent transfers, and both alignment markers. Default selection, explicit
provider precedence, provider failures, malformed geometry and concurrent path
ownership are tested separately. Run them with `dotnet test MajRadar.slnx`.

The previous 5,757-row snapshot and its generator were removed: the broad grid
duplicated the same path rules and encoded macOS floating-point boundary choices
as universal integer answers. .NET trigonometric operations use the native C
runtime and may differ across operating systems and architectures:

https://learn.microsoft.com/en-us/dotnet/api/system.math.sin#remarks

Four SmoothAlign paths (`8Q69K4`, `4P39K1`, `8Q69K8`, `1Q39K4`) end within a few
ULPs of a segment boundary or endpoint. Play's retained strict comparisons may
therefore include one additional arrow sample. Their tests explicitly allow
the two observed macOS/Ubuntu counts; other representative cases still require
exact values. Concurrency tests compare with sequential results from the same
process. The runtime algorithm and `GeometryVersion` are unchanged.

The fallback follows the pinned Play algorithm on the running platform; it does
not promise bit-identical arrow counts across all platforms. Making those counts
platform-independent would require a shared numerical policy in Play and MajRadar,
not a test-only change.

MajSimai 2.2.3 is required for end-to-end parsing: version 2.2.2 misclassifies
some extended paths containing A/B/C, even when the geometry provider is correct.
CI checks out the `2.2.3` release tag (`refs/tags/2.2.3`), aligned with the
NuGet dependency in `MajRadar.csproj`. This tag includes the same parser fix.
