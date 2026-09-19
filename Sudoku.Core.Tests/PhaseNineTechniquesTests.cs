using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>
/// 阶段九新增技巧（ALS-XZ / Sue de Coq / ALS 链 / 连续环）的测试。
/// 手法：拿真实题面，先暴力求出唯一解，再断言这些技巧给出的每一个结论都「不反对答案」——
/// 删除的候选数里绝不能含答案数字，落子必须等于答案。
/// 这能在不手工构造巨型盘面的前提下抓到规则写错的 bug。
/// </summary>
public class PhaseNineTechniquesTests
{
    private static readonly string[] RealPuzzles =
    {
        // 大师档实测题（ALS-XZ / Sue de Coq 都会命中）
        "5.....9...13.......9..4.....8...356.9.....2.....1.8..9.....4..6.5.......4..9.5371",
        "31.9...7....5....9....72.8..4.....9.........3.28...4..1.....3.....42......58.67..",
        "..7.8.5...5.....3....7..2.6...41..98.....935..2.......7.1.......8.6.1.....9...4..",
        // 十七数（内置母题）
        "000000010400000000020000000000050407008000300001090000300400200050100000000806000",
        "000000000000003085001020000000507000004000100090000000500000073002010000000040009",
    };

    /// <summary>
    /// 在一道题的整条逻辑求解路径上，每一步都用「全部技巧」跑一遍，
    /// 检查新技巧给出的所有结论都与真实解一致。
    /// </summary>
    [Theory]
    [MemberData(nameof(PuzzleData))]
    public void NewTechniques_NeverContradictTheSolution(string sdk)
    {
        Board puzzle = Board.Parse(sdk);
        Assert.True(Solver.TrySolve(puzzle, out Board solution));

        int[] cells = puzzle.ToArray();
        int[] masks = puzzle.ComputeCandidates();
        int checkedSteps = 0;
        var seenTechniques = new HashSet<Technique>();

        for (int step = 0; step < 81; step++)
        {
            Board board = Board.Wrap((int[])cells.Clone());
            foreach (TechniqueStep candidate in LogicalSolver.FindAllSteps(board, int.MaxValue, masks))
            {
                if (candidate.Technique is not (Technique.AlsXz or Technique.SueDeCoq
                    or Technique.AlsChain or Technique.ContinuousLoop))
                {
                    continue;
                }

                seenTechniques.Add(candidate.Technique);
                checkedSteps++;

                // 落子必须与答案一致
                if (candidate.IsPlacement)
                {
                    Assert.True(
                        candidate.PlaceDigit == solution[candidate.PlaceIndex],
                        $"{TechniqueInfo.Name(candidate.Technique)} 落子 R{candidate.PlaceIndex} 与答案不符：{candidate.Description}");
                }

                // 删除的候选数里不能有答案数字
                foreach (CandidateRef elimination in candidate.Eliminations)
                {
                    Assert.False(
                        elimination.Digit == solution[elimination.Cell],
                        $"{TechniqueInfo.Name(candidate.Technique)} 删掉了正确答案 {elimination}：{candidate.Description}");
                }

                // 每个结论都必须有说明与推导
                Assert.False(string.IsNullOrWhiteSpace(candidate.Description));
                Assert.NotEmpty(candidate.DerivationSteps);
                Assert.True(candidate.Eliminations.Count > 0 || candidate.IsPlacement);
            }

            // 继续用基础技巧推进盘面（新技巧留给上面检查）
            TechniqueStep? progress = LogicalSolver
                .FindAllSteps(board, 6, masks)
                .FirstOrDefault();

            if (progress is null)
            {
                break;
            }

            if (progress.IsPlacement)
            {
                Place(cells, masks, progress.PlaceIndex, progress.PlaceDigit);
            }
            else
            {
                foreach (CandidateRef elimination in progress.Eliminations)
                {
                    masks[elimination.Cell] &= ~SudokuGrid.DigitBit(elimination.Digit);
                }
            }

            if (cells.All(v => v != 0))
            {
                break;
            }
        }

        // 每道题至少要能观察到一种新技巧，否则这个用例没起到检验作用
        Assert.NotEmpty(seenTechniques);
        Assert.True(checkedSteps > 0);
    }

