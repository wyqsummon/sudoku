using Sudoku.Core;
using Xunit;
using Xunit.Abstractions;

namespace Sudoku.Core.Tests;

/// <summary>
/// 提示高亮标记（阶段五）的回归测试：标记的角色分色、鱼鳍单独标记、以及「按推导进度渐显」。
/// 两个夹具都是从大师档题目解题路径上截下来的真实中间盘面（保证盘面上确实存在该技巧）。
/// </summary>
public class HintMarksTests
{
    /// <summary>盘面上存在 XY 翼：枢轴 R2C8(3/4)，两翼 R2C2(3/2)、R1C9(4/2)，可删 R1C3(2)。</summary>
    private const string XyWingSdk = "18.563.9...91875.65.69428.191.6.8....682.9.1..5.4.1689.417259688..3961..695814327";

    /// <summary>盘面上存在带鳍 X 翼：数字 4，鳍格 R5C7，可删 R4C8(4)、R4C9(4)。</summary>
    private const string FinnedFishSdk = "18.563.9...91875.65.69428.191.6.8....682.9.1..5.4.16893417259688..3961..695814327";

    private readonly ITestOutputHelper _output;

    public HintMarksTests(ITestOutputHelper output) => _output = output;

    private static int Cell(string name)
    {
        int row = name[1] - '1';
        int col = name[3] - '1';
        return (row * SudokuGrid.Size) + col;
    }

    private static Board Board(string sdk) => Sudoku.Core.Board.Parse(sdk);

    [Fact]
    public void Marks_ShouldBeStaged_And_RevealProgressively()
    {
        var step = new TechniqueStep(
            Technique.XWing,
            new[] { 0, 3, 27, 30 },
            -1,
            0,
            new[] { new CandidateRef(9, 4) },
            "测试用步骤",
            null,
            new[] { "第一步", "第二步", "第三步" });

        IReadOnlyList<HintMark> all = step.VisualMarks;

        Assert.NotEmpty(all);
        Assert.All(all, m => Assert.InRange(m.Stage, 0, 2));

        // 结构格第 1 步就亮，结论（要删的候选数）到第 3 步才出现
        IReadOnlyList<HintMark> first = step.MarksAt(0);
        Assert.Contains(first, m => m.Role == HintMarkRole.Pattern && m.Cell == 0);
        Assert.DoesNotContain(first, m => m.Role == HintMarkRole.Elimination);

        IReadOnlyList<HintMark> last = step.MarksAt(2);
        Assert.Contains(last, m => m.Role == HintMarkRole.Elimination && m.Cell == 9 && m.Digit == 4);

        // 负数表示整步都显示
        Assert.Equal(all.Count, step.MarksAt(-1).Count);

        // 只有一句话（没有分阶段推导）时，结论不会被藏起来
        var flat = step with { Derivation = new[] { "只有一句结论" } };
        Assert.Contains(flat.VisualMarks, m => m.Role == HintMarkRole.Elimination && m.Stage == 0);
    }

    [Fact]
    public void Marks_ShouldColourChainDigits_ByDigit()
    {
        var step = new TechniqueStep(
            Technique.XChain,
            new[] { 0, 3, 30 },
            -1,
            0,
            new[] { new CandidateRef(12, 5) },
            "测试用链",
            new[]
            {
                new TechniqueLink(new CandidateRef(0, 5), new CandidateRef(3, 5), true, "同单元只剩两处"),
                new TechniqueLink(new CandidateRef(3, 5), new CandidateRef(30, 7), false, "同格弱链"),
            },
            new[] { "链表达式", "每一步", "结论" });

        IReadOnlyList<HintMark> marks = step.VisualMarks;

        // 数字 5 与 7 各自一种颜色（DigitA / DigitB），同数字同色
        Assert.Contains(marks, m => m.Cell == 0 && m.Digit == 5 && m.Role == HintMarkRole.DigitA);
        Assert.Contains(marks, m => m.Cell == 3 && m.Digit == 5 && m.Role == HintMarkRole.DigitA);
        Assert.Contains(marks, m => m.Cell == 30 && m.Digit == 7 && m.Role == HintMarkRole.DigitB);
    }

