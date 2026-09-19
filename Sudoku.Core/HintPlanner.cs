namespace Sudoku.Core;

/// <summary>同一技巧下的一个具体实例（即一条「路径」）。</summary>
public sealed record HintOption(TechniqueStep Step, string Label);

/// <summary>按技巧分组的提示候选项。</summary>
public sealed record HintGroup(Technique Technique, IReadOnlyList<HintOption> Options)
{
    /// <summary>技巧等级。</summary>
    public int Level => TechniqueInfo.Level(Technique);

    /// <summary>技巧中文名。</summary>
    public string Name => TechniqueInfo.Name(Technique);

    /// <summary>技巧一句话说明。</summary>
    public string Summary => TechniqueInfo.Summary(Technique);

    /// <summary>该技巧当前可用的实例数量。</summary>
    public int Count => Options.Count;
}

/// <summary>分段式提示的第一段：当前盘面可用的技巧与各自的实例。</summary>
public sealed class HintPlan
{
    /// <summary>按等级从低到高排列的技巧分组。</summary>
    public IReadOnlyList<HintGroup> Groups { get; }

    public HintPlan(IReadOnlyList<HintGroup> groups)
    {
        Groups = groups;
        TotalOptions = groups.Sum(g => g.Count);
    }

    /// <summary>是否没有任何可用技巧（通常意味着卡死或盘面已错）。</summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>全部实例数量。</summary>
    public int TotalOptions { get; }
}

/// <summary>
/// 分段式提示的编排：把 <see cref="LogicalSolver.FindAllSteps"/> 的结果整理成
/// 「技巧 → 实例（路径）」两级列表，并为每个实例生成可读标签。
/// 纯逻辑、不依赖界面，便于单元测试。
/// </summary>
public static class HintPlanner
{
    /// <summary>单个技巧最多列出多少条路径（链级技巧可能命中很多条）。</summary>
    public const int DefaultMaxOptionsPerTechnique = 12;

    /// <summary>
    /// 整理当前盘面的可用技巧。
    /// <paramref name="candidateOverride"/> 可以直接给出各格候选数（与规则推导结果取交集），
    /// 用于让提示跟随玩家自己删减过的候选数。
    /// </summary>
    public static HintPlan Plan(
        Board board,
        int maxLevel = int.MaxValue,
        int maxOptionsPerTechnique = DefaultMaxOptionsPerTechnique,
        int[]? candidateOverride = null)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxOptionsPerTechnique, 1);

        IReadOnlyList<TechniqueStep> steps = LogicalSolver.FindAllSteps(board, maxLevel, candidateOverride);

        var groups = new List<HintGroup>();
        IEnumerable<IGrouping<Technique, TechniqueStep>> byTechnique = steps
            .GroupBy(s => s.Technique)
            .OrderBy(g => TechniqueInfo.Level(g.Key))
            .ThenBy(g => (int)g.Key);

        foreach (IGrouping<Technique, TechniqueStep> grouping in byTechnique)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var options = new List<HintOption>();

            foreach (TechniqueStep step in Order(grouping))
            {
                if (!seen.Add(Signature(step)))
                {
                    continue;
                }

                options.Add(new HintOption(step, string.Empty));

                if (options.Count >= maxOptionsPerTechnique)
                {
                    break;
                }
            }

            // 编号在使用时才确定（①②③…）
            var labeled = new List<HintOption>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                labeled.Add(options[i] with { Label = Describe(options[i].Step, i) });
            }

            groups.Add(new HintGroup(grouping.Key, labeled));
        }

        return new HintPlan(groups);
    }

    /// <summary>实例的一句话标签，用于路径列表。</summary>
    public static string Describe(TechniqueStep step, int index)
    {
        ArgumentNullException.ThrowIfNull(step);
        string number = Number(index);

        if (step.IsPlacement)
        {
            return $"{number} 填入 {CellName(step.PlaceIndex)} = {step.PlaceDigit}";
        }

        if (step.Eliminations.Count == 1)
        {
            return $"{number} 删除 {step.Eliminations[0]}";
        }

        return $"{number} 删除 {step.Eliminations[0]} 等 {step.Eliminations.Count} 个候选数";
    }

    /// <summary>该步骤作用范围的摘要（应用按钮、状态栏用）。</summary>
    public static string Effect(TechniqueStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (step.IsPlacement)
        {
            return $"填入 {CellName(step.PlaceIndex)} = {step.PlaceDigit}";
        }

        return $"删除 {string.Join("、", step.Eliminations.Select(e => e.ToString()))}";
    }

    /// <summary>路径排序：先落子、后删候选数，再按格号稳定排列。</summary>
    private static IEnumerable<TechniqueStep> Order(IEnumerable<TechniqueStep> steps) => steps
        .OrderBy(s => s.IsPlacement ? 0 : 1)
        .ThenBy(s => s.IsPlacement ? s.PlaceIndex : s.Eliminations.Count > 0 ? s.Eliminations.Min(e => e.Cell) : int.MaxValue)
        .ThenBy(s => s.IsPlacement ? s.PlaceDigit : s.Eliminations.Count > 0 ? s.Eliminations.Min(e => e.Digit) : 0);

    /// <summary>用「作用效果」作为去重签名：同效果的路径只保留最短的一条。</summary>
    private static string Signature(TechniqueStep step)
    {
        if (step.IsPlacement)
        {
            return $"P{step.PlaceIndex}:{step.PlaceDigit}";
        }

        return "E" + string.Join(
            ",",
            step.Eliminations
                .Select(e => (e.Cell * 10) + e.Digit)
                .Distinct()
                .OrderBy(v => v));
    }

    private static string CellName(int index) => $"R{SudokuGrid.Row(index) + 1}C{SudokuGrid.Col(index) + 1}";

    private static string Number(int index) => index switch
    {
        0 => "①",
        1 => "②",
        2 => "③",
        3 => "④",
        4 => "⑤",
        5 => "⑥",
        6 => "⑦",
        7 => "⑧",
        8 => "⑨",
        9 => "⑩",
        10 => "⑪",
        _ => "⑫",
    };
}
