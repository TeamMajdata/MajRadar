namespace MajRadar.MajSimaiAdapter;

/// <summary>
/// Supplies the gameplay arrow count for a normalized extended K Slide code.
/// Standard Slides continue to use MajRadar's fixed reference table.
/// Implementations used by a shared <c>RadarRuntime</c> must be thread-safe and
/// must not access Unity main-thread-only objects.
/// </summary>
public interface IExtendedSlideBarCountProvider
{
    int ResolveBarCount(string slideCode);
}
