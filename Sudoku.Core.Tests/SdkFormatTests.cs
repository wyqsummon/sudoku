using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>SDK 文本导入导出（兼容 Hodoku 习惯写法）的测试。</summary>
public class SdkFormatTests
{
    private const string KnownPuzzle =
        "530070000" +
        "600195000" +
        "098000060" +
        "800060003" +
        "400803001" +
        "700020006" +
        "060000280" +
        "000419005" +
        "000080079";

    private const string KnownSolution =
        "534678912" +
        "672195348" +
        "198342567" +
        "859761423" +
        "426853791" +
        "713924856" +
        "961537284" +
        "287419635" +
        "345286179";

    [Fact]
    public void Format_ShouldUseDotForEmptyCells()
    {
        Board board = Board.Parse(KnownPuzzle);

        // KnownPuzzle 用 '0' 表示空格，导出默认用 '.'
        string dotted = KnownPuzzle.Replace('0', '.');
        Assert.Equal(81, dotted.Length);
        Assert.Equal(dotted, SdkFormat.Format(board));

        // 空位字符可自定义
        Assert.Equal(KnownPuzzle, SdkFormat.Format(board, '0'));
        Assert.Equal(new string('.', 81), SdkFormat.Format(Board.Empty()));
    }

    [Fact]
    public void Import_ShouldAcceptTheClassicPuzzleAndFindItsSolution()
    {
        SdkImportResult result = SdkFormat.Import(KnownPuzzle);

        Assert.True(result.Success, result.Message);
        Assert.Equal(30, result.ClueCount);
        Assert.Equal(51, result.EmptyCount);
        Assert.True(result.HasSolution);
        Assert.Equal(KnownSolution, SdkFormat.Format(result.Solution!));
        Assert.Contains("唯一解", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldRoundTripFormatOutput()
    {
        var generator = new Generator(seed: 4242);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Medium);

        string exported = SdkFormat.Format(puzzle.Given);
        SdkImportResult imported = SdkFormat.Import(exported);

        Assert.True(imported.Success, imported.Message);
        Assert.Equal(puzzle.Given, imported.Board);
        Assert.NotNull(imported.Solution);
        Assert.Equal(puzzle.Solution, imported.Solution);
    }

    [Fact]
    public void Import_ShouldTolerateHodokuStyleLayouts()
    {
        // 带空格、宫线与分隔行的可读排版 + '0' 表示空格
        string pretty =
            "# 经典示例题（Hodoku 风格排版）\n" +
            "5 3 0 | 0 7 0 | 0 0 0\n" +
            "6 0 0 | 1 9 5 | 0 0 0\n" +
            "0 9 8 | 0 0 0 | 0 6 0\n" +
            "------+-------+------\n" +
            "8 0 0 | 0 6 0 | 0 0 3\n" +
            "4 0 0 | 8 0 3 | 0 0 1\n" +
            "7 0 0 | 0 2 0 | 0 0 6\n" +
            "------+-------+------\n" +
            "0 6 0 | 0 0 0 | 2 8 0\n" +
            "0 0 0 | 4 1 9 | 0 0 5\n" +
            "0 0 0 | 0 8 0 | 0 7 9\n";

        SdkImportResult result = SdkFormat.Import(pretty);

        Assert.True(result.Success, result.Message);
        Assert.Equal(KnownPuzzle.Replace('0', '.'), SdkFormat.Format(result.Board));

        // '_' 与 '*' 同样视为空格
        string underscores = KnownPuzzle.Replace('0', '_');
        Assert.True(SdkFormat.Import(underscores).Success);

        string stars = KnownPuzzle.Replace('0', '*');
        Assert.True(SdkFormat.Import(stars).Success);
    }

    [Fact]
    public void FormatPretty_ShouldRoundTrip()
    {
        Board board = Board.Parse(KnownPuzzle);

        string pretty = SdkFormat.FormatPretty(board);
        SdkImportResult back = SdkFormat.Import(pretty);

        Assert.Contains("+", pretty, StringComparison.Ordinal);
        Assert.True(back.Success, back.Message);
        Assert.Equal(board, back.Board);
    }

    [Fact]
    public void Import_ShouldTolerateWindowsCarriageReturnLineEndings()
    {
        // WinUI 的文本框用 '\r' 作换行；粘贴进应用时拿到的就是这种文本
        string cr = "# 注释行\r5 3 0 | 0 7 0 | 0 0 0\r6 0 0 | 1 9 5 | 0 0 0\r0 9 8 | 0 0 0 | 0 6 0\r" +
            "------+-------+------\r8 0 0 | 0 6 0 | 0 0 3\r4 0 0 | 8 0 3 | 0 0 1\r7 0 0 | 0 2 0 | 0 0 6\r" +
            "------+-------+------\r0 6 0 | 0 0 0 | 2 8 0\r0 0 0 | 4 1 9 | 0 0 5\r0 0 0 | 0 8 0 | 0 7 9\r";

        SdkImportResult result = SdkFormat.Import(cr);

        Assert.True(result.Success, result.Message);
        Assert.Equal(KnownPuzzle.Replace('0', '.'), SdkFormat.Format(result.Board));

        // '#' 注释必须只吃掉自己那一行，不能把后面全部截断
        Assert.Single(SdkFormat.SplitPuzzles("# 只有注释行\r" + KnownPuzzle));
        Assert.Equal(2, SdkFormat.SplitPuzzles(KnownPuzzle + "\r" + KnownSolution).Count);

        // 混合换行（'\\r\\n' 与 '\\n'）同样可用
        string mixed = ("# 注释\r\n" + KnownPuzzle + "\n").Replace('0', '.');
        SdkImportResult mixedResult = SdkFormat.Import(mixed);
        Assert.True(mixedResult.Success, mixedResult.Message);
        Assert.Equal(30, mixedResult.ClueCount);
    }

    [Fact]
    public void SplitPuzzles_ShouldSkipCommentsAndBlankLines()
    {
        string library =
            "# 题库：两道题\n" +
            "\n" +
            KnownPuzzle + "\n" +
            "   " + KnownSolution + "  \n" +
            "# 结束\n";

        IReadOnlyList<string> puzzles = SdkFormat.SplitPuzzles(library);

        Assert.Equal(2, puzzles.Count);
        Assert.Equal(KnownPuzzle, puzzles[0]);
        Assert.Equal(KnownSolution, puzzles[1]);

        // 多行题库导入时取第一道
        SdkImportResult first = SdkFormat.Import(library);
        Assert.True(first.Success, first.Message);
        Assert.Equal(30, first.ClueCount);
    }

    [Fact]
    public void FormatLibrary_ShouldProduceOneLinePerPuzzle()
    {
        Board a = Board.Parse(KnownPuzzle);
        Board b = Board.Parse(KnownSolution);

        string text = SdkFormat.FormatLibrary(new[] { a, b });

        Assert.Equal(2, SdkFormat.SplitPuzzles(text).Count);
        Assert.Contains(KnownPuzzle.Replace('0', '.'), text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n  \n")]
    public void Import_ShouldRejectEmptyInput(string? text)
    {
        SdkImportResult result = SdkFormat.Import(text);

        Assert.False(result.Success);
        Assert.Contains("粘贴", result.Message, StringComparison.Ordinal);
        Assert.Null(result.Solution);
    }

    [Fact]
    public void Import_ShouldReportCellCountMismatch()
    {
        SdkImportResult result = SdkFormat.Import("123456789");

        Assert.False(result.Success);
        Assert.Contains("81 个格值", result.Message, StringComparison.Ordinal);
        Assert.Contains("9", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldReportUnknownCharacters()
    {
        string withJunk = KnownPuzzle[..10] + "xyz" + KnownPuzzle[13..];

        SdkImportResult result = SdkFormat.Import(withJunk);

        Assert.False(result.Success);
        Assert.Contains("无法识别的字符", result.Message, StringComparison.Ordinal);
        Assert.Contains("x", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldReportDuplicateWithCoordinates()
    {
        // 第 1 行第 5 格也填 5 → 与第 1 格重复
        string duplicated = "530050000" + KnownPuzzle[9..];

        SdkImportResult result = SdkFormat.Import(duplicated);

        Assert.False(result.Success);
        Assert.Contains("不合法", result.Message, StringComparison.Ordinal);
        Assert.Contains("R1C1", result.Message, StringComparison.Ordinal);
        Assert.Contains("R1C5", result.Message, StringComparison.Ordinal);
        Assert.Contains("第 1 行", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldRejectUnsolvableBoard()
    {
        // 第 1 行只剩 R1C9 可填 9，但第 2 行第 9 列已填 9 → 无解
        string unsolvable =
            "123456780" +
            "000000009" +
            "000000000" +
            new string('.', 54);

        SdkImportResult result = SdkFormat.Import(unsolvable);

        Assert.False(result.Success);
        Assert.Contains("无解", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldRejectMultipleSolutions()
    {
        // 只有一个已知数 → 必然多解
        string many = "5" + new string('.', 80);

        SdkImportResult result = SdkFormat.Import(many);

        Assert.False(result.Success);
        Assert.Contains("多个解", result.Message, StringComparison.Ordinal);

        // 允许关闭唯一解校验时应当能导入
        SdkImportResult relaxed = SdkFormat.Import(many, requireUniqueSolution: false);
        Assert.True(relaxed.Success, relaxed.Message);
        Assert.True(relaxed.HasSolution);
    }

    [Fact]
    public void Import_ShouldRejectCompleteSolution()
    {
        SdkImportResult result = SdkFormat.Import(KnownSolution);

        Assert.False(result.Success);
        Assert.Contains("完整解", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldRejectEmptyBoard()
    {
        SdkImportResult result = SdkFormat.Import(new string('.', 81));

        Assert.False(result.Success);
        Assert.Contains("空的", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ShouldHandleEveryGeneratedLevel()
    {
        var generator = new Generator(seed: 9090);

        foreach (DifficultyLevel level in Difficulty.All)
        {
            Puzzle puzzle = generator.Generate(level, maxAttempts: 8);
            SdkImportResult imported = SdkFormat.Import(SdkFormat.Format(puzzle.Given));

            Assert.True(imported.Success, $"{Difficulty.Name(level)}：{imported.Message}");
            Assert.Equal(puzzle.Solution, imported.Solution);
            Assert.Equal(puzzle.Given.FilledCount, imported.ClueCount);

            // 导入后再评分应与出题时的档位一致
            RatingReport rating = DifficultyRating.Rate(imported.Board);
            Assert.Equal(puzzle.Level, rating.Level);
        }
    }
}
