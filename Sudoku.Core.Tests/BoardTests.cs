using Xunit;

namespace Sudoku.Core.Tests;

public class BoardTests
{
    private static Board SampleSolution() => new Generator(seed: 20260917).GenerateSolution();

    [Fact]
    public void Empty_ShouldHaveNoValuesAndFullCandidates()
    {
        Board board = Board.Empty();

        Assert.Equal(0, board.FilledCount);
        Assert.False(board.IsComplete);
        Assert.True(board.IsValid());

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            Assert.Equal(0, board[cell]);
            Assert.Equal(SudokuGrid.AllDigitsMask, board.CandidateMask(cell));
        }
    }

    [Fact]
    public void ParseAndToSdkString_ShouldRoundTrip()
    {
        string sdk = "53..7....6..195....98....6.8...6...34..8.3..17...2...6.6....28....419..5....8..79";

        Board board = Board.Parse(sdk);
        Assert.Equal(sdk, board.ToSdkString());
        Assert.Equal(81, board.ToSdkString().Length);
    }

    [Fact]
    public void Parse_ShouldAcceptSeparatorsAndZero()
    {
        string sdk = SampleSolution().ToSdkString();
        string spaced = string.Join(" ", sdk.Select(c => c == '.' ? '0' : c));

        Board board = Board.Parse(spaced);
        Assert.Equal(sdk, board.ToSdkString());
    }

    [Fact]
    public void Parse_ShouldThrowWhenCellCountIsWrong()
    {
        Assert.Throws<FormatException>(() => Board.Parse("12345"));
        Assert.False(Board.TryParse("12345", out _));
        Assert.True(Board.TryParse(SampleSolution().ToSdkString(), out _));
    }

    [Fact]
    public void IsValid_ShouldRejectDuplicateInRowColumnAndBox()
    {
        Board solution = SampleSolution();
        Assert.True(solution.IsValid());
        Assert.True(solution.IsSolved());

        Board duplicatedRow = solution.Clone();
        duplicatedRow[SudokuGrid.Index(0, 1)] = duplicatedRow[SudokuGrid.Index(0, 0)];
        Assert.False(duplicatedRow.IsValid());

        Board duplicatedColumn = solution.Clone();
        duplicatedColumn[SudokuGrid.Index(1, 0)] = duplicatedColumn[SudokuGrid.Index(0, 0)];
        Assert.False(duplicatedColumn.IsValid());

        Board duplicatedBox = solution.Clone();
        int boxCell = SudokuGrid.Boxes[SudokuGrid.BoxOf[0]].First(c => SudokuGrid.Row(c) != 0);
        duplicatedBox[boxCell] = duplicatedBox[SudokuGrid.Index(0, 0)];
        Assert.False(duplicatedBox.IsValid());
    }

    [Fact]
    public void CandidateMask_ShouldExcludeDigitsUsedByPeers()
    {
        var board = Board.Empty();
        board[SudokuGrid.Index(0, 0)] = 5;

        Assert.Equal(0, board.CandidateMask(SudokuGrid.Index(0, 0)));

        foreach (int peer in SudokuGrid.Peers[SudokuGrid.Index(0, 0)])
        {
            Assert.Equal(0, board.CandidateMask(peer) & SudokuGrid.DigitBit(5));
        }

        int unrelated = SudokuGrid.Index(4, 4);
        Assert.Equal(SudokuGrid.DigitBit(5), board.CandidateMask(unrelated) & SudokuGrid.DigitBit(5));
    }

    [Fact]
    public void Clone_ShouldBeIndependent()
    {
        Board solution = SampleSolution();
        Board clone = solution.Clone();

        clone[0] = clone[0] == 9 ? 1 : 9;

        Assert.NotEqual(solution.ToSdkString(), clone.ToSdkString());
        Assert.True(solution.IsSolved());
    }

    [Fact]
    public void Indexer_ShouldRejectOutOfRangeValues()
    {
        var board = Board.Empty();
        Assert.Throws<ArgumentOutOfRangeException>(() => board[0] = 10);
        Assert.Throws<ArgumentOutOfRangeException>(() => board[1, 1] = -1);
    }
}
