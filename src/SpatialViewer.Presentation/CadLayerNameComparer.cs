using System.Globalization;
using System.Text;

namespace SpatialViewer.Presentation;

/// <summary>
/// Orders CAD layer names the way the product layer palette expects:
/// ASCII digits first, then ASCII letters, then Han names in Simplified
/// Chinese pinyin collation order, with every other leading character last.
/// </summary>
public sealed class CadLayerNameComparer : IComparer<string?>
{
    public static CadLayerNameComparer Instance { get; } = new();

    private static readonly CompareInfo ChineseCompareInfo = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;
    private static readonly CompareOptions ChineseCompareOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreWidth;

    private CadLayerNameComparer()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        var xGroup = GetLeadingGroup(x);
        var yGroup = GetLeadingGroup(y);
        if (xGroup != yGroup) return xGroup.CompareTo(yGroup);

        var comparison = xGroup switch
        {
            LayerNameGroup.Han => ChineseCompareInfo.Compare(x, y, ChineseCompareOptions),
            _ => StringComparer.OrdinalIgnoreCase.Compare(x, y)
        };

        return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(x, y);
    }

    private static LayerNameGroup GetLeadingGroup(string value)
    {
        if (value.Length == 0) return LayerNameGroup.Empty;

        var first = value.EnumerateRunes().First();
        var scalar = first.Value;
        if (scalar is >= '0' and <= '9') return LayerNameGroup.Digit;
        if (scalar is >= 'A' and <= 'Z' or >= 'a' and <= 'z') return LayerNameGroup.Latin;
        if (IsHan(scalar)) return LayerNameGroup.Han;
        return LayerNameGroup.Other;
    }

    private static bool IsHan(int scalar) => scalar is
        >= 0x3400 and <= 0x4DBF or
        >= 0x4E00 and <= 0x9FFF or
        >= 0xF900 and <= 0xFAFF or
        >= 0x20000 and <= 0x2EBEF or
        >= 0x30000 and <= 0x323AF;

    private enum LayerNameGroup
    {
        Digit = 0,
        Latin = 1,
        Han = 2,
        Other = 3,
        Empty = 4
    }
}
