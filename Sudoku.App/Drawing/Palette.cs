namespace Sudoku.App.Drawing;

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
    };

    /// <summary>根据当前应用主题取配色。</summary>
    public static Palette Current =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Dark : Light;
}
