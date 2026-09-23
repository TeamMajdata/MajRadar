using MajRadar.MajSimaiAdapter;
using Xunit;

namespace MajdataPlay.Utils.ChartRadar
{
    internal sealed class PlayExtendedSlideBarCountProvider : IExtendedSlideBarCountProvider
    {
        public int ResolveBarCount(string slideCode) => 22;
    }
}

namespace MajRadar.Tests
{
    using MajdataPlay.Utils.ChartRadar;

    public sealed class PlayIntegrationSampleTests
    {
        [Fact]
        public async Task ThinServiceReturnsLightweightSuccessfulSnapshot()
        {
            var snapshot = await ChartRadarService.AnalyzeAsync(
                await MajSimai.SimaiParser.ParseChartAsync("(120){4}1,2,E"));

            Assert.True(snapshot.IsSuccess, string.Join("; ", snapshot.Errors));
            Assert.Equal("ok", snapshot.Status);
            Assert.NotNull(snapshot.FittedConstant);
            Assert.All(snapshot.Scores.Values, value => Assert.NotNull(value));
        }

        [Fact]
        public async Task StaticFacadeSupportsConcurrentCallers()
        {
            var chart = await MajSimai.SimaiParser.ParseChartAsync("(120){4}1,2,3,4,E");

            var snapshots = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => ChartRadarService.AnalyzeAsync(chart)));

            Assert.All(snapshots, snapshot =>
                Assert.True(snapshot.IsSuccess, string.Join("; ", snapshot.Errors)));
        }
    }
}
