namespace Sudoku.Core;

/// <summary>
/// 提示高亮的角色：决定「这个标记画成什么颜色/什么形状」。
/// 界面按角色取色，所以角色语义必须稳定：结构一种色、不同候选数各自一种色、
/// 鱼鳍单独一种色、结论（要删的候选数 / 要填的数字）再用一种色。
/// </summary>
public enum HintMarkRole
{
    /// <summary>结构本体所在的格（整格标记，Digit 可能为 0）。</summary>
    Pattern,

    /// <summary>结构里第一种关键候选数（如 XY 翼枢轴与翼共有的那个数字）。</summary>
    DigitA,

    /// <summary>结构里第二种关键候选数。</summary>
    DigitB,

    /// <summary>结构里第三种关键候选数（如 XY 翼两翼共有的、要被删掉的那个数字）。</summary>
    DigitC,

    /// <summary>鱼鳍格（带鳍鱼的鳍）。</summary>
    Fin,

    /// <summary>结论：可以删除的候选数。</summary>
    Elimination,

    /// <summary>结论：可以直接填入的数字。</summary>
    Placement,
}

/// <summary>
/// 一个高亮标记：某格中的某个候选数（<see cref="Digit"/> 为 0 表示只标记整格）。
/// <see cref="Stage"/> 是「推导到第几步才出现」（0 基，对应提示面板里的第几条推导文字），
/// 这样棋盘高亮就能跟着面板一步步亮起来，而不是一次性全糊上去。
/// </summary>
public readonly record struct HintMark(int Cell, int Digit, HintMarkRole Role, int Stage = 0)
{
    /// <summary>是否整格标记（不针对某个候选数）。</summary>
    public bool IsCellWide => Digit is < 1 or > SudokuGrid.Size;

    public override string ToString() => IsCellWide
        ? $"{CellName(Cell)}[{Role}]"
        : $"{CellName(Cell)}({Digit})[{Role}]";

    private static string CellName(int cell) => $"R{SudokuGrid.Row(cell) + 1}C{SudokuGrid.Col(cell) + 1}";
}

/// <summary>
/// 提示高亮的取用规则：把技巧给出的标记按「推导进度」切片，并提供一个兜底推导——
/// 老技巧（只给了 HighlightCells / Eliminations）也能画出一份合理的高亮。
/// </summary>
public static class HintMarks
{
    /// <summary>按推导进度（0 基）取出应当显示的标记；进度小于 0 表示全部显示。</summary>
    public static IReadOnlyList<HintMark> UpTo(IReadOnlyList<HintMark> marks, int stage)
    {
        ArgumentNullException.ThrowIfNull(marks);

        if (marks.Count == 0)
        {
            return Array.Empty<HintMark>();
        }

        if (stage < 0)
        {
            return marks;
        }

        var visible = new List<HintMark>(marks.Count);
        foreach (HintMark mark in marks)
        {
            if (mark.Stage <= stage)
            {
                visible.Add(mark);
            }
        }

        return visible;
    }

    /// <summary>最后一个标记出现的进度（没有任何标记时返回 0）。</summary>
    public static int LastStage(IReadOnlyList<HintMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);

        int last = 0;
        foreach (HintMark mark in marks)
        {
            if (mark.Stage > last)
            {
                last = mark.Stage;
            }
        }

        return last;
    }

    /// <summary>某个角色是否属于「结论」（删除 / 落子），界面用对比色画它。</summary>
    public static bool IsConclusion(HintMarkRole role) => role is HintMarkRole.Elimination or HintMarkRole.Placement;

    /// <summary>
    /// 兜底标记：技巧没自带标记时，用 HighlightCells 当结构格、Eliminations 当结论，
    /// 并把链上出现的候选数按数字分色（同数字同色，最多三色循环）。
    /// </summary>
    public static IReadOnlyList<HintMark> Synthesize(TechniqueStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        int lastStage = step.DerivationSteps.Count > 1 ? step.DerivationSteps.Count - 1 : 0;
        int primary = step.IsPlacement
            ? step.PlaceDigit
            : step.Eliminations.Count > 0 ? step.Eliminations[0].Digit : 0;

        var marks = new List<HintMark>();

        // 结构格：整格淡淡铺一层，先把「这一步和哪些格有关」交代清楚
        foreach (int cell in step.HighlightCells.Distinct().OrderBy(c => c))
        {
            marks.Add(new HintMark(cell, primary, HintMarkRole.Pattern, 0));
        }

        // 链上的候选数按数字分色；没有链时用主角数字给结构格上的该候选数上色
        var digits = new List<int>();
        foreach (TechniqueLink link in step.LinkList)
        {
            if (!digits.Contains(link.From.Digit))
            {
                digits.Add(link.From.Digit);
            }

            if (!digits.Contains(link.To.Digit))
            {
                digits.Add(link.To.Digit);
            }
        }

        foreach (TechniqueLink link in step.LinkList)
        {
            marks.Add(new HintMark(link.From.Cell, link.From.Digit, RoleForDigit(digits, link.From.Digit), 0));
            marks.Add(new HintMark(link.To.Cell, link.To.Digit, RoleForDigit(digits, link.To.Digit), 0));
        }

        if (digits.Count == 0 && primary is >= 1 and <= SudokuGrid.Size)
        {
            foreach (int cell in step.HighlightCells.Distinct())
            {
                marks.Add(new HintMark(cell, primary, HintMarkRole.DigitA, 0));
            }
        }

        foreach (CandidateRef elimination in step.Eliminations)
        {
            marks.Add(new HintMark(elimination.Cell, elimination.Digit, HintMarkRole.Elimination, lastStage));
        }

        if (step.IsPlacement && step.PlaceIndex >= 0)
        {
            marks.Add(new HintMark(step.PlaceIndex, step.PlaceDigit, HintMarkRole.Placement, lastStage));
        }

        return Normalize(marks);
    }

    /// <summary>按数字在链上出现的顺序取角色：第一个数字用 DigitA，第二个 DigitB，第三个及以后 DigitC。</summary>
    private static HintMarkRole RoleForDigit(IReadOnlyList<int> digits, int digit)
    {
        int index = 0;
        for (int i = 0; i < digits.Count; i++)
        {
            if (digits[i] == digit)
            {
                index = i;
                break;
            }
        }

        return index switch
        {
            0 => HintMarkRole.DigitA,
            1 => HintMarkRole.DigitB,
            _ => HintMarkRole.DigitC,
        };
    }

    /// <summary>去重：同一格同一数字同一角色只留最先出现（进度最小）的那个。</summary>
    public static IReadOnlyList<HintMark> Normalize(IEnumerable<HintMark> marks)
    {
        ArgumentNullException.ThrowIfNull(marks);

        var best = new Dictionary<(int Cell, int Digit, HintMarkRole Role), HintMark>();
        foreach (HintMark mark in marks)
        {
            var key = (mark.Cell, mark.Digit, mark.Role);
            if (!best.TryGetValue(key, out HintMark existing) || mark.Stage < existing.Stage)
            {
                best[key] = mark;
            }
        }

        return best.Values
            .OrderBy(m => m.Stage)
            .ThenBy(m => m.Cell)
            .ThenBy(m => m.Role)
            .ThenBy(m => m.Digit)
            .ToArray();
    }
}
