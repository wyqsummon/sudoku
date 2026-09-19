namespace Sudoku.App.Drawing;

using Sudoku.Core;

/// <summary>棋盘绘制用配色（浅色 / 深色两套）。</summary>
public sealed class Palette
{
    public required Color Background { get; init; }
    public required Color BoardBackground { get; init; }
    public required Color GivenCellBackground { get; init; }
    public required Color ThinLine { get; init; }
    public required Color ThickLine { get; init; }
    public required Color GivenText { get; init; }
    public required Color UserText { get; init; }
    public required Color NoteText { get; init; }
    public required Color SelectedCell { get; init; }
    public required Color SameNumberHighlight { get; init; }
    public required Color UnitHighlight { get; init; }
    public required Color PeerHighlight { get; init; }
    public required Color ConflictText { get; init; }
    public required Color ConflictBackground { get; init; }
    public required Color HintBackground { get; init; }
    public required Color HintText { get; init; }
    public required Color AccentText { get; init; }
    public required Color LinkStrong { get; init; }
    public required Color LinkWeak { get; init; }

    /// <summary>候选数高亮底色（锁定数字 / 同类数字时，把该数字的候选数标记出来）。</summary>
    public required Color CandidateHighlight { get; init; }

    // ---------- 提示高亮（阶段五）：按标记角色取色 ----------
    // 取色的两条原则：① 浅色主题也用深色，绝不用浅蓝这种在白色棋盘上糊掉的顏色；
    // ② 结构、结构里的不同候选数、鱼鳍、结论（删除/落子）彼此色相拉开，一眼能分开。

    /// <summary>结构格 / 结构里的候选数（鱼身、链上格等）。</summary>
    public required Color HintStructure { get; init; }

    /// <summary>结构里第一种关键候选数（如 XY 翼枢轴与翼共有的那个数字）。</summary>
    public required Color HintDigitA { get; init; }

    /// <summary>结构里第二种关键候选数。</summary>
    public required Color HintDigitB { get; init; }

    /// <summary>结构里第三种关键候选数（如 XY 翼两翼共有的 z）。</summary>
    public required Color HintDigitC { get; init; }

    /// <summary>鱼鳍格（带鳍鱼的鳍）。</summary>
    public required Color HintFin { get; init; }

    /// <summary>结论：可删除的候选数。</summary>
    public required Color HintElimination { get; init; }

    /// <summary>结论：可填入的数字。</summary>
    public required Color HintPlacement { get; init; }

    /// <summary>「两个候选数」高亮的描边色（数字键右侧 XY 按钮打开时用）。</summary>
    public required Color BivalueHighlight { get; init; }

    /// <summary>画在色块上的数字颜色（保证在彩色底上仍然清楚）。</summary>
    public required Color HintMarkText { get; init; }

    /// <summary>按标记角色取色。</summary>
    public Color HintMarkColor(HintMarkRole role) => role switch
    {
        HintMarkRole.DigitA => HintDigitA,
        HintMarkRole.DigitB => HintDigitB,
        HintMarkRole.DigitC => HintDigitC,
        HintMarkRole.Fin => HintFin,
        HintMarkRole.Elimination => HintElimination,
        HintMarkRole.Placement => HintPlacement,
        _ => HintStructure,
    };

    /// <summary>绘制功能可选的 6 种颜色（同时用于格子涂色与连线）。</summary>
    public required Color[] DrawColors { get; init; }

    /// <summary>
    /// 与 <see cref="DrawColors"/> 同色相的「线条色」：格子底色要柔和，线条要醒目，
    /// 所以同一个色号在两处用不同的明度/饱和度，玩家选一个颜色即可同时决定涂色与连线。
    /// </summary>
    public required Color[] DrawLineColors { get; init; }

    /// <summary>取格子涂色；0 或越界返回 null（表示不涂色）。</summary>
    public Color? DrawColor(int index) =>
        index >= 1 && index <= DrawColors.Length ? DrawColors[index - 1] : null;

    /// <summary>取线条颜色；0 或越界返回 null（表示用默认的强弱链配色）。</summary>
    public Color? DrawLineColor(int index) =>
        index >= 1 && index <= DrawLineColors.Length ? DrawLineColors[index - 1] : null;

    /// <summary>连线颜色：玩家选过颜色就用同色相的线条色，否则强链一色、弱链一色。</summary>
    public Color LinkColor(int colorIndex, bool isStrong) =>
        DrawLineColor(colorIndex) ?? (isStrong ? LinkStrong : LinkWeak);

