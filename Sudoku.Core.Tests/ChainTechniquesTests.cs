using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>阶段二链级技巧的测试：图案构造 + 与真实答案一致性的不变量检查。</summary>
public class ChainTechniquesTests
{
    private static int Hash(int value) => unchecked((value * 1103515245) + 12345);

    /// <summary>从终盘出发挖掉若干格（保证唯一解），得到中局盘面。</summary>
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
    public void FindXWing_ShouldDetectConstructedPattern()
    {
        var board = new Board();

        // 用「列阻挡」把数字 5 逼到第 1、4 列：第 2、3、5、6、7、8、9 列各放一个 5
        board[SudokuGrid.Index(6, 1)] = 5; // R7C2
        board[SudokuGrid.Index(7, 2)] = 5; // R8C3
        board[SudokuGrid.Index(3, 4)] = 5; // R4C5
        board[SudokuGrid.Index(4, 5)] = 5; // R5C6
        board[SudokuGrid.Index(5, 6)] = 5; // R6C7
        board[SudokuGrid.Index(6, 7)] = 5; // R7C8
        board[SudokuGrid.Index(7, 8)] = 5; // R8C9

        // 此时第 1、2 行的 5 只可能出现在 C1 与 C4 → 构成 X 翼
        List<TechniqueStep> steps = ChainTechniques.FindXWings(board).ToList();

        Assert.NotEmpty(steps);

        TechniqueStep xwing = steps[0];
        Assert.Equal(Technique.XWing, xwing.Technique);
        Assert.Equal(6, xwing.Level);
        Assert.NotEmpty(xwing.Eliminations);
        Assert.NotEmpty(xwing.Description);
        Assert.NotEmpty(xwing.DerivationSteps);
        Assert.NotEmpty(xwing.LinkList);
        Assert.All(xwing.LinkList, link => Assert.True(link.IsStrong, "X 翼内部的推断都是强链"));

        foreach (CandidateRef elimination in xwing.Eliminations)
        {
            Assert.Equal(5, elimination.Digit);
            Assert.Contains(SudokuGrid.Col(elimination.Cell), new[] { 0, 3 });
            Assert.DoesNotContain(SudokuGrid.Row(elimination.Cell), new[] { 0, 1 });
        }

        // 四个角格必须被高亮，便于界面画线
        Assert.Contains(SudokuGrid.Index(0, 0), xwing.HighlightCells);
        Assert.Contains(SudokuGrid.Index(0, 3), xwing.HighlightCells);
        Assert.Contains(SudokuGrid.Index(1, 0), xwing.HighlightCells);
        Assert.Contains(SudokuGrid.Index(1, 3), xwing.HighlightCells);
    }

    [Fact]
    public void AdvancedTechniques_ShouldBeSoundAndActuallyUsed()
    {
        var generator = new Generator(seed: 4242);
        var counts = new Dictionary<Technique, int>();
        int puzzles = 0;
        int solved = 0;

        for (int i = 0; i < 12; i++)
        {
            Board solution = generator.GenerateSolution();
            Board puzzle = DigUnique(solution, 56, seed: 100 + i);
            puzzles++;

            LogicalSolveResult result = LogicalSolver.Solve(puzzle, collectSteps: true);

            foreach (TechniqueStep step in result.Steps)
            {
                counts[step.Technique] = counts.GetValueOrDefault(step.Technique) + 1;

                if (step.IsPlacement)
                {
                    Assert.Equal(solution[step.PlaceIndex], step.PlaceDigit);
                }

                foreach (CandidateRef elimination in step.Eliminations)
                {
                    Assert.True(
                        solution[elimination.Cell] != elimination.Digit,
                        $"误删真实候选：技巧「{TechniqueInfo.Name(step.Technique)}」(等级 {step.Level}) 删除了 {elimination}，" +
                        $"但该格答案是 {solution[elimination.Cell]}。说明：{step.Description}");
                }

                Assert.NotEmpty(step.HighlightCells);
                Assert.NotEmpty(step.Description);

                if (step.Level >= 6)
                {
                    Assert.NotEmpty(step.DerivationSteps);
                }
            }

            if (result.Solved)
            {
                solved++;
                Assert.Equal(solution.ToSdkString(), result.Result.ToSdkString());
            }
        }

        int advanced = counts
            .Where(kv => kv.Key is Technique.XWing or Technique.Swordfish or Technique.XYWing)
            .Sum(kv => kv.Value);

        string report = string.Join(", ", counts.Select(kv => $"{TechniqueInfo.Name(kv.Key)}={kv.Value}"));
        Assert.True(advanced > 0, $"{puzzles} 个深挖盘面（解出 {solved} 个）未命中任何链级技巧。统计：{report}");

        // 顺带留痕：链级技巧的分布会随出题器与引擎演进变化，这里只要求"确实用上了"
        Assert.True(counts.Count > 0, "求解过程没有任何技巧步骤");
    }

    [Fact]
    public void FindAllSteps_ShouldIncludeChainLevelTechniquesWhenRequested()
    {
        var generator = new Generator(seed: 8888);
        Board solution = generator.GenerateSolution();
        Board puzzle = DigUnique(solution, 58, seed: 7);

        IReadOnlyList<TechniqueStep> withChains = LogicalSolver.FindAllSteps(puzzle, maxLevel: 9);
        IReadOnlyList<TechniqueStep> withoutChains = LogicalSolver.FindAllSteps(puzzle, maxLevel: 5);

        foreach (TechniqueStep step in withoutChains)
        {
            Assert.True(step.Level <= 5);
        }

        foreach (TechniqueStep step in withChains.Where(s => s.Level >= 6))
        {
            Assert.NotEqual(0, (int)step.Technique);
            Assert.NotEmpty(TechniqueInfo.Name(step.Technique));
            Assert.NotEmpty(TechniqueInfo.Summary(step.Technique));
        }

        // 所有找到的步骤都不能与答案冲突
        foreach (TechniqueStep step in withChains)
        {
            foreach (CandidateRef elimination in step.Eliminations)
            {
                Assert.NotEqual(solution[elimination.Cell], elimination.Digit);
            }
        }
    }
}
