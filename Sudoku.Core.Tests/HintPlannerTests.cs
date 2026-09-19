using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>分段式提示编排（技巧分组 / 路径标签 / 等级上限）的测试。</summary>
public class HintPlannerTests
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
    public void Plan_ShouldGroupByTechniqueInLevelOrder()
    {
        var generator = new Generator(seed: 606);
        Board solution = generator.GenerateSolution();
        Board puzzle = DigUnique(solution, 45, seed: 11);

        HintPlan plan = HintPlanner.Plan(puzzle);

        Assert.False(plan.IsEmpty);
        Assert.True(plan.Groups.Count > 0);

        int previousLevel = 0;
        int total = 0;

        foreach (HintGroup group in plan.Groups)
        {
            Assert.True(group.Level >= previousLevel, "技巧分组必须按等级从低到高排列");
            previousLevel = group.Level;

            Assert.NotEmpty(group.Name);
            Assert.NotEmpty(group.Summary);
            Assert.True(group.Count > 0);
            Assert.Equal(group.Count, group.Options.Count);

            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (HintOption option in group.Options)
            {
                Assert.NotNull(option.Step);
                Assert.NotEmpty(option.Label);
                Assert.NotEmpty(option.Step.Description);
                Assert.NotEmpty(option.Step.HighlightCells);
                Assert.True(labels.Add(option.Label), $"同一技巧下的路径标签不应重复：{option.Label}");

                if (!option.Step.IsPlacement)
                {
                    Assert.True(option.Step.Eliminations.Count > 0, "删除类步骤必须列出被删除的候选数");
                }
            }

            total += group.Count;
        }

        Assert.Equal(total, plan.TotalOptions);
    }

    [Fact]
    public void Plan_ShouldRespectMaxLevel()
    {
        var generator = new Generator(seed: 707);
        Board solution = generator.GenerateSolution();
        Board puzzle = DigUnique(solution, 52, seed: 3);

        foreach (int cap in new[] { 1, 2, 3, 4, 5, 6 })
        {
            HintPlan plan = HintPlanner.Plan(puzzle, cap);
            Assert.All(plan.Groups, g => Assert.True(g.Level <= cap, $"等级 {g.Level} 超过上限 {cap}"));
        }
    }

    [Fact]
    public void Plan_ShouldOnlyOfferStepsThatMatchTheSolution()
    {
        var generator = new Generator(seed: 808);

        for (int i = 0; i < 6; i++)
        {
            Board solution = generator.GenerateSolution();
            Board puzzle = DigUnique(solution, 55, seed: 200 + i);

            foreach (HintGroup group in HintPlanner.Plan(puzzle).Groups)
            {
                foreach (HintOption option in group.Options)
                {
                    TechniqueStep step = option.Step;

                    if (step.IsPlacement)
                    {
                        Assert.Equal(solution[step.PlaceIndex], step.PlaceDigit);
                    }

                    foreach (CandidateRef elimination in step.Eliminations)
                    {
                        Assert.True(
                            solution[elimination.Cell] != elimination.Digit,
                            $"提示误删真实候选：{group.Name} 删除 {elimination}，该格答案是 {solution[elimination.Cell]}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Plan_ShouldBeEmptyWhenBoardIsSolved()
    {
        var generator = new Generator(seed: 909);
        Board solution = generator.GenerateSolution();

        HintPlan plan = HintPlanner.Plan(solution);

        Assert.True(plan.IsEmpty);
        Assert.Equal(0, plan.TotalOptions);
    }

    [Fact]
    public void Plan_ShouldDeduplicatePathsWithSameEffect()
    {
        var generator = new Generator(seed: 1010);
        Board solution = generator.GenerateSolution();
        Board puzzle = DigUnique(solution, 58, seed: 12);

        HintPlan plan = HintPlanner.Plan(puzzle, maxLevel: 9);

        foreach (HintGroup group in plan.Groups)
        {
            // 同一技巧内「作用效果」相同的路径只保留一条
            var effects = new HashSet<string>(StringComparer.Ordinal);
            foreach (HintOption option in group.Options)
            {
                Assert.True(effects.Add(HintPlanner.Effect(option.Step)), $"{group.Name} 出现重复效果：{option.Label}");
            }
        }
    }

    [Fact]
    public void Effect_ShouldDescribePlacementAndElimination()
    {
        var placement = new TechniqueStep(
            Technique.NakedSingle,
            new[] { 0 },
            0,
            5,
            Array.Empty<CandidateRef>(),
            "R1C1 只能填 5",
            Array.Empty<TechniqueLink>(),
            Array.Empty<string>());

        Assert.Equal("填入 R1C1 = 5", HintPlanner.Effect(placement));
        Assert.Equal("① 填入 R1C1 = 5", HintPlanner.Describe(placement, 0));

        var elimination = new TechniqueStep(
            Technique.XWing,
            new[] { 0, 3, 54, 57 },
            -1,
            0,
            new[] { new CandidateRef(27, 5), new CandidateRef(30, 5) },
            "X 翼删除两个候选数",
            Array.Empty<TechniqueLink>(),
            Array.Empty<string>());

        Assert.Equal("删除 R4C1(5)、R4C4(5)", HintPlanner.Effect(elimination));
        Assert.Equal("① 删除 R4C1(5) 等 2 个候选数", HintPlanner.Describe(elimination, 0));
    }

    /// <summary>玩家自己删掉的候选数要被提示考虑进去：候选数覆盖参数要能过滤掉技巧。</summary>
    [Fact]
    public void FindAllSteps_WithCandidateOverride_ShouldFollowUserEliminations()
    {
        var generator = new Generator(seed: 1207);
        Board puzzle = generator.GenerateSolution();

        // 挖掉一格 → 该格是标准的「唯一候选数」
        int target = 40;
        int digit = puzzle[target];
        puzzle[target] = 0;

        IReadOnlyList<TechniqueStep> before = LogicalSolver.FindAllSteps(puzzle);
        Assert.Contains(before, s => s.Technique == Technique.NakedSingle && s.PlaceIndex == target);

        // 玩家把这格的候选数全删了 → 提示不该再给出这一格的唯一候选数
        int[] candidates = puzzle.ComputeCandidates();
        candidates[target] = 0;

        IReadOnlyList<TechniqueStep> after = LogicalSolver.FindAllSteps(puzzle, candidateOverride: candidates);
        Assert.DoesNotContain(after, s => s.PlaceIndex == target);

        // 交集的语义：覆盖值里多出来的非法候选数不会被引入
        int[] inflated = new int[SudokuGrid.CellCount];
        Array.Fill(inflated, (1 << SudokuGrid.Size) - 1);
        IReadOnlyList<TechniqueStep> same = LogicalSolver.FindAllSteps(puzzle, candidateOverride: inflated);
        Assert.Equal(before.Count, same.Count);
        Assert.Contains(same, s => s.Technique == Technique.NakedSingle && s.PlaceIndex == target && s.PlaceDigit == digit);
    }

    /// <summary>分段式提示（HintPlanner.Plan）同样要跟随玩家删减过的候选数。</summary>
    [Fact]
    public void Plan_WithCandidateOverride_ShouldDropTechniquesHiddenByUser()
    {
        var generator = new Generator(seed: 1208);
        Board puzzle = generator.GenerateSolution();
        int target = 40;
        puzzle[target] = 0;

        HintPlan basePlan = HintPlanner.Plan(puzzle);
        Assert.Contains(
            basePlan.Groups.SelectMany(g => g.Options),
            o => o.Step.Technique == Technique.NakedSingle && o.Step.PlaceIndex == target);

        int[] candidates = puzzle.ComputeCandidates();
        candidates[target] = 0;
        HintPlan filtered = HintPlanner.Plan(puzzle, candidateOverride: candidates);

        Assert.DoesNotContain(
            filtered.Groups.SelectMany(g => g.Options),
            o => o.Step.PlaceIndex == target);
    }
}
