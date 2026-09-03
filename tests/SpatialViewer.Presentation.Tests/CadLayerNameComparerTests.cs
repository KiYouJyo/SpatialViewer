using SpatialViewer.Presentation;

namespace SpatialViewer.Presentation.Tests;

public sealed class CadLayerNameComparerTests
{
    [Fact]
    public void SortsCadLayersByDigitLatinHanThenOther()
    {
        string[] names =
        [
            "墙体",
            "B-WALL",
            "9-标注",
            "阿轴",
            "a-door",
            "2-轴网",
            "轴网",
            "电气",
            "0-默认",
            "图框",
            "层高",
            "8-家具",
            "给排水",
            "八层",
            "Z-ANNO",
            "_TEMP"
        ];

        var sorted = names.OrderBy(name => name, CadLayerNameComparer.Instance).ToArray();

        Assert.Equal(
        [
            "0-默认",
            "2-轴网",
            "8-家具",
            "9-标注",
            "a-door",
            "B-WALL",
            "Z-ANNO",
            "阿轴",
            "八层",
            "层高",
            "电气",
            "给排水",
            "墙体",
            "图框",
            "轴网",
            "_TEMP"
        ], sorted);
    }

    [Fact]
    public void LatinSortingIsCaseInsensitiveWithDeterministicTieBreak()
    {
        string[] names = ["b", "A", "a", "B"];

        var sorted = names.OrderBy(name => name, CadLayerNameComparer.Instance).ToArray();

        Assert.Equal(["A", "a", "B", "b"], sorted);
    }
}
