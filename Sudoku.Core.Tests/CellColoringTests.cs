using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>格子涂色（<see cref="CellColoring"/>）的测试。</summary>
public class CellColoringTests
{
    [Fact]
    public void Set_ShouldPaintAndErase()
    {
        var coloring = new CellColoring();

        Assert.True(coloring.IsEmpty);
        Assert.True(coloring.Set(0, 1));
        Assert.True(coloring.Set(40, CellColoring.MaxColor));

        Assert.Equal(1, coloring[0]);
        Assert.Equal(CellColoring.MaxColor, coloring[40]);
        Assert.Equal(2, coloring.Count);
        Assert.False(coloring.IsEmpty);

        // 同色重复涂不产生变化
        Assert.False(coloring.Set(0, 1));

        // 0 表示擦除
        Assert.True(coloring.Set(0, 0));
        Assert.Equal(0, coloring[0]);
        Assert.Equal(1, coloring.Count);

        Assert.True(coloring.Clear(40));
        Assert.True(coloring.IsEmpty);
        Assert.Equal(0, coloring.Count);
    }

    [Fact]
    public void Set_ShouldRejectInvalidCellOrColor()
    {
        var coloring = new CellColoring();

        Assert.False(coloring.Set(-1, 1));
        Assert.False(coloring.Set(SudokuGrid.CellCount, 1));
        Assert.False(coloring.Set(0, -1));
        Assert.False(coloring.Set(0, CellColoring.MaxColor + 1));
        Assert.True(coloring.IsEmpty);

        // 越界读取返回 0
        Assert.Equal(0, coloring[-5]);
        Assert.Equal(0, coloring[SudokuGrid.CellCount + 3]);
    }

    [Fact]
    public void ClearAll_ShouldWipeEverything()
    {
        var coloring = new CellColoring();
        for (int cell = 0; cell < 10; cell++)
        {
            coloring.Set(cell, (cell % CellColoring.MaxColor) + 1);
        }

        Assert.Equal(10, coloring.Count);
        coloring.ClearAll();
        Assert.True(coloring.IsEmpty);
    }

    [Fact]
    public void Clone_And_ToArray_ShouldBeIndependent()
    {
        var coloring = new CellColoring();
        coloring.Set(3, 2);

        CellColoring copy = coloring.Clone();
        copy.Set(3, 5);
        copy.Set(4, 5);

        Assert.Equal(2, coloring[3]);
        Assert.Equal(1, coloring.Count);
        Assert.Equal(5, copy[3]);
        Assert.Equal(2, copy.Count);

        int[] array = coloring.ToArray();
        array[3] = 6;
        Assert.Equal(2, coloring[3]);
    }

    [Fact]
    public void Serialize_And_Parse_ShouldRoundTrip()
    {
        var coloring = new CellColoring();
        coloring.Set(0, 1);
        coloring.Set(40, 5);
        coloring.Set(80, CellColoring.MaxColor);

        string text = coloring.Serialize();
        CellColoring restored = CellColoring.Parse(text);

        Assert.Equal(3, restored.Count);
        Assert.Equal(coloring.ToArray(), restored.ToArray());
        Assert.Equal(text, restored.Serialize());
        Assert.StartsWith("0:1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("坏数据;1:2;3;5:99;1:1:1;;")]
    public void Parse_ShouldIgnoreMalformedEntries(string? text)
    {
        CellColoring coloring = CellColoring.Parse(text);
        Assert.True(coloring.Count <= 1);
    }

    [Fact]
    public void IsValidColor_ShouldMatchMaxColor()
    {
        Assert.True(CellColoring.IsValidColor(0));
        Assert.True(CellColoring.IsValidColor(CellColoring.MaxColor));
        Assert.False(CellColoring.IsValidColor(CellColoring.MaxColor + 1));
        Assert.False(CellColoring.IsValidColor(-1));
    }
}