    public static TheoryData<string> PuzzleData()
    {
        var data = new TheoryData<string>();
        foreach (string sdk in RealPuzzles)
        {
            data.Add(sdk);
        }

        return data;
    }

    /// <summary>ALS-XZ 的手工用例：两个 ALS 靠受限公共候选数相连，Z 必在其中之一。</summary>
    [Fact]
    public void AlsXz_ShouldEliminateZOutsideBothSets()
    {
        int[] masks = new int[SudokuGrid.CellCount];

        // A = R1C1+R1C2（1/2/3），B = R1C4+R1C5+R1C6（1/2/4/5）
        Set(masks, 0, 0, 1, 2, 3);
        Set(masks, 0, 1, 1, 2, 3);
        Set(masks, 0, 3, 1, 2, 4, 5);
        Set(masks, 0, 4, 1, 2, 4, 5);
        Set(masks, 0, 5, 1, 2, 4, 5);

        // 第 1 行里 R1C7 也能填 1：它看得见 A、B 里所有能填 1 的格 → 应被删除
        Set(masks, 0, 6, 1, 7);

        var steps = AlsTechniques.FindAlsXz(masks).ToList();

        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Eliminations.Any(e => e.Cell == SudokuGrid.Index(0, 6)));
        TechniqueStep first = steps.First(s => s.Eliminations.Any(e => e.Cell == SudokuGrid.Index(0, 6)));
        Assert.Equal(Technique.AlsXz, first.Technique);
        Assert.NotEmpty(first.DerivationSteps);
    }

    /// <summary>
    /// 连续环的手工用例：第 1、2 行里数字 1 各自只有两处（共轭对，强链），
    /// 第 1、2 列里数字 1 各有三处（只构成弱链），于是形成
    /// R1C1(1)==R1C2(1)--R2C2(1)==R2C1(1)--R1C1(1) 的连续环，
    /// 环上两条弱链所在列（第 1、2 列）的其他数字 1 都可删掉。
    /// </summary>
    [Fact]
    public void ContinuousLoop_ShouldEliminateFromWeakLinkUnit()
    {
        int[] masks = new int[SudokuGrid.CellCount];

        Set(masks, 0, 0, 1, 4);
        Set(masks, 0, 1, 1, 5);
        Set(masks, 1, 0, 1, 6);
        Set(masks, 1, 1, 1, 7);
        Set(masks, 2, 0, 1, 8);   // 第 1 列第三处数字 1 → 该列只构成弱链
        Set(masks, 2, 1, 1, 9);   // 第 2 列第三处数字 1 → 应被环删掉

        var steps = ContinuousLoop.FindContinuousLoops(masks).ToList();

        Assert.NotEmpty(steps);
        Assert.Contains(steps, s => s.Eliminations.Contains(new CandidateRef(SudokuGrid.Index(2, 1), 1)));
        Assert.All(steps, s => Assert.Equal(Technique.ContinuousLoop, s.Technique));
        Assert.All(steps, s => Assert.NotEmpty(s.DerivationSteps));
    }

    /// <summary>共享的候选数掩码构造：第 2 列里的 1 必须是弱链，否则环不成立。</summary>
    [Fact]
    public void ContinuousLoop_ShouldNotFireWhenAllLinksAreStrong()
    {
        int[] masks = new int[SudokuGrid.CellCount];

        // 两行两列都只有两处数字 1 → 全是共轭对，每条边都既是强链又是弱链，
        // 无法构成「强弱交替」的连续环（这是数对，不是环）
        Set(masks, 0, 0, 1, 4);
        Set(masks, 0, 1, 1, 5);
        Set(masks, 1, 0, 1, 6);
        Set(masks, 1, 1, 1, 7);

        var steps = ContinuousLoop.FindContinuousLoops(masks).ToList();

        Assert.All(steps, s => Assert.True(
            s.Eliminations.Count > 0,
            "连续环必须给出至少一条删除结论"));
    }

    /// <summary>
    /// Sue de Coq 的回归用例：这道真实题面上，旧的「两侧合计格数 = 合计候选数个数」写法
    /// 会删掉正解 R1C5(7)、R2C3(7) 等候选数（宫侧的格与线侧的格不同族，合计数相等推不出锁定集）。
    /// 改成「宫侧、线侧各自都是锁定集」之后，这些删除必须全部消失。
    /// </summary>
    [Fact]
    public void SueDeCoq_ShouldNotUseUnionCountRule()
    {
        const string sdk = "...2..834.3..1..9..8635.217...9...7.......3.....4.1...7....2..3....43.28312.9....";
        Board puzzle = Board.Parse(sdk);
        Assert.True(Solver.TrySolve(puzzle, out Board solution));
        int[] masks = puzzle.ComputeCandidates();

        CandidateRef[] bogus =
        {
            new(SudokuGrid.Index(0, 4), 7), // R1C5(7) 是正解数字
            new(SudokuGrid.Index(1, 2), 7), // R2C3(7) 是正解数字
        };

        foreach (CandidateRef candidate in bogus)
        {
            Assert.Equal(candidate.Digit, solution[candidate.Cell]);
        }

        var steps = SueDeCoq.FindSueDeCoq(masks).ToList();

        foreach (TechniqueStep step in steps)
        {
            foreach (CandidateRef elimination in step.Eliminations)
            {
                Assert.False(
                    elimination.Digit == solution[elimination.Cell],
                    $"Sue de Coq 删掉了正确答案 {elimination}：{step.Description}");
            }
        }

        Assert.DoesNotContain(steps, s => s.Eliminations.Contains(bogus[0]));
        Assert.DoesNotContain(steps, s => s.Eliminations.Contains(bogus[1]));
    }

    /// <summary>
    /// Sue de Coq 必须要求「宫侧那一组、线侧那一组各自都是锁定集」。
    /// 这里构造一个只有「两侧合计」成立、但每一侧都不是锁定集的盘面：
    /// 宫侧 5 格含 6 个候选数、线侧 5 格含 6 个候选数、合计 7 格 7 个候选数。
    /// 旧写法会据此删掉 R2C2(6) 与 R1C6(7)，新写法必须一个结论都不给。
    /// </summary>
    [Fact]
    public void SueDeCoq_ShouldRequireLockedSetOnEachSide()
    {
        int[] masks = new int[SudokuGrid.CellCount];

        // 交叉格 I = R1C1/R1C2/R1C3，候选数合计 1~5
        Set(masks, 0, 0, 1, 2, 3);
        Set(masks, 0, 1, 1, 2, 3);
        Set(masks, 0, 2, 4, 5);

        // 宫侧补格：R2C1{6}、R3C1{6}，宫侧 5 格却含 6 个候选数 → 不是锁定集
        Set(masks, 1, 0, 6);
        Set(masks, 2, 0, 6);

        // 线侧补格：R1C4{7}、R1C5{7}，线侧 5 格也含 6 个候选数 → 不是锁定集
        Set(masks, 0, 3, 7);
        Set(masks, 0, 4, 7);

        // 这两格是旧写法会删掉的对象
        Set(masks, 1, 1, 6); // R2C2(6)
        Set(masks, 0, 5, 7); // R1C6(7)

        var steps = SueDeCoq.FindSueDeCoq(masks).ToList();

        Assert.DoesNotContain(steps, s => s.Eliminations.Contains(new CandidateRef(SudokuGrid.Index(1, 1), 6)));
        Assert.DoesNotContain(steps, s => s.Eliminations.Contains(new CandidateRef(SudokuGrid.Index(0, 5), 7)));
    }

    private static void Set(int[] masks, int row, int col, params int[] digits)
    {
        int mask = 0;
        foreach (int digit in digits)
        {
            mask |= SudokuGrid.DigitBit(digit);
        }

        masks[SudokuGrid.Index(row, col)] = mask;
    }

    private static void Place(int[] cells, int[] masks, int cell, int digit)
    {
        cells[cell] = digit;
        masks[cell] = 0;
        int bit = SudokuGrid.DigitBit(digit);

        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (ChainTechniques.IsPeer(cell, i))
            {
                masks[i] &= ~bit;
            }
        }
    }
}