    /// <summary>浅色主题。</summary>
    public static Palette Light { get; } = new()
    {
        Background = Color.FromArgb("#F5F6F8"),
        BoardBackground = Color.FromArgb("#FFFFFF"),
        GivenCellBackground = Color.FromArgb("#FFFFFF"),
        ThinLine = Color.FromArgb("#C9CED6"),
        ThickLine = Color.FromArgb("#5A6472"),
        GivenText = Color.FromArgb("#1F2430"),
        UserText = Color.FromArgb("#1565C0"),
        NoteText = Color.FromArgb("#6B7280"),
        SelectedCell = Color.FromArgb("#BBDEFB"),
        SameNumberHighlight = Color.FromArgb("#D7E9FF"),
        UnitHighlight = Color.FromArgb("#EEF3FA"),
        PeerHighlight = Color.FromArgb("#F3F7FC"),
        ConflictText = Color.FromArgb("#C62828"),
        ConflictBackground = Color.FromArgb("#FFE1E1"),
        HintBackground = Color.FromArgb("#FFF2C2"),
        HintText = Color.FromArgb("#8A6100"),
        AccentText = Color.FromArgb("#0B7A5A"),
        LinkStrong = Color.FromArgb("#1E88E5"),
        LinkWeak = Color.FromArgb("#8E24AA"),
        CandidateHighlight = Color.FromArgb("#FFE082"),
        HintStructure = Color.FromArgb("#B26A00"),
        HintDigitA = Color.FromArgb("#1B7F3B"),
        HintDigitB = Color.FromArgb("#C62828"),
        HintDigitC = Color.FromArgb("#4527A0"),
        HintFin = Color.FromArgb("#E65100"),
        HintElimination = Color.FromArgb("#AD1457"),
        HintPlacement = Color.FromArgb("#00695C"),
        BivalueHighlight = Color.FromArgb("#00695C"),
        HintMarkText = Color.FromArgb("#FFFFFF"),
        DrawColors = new[]
        {
            Color.FromArgb("#FFCDD2"),
            Color.FromArgb("#FFE0B2"),
            Color.FromArgb("#FFF9C4"),
            Color.FromArgb("#C8E6C9"),
            Color.FromArgb("#B3E5FC"),
            Color.FromArgb("#E1BEE7"),
        },
        DrawLineColors = new[]
        {
            Color.FromArgb("#C62828"),
            Color.FromArgb("#EF6C00"),
            Color.FromArgb("#F9A825"),
            Color.FromArgb("#2E7D32"),
            Color.FromArgb("#0277BD"),
            Color.FromArgb("#6A1B9A"),
        },
    };

    /// <summary>深色主题。</summary>
    public static Palette Dark { get; } = new()
    {
        Background = Color.FromArgb("#15181D"),
        BoardBackground = Color.FromArgb("#1E2229"),
        GivenCellBackground = Color.FromArgb("#1E2229"),
        ThinLine = Color.FromArgb("#3A414B"),
        ThickLine = Color.FromArgb("#8A94A6"),
        GivenText = Color.FromArgb("#ECEFF4"),
        UserText = Color.FromArgb("#64B5F6"),
        NoteText = Color.FromArgb("#9AA4B2"),
        SelectedCell = Color.FromArgb("#2C4A6B"),
        SameNumberHighlight = Color.FromArgb("#26405C"),
        UnitHighlight = Color.FromArgb("#232A34"),
        PeerHighlight = Color.FromArgb("#1A1F26"),
        ConflictText = Color.FromArgb("#FF8A80"),
        ConflictBackground = Color.FromArgb("#4A2326"),
        HintBackground = Color.FromArgb("#4A3C10"),
        HintText = Color.FromArgb("#FFD54F"),
        AccentText = Color.FromArgb("#4DD0A5"),
        LinkStrong = Color.FromArgb("#64B5F6"),
        LinkWeak = Color.FromArgb("#CE93D8"),
        CandidateHighlight = Color.FromArgb("#7A5F1E"),
        HintStructure = Color.FromArgb("#FFC14D"),
        HintDigitA = Color.FromArgb("#6EE787"),
        HintDigitB = Color.FromArgb("#FF8A80"),
        HintDigitC = Color.FromArgb("#B39DFF"),
        HintFin = Color.FromArgb("#FFB74D"),
        HintElimination = Color.FromArgb("#F48FB1"),
        HintPlacement = Color.FromArgb("#4DD0A5"),
        BivalueHighlight = Color.FromArgb("#4DD0A5"),
        HintMarkText = Color.FromArgb("#12161B"),
        DrawColors = new[]
        {
            Color.FromArgb("#5A2A2E"),
            Color.FromArgb("#5A452A"),
            Color.FromArgb("#5A5324"),
            Color.FromArgb("#2C4A2F"),
            Color.FromArgb("#274454"),
            Color.FromArgb("#402E4A"),
        },
        DrawLineColors = new[]
        {
            Color.FromArgb("#EF9A9A"),
            Color.FromArgb("#FFCC80"),
            Color.FromArgb("#FFF176"),
            Color.FromArgb("#A5D6A7"),
            Color.FromArgb("#81D4FA"),
            Color.FromArgb("#CE93D8"),
        },
    };

    /// <summary>根据当前应用主题取配色。</summary>
    public static Palette Current =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Dark : Light;
}
