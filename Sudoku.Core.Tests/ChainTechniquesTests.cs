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

    /// <summary>用「格 → 候选数」清单构造候选数掩码，便于对链级搜索做确定性测试。</summary>
    private static int[] Masks(params (int Cell, int Digit)[] candidates)
    {
        var masks = new int[SudokuGrid.CellCount];
        foreach ((int cell, int digit) in candidates)
        {
            masks[cell] |= SudokuGrid.DigitBit(digit);
        }

        return masks;
    }

    [Fact]
    public void FindXChain_ShouldDetectConstructedChain()
    {
        // 合成候选数图（数字 1）：
        //   R1C1 == R1C4（第 1 行只剩两处，强链）
        //   R1C4 -- R7C4（第 4 列上不能同真，弱链）
        //   R7C4 == R7C1（第 7 行只剩两处，强链）
        // 两端为 R1C1 与 R7C1，同列，因此 R4C1 同时看见两端 → 可删除。
        int start = SudokuGrid.Index(0, 0);
        int a = SudokuGrid.Index(0, 3);
        int b = SudokuGrid.Index(6, 3);
        int end = SudokuGrid.Index(6, 0);
        int victim = SudokuGrid.Index(3, 0);
        int extra = SudokuGrid.Index(3, 3); // 让第 4 列不止两个候选数，使 a--b 只是弱链

        int[] masks = Masks(
            (start, 1),
            (a, 1),
            (b, 1),
            (end, 1),
            (victim, 1),
            (extra, 1));

        List<TechniqueStep> steps = ChainSearch.FindXChains(masks).ToList();

        Assert.NotEmpty(steps);

        TechniqueStep? step = steps.FirstOrDefault(s => s.Eliminations.Any(e => e.Cell == victim && e.Digit == 1));
        Assert.NotNull(step);
        Assert.Equal(Technique.XChain, step!.Technique);
        Assert.Equal(7, step.Level);
        Assert.NotEmpty(step.DerivationSteps);

        // 全链同一数字，强弱交替且两端为强链
        Assert.All(step.LinkList, link =>
        {
            Assert.Equal(1, link.From.Digit);
            Assert.Equal(1, link.To.Digit);
            Assert.NotEmpty(link.Reason);
        });

        for (int k = 1; k < step.LinkList.Count; k++)
        {
            Assert.NotEqual(step.LinkList[k - 1].IsStrong, step.LinkList[k].IsStrong);
        }

        Assert.True(step.LinkList[0].IsStrong);
        Assert.True(step.LinkList[^1].IsStrong);
        Assert.Equal(3, step.LinkList.Count);
    }

    [Fact]
    public void FindXYChain_ShouldDetectConstructedChain()
    {
        // 合成候选数图：四个双值格串联
        //   R1C1{1,2} -- R1C4{2,3} -- R5C4{3,4} -- R5C7{4,1}
        // 两端都是数字 1，R1C7{1,9} 同时看见 R1C1（同行）与 R5C7（同列）→ 可删除 1。
        int c1 = SudokuGrid.Index(0, 0);
        int c2 = SudokuGrid.Index(0, 3);
        int c3 = SudokuGrid.Index(4, 3);
        int c4 = SudokuGrid.Index(4, 6);
        int victim = SudokuGrid.Index(0, 6);

        int[] masks = Masks(
            (c1, 1), (c1, 2),
            (c2, 2), (c2, 3),
            (c3, 3), (c3, 4),
            (c4, 4), (c4, 1),
            (victim, 1), (victim, 9));

        List<TechniqueStep> steps = ChainSearch.FindXYChains(masks).ToList();

        Assert.NotEmpty(steps);

        TechniqueStep? step = steps.FirstOrDefault(s => s.Eliminations.Any(e => e.Cell == victim && e.Digit == 1));
        Assert.NotNull(step);
        Assert.Equal(Technique.XYChain, step!.Technique);
        Assert.Equal(8, step.Level);
        Assert.NotEmpty(step.DerivationSteps);

        // 每个节点都必须落在双值格内
        Assert.All(step.LinkList, link =>
        {
            Assert.Equal(2, SudokuGrid.CountDigits(masks[link.From.Cell]));
            Assert.Equal(2, SudokuGrid.CountDigits(masks[link.To.Cell]));
        });

        for (int k = 1; k < step.LinkList.Count; k++)
        {
            Assert.NotEqual(step.LinkList[k - 1].IsStrong, step.LinkList[k].IsStrong);
        }

        Assert.True(step.LinkList[0].IsStrong);
        Assert.True(step.LinkList[^1].IsStrong);
    }

    [Fact]
    public void ChainSearch_ShouldFindChainsOnBoardsWhereBasicsGetStuck()
    {
        var generator = new Generator(seed: 20260101);
        var counts = new Dictionary<Technique, int>();
        int stuck = 0;

        for (int i = 0; i < 16; i++)
        {
            Board solution = generator.GenerateSolution();
            Board puzzle = DigUnique(solution, 62, seed: 900 + i);

            // 先用基础技巧（等级 ≤5）推进，卡住后再让链级技巧接手
            LogicalSolveResult result = LogicalSolver.Solve(puzzle, collectSteps: true, maxLevel: 5);
            if (result.Stuck)
            {
                stuck++;
            }

            Board state = result.Result;

            IEnumerable<TechniqueStep> chains = ChainSearch.FindXChains(state)
                .Concat(ChainSearch.FindXYChains(state))
                .Concat(ChainSearch.FindAics(state));

            foreach (TechniqueStep step in chains)
            {
                counts[step.Technique] = counts.GetValueOrDefault(step.Technique) + 1;

                Assert.True(step.Level >= 7);
                Assert.NotEmpty(step.LinkList);
                Assert.True(step.DerivationSteps.Count >= 3, "链级技巧必须给出台阶式推导");
                Assert.All(step.LinkList, link => Assert.NotEmpty(link.Reason));

                // 强弱严格交替，且两端都是强链
                for (int k = 1; k < step.LinkList.Count; k++)
                {
                    Assert.NotEqual(step.LinkList[k - 1].IsStrong, step.LinkList[k].IsStrong);
                }

                Assert.True(step.LinkList[0].IsStrong, "链必须以强链开头");
                Assert.True(step.LinkList[^1].IsStrong, "链必须以强链结尾");

                foreach (CandidateRef elimination in step.Eliminations)
                {
                    Assert.True(
                        solution[elimination.Cell] != elimination.Digit,
                        $"误删真实候选：技巧「{TechniqueInfo.Name(step.Technique)}」删除了 {elimination}，" +
                        $"但该格答案是 {solution[elimination.Cell]}。说明：{step.Description}");
                }
            }
        }

        string report = string.Join(", ", counts.Select(kv => $"{TechniqueInfo.Name(kv.Key)}={kv.Value}"));

        Assert.True(stuck > 0, "样本里应当出现基础技巧卡住的盘面");
        Assert.True(counts.Values.Sum() > 0, $"卡住盘面上未命中任何链式技巧。统计：{report}");
    }

    [Fact]
    public void ChainSearch_ShouldStayFastEnoughForInteractiveHints()
    {
        var generator = new Generator(seed: 31415);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int hints = 0;

        for (int i = 0; i < 5; i++)
        {
            Board solution = generator.GenerateSolution();
            Board puzzle = DigUnique(solution, 58, seed: 2000 + i);

            // 提示路径：每次只找一步（命中即停）
            TechniqueStep? step = LogicalSolver.FindStep(puzzle, maxLevel: 9);
            if (step is not null)
            {
                hints++;
                foreach (CandidateRef elimination in step.Eliminations)
                {
                    Assert.NotEqual(solution[elimination.Cell], elimination.Digit);
                }
            }
        }

        watch.Stop();

        Assert.True(hints > 0, "深挖盘面上应至少能给出一步提示");
        Assert.True(
            watch.ElapsedMilliseconds < 8000,
            $"单步提示太慢：5 次共 {watch.ElapsedMilliseconds} ms，交互式提示需要更快");
    }
}
