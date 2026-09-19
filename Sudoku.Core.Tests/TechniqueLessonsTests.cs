using Sudoku.Core;
using Xunit;

namespace Sudoku.Core.Tests;

/// <summary>技巧教程 + 专项练习的校验：内容完整性、例题真实性、练习开局可用性。</summary>
public class TechniqueLessonsTests
{
    /// <summary>每种会进入教程的技巧都要有实际内容。</summary>
    [Fact]
    public void EveryLesson_ShouldHaveText()
    {
        Assert.NotEmpty(TechniqueLessons.All);

        foreach (TechniqueLesson lesson in TechniqueLessons.All)
        {
            Assert.NotEmpty(lesson.Name);
            Assert.NotEmpty(lesson.Summary);
            Assert.NotEmpty(lesson.Idea);
            Assert.NotEmpty(lesson.HowToFind);
            Assert.NotEmpty(lesson.Action);
            Assert.Contains(lesson.Group, new[] { "基础技巧", "进阶技巧", "高阶技巧" });
        }
    }

    /// <summary>同一技巧不能有两条教程，也不能漏掉解题器里存在的技巧。</summary>
    [Fact]
    public void Lessons_ShouldCoverEveryTechniqueOnce()
    {
        Technique[] all = Enum.GetValues<Technique>().Where(t => t != Technique.None).ToArray();
        Technique[] covered = TechniqueLessons.All.Select(l => l.Technique).ToArray();

        Assert.Equal(covered.Length, covered.Distinct().Count());

        // 每种技巧都要有教程（「十七数」是按提示数判定的题型，不是技巧，不在枚举里）
        Technique[] missing = all.Except(covered).ToArray();
        Assert.Empty(missing);
    }

    /// <summary>教程要按难度分成三组，顺序从易到难。</summary>
    [Fact]
    public void All_ShouldBeOrderedByLevel()
    {
        int[] levels = TechniqueLessons.All.Select(l => l.Level).ToArray();
        Assert.Equal(levels.OrderBy(v => v), levels);
        Assert.Equal(3, TechniqueLessons.Grouped().Count);
    }

    /// <summary>例题题面必须真的解析得出来、是唯一解。</summary>
    [Fact]
    public void ExamplePuzzles_ShouldBeValid()
    {
        foreach (TechniqueLesson lesson in TechniqueLessons.All.Where(l => l.HasExample))
        {
            Board given = Board.Parse(lesson.ExamplePuzzle);
            Assert.Equal(SudokuGrid.CellCount, lesson.ExamplePuzzle.Length);
            Assert.True(Solver.HasUniqueSolution(given), $"{lesson.Name}：例题题面不是唯一解");
        }
    }

    /// <summary>
    /// 专项练习必须真的停在卡点上：盘面是题面的推进、这一步确实能用、
    /// 结论不与真解冲突，而且提示面板能给出这一招。
    /// </summary>
    [Fact]
    public void Practice_ShouldStartAtTheStickingPoint()
    {
        int checkedCount = 0;

        foreach (TechniqueLesson lesson in TechniqueLessons.All.Where(l => l.HasExample))
        {
            PracticeSetup? setup = TechniqueLessons.CreatePractice(lesson.Technique);
            if (setup is null)
            {
                continue; // 这道例题走不到这一招，由 PracticeCoverage 单独统计
            }

            checkedCount++;
            Assert.Equal(lesson.Technique, setup.Target.Technique);
            Assert.NotEmpty(setup.Goal);
            Assert.NotEmpty(setup.Intro);
            Assert.NotEmpty(setup.Note);

            Board given = Board.Parse(lesson.ExamplePuzzle);
            Assert.True(Solver.TrySolve(given, out Board solution), $"{lesson.Name}：例题题面解不出来");

            int[] givenCells = given.ToArray();
            int[] stateCells = setup.Puzzle.Given.ToArray();
            int[] solutionCells = solution.ToArray();

            for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
            {
                if (givenCells[cell] != 0)
                {
                    Assert.Equal(givenCells[cell], stateCells[cell]);
                }

                if (stateCells[cell] != 0)
                {
                    Assert.Equal(solutionCells[cell], stateCells[cell]);
                }
            }

            // 结论不能与真解冲突
            if (setup.Target.IsPlacement)
            {
                Assert.Equal(solutionCells[setup.Target.PlaceIndex], setup.Target.PlaceDigit);
            }

            foreach (CandidateRef elimination in setup.Target.Eliminations)
            {
                Assert.NotEqual(solutionCells[elimination.Cell], elimination.Digit);
            }

            // 提示引擎（同一套 FindAllSteps）必须能给出这一招
            HintPlan plan = HintPlanner.Plan(setup.Puzzle.Given, lesson.Level);
            Assert.Contains(plan.Groups, g => g.Technique == lesson.Technique);
        }

        Assert.True(checkedCount >= 15, $"可练习的技巧太少：只有 {checkedCount} 种");
    }

    /// <summary>至少要有 15 种技巧能开出专项练习。</summary>
    [Fact]
    public void PracticeCoverage_ShouldBeBroadEnough()
    {
        Technique[] playable = TechniqueLessons.All
            .Where(l => TechniqueLessons.CreatePractice(l.Technique) is not null)
            .Select(l => l.Technique)
            .ToArray();

        Assert.True(playable.Length >= 15, $"只有 {playable.Length} 种技巧能开出练习");

        // 三个阶段都要有覆盖：基础 / 进阶 / 高阶
        Assert.Contains(playable, t => TechniqueInfo.Level(t) <= 2);
        Assert.Contains(playable, t => TechniqueInfo.Level(t) is >= 3 and <= 6);
        Assert.Contains(playable, t => TechniqueInfo.Level(t) >= 7);
    }

    /// <summary>没有例题的技巧，专项练习要能优雅降级而不是抛异常。</summary>
    [Fact]
    public void MissingExample_ShouldReturnNullInsteadOfThrowing()
    {
        foreach (TechniqueLesson lesson in TechniqueLessons.All.Where(l => !l.HasExample))
        {
            Assert.Null(TechniqueLessons.CreatePractice(lesson.Technique));
        }
    }
}
