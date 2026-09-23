using MajSimai;
using MajRadar.Analysis;
using MajRadar.Core;
using MajRadar.MajSimaiAdapter;
using MajRadar.Regression;
using MajRadar.Scoring;
using System.Threading;

namespace MajRadar.Runtime;

/// <summary>Stable public scalar projection for Play and other UI callers.</summary>
public static class RadarOutputDimensions
{
    public const string Note = RadarFeatureNames.Note;
    public const string Peak = RadarFeatureNames.Peak;
    public const string Sweep = RadarFeatureNames.Sweep;
    public const string SlideTricky = RadarFeatureNames.SlideTricky;
    public const string SlideSequence = RadarFeatureNames.SlideSequence;
    public const string Jack = RadarFeatureNames.Jack;
    public const string FittedConstant = RadarFeatureNames.FittedConstant;

    public static readonly IReadOnlyList<string> DefaultOrder = Array.AsReadOnly(new[]
    {
        Note, Peak, Sweep, SlideTricky, SlideSequence, Jack, FittedConstant
    });

    internal static IReadOnlyDictionary<string, double?> EmptyValues() =>
        DefaultOrder.ToDictionary(name => name, _ => (double?)null);
}

public sealed class RadarResult
{
    public RadarChartInput? ChartInput { get; set; }
    public RadarAnalysisResult? Analysis { get; set; }
    public double? FittedConstant { get; set; }
    public IReadOnlyDictionary<string, double?> RawValues { get; set; } =
        RadarOutputDimensions.EmptyValues();
    public IReadOnlyDictionary<string, double?> Scores { get; set; } =
        RadarOutputDimensions.EmptyValues();
    public IReadOnlyList<string> DimensionOrder { get; set; } =
        RadarOutputDimensions.DefaultOrder;
    public string ModelVersion { get; set; } = RegressionBetaParameters.Version;
    public string? MappingVersion { get; set; }
    public bool IsCancelled { get; set; }
    public string Status => IsCancelled ? "cancelled" : IsSuccess ? "ok" :
        Analysis?.IsSuccess == true ? "error" : Analysis?.Status ?? "error";
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public bool IsSuccess => ChartInput is not null && Analysis?.IsSuccess == true &&
                             FittedConstant is not null && MappingVersion is not null &&
                             Errors.Count == 0 && !IsCancelled;
}

/// <summary>
/// Unity-free public entry point for Play and standalone callers. Instances do
/// not retain chart or result state and may be reused concurrently when the
/// injected extended-Slide provider is thread-safe.
/// </summary>
public sealed class RadarRuntime
{
    private readonly MajSimaiChartAdapter _adapter;
    private readonly RadarAnalyzer _analyzer = new();
    private readonly RegressionBetaModel _model = new();
    private readonly RadarScoreMapper _scorer = new();

    public RadarRuntime(IExtendedSlideBarCountProvider? extendedSlides = null)
    {
        _adapter = new MajSimaiChartAdapter(extendedSlides);
    }

    public RadarResult Analyze(
        SimaiChart? chart,
        CancellationToken cancellationToken = default)
    {
        if (chart is null)
            return Failure("SimaiChart is null.");
        if (cancellationToken.IsCancellationRequested)
            return Cancelled();
        var adapted = _adapter.Adapt(chart, cancellationToken);
        if (adapted.IsCancelled)
            return Cancelled();
        if (!adapted.IsSuccess)
            return Failure(adapted.Errors);
        return Analyze(adapted.Chart!, cancellationToken);
    }

    public Task<RadarResult> AnalyzeAsync(
        SimaiChart? chart,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(chart, cancellationToken));

    public async Task<RadarResult> ParseAndAnalyzeAsync(
        string inote,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Cancelled();
        var adapted = await _adapter.ParseAndAdaptAsync(inote, cancellationToken).ConfigureAwait(false);
        if (adapted.IsCancelled)
            return Cancelled();
        if (!adapted.IsSuccess)
            return Failure(adapted.Errors);
        return Analyze(adapted.Chart!, cancellationToken);
    }

    public RadarResult Analyze(
        RadarChartInput? chart,
        CancellationToken cancellationToken = default)
    {
        if (chart is null)
            return Failure("RadarChartInput is null.");
        var result = new RadarResult { ChartInput = chart };
        try
        {
            var analysis = _analyzer.Analyze(chart, cancellationToken);
            result.Analysis = analysis;
            if (analysis.IsCancelled)
            {
                result.IsCancelled = true;
                return result;
            }

            var rawValues = analysis.Features
                .Where(pair => pair.Value.IsSuccess && pair.Value.Value is not null)
                .ToDictionary(pair => pair.Key, pair => pair.Value.Value!.Value);
            result.RawValues = Project(rawValues);
            if (!analysis.IsSuccess)
            {
                result.Errors = analysis.Features
                    .Where(pair => !pair.Value.IsSuccess)
                    .Select(pair => $"{pair.Key}: {pair.Value.Error}")
                    .ToArray();
                return result;
            }
            var raw = RadarFeatureNames.ModelInputOrder
                .Select(name => analysis.Features[name].Value!.Value).ToArray();
            result.FittedConstant = _model.Predict(raw);
            rawValues[RadarFeatureNames.FittedConstant] = result.FittedConstant.Value;
            result.RawValues = Project(rawValues);
            result.Scores = Project(_scorer.Map(analysis, result.FittedConstant.Value));
            result.MappingVersion = RadarScoreMapper.MappingVersion;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result.IsCancelled = true;
            return result;
        }
        catch (Exception exception)
        {
            result.Errors = new[] { $"Radar regression or scoring failed: {exception.Message}" };
            return result;
        }
    }

    private static RadarResult Cancelled() => new() { IsCancelled = true };

    private static RadarResult Failure(string error) => Failure(new[] { error });

    private static RadarResult Failure(IReadOnlyList<string> errors) => new()
    {
        Errors = errors
    };

    private static IReadOnlyDictionary<string, double?> Project(
        IReadOnlyDictionary<string, double>? source) =>
        RadarOutputDimensions.DefaultOrder.ToDictionary(
            name => name,
            name => source is not null && source.TryGetValue(name, out var value)
                ? (double?)value
                : null);
}
