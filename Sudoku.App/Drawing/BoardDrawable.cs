using Sudoku.App.Game;
using Sudoku.Core;

namespace Sudoku.App.Drawing;

/// <summary>
/// 棋盘绘制器：负责格子底色、候选数、题面/填入数字、网格线与提示高亮。
/// 阶段二的强弱链画线也会叠加在这一层（保证与用户手绘线同一套视觉）。
/// </summary>
public sealed class BoardDrawable : IDrawable
{
    private GameSession? _session;

    /// <summary>当前对局。</summary>
    public GameSession? Session
    {
        get => _session;
        set => _session = value;
    }

    /// <summary>最近一次绘制的棋盘矩形（用于命中测试）。</summary>
    public RectF BoardRect { get; private set; }

    /// <summary>单格边长（像素）。</summary>
    public float CellSize { get; private set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        Palette palette = Palette.Current;

        canvas.FillColor = palette.Background;
        canvas.FillRectangle(dirtyRect);

        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        if (size <= 0)
        {
            return;
        }

        float left = dirtyRect.Left + ((dirtyRect.Width - size) / 2f);
        float top = dirtyRect.Top + ((dirtyRect.Height - size) / 2f);
        BoardRect = new RectF(left, top, size, size);
        CellSize = size / SudokuGrid.Size;

        canvas.FillColor = palette.BoardBackground;
        canvas.FillRectangle(BoardRect);

        GameSession? session = _session;
        if (session is null)
        {
            DrawGridLines(canvas, palette);
            return;
        }

        DrawCellBackgrounds(canvas, palette, session);
        DrawCellContents(canvas, palette, session);
        DrawGridLines(canvas, palette);
        DrawSelectionOutline(canvas, palette, session);
    }

    /// <summary>坐标 → 格索引（-1 表示落在棋盘外）。</summary>
    public int HitTest(PointF point)
    {
        if (CellSize <= 0 || BoardRect.Width <= 0)
        {
            return -1;
        }

        if (point.X < BoardRect.Left || point.X > BoardRect.Right || point.Y < BoardRect.Top || point.Y > BoardRect.Bottom)
        {
            return -1;
        }

        int col = (int)((point.X - BoardRect.Left) / CellSize);
        int row = (int)((point.Y - BoardRect.Top) / CellSize);
        col = Math.Clamp(col, 0, SudokuGrid.Size - 1);
        row = Math.Clamp(row, 0, SudokuGrid.Size - 1);
        return SudokuGrid.Index(row, col);
    }

    /// <summary>格索引 → 屏幕矩形。</summary>
    public RectF CellRect(int cell) => new(
        BoardRect.Left + (SudokuGrid.Col(cell) * CellSize),
        BoardRect.Top + (SudokuGrid.Row(cell) * CellSize),
        CellSize,
        CellSize);

