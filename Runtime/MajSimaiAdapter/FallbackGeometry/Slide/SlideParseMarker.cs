// Adapted from TeamMajdata/MajdataPlay, commit acd295d3f878f53ecd412d150936a465ed2e3d28.
// GPL-3.0; see Documentation~/FallbackGeometry.md for provenance and parity tests.
namespace MajRadar.MajSimaiAdapter.FallbackGeometry.Slide
{
    /// <summary>
    /// <p>用来控制箭头对齐的标志</p>
    /// </summary>
    internal enum SlideParseMarker
    {
        None = 0,

        /// <summary>
        /// 调整箭头间距，以保证本段结束时箭头位置恰好对齐本段终点
        /// </summary>
        SmoothAlign,

        /// <summary>
        /// 不调整箭头间距，但本段结束时把箭头位置强制设为本段终点
        /// </summary>
        ForceAlign,
    }
}
