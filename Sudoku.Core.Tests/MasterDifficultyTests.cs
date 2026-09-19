using Sudoku.Core;
using Xunit;
using Xunit.Abstractions;

namespace Sudoku.Core.Tests;

/// <summary>「大师」档（阶段五新增难度）：用到高阶技巧才算，且低难度不得混入高阶技巧。</summary>
public sealed class MasterDifficultyTests
{
    private readonly ITestOutputHelper _output;

    public MasterDifficultyTests(ITestOutputHelper output) => _output = output;

    private static TechniqueStep Step(Technique technique) =>
        new(technique, Array.Empty<int>(), -1, 0, Array.Empty<CandidateRef>(), "测试步骤");

    private static LogicalSolveResult Result(Technique maxTechnique, int maxLevel, bool advanced, params Technique[] techniques) =>
        new(true, false, techniques.Select(Step).ToArray(), maxTechnique, maxLevel, new Board(), advanced);

    [Theory]
    [InlineData(Technique.RemotePair)]
    [InlineData(Technique.UniqueRectangle)]
    [InlineData(Technique.FinnedXWing)]
    [InlineData(Technique.FinnedSwordfish)]
    [InlineData(Technique.BugPlusOne)]
    public void IsAdvancedTechnique_ShouldCoverPhaseFourTechniques(Technique technique) =>
        Assert.True(Difficulty.IsAdvancedTechnique(technique));

    [Theory]
    [InlineData(Technique.HiddenSingle)]
    [InlineData(Technique.NakedPair)]
    [InlineData(Technique.XWing)]
    [InlineData(Technique.Swordfish)]
    [InlineData(Technique.XChain)]
    [InlineData(Technique.XYChain)]
    [InlineData(Technique.Aic)]
    public void IsAdvancedTechnique_ShouldExcludeOlderTechniques(Technique technique) =>
        Assert.False(Difficulty.IsAdvancedTechnique(technique));

    [Fact]
    public void FromSolve_ShouldMapAdvancedStepToMaster()
    {
        // 步骤里出现带鳍 X 翼 → 大师（真值取自 Steps，不依赖 UsesAdvancedTechnique 标记）
        LogicalSolveResult result = Result(Technique.FinnedXWing, 8, advanced: false, Technique.HiddenSingle, Technique.FinnedXWing);

        Assert.Equal(DifficultyLevel.Master, Difficulty.FromSolve(result));
    }

    [Fact]
    public void FromSolve_ShouldUseFlagWhenStepsAreNotCollected()
    {
        // collectSteps=false 时 Steps 为空，只能靠标记
        LogicalSolveResult result = Result(Technique.Aic, 9, advanced: true, Array.Empty<Technique>());

        Assert.Equal(DifficultyLevel.Master, Difficulty.FromSolve(result));
    }

    [Fact]
    public void FromSolve_ShouldKeepPureChainPuzzleExpert()
    {
        LogicalSolveResult result = Result(Technique.Aic, 9, advanced: false, Technique.HiddenSingle, Technique.XChain, Technique.Aic);

        Assert.Equal(DifficultyLevel.Expert, Difficulty.FromSolve(result));
    }

    [Theory]
    [InlineData(2, DifficultyLevel.Easy)]
    [InlineData(4, DifficultyLevel.Medium)]
    [InlineData(6, DifficultyLevel.Hard)]
    [InlineData(7, DifficultyLevel.Expert)]
    public void FromSolve_ShouldStillGradeLowerLevels(int maxLevel, DifficultyLevel expected)
    {
        LogicalSolveResult result = Result(Technique.XChain, maxLevel, advanced: false, Technique.HiddenSingle);

        Assert.Equal(expected, Difficulty.FromSolve(result));
    }

    [Fact]
    public void FromSolve_ShouldTreatUnsolvedPuzzleAsExpert()
    {
        var result = new LogicalSolveResult(false, true, Array.Empty<TechniqueStep>(), Technique.Aic, 9, new Board(), false);

        Assert.Equal(DifficultyLevel.Expert, Difficulty.FromSolve(result));
    }

