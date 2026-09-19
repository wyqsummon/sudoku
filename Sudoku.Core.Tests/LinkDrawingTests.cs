using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>用户手绘强弱链（<see cref="UserLink"/> / <see cref="LinkDrawing"/>）的测试。</summary>
public class LinkDrawingTests
{
    private static CandidateRef C(int cell, int digit) => new(cell, digit);

    [Fact]
    public void Create_ShouldKeepDrawingDirection()
    {
        UserLink forward = UserLink.Create(C(3, 5), C(40, 2), isStrong: true, colorIndex: 2);
        UserLink backward = UserLink.Create(C(40, 2), C(3, 5), isStrong: true, colorIndex: 2);

        // 方向有意义（箭头指向终点），因此两条不是同一条
        Assert.NotEqual(forward, backward);
        Assert.Equal(backward, forward.Reversed());

        Assert.Equal(3, forward.From.Cell);
        Assert.Equal(40, forward.To.Cell);
        Assert.True(forward.IsStrong);
        Assert.Equal(2, forward.ColorIndex);
        Assert.False(forward.IsInternal);
        Assert.Equal("R1C4(5) ==▶ R5C5(2)", forward.ToString());

        // 颜色编号为负时归零
        Assert.Equal(0, UserLink.Create(C(3, 5), C(40, 2), true, -5).ColorIndex);
    }

    [Fact]
    public void Create_ShouldKeepSameCellLinksAndMarkThemInternal()
    {
        UserLink link = UserLink.Create(C(0, 1), C(0, 9), isStrong: false);

        Assert.True(link.IsInternal);
        Assert.Equal("R1C1(1) --▶ R1C1(9)", link.ToString());
        Assert.Equal("弱链", link.KindName);
    }

    [Fact]
    public void Add_ShouldDeduplicateAndRejectInvalidEndpoints()
    {
        var drawing = new LinkDrawing();

        Assert.True(drawing.Add(UserLink.Create(C(0, 1), C(9, 1), true)));
        Assert.True(drawing.Add(UserLink.Create(C(0, 2), C(9, 1), false)));

        // 完全相同的连线不重复添加
        Assert.False(drawing.Add(UserLink.Create(C(0, 1), C(9, 1), true)));

        // 反向连线是另一条（箭头方向不同），可以共存
        Assert.True(drawing.Add(UserLink.Create(C(9, 1), C(0, 1), true)));

        // 强弱不同视为不同连线，可以共存
        Assert.True(drawing.Add(UserLink.Create(C(0, 1), C(9, 1), false)));

        // 非法坐标 / 自环
        Assert.False(drawing.Add(UserLink.Create(C(0, 0), C(9, 1), true)));
        Assert.False(drawing.Add(UserLink.Create(C(0, 10), C(9, 1), true)));
        Assert.False(drawing.Add(UserLink.Create(C(-1, 1), C(9, 1), true)));
        Assert.False(drawing.Add(UserLink.Create(C(SudokuGrid.CellCount, 1), C(9, 1), true)));
        Assert.False(drawing.Add(UserLink.Create(C(4, 4), C(4, 4), true)));

        Assert.Equal(4, drawing.Count);
        Assert.Equal(2, drawing.StrongCount);
        Assert.Equal(2, drawing.WeakCount);
    }

    [Fact]
    public void Remove_And_RemoveForCell_ShouldCleanUp()
    {
        var drawing = new LinkDrawing();
        drawing.Add(UserLink.Create(C(0, 1), C(9, 1), true));
        drawing.Add(UserLink.Create(C(0, 2), C(9, 5), false));
        drawing.Add(UserLink.Create(C(20, 1), C(30, 1), true));

        Assert.True(drawing.Remove(UserLink.Create(C(20, 1), C(30, 1), true)));
        Assert.False(drawing.Remove(UserLink.Create(C(20, 1), C(30, 1), true)));

        // 填了数字的格子，其相关连线一并清掉
        Assert.Equal(2, drawing.RemoveForCell(9));
        Assert.True(drawing.IsEmpty);

        drawing.Add(UserLink.Create(C(5, 1), C(6, 1), true));
        drawing.Clear();
        Assert.True(drawing.IsEmpty);
        Assert.Equal(0, drawing.StrongCount + drawing.WeakCount);
    }

    [Fact]
    public void Serialize_And_Parse_ShouldRoundTrip()
    {
        var drawing = new LinkDrawing();
        drawing.Add(UserLink.Create(C(0, 1), C(9, 3), true, colorIndex: 3));
        drawing.Add(UserLink.Create(C(40, 9), C(12, 2), false));
        drawing.Add(UserLink.Create(C(7, 7), C(7, 8), true));

        string text = drawing.Serialize();
        LinkDrawing restored = LinkDrawing.Parse(text);

        Assert.Equal(drawing.Count, restored.Count);
        Assert.Equal(drawing.ToArray(), restored.ToArray());
        Assert.Equal(drawing.Serialize(), restored.Serialize());
        Assert.Contains(":S", text, StringComparison.Ordinal);
        Assert.Contains(":W", text, StringComparison.Ordinal);

        // 方向与颜色都要活下来
        UserLink coloured = restored.Links.Single(l => l.ColorIndex == 3);
        Assert.Equal(0, coloured.From.Cell);
        Assert.Equal(9, coloured.To.Cell);
        Assert.True(coloured.IsStrong);
    }

    [Fact]
    public void Parse_ShouldAcceptOldFormatWithoutColor()
    {
        // 阶段二早期存档没有颜色字段，必须仍能读入（默认配色 0）
        LinkDrawing drawing = LinkDrawing.Parse("0:1-9:3:S;12:2-30:7:W");

        Assert.Equal(2, drawing.Count);
        Assert.All(drawing.Links, link => Assert.Equal(0, link.ColorIndex));
        Assert.Contains(UserLink.Create(C(0, 1), C(9, 3), true), drawing.Links);
    }

    [Fact]
    public void Parse_ShouldIgnoreMalformedEntries()
    {
        LinkDrawing drawing = LinkDrawing.Parse("0:1-9:3:S;坏数据;1:2-3:4;1:2-3:4:X;0:0-1:1:S;5:5-5:5:S;12:2-30:7:W;;");

        // 只保留两组合法条目
        Assert.Equal(2, drawing.Count);
        Assert.Contains(UserLink.Create(C(0, 1), C(9, 3), true), drawing.Links);
        Assert.Contains(UserLink.Create(C(12, 2), C(30, 7), false), drawing.Links);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ShouldReturnEmptyForBlankInput(string? text)
    {
        Assert.True(LinkDrawing.Parse(text).IsEmpty);
    }

    [Fact]
    public void Clone_ShouldBeIndependent()
    {
        var drawing = new LinkDrawing();
        drawing.Add(UserLink.Create(C(0, 1), C(9, 1), true));

        LinkDrawing copy = drawing.Clone();
        copy.Add(UserLink.Create(C(2, 2), C(11, 2), false));

        Assert.Equal(1, drawing.Count);
        Assert.Equal(2, copy.Count);
    }
}
