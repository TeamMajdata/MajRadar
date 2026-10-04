// Adapted from TeamMajdata/MajdataPlay, commit acd295d3f878f53ecd412d150936a465ed2e3d28.
// GPL-3.0; see Documentation~/FallbackGeometry.md for provenance and parity tests.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MajRadar.MajSimaiAdapter.FallbackGeometry
{
    internal readonly struct ComplexCircle
    {
        public readonly Complex Center;
        public readonly double Radius;

        public ComplexCircle(Complex center, double radius)
        {
            Center = center;
            Radius = radius;
        }
    }
}