    private void DrawCellBackgrounds(ICanvas canvas, Palette palette, GameSession session)
    {
        int selected = session.SelectedCell;
        int selectedDigit = selected >= 0 ? session.Values[selected] : 0;

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            Color? fill = null;

            if (session.Settings.HighlightUnit && selected >= 0 && IsPeerOf(selected, cell))
            {
                fill = palette.PeerHighlight;
                if (SudokuGrid.Row(cell) == SudokuGrid.Row(selected) || SudokuGrid.Col(cell) == SudokuGrid.Col(selected))
                {
                    fill = palette.UnitHighlight;
                }
            }

            if (session.Settings.HighlightSameNumber && selectedDigit != 0 && session.Values[cell] == selectedDigit)
            {
                fill = palette.SameNumberHighlight;
            }

            if (session.Settings.HighlightConflict && session.HasConflict(cell))
            {
                fill = palette.ConflictBackground;
            }

            if (session.HintCell == cell)
            {
                fill = palette.HintBackground;
            }

            if (selected == cell)
            {
                fill = palette.SelectedCell;
            }

            if (fill is not null)
            {
                canvas.FillColor = fill;
                canvas.FillRectangle(CellRect(cell));
            }
        }
    }

    private void DrawCellContents(ICanvas canvas, Palette palette, GameSession session)
    {
        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int value = session.Values[cell];

            if (value != 0)
            {
                DrawValue(canvas, palette, session, cell, value, isHint: false);
                continue;
            }

            if (session.HintCell == cell && session.HintDigit != 0)
            {
                DrawValue(canvas, palette, session, cell, session.HintDigit, isHint: true);
                continue;
            }

            int notes = session.Notes[cell];
            if (notes != 0)
            {
                DrawNotes(canvas, palette, cell, notes);
            }
        }
    }

    private void DrawValue(ICanvas canvas, Palette palette, GameSession session, int cell, int value, bool isHint)
    {
        Color color;

        if (isHint)
        {
            color = palette.HintText;
        }
        else if (session.IsGiven[cell])
        {
            color = palette.GivenText;
        }
        else if (session.Settings.HighlightConflict && session.IsWrongEntry(cell))
        {
            color = palette.ConflictText;
        }
        else
        {
            color = palette.UserText;
        }

        canvas.FontColor = color;
        canvas.FontSize = CellSize * (session.IsGiven[cell] ? 0.60f : 0.62f);

        RectF rect = CellRect(cell);
        canvas.DrawString(
            value.ToString(),
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            Microsoft.Maui.Graphics.HorizontalAlignment.Center,
            Microsoft.Maui.Graphics.VerticalAlignment.Center);
    }

    private void DrawNotes(ICanvas canvas, Palette palette, int cell, int notes)
    {
        canvas.FontColor = palette.NoteText;
        canvas.FontSize = CellSize * 0.25f;

        RectF rect = CellRect(cell);
        float w = rect.Width / 3f;
        float h = rect.Height / 3f;

        for (int digit = 1; digit <= SudokuGrid.Size; digit++)
        {
            if ((notes & SudokuGrid.DigitBit(digit)) == 0)
            {
                continue;
            }

            int row = (digit - 1) / 3;
            int col = (digit - 1) % 3;

            canvas.DrawString(
                digit.ToString(),
                rect.X + (col * w),
                rect.Y + (row * h),
                w,
                h,
                Microsoft.Maui.Graphics.HorizontalAlignment.Center,
                Microsoft.Maui.Graphics.VerticalAlignment.Center);
        }
    }

    private void DrawGridLines(ICanvas canvas, Palette palette)
    {
        float thin = Math.Max(1f, CellSize * 0.018f);
        float thick = Math.Max(2f, CellSize * 0.055f);

        for (int i = 0; i <= SudokuGrid.Size; i++)
        {
            bool isBoxLine = i % SudokuGrid.BoxSize == 0;
            canvas.StrokeColor = isBoxLine ? palette.ThickLine : palette.ThinLine;
            canvas.StrokeSize = isBoxLine ? thick : thin;

            float offset = i * CellSize;
            canvas.DrawLine(BoardRect.Left + offset, BoardRect.Top, BoardRect.Left + offset, BoardRect.Bottom);
            canvas.DrawLine(BoardRect.Left, BoardRect.Top + offset, BoardRect.Right, BoardRect.Top + offset);
        }
    }

    private void DrawSelectionOutline(ICanvas canvas, Palette palette, GameSession session)
    {
        int selected = session.SelectedCell;
        if (selected < 0)
        {
            return;
        }

        RectF rect = CellRect(selected);
        canvas.StrokeColor = palette.LinkStrong;
        canvas.StrokeSize = Math.Max(2f, CellSize * 0.07f);
        canvas.DrawRectangle(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static bool IsPeerOf(int first, int second) =>
        first != second &&
        (SudokuGrid.Row(first) == SudokuGrid.Row(second) || SudokuGrid.Col(first) == SudokuGrid.Col(second) || SudokuGrid.Box(first) == SudokuGrid.Box(second));
}