    [Fact]
    public void XYWing_Marks_ShouldSeparate_Pivot_Digits_And_Shared_Digit()
    {
        Board board = Board(XyWingSdk);
        TechniqueStep? found = null;
        foreach (TechniqueStep step in LogicalSolver.FindAllSteps(board, maxLevel: int.MaxValue))
        {
            if (step.Technique == Technique.XYWing)
            {
                found = step;
                break;
            }
        }

        Assert.NotNull(found);
        TechniqueStep wing = found!;
        _output.WriteLine(wing.Description);

        int pivot = Cell("R2C8");
        int z = 2;

        Assert.Equal(pivot, wing.HighlightCells[0]);
        Assert.Contains(wing.Eliminations, e => e.Cell == Cell("R1C3") && e.Digit == z);

        // 枢轴的两个候选数用两种不同颜色（DigitA / DigitB）
        HintMark[] pivotMarks = wing.VisualMarks
            .Where(m => m.Cell == pivot && m.Role is HintMarkRole.DigitA or HintMarkRole.DigitB)
            .ToArray();
        Assert.Equal(2, pivotMarks.Length);
        Assert.Equal(new[] { 3, 4 }, pivotMarks.Select(m => m.Digit).OrderBy(d => d).ToArray());

        // 每个翼：与枢轴共享的那个数字沿用枢轴同色，两翼共有的 z 用第三种颜色
        foreach ((int cell, int shared) in new[] { (Cell("R2C2"), 3), (Cell("R1C9"), 4) })
        {
            HintMarkRole role = pivotMarks.Single(m => m.Digit == shared).Role;
            Assert.Contains(wing.VisualMarks, m => m.Cell == cell && m.Digit == shared && m.Role == role);
            Assert.Contains(wing.VisualMarks, m => m.Cell == cell && m.Digit == z && m.Role == HintMarkRole.DigitC);
        }

        // 第 1 条推导只讲枢轴 → 棋盘上只亮枢轴的候选数；结论留到最后一条
        Assert.All(wing.MarksAt(0), m => Assert.Equal(pivot, m.Cell));
        Assert.DoesNotContain(wing.MarksAt(1), m => m.Role == HintMarkRole.Elimination);
        Assert.Contains(wing.MarksAt(5), m => m.Role == HintMarkRole.Elimination && m.Cell == Cell("R1C3") && m.Digit == z);
    }

    [Fact]
    public void FinnedFish_Marks_Should_Have_Fins_And_Stage_Them()
    {
        Board board = Board(FinnedFishSdk);
        TechniqueStep? found = null;
        foreach (TechniqueStep step in LogicalSolver.FindAllSteps(board, maxLevel: int.MaxValue))
        {
            if (step.Technique is Technique.FinnedXWing or Technique.FinnedSwordfish)
            {
                found = step;
                break;
            }
        }

        Assert.NotNull(found);
        TechniqueStep fish = found!;
        _output.WriteLine(fish.Description);

        Assert.Equal(Technique.FinnedXWing, fish.Technique);

        // 鳍格单独一种角色：只有 R5C7(4) 是鳍
        HintMark[] fins = fish.VisualMarks.Where(m => m.Role == HintMarkRole.Fin).ToArray();
        Assert.Single(fins);
        Assert.Equal(Cell("R5C7"), fins[0].Cell);
        Assert.Equal(4, fins[0].Digit);
        Assert.Equal(2, fins[0].Stage);

        // 鱼身（落在覆盖线上的基准格）第 1 步就亮，且第 1 步看不到鳍
        Assert.Contains(fish.MarksAt(0), m => m.Role == HintMarkRole.Pattern && m.Cell == Cell("R1C3"));
        Assert.DoesNotContain(fish.MarksAt(0), m => m.Role == HintMarkRole.Fin);

        // 第 3 条推导（进度 2）才看到鳍；结论（要删的 4）到第 6 条才出现
        Assert.Contains(fish.MarksAt(2), m => m.Role == HintMarkRole.Fin);
        Assert.DoesNotContain(fish.MarksAt(2), m => m.Role == HintMarkRole.Elimination);
        Assert.Contains(fish.MarksAt(5), m => m.Role == HintMarkRole.Elimination && m.Cell == Cell("R4C8") && m.Digit == 4);
        Assert.Contains(fish.MarksAt(5), m => m.Role == HintMarkRole.Elimination && m.Cell == Cell("R4C9") && m.Digit == 4);
    }
}
