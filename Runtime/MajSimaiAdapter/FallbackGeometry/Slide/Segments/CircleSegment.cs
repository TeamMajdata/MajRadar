// Adapted from TeamMajdata/MajdataPlay, commit acd295d3f878f53ecd412d150936a465ed2e3d28.
// GPL-3.0; see Documentation~/FallbackGeometry.md for provenance and parity tests.
using MajRadar.MajSimaiAdapter.FallbackGeometry;
using System;
using System.Numerics;

namespace MajRadar.MajSimaiAdapter.FallbackGeometry.Slide.Segments
{
    /// <summary>
    /// <p>slide 圆周片段，总之就是转一整圈</p>
    /// </summary>
    internal class CircleSegment : PathSegment
    {
        public readonly ComplexCircle Circle;
        public readonly double StartRadian;
        public readonly bool IsCcw;

        public CircleSegment(ComplexCircle circle, double startRadian, bool isCcw)
        {
            Circle = circle;
            StartRadian = startRadian;
            IsCcw = isCcw;
        }

        public override bool IsCurve { get; } = true;

        public override Complex GetPointAt(double t)
        {
            double angle;
            if (IsCcw)
            {
                angle = StartRadian + t * Math.PI * 2.0;
            }
            else
            {
                angle = StartRadian - t * Math.PI * 2.0;
            }

            return Circle.Center + Complex.FromPolarCoordinates(Circle.Radius, angle);
        }

        public override Complex GetTangentAt(double t)
        {
            double angle;
            if (IsCcw)
            {
                angle = StartRadian + t * Math.PI * 2.0;
                return Complex.FromPolarCoordinates(1, angle) * Complex.ImaginaryOne;
            }
            else
            {
                angle = StartRadian - t * Math.PI * 2.0;
                return Complex.FromPolarCoordinates(-1, angle) * Complex.ImaginaryOne;
            }
        }

        public override double GetSegmentLength()
        {
            return Math.PI * Circle.Radius * 2.0;
        }
    }
}
