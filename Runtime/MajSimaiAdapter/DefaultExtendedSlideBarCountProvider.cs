using System;
using MajRadar.MajSimaiAdapter.FallbackGeometry;
using MajRadar.MajSimaiAdapter.FallbackGeometry.Slide;

namespace MajRadar.MajSimaiAdapter;

/// <summary>
/// Unity-free fallback for hosts without their own extended-Slide geometry.
/// Counts arrows using a pinned copy of MajdataPlay's path and alignment rules.
/// Each call creates its own path, so the shared instance is thread-safe.
/// </summary>
public sealed class DefaultExtendedSlideBarCountProvider : IExtendedSlideBarCountProvider
{
    /// <summary>Include this identifier in persistent analysis cache versions.</summary>
    public const string GeometryVersion = "play-acd295d-count-v1";

    public static DefaultExtendedSlideBarCountProvider Instance { get; } = new();

    public int ResolveBarCount(string slideCode)
    {
        if (slideCode is null)
            throw new ArgumentNullException(nameof(slideCode));
        if (slideCode.Length < 3 || slideCode[0] is < '1' or > '8' ||
            slideCode[^2] != 'K' || slideCode[^1] is < '1' or > '8')
            throw new ArgumentException("Expected a normalized extended Slide code, such as 1P6K7.", nameof(slideCode));

        var path = SlideCodeParser.Parse(slideCode);
        var totalLength = path.GetPathLength();
        if (!IsFinite(totalLength) || totalLength <= 0)
            throw new ArgumentException("Extended Slide path must have a finite positive length.", nameof(slideCode));

        var currentLength = 0.0;
        var segmentIndex = 0;
        var samples = 0;
        while (currentLength < totalLength)
        {
            samples = checked(samples + 1);
            var segment = path.Segments[segmentIndex];
            var nextLength = currentLength + segment.ArrowDistance;

            // Keep the single-segment transition and floating-point operation order
            // from Play's BuildArrowData. Rounding the total length is not equivalent.
            if (segmentIndex < path.Segments.Length - 1 &&
                nextLength >= path.AccumulatedLengths[segmentIndex])
            {
                var nextSegment = path.Segments[segmentIndex + 1];
                if (nextSegment.ParseMarker == SlideParseMarker.SmoothAlign)
                {
                    var delta = path.AccumulatedLengths[segmentIndex + 1] - currentLength;
                    var n = Math.Round(delta / SlideGeo.DefaultDistance);
                    nextSegment.SetArrowDistance(delta / n);
                    nextLength = currentLength + delta / n;
                }
                if (segment.ParseMarker == SlideParseMarker.ForceAlign)
                    nextLength = path.AccumulatedLengths[segmentIndex] + nextSegment.ArrowDistance;
                segmentIndex++;
            }

            // Invalid geometry must fail rather than stall a server worker forever.
            if (!IsFinite(nextLength) || nextLength <= currentLength)
                throw new ArgumentException("Extended Slide arrow placement must advance by a finite positive distance.", nameof(slideCode));
            currentLength = nextLength;
        }

        // BuildArrowData includes the starting sample and appends the endpoint.
        // Its Length - 2 therefore equals the number of loop samples minus one.
        var count = samples - 1;
        if (count <= 0)
            throw new ArgumentException("Extended Slide path has no arrows.", nameof(slideCode));
        return count;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
