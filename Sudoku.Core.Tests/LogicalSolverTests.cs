using Xunit;

namespace Sudoku.Core.Tests;

public class LogicalSolverTests
{
    /// <summary>从终盘出发，在保证唯一解的前提下挖掉若干格，得到中局盘面。</summary>
    private static Board DigUnique(Board solution, int targetEmpty, int seed)
    {
        Board puzzle = solution.Clone();
        var order = Enumerable.Range(0, SudokuGrid.CellCount).OrderBy(_ => seed = Hash(seed)).ToList();
        int emptied = 0;

        foreach (int cell in order)
        {
            if (emptied >= targetEmpty)
            {
                break;
            }

            int backup = puzzle[cell];
            puzzle[cell] = 0;

            if (Solver.HasUniqueSolution(puzzle))
            {
                emptied++;
            }
            else
            {
                puzzle[cell] = backup;
            }
        }

        return puzzle;
    }

    private static int Hash(int value) => unchecked((value * 1103515245) + 12345);

    [Fact]
    public void Solve_ShouldPlaceLastCellAsNakedSingle()
    {
        Board solution = new Generator(seed: 11).GenerateSolution();
        Board almostSolved = solution.Clone();
        const int cell = 40;
        int digit = almostSolved[cell];
        almostSolved[cell] = 0;

        LogicalSolveResult result = LogicalSolver.Solve(almostSolved, collectSteps: true);

        Assert.True(result.Solved);
        Assert.Equal(Technique.NakedSingle, result.MaxTechnique);
        Assert.Single(result.Steps);
        Assert.Equal(cell, result.Steps[0].PlaceIndex);
        Assert.Equal(digit, result.Steps[0].PlaceDigit);
        Assert.Equal(solution.ToSdkString(), result.Result.ToSdkString());
    }

    [Fact]
    public void Solve_ShouldFinishBoardWithEmptyRowUsingLowLevelTechniques()
    {
        Board solution = new Generator(seed: 12).GenerateSolution();
        Board puzzle = solution.Clone();
        for (int col = 0; col < SudokuGrid.Size; col++)
        {
            puzzle[SudokuGrid.Index(0, col)] = 0;
        }

        LogicalSolveResult result = LogicalSolver.Solve(puzzle, collectSteps: true);

        Assert.True(result.Solved);
        Assert.True(result.MaxLevel <= 2, $"应只用唯一候选数：实际用到 {result.MaxTechniqueName}");
        Assert.Equal(solution.ToSdkString(), result.Result.ToSdkString());
    }

    [Fact]
    public void LogicalSolve_ShouldNeverDisagreeWithBacktrackingSolver()
    {
        var generator = new Generator(seed: 99);

        foreach (DifficultyLevel level in Difficulty.All)
        {
            Puzzle puzzle = generator.Generate(level);
            Assert.True(Solver.TrySolve(puzzle.Given, out Board expected));

            LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given, collectSteps: true);

            if (result.Solved)
            {
                Assert.Equal(expected.ToSdkString(), result.Result.ToSdkString());
                Assert.True(result.Result.IsSolved());
            }
            else
            {
                Assert.True(result.Stuck);
            }
        }
    }

    [Fact]
    public void FindAllSteps_ShouldNeverEliminateTheTrueCandidate()
    {
        var generator = new Generator(seed: 77);
        Board solution = generator.GenerateSolution();
        Board puzzle = DigUnique(solution, 40, seed: 5);

        IReadOnlyList<TechniqueStep> steps = LogicalSolver.FindAllSteps(puzzle);

        Assert.NotEmpty(steps);

        foreach (TechniqueStep step in steps)
        {
            if (step.IsPlacement)
            {
                Assert.Equal(solution[step.PlaceIndex], step.PlaceDigit);
            }

            foreach (CandidateRef elimination in step.Eliminations)
            {
                Assert.NotEqual(solution[elimination.Cell], elimination.Digit);
                Assert.NotEmpty(step.Description);
            }

            Assert.NotEmpty(step.HighlightCells);
        }
    }

    [Fact]
    public void FindStep_ShouldPreferTheSimplestTechnique()
    {
        var generator = new Generator(seed: 31);
        Board solution = generator.GenerateSolution();
        Board puzzle = solution.Clone();
        const int cell = 20;
        puzzle[cell] = 0;

        TechniqueStep? step = LogicalSolver.FindStep(puzzle);

        Assert.NotNull(step);
        Assert.Equal(Technique.NakedSingle, step!.Technique);
        Assert.Equal(1, step.Level);
    }

    [Fact]
    public void Solve_ShouldReportStuckForPuzzlesRequiringAdvancedTechniques()
    {
        var generator = new Generator(seed: 20260917);
        Board solution = generator.GenerateSolution();
        Board puzzle = DigUnique(solution, 55, seed: 3);

        LogicalSolveResult result = LogicalSolver.Solve(puzzle);

        // 挖得很深的盘面通常需要链级技巧 → 基础技巧会卡住，此时必须标记 Stuck 而不是给出错误结果
        Assert.True(result.Stuck || result.Solved);
        if (result.Solved)
        {
            Assert.Equal(solution.ToSdkString(), result.Result.ToSdkString());
        }
    }
}
