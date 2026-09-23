namespace MajRadar.MajSimaiAdapter;

/// <summary>
/// Supplies the gameplay arrow count for a normalized extended K Slide code.
/// Standard Slides continue to use MajRadar's fixed reference table.
/// </summary>
public interface IExtendedSlideBarCountProvider
{
    int ResolveBarCount(string slideCode);
}
