using System.Threading.Tasks;
using MajRadar.Analysis;
using MajRadar.Core;
using MajRadar.Runtime;
using MajSimai;
using Xunit;

#nullable enable

namespace MajRadar.MajSimaiAdapter.Tests
{
    /// <summary>
    /// Verifies radar analysis with MajSimai's comment-normalized chart hashes.
    /// </summary>
    public sealed class MajSimaiCompatibilityTests
    {
        /// <summary>
        /// Exercises shared slide heads, independent path flags, headless slides, holds, touches and extended paths.
        /// </summary>
        private const string CompactChart = "(120){4}1bx-5[4:1]b*-7[4:2]m,2x,3?-7[4:1],4h[8:1],B1,C,1A3P9K5[4:1],,,E";

        /// <summary>
        /// Checks that comment-normalized hashes and both runtime entry points preserve the same radar analysis.
        /// </summary>
        /// <param name="lineEnding">The line ending that terminates each chart comment.</param>
        /// <returns>A task that completes after both forms of the chart have been analyzed.</returns>
        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public async Task CommentNormalizedChartsPreserveRadarResults(string lineEnding)
        {
            var commented = "|| ignored header ,(999){1}8," + lineEnding + "\t" +
                            CompactChart.Replace(",", ", || ignored notes ,8-4[4:1]," + lineEnding + "\t") +
                            " || ignored footer ,8,";
            var compactChart = await SimaiParser.ParseChartAsync(CompactChart);
            var commentedChart = await SimaiParser.ParseChartAsync(commented);

            Assert.Equal(compactChart.Md5, commentedChart.Md5);
            Assert.Equal(compactChart.Sha256, commentedChart.Sha256);

            var runtime = new RadarRuntime();
            var expected = runtime.Analyze(compactChart);

            AssertSameRadarResults(expected, runtime.Analyze(commentedChart));
            AssertSameRadarResults(expected, await runtime.ParseAndAnalyzeAsync(commented));
        }

        /// <summary>
        /// Compares timing, all seven raw features and every public score of two successful analyses.
        /// </summary>
        /// <param name="expected">The analysis of the compact chart.</param>
        /// <param name="actual">The analysis of the equivalent chart containing comments and white space.</param>
        private static void AssertSameRadarResults(RadarResult expected, RadarResult actual)
        {
            Assert.True(expected.IsSuccess, string.Join("; ", expected.Errors));
            Assert.True(actual.IsSuccess, string.Join("; ", actual.Errors));
            Assert.Equal(expected.DimensionOrder, actual.DimensionOrder);
            Assert.Equal(expected.FittedConstant, actual.FittedConstant);
            Assert.Equal(expected.ModelVersion, actual.ModelVersion);
            Assert.Equal(expected.MappingVersion, actual.MappingVersion);

            var expectedInput = Assert.IsType<RadarChartInput>(expected.ChartInput);
            var actualInput = Assert.IsType<RadarChartInput>(actual.ChartInput);
            Assert.Equal(expectedInput.ChartEndTimeSeconds, actualInput.ChartEndTimeSeconds);
            Assert.Equal(expectedInput.LastEventEndTimeSeconds, actualInput.LastEventEndTimeSeconds);
            Assert.Equal(expectedInput.Events.Count, actualInput.Events.Count);

            var expectedAnalysis = Assert.IsType<RadarAnalysisResult>(expected.Analysis);
            var actualAnalysis = Assert.IsType<RadarAnalysisResult>(actual.Analysis);
            foreach (var feature in RadarFeatureNames.ModelInputOrder)
            {
                Assert.Equal(expectedAnalysis.Features[feature].Value, actualAnalysis.Features[feature].Value);
            }
            foreach (var dimension in expected.DimensionOrder)
            {
                Assert.Equal(expected.RawValues[dimension], actual.RawValues[dimension]);
                Assert.Equal(expected.Scores[dimension], actual.Scores[dimension]);
            }
        }
    }
}
