using Sudoku.Core;
using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>
/// 十七数题库（阶段九）的验收：
/// 每一道内置母题都必须「恰好 17 个提示数 + 唯一解 + 纯逻辑可解 + 必须用到高阶技巧」，
/// 也就是难度系数不低于大师档。
/// </summary>
public class SeventeenCluesTests
{
    /// <summary>大师档的最低分：进阶技巧里分值最低的远程数对。</summary>
    private static readonly double MasterFloor = DifficultyRating.BaseScore(Technique.RemotePair);

    [Fact]
    public void Bases_ShouldAllBeSeventeenClueUniquePuzzles()
    {
        Assert.True(SeventeenClues.IsAvailable);
        Assert.True(SeventeenClues.Bases.Count >= 12, $"只找到 {SeventeenClues.Bases.Count} 道母题，太少。");

        foreach (Board basis in SeventeenClues.Bases)
        {
            Assert.Equal(SeventeenClues.ClueCount, basis.FilledCount);
            Assert.Equal(SeventeenClues.ClueCount, basis.ToSdkString().Count(char.IsDigit));
            Assert.True(basis.IsValid());
            Assert.True(Solver.HasUniqueSolution(basis), $"母题不是唯一解：{basis.ToSdkString()}");
        }
    }

    [Fact]
    public void Bases_ShouldAllRequireAdvancedTechniques()
    {
        foreach (Board basis in SeventeenClues.Bases)
        {
            string sdk = basis.ToSdkString();
            LogicalSolveResult result = LogicalSolver.Solve(basis, collectSteps: true);
            RatingReport report = DifficultyRating.Rate(result);

            Assert.True(result.Solved, $"母题不是纯逻辑可解：{sdk}");
            Assert.True(result.UsesAdvancedTechnique, $"母题没用到高阶技巧：{sdk}（最难={report.HardestName}）");
            Assert.True(Difficulty.IsAdvancedTechnique(report.Hardest), $"母题最难一步不是高阶技巧：{sdk}");
            Assert.True(
                report.Score >= MasterFloor,
                $"母题难度 {report.Score:0.0} 低于大师档下限 {MasterFloor:0.0}：{sdk}");
            // 按技巧评，这些题面就是大师档；DifficultyRating.Rate(Board) 另外会把
            // 恰好 17 提示数的题目归到「十七数」档（见下面的断言）
            Assert.Equal(DifficultyLevel.Master, report.Level);
            Assert.True(
                result.MaxLevel <= SeventeenClues.MaxTechniqueLevel,
                $"母题用到级 {result.MaxLevel}，超出提示上限 {SeventeenClues.MaxTechniqueLevel}：{sdk}");
        }

        Assert.Equal(DifficultyLevel.Seventeen, DifficultyRating.Rate(SeventeenClues.Bases[0]).Level);
    }

    [Fact]
    public void Bases_ShouldCoverMultipleAdvancedTechniques()
    {
        var techniques = new HashSet<Technique>();
        foreach (Board basis in SeventeenClues.Bases)
        {
            LogicalSolveResult result = LogicalSolver.Solve(basis, collectSteps: true);
            Assert.True(result.Solved);
            foreach (TechniqueStep step in result.Steps)
            {
                if (Difficulty.IsAdvancedTechnique(step.Technique))
                {
                    techniques.Add(step.Technique);
                }
            }
        }

        Assert.True(techniques.Count >= 4, "十七数母题覆盖的高阶技巧太少：" + string.Join("、", techniques.Select(TechniqueInfo.Name)));
        Assert.Contains(Technique.AlsChain, techniques);
        Assert.Contains(Technique.AlsXz, techniques);
    }

    [Fact]
    public void Create_ShouldProduceHardSeventeenCluePuzzles()
    {
        var random = new Random(20260117);

        for (int i = 0; i < 12; i++)
        {
            Board puzzle = SeventeenClues.Create(random);
            string sdk = puzzle.ToSdkString();

            Assert.Equal(SeventeenClues.ClueCount, puzzle.FilledCount);
            Assert.Equal(SeventeenClues.ClueCount, sdk.Count(char.IsDigit));
            Assert.True(Solver.HasUniqueSolution(puzzle), $"派生题面不是唯一解：{sdk}");
            Assert.True(SeventeenClues.RequiresAdvancedTechnique(puzzle), $"派生题面不需要高阶技巧：{sdk}");

            LogicalSolveResult result = LogicalSolver.Solve(puzzle, collectSteps: true);
            RatingReport report = DifficultyRating.Rate(result);
            Assert.True(report.Score >= MasterFloor, $"派生题面难度 {report.Score:0.0} 低于大师档：{sdk}");
        }
    }

    [Fact]
    public void Transform_ShouldKeepDifficultyAndClueCount()
    {
        var random = new Random(99);
        Board basis = SeventeenClues.Bases[0];
        int expected = LogicalSolver.Solve(basis, collectSteps: true).MaxLevel;

        for (int i = 0; i < 5; i++)
        {
            Board transformed = SeventeenClues.Transform(basis, random);
            Assert.Equal(SeventeenClues.ClueCount, transformed.FilledCount);
            Assert.True(Solver.HasUniqueSolution(transformed));

            LogicalSolveResult result = LogicalSolver.Solve(transformed, collectSteps: true);
            Assert.Equal(expected, result.MaxLevel);
            Assert.True(result.UsesAdvancedTechnique);
        }
    }

    [Fact]
    public void Generate_Seventeen_ShouldBeAtLeastMasterDifficulty()
    {
        var generator = new Generator(seed: 4242);

        for (int i = 0; i < 3; i++)
        {
            Puzzle puzzle = generator.Generate(DifficultyLevel.Seventeen);

            Assert.Equal(DifficultyLevel.Seventeen, puzzle.Level);
            Assert.Equal(SeventeenClues.ClueCount, puzzle.ClueCount);
            Assert.True(puzzle.Score >= MasterFloor, $"生成出来的十七数只有 {puzzle.Score:0.0} 分。");
            Assert.True(
                puzzle.TechniqueLevel >= TechniqueInfo.Level(Technique.RemotePair),
                $"十七数用到的最高技巧只有 {puzzle.TechniqueLevel} 级，达不到大师档。");
            Assert.Contains("十七数", puzzle.DifficultyText);
        }
    }

    /// <summary>
    /// 反向把关：光有 17 个提示数还不够，推得动就不算「十七数」。
    /// 公开目录里这类题占绝大多数，如果只看提示数个数，就会出现「十七数 · 2.3」这种名不副实的评分。
    /// </summary>
    [Fact]
    public void EasySeventeenCluePuzzle_ShouldNotBeRatedAsSeventeen()
    {
        Board classic = Board.Parse("000000010400000000020000000000050407008000300001090000300400200050100000000806000");

        Assert.Equal(SeventeenClues.ClueCount, classic.FilledCount);
        Assert.True(Solver.HasUniqueSolution(classic));
        Assert.False(SeventeenClues.RequiresAdvancedTechnique(classic));

        RatingReport report = DifficultyRating.Rate(classic);
        Assert.Equal(DifficultyLevel.Easy, report.Level);
        Assert.True(report.Score < MasterFloor, $"这道经典母题只该值 {report.Score:0.0} 分。");
    }
}
