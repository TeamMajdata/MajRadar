using MajRadar.MajSimaiAdapter;
using MajRadar.Runtime;
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
        [Theory]
        [InlineData(RadarOutputDimension.Note, "note")]
        [InlineData(RadarOutputDimension.Peak, "peak")]
        [InlineData(RadarOutputDimension.Sweep, "sweep")]
        [InlineData(RadarOutputDimension.SlideTricky, "slide_tricky")]
        [InlineData(RadarOutputDimension.SlideSequence, "slide_sequence")]
        [InlineData(RadarOutputDimension.Jack, "jack")]
        [InlineData(RadarOutputDimension.FittedConstant, "fitted_constant")]
        public void EnumMapsToExistingPublicKey(
            RadarOutputDimension dimension,
            string expectedKey)
        {
            Assert.Equal(expectedKey, RadarOutputDimensions.Key(dimension));
        }

        [Fact]
        public async Task ThinServiceReturnsLightweightSuccessfulSnapshot()
        {
            var snapshot = await ChartRadarService.AnalyzeAsync(
                await MajSimai.SimaiParser.ParseChartAsync("(120){4}1,2,E"));

            Assert.True(snapshot.IsSuccess, string.Join("; ", snapshot.Errors));
            Assert.Equal("ok", snapshot.Status);
            Assert.NotNull(snapshot.FittedConstant);
            Assert.All(snapshot.Scores.Values, value => Assert.NotNull(value));
            Assert.Equal(
                snapshot.Scores[RadarOutputDimensions.Note],
                snapshot.GetScore(RadarOutputDimension.Note));
            Assert.Equal(
                snapshot.RawValues[RadarOutputDimensions.SlideTricky],
                snapshot.GetRawValue(RadarOutputDimension.SlideTricky));
            Assert.Equal(
                snapshot.FittedConstant,
                snapshot.GetScore(RadarOutputDimension.FittedConstant));
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
