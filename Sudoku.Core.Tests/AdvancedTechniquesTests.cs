using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>
/// 阶段四高阶技巧（带鳍鱼 / 唯一矩形 / BUG+1 / 远程数对）的测试。
/// 这些技巧只看候选数掩码，因此多数用例直接手工构造掩码，形状可控、断言精确。
/// </summary>
public class AdvancedTechniquesTests
{
    private static int[] EmptyMasks() => new int[SudokuGrid.CellCount];

    private static void Set(int[] masks, int row, int col, params int[] digits)
    {
        int mask = 0;
        foreach (int digit in digits)
        {
            mask |= SudokuGrid.DigitBit(digit);
        }

        masks[SudokuGrid.Index(row, col)] = mask;
    }

    // ------------------------------------------------------------------
    // 带鳍鱼
    // ------------------------------------------------------------------

    [Fact]
    public void FinnedXWing_ShouldEliminateFromCoverCellSeeingTheFin()
    {
        int[] masks = EmptyMasks();

        // 数字 5 在两行里几乎只出现在第 1、2 列，另外第 2 行第 3 列多出一个鳍格
        Set(masks, 0, 0, 5, 7);
        Set(masks, 0, 1, 5, 8);
        Set(masks, 1, 0, 5, 7);
        Set(masks, 1, 1, 5, 8);
        Set(masks, 1, 2, 5, 9);   // 鳍格（与第 2 行第 1 列同宫）
        Set(masks, 2, 0, 5, 6);   // 覆盖列上、能看见鳍格 → 应被删除 5

        var steps = FinnedFish.FindFinnedFish(masks, 2).ToList();

        Assert.NotEmpty(steps);
        TechniqueStep step = steps[0];
        Assert.Equal(Technique.FinnedXWing, step.Technique);
        Assert.Contains(new CandidateRef(SudokuGrid.Index(2, 0), 5), step.Eliminations);
        Assert.Contains(SudokuGrid.Index(1, 2), step.HighlightCells);
        Assert.NotEmpty(step.LinkList);
        Assert.NotEmpty(step.DerivationSteps);
    }

    [Fact]
    public void FinnedFish_ShouldNotFireOnPlainXWing()
    {
        int[] masks = EmptyMasks();

        // 普通的 2×2 X 翼（没有鳍格）：不应被「带鳍鱼」命中
        Set(masks, 0, 0, 5, 7);
        Set(masks, 0, 1, 5, 8);
        Set(masks, 1, 0, 5, 7);
        Set(masks, 1, 1, 5, 8);

        Assert.Empty(FinnedFish.FindFinnedFish(masks, 2));
    }

    // ------------------------------------------------------------------
    // 唯一矩形
    // ------------------------------------------------------------------

    [Fact]
    public void UniqueRectangle_Type1_ShouldRemoveExtraCandidates()
    {
        int[] masks = EmptyMasks();

        // 两行两列两宫（第 1、2 行同宫带，第 1、4 列不同堆），四格都能填 1/2；只有第四格多出 7、9
        Set(masks, 0, 0, 1, 2);
        Set(masks, 0, 3, 1, 2);
        Set(masks, 1, 0, 1, 2);
        Set(masks, 1, 3, 1, 2, 7, 9);

        var steps = UniqueRectangle.FindUniqueRectangles(masks).ToList();

        Assert.NotEmpty(steps);
        TechniqueStep step = steps[0];
        Assert.Equal(Technique.UniqueRectangle, step.Technique);
        Assert.Equal(SudokuGrid.Index(1, 3), step.Eliminations[0].Cell);
        Assert.Equal(new[] { 7, 9 }, step.Eliminations.Select(e => e.Digit).OrderBy(d => d).ToArray());
        Assert.Equal(4, step.HighlightCells.Count);
        Assert.Equal(4, step.LinkList.Count);
    }

