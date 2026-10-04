using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    [MemberData(nameof(PlayReferenceCases))]
    public void MatchesUnmodifiedPlayArrowBuilder(string code, int expected)
    {
        Assert.Equal(expected, DefaultExtendedSlideBarCountProvider.Instance.ResolveBarCount(code));
    }

    public static IEnumerable<object[]> PlayReferenceCases()
    {
        var assembly = typeof(DefaultExtendedSlideBarCountProviderTests).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(".PlaySlideBarCounts.tsv", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
            var columns = line.Split('\t');
            yield return new object[] { columns[0], int.Parse(columns[1], CultureInfo.InvariantCulture) };
        }
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
        var cases = PlayReferenceCases().Where(row => ((string)row[0]).Contains("P69"))
            .Select(row => (Code: (string)row[0], Count: (int)row[1])).ToArray();
        Assert.NotEmpty(cases);
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
