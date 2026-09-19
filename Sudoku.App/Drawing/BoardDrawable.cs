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
        DrawBivalueHighlights(canvas, palette, session);
        DrawHintCells(canvas, palette, session);
        DrawHintMarkBoxes(canvas, palette, session);

        if (session.Settings.HighlightCandidateNotes)
        {
            DrawCandidateHighlights(canvas, palette, session);
        }

        DrawCellContents(canvas, palette, session);
        DrawHintMarkDigits(canvas, palette, session);
        DrawGridLines(canvas, palette);
        DrawLinks(canvas, palette, session);
        DrawHintStep(canvas, palette, session);
        DrawSelectionOutline(canvas, palette, session);
    }

    /// <summary>
    /// 提示高亮的底层：结构格铺一层淡色，结构里/鱼鳍/结论涉及的候选数各按角色铺一块彩色底。
    /// 只画「当前推导进度已经亮起」的标记，所以棋盘会跟着面板一步步亮起来。
    /// </summary>
    private void DrawHintMarkBoxes(ICanvas canvas, Palette palette, GameSession session)
    {
        if (session.ActiveHintStep is null)
        {
            return;
        }

        foreach (HintMark mark in session.VisibleHintMarks)
        {
            Color color = palette.HintMarkColor(mark.Role);

            if (mark.IsCellWide)
            {
                canvas.FillColor = color.WithAlpha(0.18f);
                canvas.FillRectangle(CellRect(mark.Cell));
                continue;
            }

            RectF rect = CandidateRect(mark.Cell, mark.Digit);
            float inset = rect.Width * (HintMarks.IsConclusion(mark.Role) ? 0.05f : 0.11f);
            canvas.FillColor = color.WithAlpha(0.95f);
            canvas.FillRoundedRectangle(
                rect.X + inset,
                rect.Y + inset,
                rect.Width - (2 * inset),
                rect.Height - (2 * inset),
                rect.Width * 0.26f);
        }
    }

    /// <summary>彩色块上的候选数重画一遍：用高对比文字色，免得灰字压在深色块上看不清。</summary>
    private void DrawHintMarkDigits(ICanvas canvas, Palette palette, GameSession session)
    {
        if (session.ActiveHintStep is null)
        {
            return;
        }

        canvas.FontColor = palette.HintMarkText;
        canvas.FontSize = CellSize * 0.25f;

        foreach (HintMark mark in session.VisibleHintMarks)
        {
            if (mark.IsCellWide || session.Values[mark.Cell] != 0)
            {
                continue;
            }

            RectF rect = CandidateRect(mark.Cell, mark.Digit);
            canvas.DrawString(
                mark.Digit.ToString(),
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                Microsoft.Maui.Graphics.HorizontalAlignment.Center,
                Microsoft.Maui.Graphics.VerticalAlignment.Center);
        }
    }

    /// <summary>候选数（笔记）在格内的矩形。</summary>
    public RectF CandidateRect(int cell, int digit)
    {
        RectF rect = CellRect(cell);
        float w = rect.Width / 3f;
        float h = rect.Height / 3f;
        int row = (digit - 1) / 3;
        int col = (digit - 1) % 3;
        return new RectF(rect.X + (col * w), rect.Y + (row * h), w, h);
    }

    /// <summary>候选数中心点（画线端点用）。</summary>
    public PointF CandidateCenter(int cell, int digit)
    {
        RectF rect = CandidateRect(cell, digit);
        return new PointF(rect.Center.X, rect.Center.Y);
    }

    /// <summary>坐标 → 候选数；落在棋盘外或已填数字的格返回 null。</summary>
    public CandidateRef? HitTestCandidate(PointF point)
    {
        int cell = HitTest(point);
        if (cell < 0)
        {
            return null;
        }

        if (_session is not null && _session.Values[cell] != 0)
        {
            return null;
        }

        RectF rect = CellRect(cell);
        float w = rect.Width / 3f;
        float h = rect.Height / 3f;
        int col = Math.Clamp((int)((point.X - rect.X) / w), 0, 2);
        int row = Math.Clamp((int)((point.Y - rect.Y) / h), 0, 2);
        return new CandidateRef(cell, (row * 3) + col + 1);
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
        // 绘图模式下不画任何「选中」相关的高亮：选中框会把玩家涂的颜色整块盖掉
        int selected = session.DrawMode ? -1 : session.SelectedCell;
        int selectedDigit = selected >= 0 ? session.Values[selected] : 0;

        // 锁定的数字优先：锁定后即使没选中格子，也一直高亮该数字
        int highlightDigit = session.LockedDigit != 0 ? session.LockedDigit : selectedDigit;

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

            if (session.Settings.HighlightSameNumber && highlightDigit != 0 && session.Values[cell] == highlightDigit)
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

            // 玩家自己涂的底色优先级最高：选中、行列宫、同类数字、冲突、提示这些高亮都不该把它盖掉
            if (palette.DrawColor(session.Coloring[cell]) is { } painted)
            {
                fill = painted;
            }

            if (fill is not null)
            {
                canvas.FillColor = fill;
                canvas.FillRectangle(CellRect(cell));
            }
        }
    }

    /// <summary>
    /// 「两个候选数」高亮（数字键最右侧的 XY 按钮）：把恰好只剩两个候选数的格子圈出来，
    /// 再把这格里的那两个候选数各自框一下。只描边不铺底色，免得盖住玩家自己涂的颜色或提示标记。
    /// </summary>
    private void DrawBivalueHighlights(ICanvas canvas, Palette palette, GameSession session)
    {
        if (!session.BivalueMode)
        {
            return;
        }

        canvas.StrokeColor = palette.BivalueHighlight;
        canvas.StrokeDashPattern = null;

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int mask = session.BivalueMask(cell);
            if (mask == 0)
            {
                continue;
            }

            canvas.StrokeSize = Math.Max(2.4f, CellSize * 0.045f);
            canvas.DrawRoundedRectangle(CellRect(cell), CellSize * 0.10f);

            canvas.StrokeSize = Math.Max(1.4f, CellSize * 0.026f);
            for (int digit = 1; digit <= SudokuGrid.Size; digit++)
            {
                if ((mask & SudokuGrid.DigitBit(digit)) == 0)
                {
                    continue;
                }

                RectF rect = CandidateRect(cell, digit);
                float inset = rect.Width * 0.13f;
                canvas.DrawRoundedRectangle(
                    rect.X + inset,
                    rect.Y + inset,
                    rect.Width - (2 * inset),
                    rect.Height - (2 * inset),
                    rect.Width * 0.22f);
            }
        }
    }

    /// <summary>
    /// 把「当前关注的数字」的候选数标记出来：既包括锁定的数字，也包括选中格所属数字
    /// （同类数字高亮打开时）。候选数标记本身为空的格子不画——因为格内本来就没有可对照的数字。
    /// </summary>
    private void DrawCandidateHighlights(ICanvas canvas, Palette palette, GameSession session)
    {
        int digit = session.LockedDigit;

        if (digit == 0 && session.Settings.HighlightSameNumber && session.SelectedCell >= 0)
        {
            digit = session.Values[session.SelectedCell];
        }

        if (digit is < 1 or > SudokuGrid.Size)
        {
            return;
        }

        int bit = SudokuGrid.DigitBit(digit);
        canvas.FillColor = palette.CandidateHighlight;

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            if (session.Values[cell] != 0 || (session.Notes[cell] & bit) == 0)
            {
                continue;
            }

            RectF rect = CandidateRect(cell, digit);
            float inset = rect.Width * 0.14f;
            canvas.FillRoundedRectangle(
                rect.X + inset,
                rect.Y + inset,
                rect.Width - (2 * inset),
                rect.Height - (2 * inset),
                rect.Width * 0.28f);
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
            color = palette.HintPlacement;
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

    /// <summary>
    /// 当前提示技巧涉及的格子铺一层淡色底（在候选数下方，不会挡数字）：
    /// 带鳍鱼的鳍格、唯一矩形的四角、链上各格等，都能一眼看出这一步用到哪些格。
    /// </summary>
    private void DrawHintCells(ICanvas canvas, Palette palette, GameSession session)
    {
        if (session.ActiveHintStep is not { } step || step.HighlightCells.Count == 0)
        {
            return;
        }

        canvas.FillColor = palette.HintStructure.WithAlpha(0.16f);
        foreach (int cell in step.HighlightCells.Distinct())
        {
            canvas.FillRectangle(CellRect(cell));
        }
    }

    /// <summary>
    /// 在棋盘上画出当前提示技巧的图示：格子描边、强链（实线带箭头）/弱链（虚线）、
    /// 要删除的候选数打叉，以及落子步骤该填的数字。高亮按推导进度逐步亮起。
    /// </summary>
    private void DrawHintStep(ICanvas canvas, Palette palette, GameSession session)
    {
        if (session.ActiveHintStep is not { } step)
        {
            return;
        }

        // 涉及的格子描边（结构色）
        canvas.StrokeColor = palette.HintStructure.WithAlpha(0.9f);
        canvas.StrokeSize = Math.Max(1.4f, CellSize * 0.03f);
        canvas.StrokeDashPattern = null;
        foreach (int cell in step.HighlightCells.Distinct())
        {
            canvas.DrawRoundedRectangle(CellRect(cell), CellSize * 0.08f);
        }

        // 鱼鳍格再单独用鳍色描一圈粗边，和鱼身区分开
        foreach (HintMark mark in session.VisibleHintMarks)
        {
            if (mark.Role != HintMarkRole.Fin)
            {
                continue;
            }

            canvas.StrokeColor = palette.HintFin;
            canvas.StrokeSize = Math.Max(2f, CellSize * 0.045f);
            canvas.DrawRoundedRectangle(CellRect(mark.Cell), CellSize * 0.10f);
        }

        // 链：强链实线、弱链虚线，箭头指向推导方向
        foreach (TechniqueLink link in step.LinkList)
        {
            DrawTechniqueLink(canvas, palette, link);
        }

        // 结论：要删除的候选数圈出来打叉——只在推导走到「结论」那一步才出现
        foreach (HintMark mark in session.VisibleHintMarks)
        {
            if (mark.Role != HintMarkRole.Elimination || mark.IsCellWide)
            {
                continue;
            }

            RectF rect = CandidateRect(mark.Cell, mark.Digit);
            float pad = rect.Width * 0.26f;

            canvas.StrokeColor = palette.HintElimination;
            canvas.StrokeSize = Math.Max(1.8f, CellSize * 0.038f);
            canvas.StrokeDashPattern = null;
            canvas.DrawCircle(rect.Center.X, rect.Center.Y, rect.Width * 0.42f);
            canvas.DrawLine(rect.X + pad, rect.Y + pad, rect.Right - pad, rect.Bottom - pad);
            canvas.DrawLine(rect.Right - pad, rect.Y + pad, rect.X + pad, rect.Bottom - pad);
        }

        // 落子步骤：把该填的数字直接写在格子里
        if (step.IsPlacement && step.PlaceIndex >= 0 && session.Values[step.PlaceIndex] == 0)
        {
            DrawValue(canvas, palette, session, step.PlaceIndex, step.PlaceDigit, isHint: true);
        }
    }

    /// <summary>
    /// 叠加用户手绘的连线：实线=强链，虚线=弱链；
    /// 箭头指向玩家第二次点选的候选数（即落笔方向），颜色取玩家在绘制模式里选的颜色。
    /// </summary>
    private void DrawLinks(ICanvas canvas, Palette palette, GameSession session)
    {
        if (!session.Settings.ShowLinks)
        {
            return;
        }

        foreach (UserLink link in session.Drawing.Links)
        {
            DrawLink(canvas, palette, link);
        }

        // 画链过程中，用空心圆标出已选定的起点
        if (session.PendingLink is { } pending)
        {
            PointF center = CandidateCenter(pending.Cell, pending.Digit);
            canvas.StrokeColor = palette.LinkColor(session.DrawColorIndex, session.NextLinkIsStrong);
            canvas.StrokeSize = Math.Max(1.5f, CellSize * 0.035f);
            canvas.DrawCircle(center.X, center.Y, Math.Max(4f, CellSize * 0.11f));
        }
    }

    private void DrawLink(ICanvas canvas, Palette palette, UserLink link) =>
        DrawArrowLink(canvas, palette.LinkColor(link.ColorIndex, link.IsStrong), link.IsStrong, link.From, link.To);

    /// <summary>把提示里的一条链画到候选数之间：强链实线、弱链虚线，箭头指向推导方向。</summary>
    private void DrawTechniqueLink(ICanvas canvas, Palette palette, TechniqueLink link) =>
        DrawArrowLink(
            canvas,
            link.IsStrong ? palette.LinkStrong : palette.LinkWeak,
            link.IsStrong,
            link.From,
            link.To);

    /// <summary>
    /// 在候选数之间画一条带箭头的线：实线表强链、虚线表弱链。
    /// 线条保持细，免得挡住沿途格子里的候选数（玩家要对照链上的候选数）。
    /// </summary>
    private void DrawArrowLink(ICanvas canvas, Color color, bool isStrong, CandidateRef from, CandidateRef to)
    {
        PointF a = CandidateCenter(from.Cell, from.Digit);
        PointF b = CandidateCenter(to.Cell, to.Digit);

        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 1f)
        {
            return;
        }

        float ux = dx / length;
        float uy = dy / length;

        float dot = Math.Max(1.8f, CellSize * 0.045f);
        float head = Math.Max(5.5f, CellSize * 0.13f);

        // 线段在箭头前收住，避免箭头把线头穿出去
        float stop = Math.Max(0f, length - (head * 0.8f));
        PointF lineEnd = new(a.X + (ux * stop), a.Y + (uy * stop));

        canvas.StrokeColor = color;
        canvas.StrokeSize = Math.Max(1.3f, CellSize * (isStrong ? 0.032f : 0.026f));
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeDashPattern = isStrong ? null : new[] { 3.5f, 2.5f };

        // 起点小圆点：候选数未标记时也能看清落点
        canvas.FillColor = color;
        canvas.FillCircle(a.X, a.Y, dot);

        canvas.DrawLine(a, lineEnd);
        canvas.StrokeDashPattern = null;
        canvas.StrokeLineCap = LineCap.Butt;

        // 箭头
        float px = -uy;
        float py = ux;
        float wing = head * 0.5f;

        var arrow = new PathF();
        arrow.MoveTo(b.X, b.Y);
        arrow.LineTo(b.X - (ux * head) + (px * wing), b.Y - (uy * head) + (py * wing));
        arrow.LineTo(b.X - (ux * head) - (px * wing), b.Y - (uy * head) - (py * wing));
        arrow.Close();

        canvas.FillColor = color;
        canvas.FillPath(arrow);
    }

    private void DrawSelectionOutline(ICanvas canvas, Palette palette, GameSession session)
    {
        int selected = session.SelectedCell;

        // 绘图模式下不画选中框：这层描边会盖住玩家涂的颜色
        if (selected < 0 || session.DrawMode)
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