    [Fact]
    public void UniqueRectangle_Type2_ShouldRemoveExtraDigitFromCommonPeers()
    {
        int[] masks = EmptyMasks();

        // 矩形四格都能填 1/2；其中同一行（第 1 行）的两格都多一个 5
        Set(masks, 0, 0, 1, 2, 5);
        Set(masks, 0, 3, 1, 2, 5);
        Set(masks, 1, 0, 1, 2);
        Set(masks, 1, 3, 1, 2);

        // 同时看见这两格的位置
        Set(masks, 0, 1, 5, 7);
        Set(masks, 0, 2, 5, 8);

        var steps = UniqueRectangle.FindUniqueRectangles(masks).ToList();

        Assert.NotEmpty(steps);
        TechniqueStep step = steps.First(s => s.Eliminations.Any(e => e.Digit == 5));
        Assert.Equal(Technique.UniqueRectangle, step.Technique);
        Assert.Contains(new CandidateRef(SudokuGrid.Index(0, 1), 5), step.Eliminations);
        Assert.Contains(new CandidateRef(SudokuGrid.Index(0, 2), 5), step.Eliminations);
    }

    // ------------------------------------------------------------------
    // 远程数对
    // ------------------------------------------------------------------

    [Fact]
    public void RemotePair_ShouldEliminateBothDigitsFromCommonPeers()
    {
        int[] masks = EmptyMasks();

        // 一条 3 段的双值格链：R1C1 - R1C4（第 1 行的 1）- R2C4（第 4 列的 2）- R2C1（第 2 行的 1）
        Set(masks, 0, 0, 1, 2);
        Set(masks, 0, 3, 1, 2);
        Set(masks, 1, 3, 1, 2);
        Set(masks, 1, 0, 1, 2);

        // 同时看见链两端 R1C1 与 R2C1 的位置（同一列）
        Set(masks, 2, 0, 1, 2, 6);

        var steps = RemotePair.FindRemotePairs(masks).ToList();

        Assert.NotEmpty(steps);
        TechniqueStep step = steps.First(s => s.Eliminations.Any(e => e.Cell == SudokuGrid.Index(2, 0)));
        Assert.Equal(Technique.RemotePair, step.Technique);
        Assert.Contains(new CandidateRef(SudokuGrid.Index(2, 0), 1), step.Eliminations);
        Assert.Contains(new CandidateRef(SudokuGrid.Index(2, 0), 2), step.Eliminations);

        // 链必须是强弱交替的（本技巧里每一步都是共轭对，即强链）
        Assert.All(step.LinkList, link => Assert.True(link.IsStrong));
        Assert.NotEmpty(step.DerivationSteps);
    }

    // ------------------------------------------------------------------
    // BUG+1
    // ------------------------------------------------------------------

    /// <summary>
    /// 用「两个合法解合并」的方式构造真正的 BUG 形状：
    /// 若 S 与 S' 是两个合法解，则在二者不同的格子上取候选数 {S, S'}，
    /// 每个单元里每个数字都恰好出现两次——这正是 BUG。
    /// 再给其中一个格子补上第三个候选数 c（要求 c 在该格的行、列、宫内都被「换过」），
    /// 就得到合法的 BUG+1，答案即为 c。
    /// </summary>
    [Fact]
    public void BugPlusOne_ShouldPlaceTheDigitThatAppearsThreeTimes()
    {
        (int[] masks, int cell, int answer)? found = null;

        for (int seed = 1; seed <= 40 && found is null; seed++)
        {
            var generator = new Generator(seed: seed);
            Board s = generator.GenerateSolution();
            Board t = new Generator(seed: seed + 5000).GenerateSolution();

            for (int cell = 0; cell < SudokuGrid.CellCount && found is null; cell++)
            {
                if (s[cell] == t[cell])
                {
                    continue;
                }

                foreach (int candidate in Enumerable.Range(1, SudokuGrid.Size))
                {
                    if (candidate == s[cell] || candidate == t[cell])
                    {
                        continue;
                    }

                    if (!IsMovedInAllUnits(s, t, cell, candidate))
                    {
                        continue;
                    }

                    int[] masks = BuildUnionMasks(s, t, cell, candidate);
                    var steps = BugPlusOne.FindBugPlusOne(masks).ToList();
                    if (steps.Count > 0)
                    {
                        Assert.Equal(Technique.BugPlusOne, steps[0].Technique);
                        found = (masks, cell, candidate);
                        break;
                    }
                }
            }
        }

        Assert.NotNull(found);

        int[] bugMasks = found!.Value.masks;
        var result = BugPlusOne.FindBugPlusOne(bugMasks).ToList();
        Assert.Single(result);

        TechniqueStep step = result[0];
        Assert.True(step.IsPlacement);
        Assert.Equal(found.Value.cell, step.PlaceIndex);
        Assert.Equal(found.Value.answer, step.PlaceDigit);
        Assert.Equal(2, step.Eliminations.Count);
    }

