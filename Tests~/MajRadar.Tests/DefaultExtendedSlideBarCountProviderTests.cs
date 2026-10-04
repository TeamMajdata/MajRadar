using System;
using System.Linq;
using System.Threading.Tasks;
using MajRadar.Core;
using MajRadar.MajSimaiAdapter;
using MajRadar.Runtime;
using Xunit;

namespace MajRadar.Tests;

public sealed class DefaultExtendedSlideBarCountProviderTests
{
    [Theory]
    [InlineData("1K3", 14)]
    [InlineData("1K5", 20)]
    [InlineData("1CK5", 20)]
    [InlineData("1B3K5", 22)]
    [InlineData("1P0K5", 21)]
    [InlineData("1Q0K5", 21)]
    [InlineData("1P6K7", 46)]
    [InlineData("1Q6K7", 35)]
    [InlineData("1P99K3", 111)]
    [InlineData("1Q99K3", 80)]
    [InlineData("1A3P9K5", 62)]
    [InlineData("1P69K7", 72)]
    [InlineData("1Q69K7", 35)]
    public void RepresentativeGeometryMatchesPlayCounts(string code, int expected)
    {
        Assert.Equal(expected, DefaultExtendedSlideBarCountProvider.Instance.ResolveBarCount(code));
    }

    [Theory]
    [InlineData("8Q69K4", 76, 77)]
    [InlineData("4P39K1", 22, 23)]
    [InlineData("8Q69K8", 44, 45)]
    [InlineData("1Q39K4", 22, 23)]
    public void AlignmentBoundaryCountsAllowPlatformRounding(string code, int minimum, int maximum)
    {
        // These specific SmoothAlign paths land within a few ULPs of a segment
        // boundary or endpoint. Math's native implementation varies by OS/CPU,
        // so the retained Play comparisons can include one extra arrow sample.
        // The two observed macOS/Ubuntu outcomes are allowed only for these cases;
        // ordinary geometry above still requires an exact count.
        Assert.InRange(DefaultExtendedSlideBarCountProvider.Instance.ResolveBarCount(code), minimum, maximum);
    }

    [Theory]
    [InlineData("1K5")]
    [InlineData("1P6K7")]
    [InlineData("1P69K7")]
    [InlineData("1A3P9K5")]
    public async Task RuntimeSupportsExtendedSlidesWithoutHostGeometry(string code)
    {
        var result = await new RadarRuntime().ParseAndAnalyzeAsync($"(120){{4}}{code}[4:1],2,E");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.NotNull(result.FittedConstant);
        var slide = Assert.Single(result.ChartInput!.Events, item => item.Kind == RadarEventKind.Slide);
        Assert.True(Assert.Single(slide.SlidePath!).BarCount > 0);
    }

    [Fact]
    public async Task HostProviderIsAuthoritative()
    {
        var provider = new RecordingProvider(1234);
        var result = await new RadarRuntime(provider).ParseAndAnalyzeAsync("(120){4}1K5[4:1],E");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var slide = Assert.Single(result.ChartInput!.Events, item => item.Kind == RadarEventKind.Slide);
        Assert.Equal(1234, Assert.Single(slide.SlidePath!).BarCount);
        Assert.Equal(1, provider.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task InvalidHostResultDoesNotSwitchToFallback(int value)
    {
        var provider = new RecordingProvider(value);
        var result = await new RadarRuntime(provider).ParseAndAnalyzeAsync("(120){4}1K5[4:1],E");

        Assert.False(result.IsSuccess);
        Assert.Contains("Non-positive arrow count", Assert.Single(result.Errors));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task HostFailureDoesNotSwitchToFallback()
    {
        var provider = new RecordingProvider(1) { Throw = true };
        var result = await new RadarRuntime(provider).ParseAndAnalyzeAsync("(120){4}1K5[4:1],E");

        Assert.False(result.IsSuccess);
        Assert.Contains("host geometry unavailable", Assert.Single(result.Errors));
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task StandardSlidesDoNotCallExtendedProvider()
    {
        var provider = new RecordingProvider(1) { Throw = true };
        var result = await new RadarRuntime(provider).ParseAndAnalyzeAsync("(120){4}1-5[4:1],E");

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task ConcurrentCallsDoNotShareMutableAlignmentState()
    {
        var provider = DefaultExtendedSlideBarCountProvider.Instance;
        // Compare parallel calls with this platform's sequential results, rather
        // than importing floating-point boundary decisions from another machine.
        var cases = new[] { "1P69K7", "1A3P9K5", "8Q69K4", "4P39K1", "8Q69K8", "1Q39K4" }
            .Select(code => (Code: code, Count: provider.ResolveBarCount(code))).ToArray();
        var runtime = new RadarRuntime();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(async index =>
        {
            var item = cases[index % cases.Length];
            var result = await runtime.ParseAndAnalyzeAsync($"(120){{4}}{item.Code}[4:1],2,E");
            Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
            var slide = Assert.Single(result.ChartInput!.Events, x => x.Kind == RadarEventKind.Slide);
            Assert.Equal(item.Count, Assert.Single(slide.SlidePath!).BarCount);
        }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1K")]
    [InlineData("0K5")]
    [InlineData("1K9")]
    [InlineData("1Z5K7")]
    [InlineData("1K1")]
    [InlineData("1C1K5")]
    [InlineData("1P0Q0K5")]
    [InlineData("1P09K5")]
    public void InvalidGeometryIsRejected(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            DefaultExtendedSlideBarCountProvider.Instance.ResolveBarCount(code!));
    }

    private sealed class RecordingProvider : IExtendedSlideBarCountProvider
    {
        private readonly int _value;
        public int Calls { get; private set; }
        public bool Throw { get; init; }
        public RecordingProvider(int value) => _value = value;

        public int ResolveBarCount(string slideCode)
        {
            Calls++;
            if (Throw) throw new InvalidOperationException("host geometry unavailable");
            return _value;
        }
    }
}
