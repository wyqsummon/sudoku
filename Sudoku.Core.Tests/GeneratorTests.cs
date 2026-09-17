using Xunit;

namespace Sudoku.Core.Tests;

public class GeneratorTests
{
    public static TheoryData<DifficultyLevel> AllLevels => new()
    {
        DifficultyLevel.Easy,
        DifficultyLevel.Medium,
        DifficultyLevel.Hard,
    };

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Generate_ShouldProduceValidPuzzleWithUniqueSolution(DifficultyLevel level)
    {
        var generator = new Generator(seed: 2026);
        Puzzle puzzle = generator.Generate(level);

        Assert.True(puzzle.Given.IsValid());
        Assert.True(puzzle.Solution.IsSolved());
        Assert.True(Solver.HasUniqueSolution(puzzle.Given));
        Assert.True(Solver.TrySolve(puzzle.Given, out Board solved));
        Assert.Equal(puzzle.Solution.ToSdkString(), solved.ToSdkString());
        Assert.InRange(puzzle.ClueCount, 17, 60);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Generate_ShouldKeepRotationalSymmetry(DifficultyLevel level)
    {
        var generator = new Generator(seed: 4242, symmetric: true);
        Puzzle puzzle = generator.Generate(level);

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int mirror = SudokuGrid.CellCount - 1 - cell;
            Assert.Equal(puzzle.Given[cell] == 0, puzzle.Given[mirror] == 0);
        }
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Generate_ShouldHitRequestedLevelInMostAttempts(DifficultyLevel level)
    {
        int matches = 0;
        const int attempts = 4;

        for (int seed = 0; seed < attempts; seed++)
        {
            Puzzle puzzle = new Generator(seed: 1000 + (seed * 37)).Generate(level);
            if (puzzle.Level == level)
            {
                matches++;
            }
        }

        Assert.True(matches >= 3, $"难度 {Difficulty.Name(level)} 命中 {matches}/{attempts} 次，低于预期。");
    }

    [Fact]
    public void Generate_ShouldBeDeterministicForTheSameSeed()
    {
        Puzzle first = new Generator(seed: 555).Generate(DifficultyLevel.Medium);
        Puzzle second = new Generator(seed: 555).Generate(DifficultyLevel.Medium);

        Assert.Equal(first.Given.ToSdkString(), second.Given.ToSdkString());
        Assert.Equal(first.Solution.ToSdkString(), second.Solution.ToSdkString());
        Assert.Equal(first.Level, second.Level);
    }

    [Fact]
    public void Generate_HardPuzzleShouldNotBeSolvedBySinglesOnly()
    {
        var generator = new Generator(seed: 8888);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Hard);
        LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given);

        bool needsMoreThanSingles = !result.Solved || result.MaxLevel > 2;
        Assert.True(needsMoreThanSingles, "困难题目不应只用唯一候选数即可完成。");
    }

    [Fact]
    public void Generate_EasyPuzzleShouldBeSolvableWithSinglesOnly()
    {
        var generator = new Generator(seed: 13579);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Easy);
        LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given);

        Assert.True(result.Solved, "简单题目应能用基础技巧推导完成。");
        Assert.True(result.MaxLevel <= 2, $"简单题目用到了 {result.MaxTechniqueName}，偏难。");
    }

    [Fact]
    public void GenerateSolution_ShouldProduceDistinctGrids()
    {
        var generator = new Generator(seed: 606);
        string first = generator.GenerateSolution().ToSdkString();
        string second = generator.GenerateSolution().ToSdkString();

        Assert.NotEqual(first, second);
        Assert.True(Board.Parse(first).IsSolved());
        Assert.True(Board.Parse(second).IsSolved());
    }
}