    [Fact]
    public void BugPlusOne_ShouldNotFireWhenTwoCellsHaveThreeCandidates()
    {
        int[] masks = EmptyMasks();
        Set(masks, 0, 0, 1, 2, 3);
        Set(masks, 1, 0, 1, 2, 3);
        Set(masks, 2, 0, 1, 2);

        Assert.Empty(BugPlusOne.FindBugPlusOne(masks));
    }

    private static int[] BuildUnionMasks(Board s, Board t, int triCell, int extra)
    {
        int[] masks = EmptyMasks();

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            if (s[cell] == t[cell])
            {
                continue; // 两个解一致的格子视为已填
            }

            masks[cell] = SudokuGrid.DigitBit(s[cell]) | SudokuGrid.DigitBit(t[cell]);
        }

        masks[triCell] |= SudokuGrid.DigitBit(extra);
        return masks;
    }

    private static bool IsMovedInAllUnits(Board s, Board t, int cell, int digit)
    {
        foreach (int unit in SudokuGrid.UnitsOf[cell])
        {
            int inS = SudokuGrid.AllUnits[unit].First(c => s[c] == digit);
            int inT = SudokuGrid.AllUnits[unit].First(c => t[c] == digit);
            if (inS == inT)
            {
                return false; // 该数字在这个单元里没被「换位」，计数只有 1
            }
        }

        return true;
    }

    // ------------------------------------------------------------------
    // 与求解器的接线
    // ------------------------------------------------------------------

    [Fact]
    public void FindAllSteps_ShouldRespectLevelCapForAdvancedTechniques()
    {
        var generator = new Generator(seed: 4242);
        Board puzzle = generator.Generate(DifficultyLevel.Expert).Given;

        foreach (int cap in new[] { 6, 7, 8, 9, 10 })
        {
            IReadOnlyList<TechniqueStep> steps = LogicalSolver.FindAllSteps(puzzle, cap);
            Assert.All(steps, step => Assert.True(step.Level <= cap, $"{step.Technique} 的等级超过了上限 {cap}"));
        }
    }

    [Fact]
    public void TechniqueInfo_ShouldKnowTheNewTechniques()
    {
        Assert.Equal(7, TechniqueInfo.Level(Technique.RemotePair));
        Assert.Equal(8, TechniqueInfo.Level(Technique.UniqueRectangle));
        Assert.Equal(8, TechniqueInfo.Level(Technique.FinnedXWing));
        Assert.Equal(9, TechniqueInfo.Level(Technique.FinnedSwordfish));
        Assert.Equal(10, TechniqueInfo.Level(Technique.BugPlusOne));

        foreach (Technique technique in new[]
        {
            Technique.RemotePair,
            Technique.UniqueRectangle,
            Technique.FinnedXWing,
            Technique.FinnedSwordfish,
            Technique.BugPlusOne,
        })
        {
            Assert.NotEmpty(TechniqueInfo.Name(technique));
            Assert.NotEmpty(TechniqueInfo.Summary(technique));
            Assert.True(DifficultyRating.BaseScore(technique) > 5.0);
        }
    }
}
