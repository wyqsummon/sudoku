using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>SER 风格难度评分与「专家」档的测试。</summary>
public class DifficultyRatingTests
{
    private static int Hash(int value) => unchecked((value * 1103515245) + 12345);

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

    [Fact]
    public void BaseScore_ShouldFollowTechniqueDifficulty()
    {
        // 基础技巧：越往后越高
        Assert.True(DifficultyRating.BaseScore(Technique.HiddenSingle) < DifficultyRating.BaseScore(Technique.NakedSingle));
        Assert.True(DifficultyRating.BaseScore(Technique.NakedSingle) < DifficultyRating.BaseScore(Technique.NakedPair));
        Assert.True(DifficultyRating.BaseScore(Technique.NakedPair) < DifficultyRating.BaseScore(Technique.HiddenTriple));
        Assert.True(DifficultyRating.BaseScore(Technique.HiddenTriple) < DifficultyRating.BaseScore(Technique.XYWing));

        // 链级技巧高于所有基础技巧
        foreach (Technique technique in new[] { Technique.XChain, Technique.XYChain, Technique.Aic })
        {
            Assert.True(DifficultyRating.BaseScore(technique) >= 5.0, $"{TechniqueInfo.Name(technique)} 的基础分应 ≥ 5.0");
        }

        Assert.True(DifficultyRating.BaseScore(Technique.XChain) < DifficultyRating.BaseScore(Technique.XYChain));
        Assert.True(DifficultyRating.BaseScore(Technique.XYChain) < DifficultyRating.BaseScore(Technique.Aic));
    }

    [Fact]
    public void StepScore_ShouldRewardLongerChainsAndMoreEliminations()
    {
        var single = new TechniqueStep(
            Technique.NakedSingle, new[] { 0 }, 0, 5, Array.Empty<CandidateRef>(), "说明",
            Array.Empty<TechniqueLink>(), Array.Empty<string>());

        var shortChain = new TechniqueStep(
            Technique.XChain, new[] { 0 }, -1, 0, new[] { new CandidateRef(4, 5) }, "说明",
            new[] { Link(0), Link(1), Link(2) }, Array.Empty<string>());

        var longChain = new TechniqueStep(
            Technique.XChain, new[] { 0 }, -1, 0, new[] { new CandidateRef(4, 5) }, "说明",
            Enumerable.Range(0, 9).Select(Link).ToArray(), Array.Empty<string>());

        Assert.Equal(2.3, DifficultyRating.StepScore(single));
        Assert.True(DifficultyRating.StepScore(longChain) > DifficultyRating.StepScore(shortChain));
        Assert.True(DifficultyRating.StepScore(shortChain) > DifficultyRating.BaseScore(Technique.XChain) - 0.001);
    }

    private static TechniqueLink Link(int index) => new(
        new CandidateRef(index, 5), new CandidateRef(index + 9, 5), index % 2 == 0, "理由");

    [Fact]
    public void Rate_ShouldTakeHardestStepAsScore()
    {
        var generator = new Generator(seed: 2024);
        Board solution = generator.GenerateSolution();

        // 挖得很浅 → 只需唯一候选数 → 简单档
        Board easy = DigUnique(solution, 8, seed: 5);
        RatingReport easyReport = DifficultyRating.Rate(easy);

        Assert.True(easyReport.Solved);
        Assert.Equal(DifficultyLevel.Easy, easyReport.Level);
        Assert.True(easyReport.Score <= 2.3, $"简单题目评分应 ≤ 2.3，实际 {easyReport.Score}");
        Assert.True(easyReport.StepCount > 0);

        // 深挖后总能找到需要更高技巧的题目；评分必须严格高于浅挖的那道
        RatingReport? harderReport = null;
        for (int i = 0; i < 10 && harderReport is null; i++)
        {
            RatingReport candidate = DifficultyRating.Rate(DigUnique(solution, 58, seed: 40 + i));
            if ((int)candidate.Level >= (int)DifficultyLevel.Medium)
            {
                harderReport = candidate;
            }
        }

        Assert.NotNull(harderReport);
        Assert.True(harderReport!.Score > easyReport.Score, "更难题目的评分必须更高");
        Assert.NotEqual(Technique.HiddenSingle, harderReport.Hardest);
    }

    [Fact]
    public void Rate_ShouldGradeChainPuzzlesAsExpert()
    {
        var generator = new Generator(seed: 31337);
        bool foundExpert = false;

        for (int i = 0; i < 12 && !foundExpert; i++)
        {
            Board solution = generator.GenerateSolution();
            Board puzzle = DigUnique(solution, 58, seed: 300 + i);
            RatingReport report = DifficultyRating.Rate(puzzle);

            if (report.Level != DifficultyLevel.Expert)
            {
                continue;
            }

            foundExpert = true;
            Assert.True(report.Score >= 5.0, $"专家题评分应 ≥ 5.0，实际 {report.Score}");
            Assert.True(
                TechniqueInfo.IsChain(report.Hardest) || report.Hardest == Technique.None,
                $"专家题最难一步应是链级技巧，实际 {report.HardestName}");
        }

        Assert.True(foundExpert, "在 12 次深挖中应当出现至少一道需要链级技巧的题");
    }

    [Fact]
    public void Generator_ShouldProduceExpertPuzzles()
    {
        var generator = new Generator(seed: 777);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Expert, maxAttempts: 12);

        Assert.Equal(DifficultyLevel.Expert, puzzle.Level);
        Assert.True(puzzle.Score >= 5.0, $"专家题评分应 ≥ 5.0，实际 {puzzle.Score}");
        Assert.True(puzzle.TechniqueLevel >= 7, $"专家题需要链级技巧，实际等级 {puzzle.TechniqueLevel}");
        Assert.True(Solver.HasUniqueSolution(puzzle.Given));
        Assert.Contains("专家", puzzle.DifficultyText, StringComparison.Ordinal);

        // 关键：专家题必须能纯逻辑解出，否则分段式提示会「没有可用技巧」
        Assert.True(LogicalSolver.Solve(puzzle.Given).Solved, "专家题应当能用已实现的技巧解完");
    }

    [Fact]
    public void Generator_ShouldPreferLogicallySolvablePuzzlesOnEveryLevel()
    {
        foreach (DifficultyLevel level in Difficulty.All)
        {
            var generator = new Generator(seed: 5000 + (int)level);
            Puzzle puzzle = generator.Generate(level, maxAttempts: 12);

            Assert.True(
                LogicalSolver.Solve(puzzle.Given).Solved,
                $"{Difficulty.Name(level)} 档生成的题目应当能纯逻辑解出（实际评分 {puzzle.Score:0.0}）");

            Assert.True(Solver.HasUniqueSolution(puzzle.Given), "题目必须唯一解");
            Assert.True(puzzle.Score > 0, "评分应当已计算");
        }
    }

    [Fact]
    public void Difficulty_ShouldNameAndDescribeEveryLevel()
    {
        foreach (DifficultyLevel level in Difficulty.All)
        {
            Assert.NotEmpty(Difficulty.Name(level));
            Assert.NotEmpty(Difficulty.Description(level));
        }

        Assert.Equal("专家", Difficulty.Name(DifficultyLevel.Expert));
        Assert.Equal("大师", Difficulty.Name(DifficultyLevel.Master));
        Assert.Equal("十七数", Difficulty.Name(DifficultyLevel.Seventeen));
        Assert.Equal(6, Difficulty.All.Count);
    }
}
