using Xunit;

namespace Sudoku.Core.Tests;

public class SolverTests
{
    [Fact]
    public void CountSolutions_ShouldBeOneForCompleteValidGrid()
    {
        Board solution = new Generator(seed: 1).GenerateSolution();

        Assert.Equal(1, Solver.CountSolutions(solution));
        Assert.True(Solver.HasUniqueSolution(solution));
    }

    [Fact]
    public void CountSolutions_ShouldRespectLimitOnEmptyBoard()
    {
        Assert.Equal(2, Solver.CountSolutions(Board.Empty(), limit: 2));
        Assert.Equal(5, Solver.CountSolutions(Board.Empty(), limit: 5));
    }

    [Fact]
    public void CountSolutions_ShouldBeZeroForInvalidBoard()
    {
        Board solution = new Generator(seed: 2).GenerateSolution();
        Board broken = solution.Clone();
        broken[SudokuGrid.Index(3, 3)] = broken[SudokuGrid.Index(3, 4)];

        Assert.False(broken.IsValid());
        Assert.Equal(0, Solver.CountSolutions(broken));
        Assert.False(Solver.TrySolve(broken, out _));
    }

    [Fact]
    public void TrySolve_ShouldRecoverGeneratedPuzzleSolution()
    {
        var generator = new Generator(seed: 42);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Medium);

        Assert.True(Solver.TrySolve(puzzle.Given, out Board solved));
        Assert.Equal(puzzle.Solution.ToSdkString(), solved.ToSdkString());
        Assert.True(solved.IsSolved());
    }

    [Fact]
    public void CountSolutions_ShouldDetectMultipleSolutions()
    {
        Board solution = new Generator(seed: 3).GenerateSolution();
        var sparse = Board.Empty();

        // 只保留 20 个线索：不足以保证唯一解
        int kept = 0;
        for (int i = 0; i < SudokuGrid.CellCount && kept < 20; i += 4)
        {
            sparse[i] = solution[i];
            kept++;
        }

        Assert.Equal(2, Solver.CountSolutions(sparse, limit: 2));
    }

    [Fact]
    public void Solver_ShouldSolveHardGeneratedPuzzleQuickly()
    {
        var generator = new Generator(seed: 7);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Hard);

        var start = System.Diagnostics.Stopwatch.StartNew();
        bool ok = Solver.TrySolve(puzzle.Given, out Board solved);
        start.Stop();

        Assert.True(ok);
        Assert.Equal(puzzle.Solution.ToSdkString(), solved.ToSdkString());
        Assert.True(start.ElapsedMilliseconds < 2000, $"求解耗时过长：{start.ElapsedMilliseconds} ms");
    }
}