    [Fact]
    public void All_ShouldEndWithMaster()
    {
        Assert.Equal(DifficultyLevel.Master, Difficulty.All[^2]);
        Assert.Equal(DifficultyLevel.Seventeen, Difficulty.All[^1]);
        Assert.Equal(6, Difficulty.All.Count);
        Assert.Equal("大师", Difficulty.Name(DifficultyLevel.Master));
        Assert.Equal("十七数", Difficulty.Name(DifficultyLevel.Seventeen));
        Assert.Contains("高阶", Difficulty.Description(DifficultyLevel.Master));
        Assert.Contains("17", Difficulty.Description(DifficultyLevel.Seventeen));
    }

    [Fact]
    public void Generator_Master_ShouldProduceAdvancedPuzzle()
    {
        var generator = new Generator(seed: 20260917);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Master, maxAttempts: 200);

        LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given, collectSteps: true);
        _output.WriteLine($"线索 {puzzle.ClueCount} / 技法等级 {result.MaxLevel} / 最难技巧 {result.MaxTechniqueName} / 档位 {Difficulty.Name(puzzle.Level)} / 分数 {puzzle.Score:0.0}");

        Assert.True(result.Solved, "大师档题目必须能纯逻辑解出，否则提示会没有技巧可用");
        Assert.True(result.UsesAdvancedTechnique, $"大师档题目必须用到高阶技巧，实际最难是 {result.MaxTechniqueName}（等级 {result.MaxLevel}）");
        Assert.Equal(DifficultyLevel.Master, puzzle.Level);
    }

    [Fact]
    public void Generator_Expert_ShouldStayFreeOfAdvancedTechniques()
    {
        var generator = new Generator(seed: 4242);
        Puzzle puzzle = generator.Generate(DifficultyLevel.Expert, maxAttempts: 40);

        LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given, collectSteps: true);
        _output.WriteLine($"专家档：线索 {puzzle.ClueCount} / 技法等级 {result.MaxLevel} / 最难技巧 {result.MaxTechniqueName} / 档位 {Difficulty.Name(puzzle.Level)}");

        Assert.False(result.UsesAdvancedTechnique, "专家档不应该需要阶段四高阶技巧（那是大师档的专属）");
        Assert.NotEqual(DifficultyLevel.Master, puzzle.Level);
    }

    [Fact]
    public void Generator_Master_SingleAttemptDistribution()
    {
        // 诊断用：同一出题器连续尝试，看单次尝试的档位分布（用于决定 maxAttempts）
        var generator = new Generator(seed: 20260917);
        var counts = new Dictionary<string, int>();
        int advanced = 0;
        const int samples = 12;
        for (int i = 0; i < samples; i++)
        {
            Puzzle puzzle = generator.Generate(DifficultyLevel.Master, maxAttempts: 1);
            LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given, collectSteps: true);
            string key = $"{Difficulty.Name(puzzle.Level)}/等级{result.MaxLevel}/{result.MaxTechniqueName}";
            counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
            if (result.UsesAdvancedTechnique)
            {
                advanced++;
            }
        }

        foreach (KeyValuePair<string, int> pair in counts.OrderByDescending(p => p.Value))
        {
            _output.WriteLine($"{pair.Key}：{pair.Value} 次");
        }

        _output.WriteLine($"单次尝试出现高阶技巧的比例：{advanced}/{samples}");
    }

    [Fact]
    public void Generator_Master_HitRateDiagnostics()
    {
        // 诊断用：统计多少个种子能稳定出大师档（不参与断言，只输出，便于调参）
        int hits = 0;
        const int samples = 3;
        for (int i = 0; i < samples; i++)
        {
            var generator = new Generator(seed: 9000 + i);
            Puzzle puzzle = generator.Generate(DifficultyLevel.Master, maxAttempts: 60);
            LogicalSolveResult result = LogicalSolver.Solve(puzzle.Given, collectSteps: true);
            bool hit = puzzle.Level == DifficultyLevel.Master && result.UsesAdvancedTechnique;
            if (hit)
            {
                hits++;
            }

            _output.WriteLine($"种子 {9000 + i}：线索 {puzzle.ClueCount} 等级 {result.MaxLevel} 技巧 {result.MaxTechniqueName} → {(hit ? "大师" : Difficulty.Name(puzzle.Level))}");
        }

        _output.WriteLine($"大师命中 {hits}/{samples}");
    }
}
