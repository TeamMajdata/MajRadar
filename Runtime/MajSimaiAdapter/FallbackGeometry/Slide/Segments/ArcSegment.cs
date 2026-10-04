// Adapted from TeamMajdata/MajdataPlay, commit acd295d3f878f53ecd412d150936a465ed2e3d28.
// GPL-3.0; see Documentation~/FallbackGeometry.md for provenance and parity tests.
using MajRadar.MajSimaiAdapter.FallbackGeometry;
using System;
using System.Numerics;

namespace MajRadar.MajSimaiAdapter.FallbackGeometry.Slide.Segments
{
    /// <summary>
    /// <p>slide 圆弧片段，对角度进行线性插值，不会自动扣除一整圈</p>
    /// </summary>
    internal class ArcSegment : PathSegment
    {
        public readonly ComplexCircle Circle;
        public readonly double StartRadian;
        public readonly double EndRadian;

        public ArcSegment(ComplexCircle circle, double startRadian, double endRadian)
        {
            Circle = circle;
            StartRadian = startRadian;
            EndRadian = endRadian;
        }

        public override bool IsCurve { get; } = true;

        public override Complex GetPointAt(double t)
        {
            var angle = StartRadian + t * (EndRadian - StartRadian);
            return Circle.Center + Complex.FromPolarCoordinates(Circle.Radius, angle);
        }

        public override Complex GetTangentAt(double t)
        {
            var angle = StartRadian + t * (EndRadian - StartRadian);
            if (StartRadian < EndRadian)
            {
                return Complex.FromPolarCoordinates(1, angle) * Complex.ImaginaryOne;
            }
            else
            {
                return Complex.FromPolarCoordinates(-1, angle) * Complex.ImaginaryOne;
            }
        }

        public override double GetSegmentLength()
        {
            return Math.Abs(EndRadian - StartRadian) * Circle.Radius;
        }
    }
}
