using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sudoku.Core;

namespace Sudoku.App.Game;

/// <summary>一次输入的结果。</summary>
public enum InputOutcome
{
    /// <summary>未处理（无选中格或不可操作）。</summary>
    Ignored,

    /// <summary>落子成功。</summary>
    Placed,

    /// <summary>笔记变化。</summary>
    NoteChanged,

    /// <summary>清除了一格。</summary>
    Erased,
}

/// <summary>画链模式下点击候选数的结果。</summary>
public enum LinkOutcome
{
    /// <summary>未处理（不可操作、已填数字的格、坐标非法）。</summary>
    Ignored,

    /// <summary>已选好起点，等待点击第二个候选数。</summary>
    Started,

    /// <summary>连线成功。</summary>
    Linked,

    /// <summary>这条连线已经画过了。</summary>
    Duplicate,

    /// <summary>取消了起点选择。</summary>
    Cancelled,
}

/// <summary>双击快速填入唯一候选数的结果。</summary>
public enum QuickFillOutcome
{
    /// <summary>未处理（不可操作 / 该格已有数字）。</summary>
    Ignored,

    /// <summary>该格没有可用候选数。</summary>
    NoCandidate,

    /// <summary>候选数不止一个，不自动填。</summary>
    MultipleCandidates,

    /// <summary>已按唯一候选数填入。</summary>
    Filled,
}

/// <summary>最简提示信息。</summary>
public sealed record HintInfo(int Cell, int Digit, string Text);

/// <summary>
/// 一局游戏的完整状态：盘面、笔记、选中格、计时、错误次数、撤销重做与存档。
/// UI 只通过本类读写状态，便于后续接入链画线与分段式提示。
/// </summary>
public sealed class GameSession : INotifyPropertyChanged
{
    private readonly record struct CellChange(
        int Cell,
        int BeforeValue,
        int BeforeNotes,
        int AfterValue,
        int AfterNotes,
        int BeforeColor = 0,
        int AfterColor = 0);

    private sealed record Batch(List<CellChange> Changes, List<UserLink> AddedLinks, List<UserLink> RemovedLinks);

    private readonly List<Batch> _undoStack = new();
    private readonly List<Batch> _redoStack = new();

    private int _selectedCell = -1;
    private bool _noteMode;
    private bool _linkMode;
    private bool _autoMark;
    private int _lockedDigit;
    private bool _lockMode;
    private bool _drawMode;
    private bool _paintMode = true;
    private int _drawColorIndex = 1;
    private CellColoring _coloring = new();
    private bool _nextLinkIsStrong = true;
    private CandidateRef? _pendingLink;
    private int _mistakeCount;
    private int _elapsedSeconds;
    private int _hintsUsed;
    private int _hintCell = -1;
    private int _hintStage = -1;
    private IReadOnlyList<HintMark> _activeHintMarks = Array.Empty<HintMark>();
    private IReadOnlyList<HintMark> _visibleHintMarks = Array.Empty<HintMark>();
    private bool _bivalueMode;
    private readonly int[] _bivalueMasks = new int[SudokuGrid.CellCount];
    private TechniqueStep? _activeHintStep;
    private bool _isPaused;
    private bool _isCompleted;
    private bool _isFailed;

    private GameSession(Puzzle puzzle)
    {
        Puzzle = puzzle;
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            int value = puzzle.Given[i];
            IsGiven[i] = value != 0;
            Values[i] = value;
        }
    }

    /// <summary>棋盘内容变化（值 / 笔记 / 高亮）。</summary>
    public event EventHandler? BoardChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前题目。</summary>
    public Puzzle Puzzle { get; }

    /// <summary>当前所有格的值（含题面已知数）。</summary>
    public int[] Values { get; } = new int[SudokuGrid.CellCount];

    /// <summary>每格笔记（候选数）掩码。</summary>
    public int[] Notes { get; } = new int[SudokuGrid.CellCount];

    /// <summary>是否为题面已知数。</summary>
    public bool[] IsGiven { get; } = new bool[SudokuGrid.CellCount];

    /// <summary>设置引用（高亮、自动笔记、错误上限等实时生效）。</summary>
    public AppSettings Settings { get; private set; } = new();

    /// <summary>选中格索引，-1 表示未选中。</summary>
    public int SelectedCell
    {
        get => _selectedCell;
        private set => SetField(ref _selectedCell, value);
    }

    /// <summary>是否处于笔记模式。</summary>
    public bool NoteMode
    {
        get => _noteMode;
        set
        {
            if (SetField(ref _noteMode, value))
            {
                OnPropertyChanged(nameof(NoteModeText));
            }
        }
    }

    /// <summary>笔记模式显示文本（是否激活由按钮高亮表示，文字里不再加「✓」）。</summary>
    public string NoteModeText => "笔记";

    /// <summary>用户手绘的强弱链标记（阶段二；随存档保存，可显隐）。</summary>
    public LinkDrawing Drawing { get; private set; } = new();

    /// <summary>是否处于画链模式（点击候选数连线）。</summary>
    public bool LinkMode
    {
        get => _linkMode;
        set
        {
            if (SetField(ref _linkMode, value))
            {
                PendingLink = null;
                OnPropertyChanged(nameof(LinkModeText));
            }
        }
    }

    /// <summary>画链模式显示文本。</summary>
    public string LinkModeText => "画链";

    /// <summary>下一条连线的类型：true 强链（实线），false 弱链（虚线）。</summary>
    public bool NextLinkIsStrong
    {
        get => _nextLinkIsStrong;
        set
        {
            if (SetField(ref _nextLinkIsStrong, value))
            {
                OnPropertyChanged(nameof(LinkStrengthText));
            }
        }
    }

    /// <summary>连线类型显示文本。</summary>
    public string LinkStrengthText => NextLinkIsStrong ? "强链 ━" : "弱链 ┅";

    /// <summary>画链时已选中的第一个端点（等待点第二个候选数）。</summary>
    public CandidateRef? PendingLink
    {
        get => _pendingLink;
        private set
        {
            if (!Nullable.Equals(_pendingLink, value))
            {
                _pendingLink = value;
                OnPropertyChanged(nameof(PendingLink));
            }
        }
    }

    /// <summary>已画连线数量。</summary>
    public int LinkCount => Drawing.Count;

    /// <summary>切换下一条连线的强弱类型。</summary>
    public void ToggleLinkStrength() => NextLinkIsStrong = !NextLinkIsStrong;

    // ── 绘制功能（涂色 + 画链） ────────────────────────────────────────────

    /// <summary>格子涂色（随存档保存、可撤销、可清空）。</summary>
    public CellColoring Coloring => _coloring;

    /// <summary>已涂色的格子数。</summary>
    public int ColorCount => _coloring.Count;

    /// <summary>是否处于绘制模式：下方工具栏切换为颜色与画笔设置。</summary>
    public bool DrawMode
    {
        get => _drawMode;
        set
        {
            if (SetField(ref _drawMode, value))
            {
                PendingLink = null;
                OnPropertyChanged(nameof(DrawModeText));
                RaiseBoardChanged();
            }
        }
    }

    /// <summary>绘制模式按钮文本。</summary>
    public string DrawModeText => "绘制";

    /// <summary>绘制工具：true = 给格子涂色，false = 在候选数之间画链。</summary>
    public bool PaintMode
    {
        get => _paintMode;
        set
        {
            if (SetField(ref _paintMode, value))
            {
                LinkMode = !value;
                PendingLink = null;
                OnPropertyChanged(nameof(PaintToolText));
                RaiseBoardChanged();
            }
        }
    }

    /// <summary>当前绘制工具文本。</summary>
    public string PaintToolText => PaintMode ? "涂色 ●" : "画链 ↗";

    /// <summary>当前绘制颜色编号（1..<see cref="CellColoring.MaxColor"/>），涂色与连线共用。</summary>
    public int DrawColorIndex
    {
        get => _drawColorIndex;
        private set
        {
            int clamped = Math.Clamp(value, 1, CellColoring.MaxColor);
            if (SetField(ref _drawColorIndex, clamped))
            {
                OnPropertyChanged(nameof(DrawColorIndex));
            }
        }
    }

    /// <summary>选择绘制颜色。</summary>
    public void SetDrawColor(int colorIndex) => DrawColorIndex = colorIndex;

    /// <summary>用当前颜色涂一格；再点同色视为擦除。返回是否发生变化。</summary>
    public bool PaintCell(int cell)
    {
        if (!CanPlay || !CellColoring.IsValidCell(cell))
        {
            return false;
        }

        int target = _coloring[cell] == DrawColorIndex ? 0 : DrawColorIndex;
        bool changed = false;
        RunBatch(Array.Empty<int>(), () => changed = _coloring.Set(cell, target));
        RaiseBoardChanged();
        return changed;
    }

    /// <summary>清空全部涂色（可撤销）。</summary>
    public bool ClearColoring()
    {
        if (_coloring.IsEmpty)
        {
            return false;
        }

        RunBatch(Array.Empty<int>(), _coloring.ClearAll);
        RaiseBoardChanged();
        return true;
    }

    /// <summary>清空全部绘制内容（连线 + 涂色，可撤销）。</summary>
    public bool ClearDrawing()
    {
        if (Drawing.IsEmpty && _coloring.IsEmpty)
        {
            return false;
        }

        PendingLink = null;
        RunBatch(Array.Empty<int>(), () =>
        {
            Drawing.Clear();
            _coloring.ClearAll();
        });
        RaiseBoardChanged();
        return true;
    }

    // ── 自动标记开关 / 数字锁定 / 双击快速填入 ─────────────────────────────

    /// <summary>自动标记开关：开 = 补全所有候选数，关 = 清空所有候选数（按钮即开关）。</summary>
    public bool AutoMark
    {
        get => _autoMark;
        private set
        {
            if (SetField(ref _autoMark, value))
            {
                OnPropertyChanged(nameof(AutoMarkText));
            }
        }
    }

    /// <summary>自动标记按钮文本。</summary>
    public string AutoMarkText => "自动标记";

    /// <summary>切换自动标记：打开就补全候选数，关闭就清空候选数。</summary>
    public bool ToggleAutoMark()
    {
        if (AutoMark)
        {
            ClearAllCandidates();
            AutoMark = false;
        }
        else
        {
            FillAllCandidates();
            AutoMark = true;
        }

        return AutoMark;
    }

    /// <summary>锁定的数字（0 = 未锁定）。锁定后该数字与其所有候选数在盘面上一并高亮。</summary>
    public int LockedDigit
    {
        get => _lockedDigit;
        private set
        {
            if (SetField(ref _lockedDigit, value))
            {
                OnPropertyChanged(nameof(LockedDigitText));
                RaiseBoardChanged();
            }
        }
    }

    /// <summary>锁定状态文本（无锁定时为空）。</summary>
    public string LockedDigitText => LockedDigit == 0 ? string.Empty : $"已锁定 {LockedDigit}";

    /// <summary>
    /// 数字锁定模式：开启后，点下方数字键是「锁定该数字」，点格子是「把该数字填进这一格」。
    /// </summary>
    public bool LockMode
    {
        get => _lockMode;
        private set
        {
            if (SetField(ref _lockMode, value))
            {
                OnPropertyChanged(nameof(LockModeText));
            }
        }
    }

    /// <summary>数字锁定模式的按钮文本。</summary>
    public string LockModeText => "数字锁定";

    /// <summary>
    /// 「两个候选数」高亮模式：只把盘面上恰好剩两个候选数的格子标出来
    /// （找 XY 翼 / 远程数对这类结构时最有用）。它只和「当前锁定的那个数字键」互斥
    /// （按 XY 让数字键弹起，按数字键让 XY 弹起），但**不影响**「数字锁定」这个开关本身。
    /// </summary>
    public bool BivalueMode
    {
        get => _bivalueMode;
        private set
        {
            if (SetField(ref _bivalueMode, value))
            {
                RefreshBivalueMasks();
                OnPropertyChanged(nameof(BivalueText));
                RaiseBoardChanged();
            }
        }
    }

    /// <summary>当前恰好只有两个候选数的格子数。</summary>
    public int BivalueCount { get; private set; }

    /// <summary>某格「恰好两个候选数」时返回它的候选数掩码，否则返回 0。</summary>
    public int BivalueMask(int cell) => _bivalueMasks[cell];

    /// <summary>数字键下方的小字：开启时显示这样的格子有几个。</summary>
    public string BivalueText => BivalueMode ? BivalueCount.ToString() : string.Empty;

    /// <summary>
    /// 切换「两个候选数」高亮。它只让「锁定的数字键」弹起，
    /// 不动「数字锁定」开关——锁定模式开着也一样能看双值格子。
    /// </summary>
    public bool ToggleBivalueMode()
    {
        if (BivalueMode)
        {
            BivalueMode = false;
            return false;
        }

        LockedDigit = 0;
        BivalueMode = true;
        return true;
    }

    /// <summary>按了数字键就退出「两个候选数」高亮（两者互斥）。</summary>
    public void ClearBivalueMode()
    {
        if (BivalueMode)
        {
            BivalueMode = false;
        }
    }

    /// <summary>重算「恰好两个候选数」的格子（只在开启该模式时算）。</summary>
    private void RefreshBivalueMasks()
    {
        Array.Clear(_bivalueMasks);
        BivalueCount = 0;

        if (!_bivalueMode)
        {
            return;
        }

        int[] masks = EffectiveCandidates();
        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            int mask = masks[cell];
            if (SudokuGrid.CountDigits(mask) != 2)
            {
                continue;
            }

            _bivalueMasks[cell] = mask;
            BivalueCount++;
        }
    }

    /// <summary>切换数字锁定模式；关闭时同时取消已锁定的数字。</summary>
    public bool ToggleLockMode()
    {
        LockMode = !LockMode;

        if (!LockMode)
        {
            LockedDigit = 0;
        }

        return LockMode;
    }

    /// <summary>锁定 / 解锁一个数字：同一个数字再点一次即解锁。返回锁定后的数字（0 = 已解锁）。</summary>
    public int ToggleLockDigit(int digit)
    {
        if (digit < 1 || digit > SudokuGrid.Size)
        {
            return LockedDigit;
        }

        LockedDigit = LockedDigit == digit ? 0 : digit;
        return LockedDigit;
    }

    /// <summary>
    /// 双击快速填入：该格只剩一个候选数时直接填入（界面仅在 Windows 上响应双击）。
    /// 用的是「当前生效的候选数」，所以玩家自己删到只剩一个候选数时也能双击填入。
    /// </summary>
    public QuickFillOutcome QuickFill(int cell)
    {
        if (!CanPlay || !CellColoring.IsValidCell(cell) || IsGiven[cell] || Values[cell] != 0)
        {
            return QuickFillOutcome.Ignored;
        }

        int mask = EffectiveCandidates()[cell];
        int count = SudokuGrid.CountDigits(mask);

        if (count == 0)
        {
            return QuickFillOutcome.NoCandidate;
        }

        if (count > 1)
        {
            return QuickFillOutcome.MultipleCandidates;
        }

        int digit = 0;
        for (int d = 1; d <= SudokuGrid.Size; d++)
        {
            if ((mask & SudokuGrid.DigitBit(d)) != 0)
            {
                digit = d;
                break;
            }
        }

        SelectedCell = cell;
        return Input(digit) == InputOutcome.Placed ? QuickFillOutcome.Filled : QuickFillOutcome.Ignored;
    }

    /// <summary>
    /// 画链模式下点击某个候选数：第一次点选起点，第二次点选终点并落线；
    /// 再点同一个候选数则取消。返回结果供界面提示。
    /// </summary>
    public LinkOutcome TapCandidate(int cell, int digit)
    {
        if (!CanPlay ||
            cell < 0 ||
            cell >= SudokuGrid.CellCount ||
            digit < 1 ||
            digit > SudokuGrid.Size ||
            Values[cell] != 0)
        {
            return LinkOutcome.Ignored;
        }

        var candidate = new CandidateRef(cell, digit);
        ClearHint();
        SelectedCell = cell;

        if (PendingLink is null)
        {
            PendingLink = candidate;
            RaiseBoardChanged();
            return LinkOutcome.Started;
        }

        if (PendingLink.Value == candidate)
        {
            PendingLink = null;
            RaiseBoardChanged();
            return LinkOutcome.Cancelled;
        }

        UserLink link = UserLink.Create(PendingLink.Value, candidate, NextLinkIsStrong, DrawColorIndex);
        bool added = false;
        RunBatch(Array.Empty<int>(), () => added = Drawing.Add(link));
        PendingLink = null;
        RaiseBoardChanged();

        return added ? LinkOutcome.Linked : LinkOutcome.Duplicate;
    }

    /// <summary>清空所有手绘连线（可撤销）。</summary>
    public bool ClearLinks()
    {
        if (Drawing.IsEmpty)
        {
            return false;
        }

        PendingLink = null;
        RunBatch(Array.Empty<int>(), Drawing.Clear);
        return true;
    }

    /// <summary>已犯错次数。</summary>
    public int MistakeCount
    {
        get => _mistakeCount;
        private set
        {
            if (SetField(ref _mistakeCount, value))
            {
                OnPropertyChanged(nameof(MistakeText));
            }
        }
    }

    /// <summary>错误次数显示文本。</summary>
    public string MistakeText => Settings.MistakeLimit <= 0
        ? $"错误 {MistakeCount}"
        : $"错误 {MistakeCount}/{Settings.MistakeLimit}";

    /// <summary>已用时间。</summary>
    public TimeSpan Elapsed => TimeSpan.FromSeconds(_elapsedSeconds);

    /// <summary>计时显示文本 mm:ss。</summary>
    public string ElapsedText => $"{(int)Elapsed.TotalMinutes:00}:{Elapsed.Seconds:00}";

    /// <summary>使用提示的次数。</summary>
    public int HintsUsed => _hintsUsed;

    /// <summary>提示高亮的格（-1 表示无）。</summary>
    public int HintCell => _hintCell;

    /// <summary>提示要展示的数字（0 表示不展示）。</summary>
    public int HintDigit => _hintCell >= 0 && Values[_hintCell] == 0 ? Puzzle.Solution[_hintCell] : 0;

    /// <summary>
    /// 正在讲解的技巧步骤（null 表示没有）。棋盘据此画出该技巧的箭头、强弱链、
    /// 涉及格子与要删除的候选数，让玩家能在盘面上对照推导。
    /// </summary>
    public TechniqueStep? ActiveHintStep
    {
        get => _activeHintStep;
        private set => SetField(ref _activeHintStep, value);
    }

    /// <summary>
    /// 当前讲解到第几步推导（0 基；-1 表示整步都显示）。
    /// 棋盘高亮按这个进度逐段亮起：先结构，再结论，和面板里「下一步」的节奏一致。
    /// </summary>
    public int HintStage
    {
        get => _hintStage;
        set
        {
            if (SetField(ref _hintStage, value))
            {
                RefreshVisibleMarks();
                RaiseBoardChanged();
            }
        }
    }

    /// <summary>当前进度下应当画在棋盘上的标记（已按技巧自带的标记或兜底推导算好）。</summary>
    public IReadOnlyList<HintMark> VisibleHintMarks => _visibleHintMarks;

    /// <summary>整步的全部标记（不管进度）。</summary>
    public IReadOnlyList<HintMark> AllHintMarks => _activeHintMarks;

    /// <summary>
    /// 设置/清除当前讲解的技巧步骤（清除即擦掉棋盘上的提示绘制）。
    /// <paramref name="stage"/> 是当前推导进度（0 基，-1 = 全部显示）。
    /// </summary>
    public void ShowHintStep(TechniqueStep? step, int stage = -1)
    {
        _activeHintMarks = step?.VisualMarks ?? Array.Empty<HintMark>();
        _hintStage = stage;
        RefreshVisibleMarks();
        ActiveHintStep = step;
        OnPropertyChanged(nameof(AllHintMarks));
        RaiseBoardChanged();
    }

    /// <summary>按当前进度切出要画的标记并缓存（每帧都取一次会反复分配）。</summary>
    private void RefreshVisibleMarks()
    {
        _visibleHintMarks = HintMarks.UpTo(_activeHintMarks, _hintStage);
        OnPropertyChanged(nameof(VisibleHintMarks));
    }

    /// <summary>是否暂停。</summary>
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetField(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(CanPlay));
            }
        }
    }

    /// <summary>是否完成。</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        private set
        {
            if (SetField(ref _isCompleted, value))
            {
                OnPropertyChanged(nameof(CanPlay));
            }
        }
    }

    /// <summary>是否因错误次数用尽而失败。</summary>
    public bool IsFailed
    {
        get => _isFailed;
        private set
        {
            if (SetField(ref _isFailed, value))
            {
                OnPropertyChanged(nameof(CanPlay));
            }
        }
    }

    /// <summary>当前是否可操作。</summary>
    public bool CanPlay => !IsCompleted && !IsFailed && !IsPaused;

    /// <summary>剩余空格数。</summary>
    public int RemainingCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                if (Values[i] == 0)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>剩余空格显示文本。</summary>
    public string ProgressText => $"剩余 {RemainingCount} 格";

    /// <summary>难度名称。</summary>
    public string LevelName => Puzzle.DifficultyText;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// 专项练习的目标（null 表示这是一局普通对局）。
    /// 练习局不写入存档、不覆盖「继续上一局」，提示直接讲解目标技巧的那一步。
    /// </summary>
    public PracticeSetup? Practice { get; private set; }

    /// <summary>是否处在技巧专项练习中。</summary>
    public bool IsPractice => Practice is not null;

    /// <summary>
    /// 是否是「十七数」对局：题面恰好 17 个提示数。
    /// 这一档的题目必须用高阶技巧才能推完，提示的技法上限要相应放宽。
    /// </summary>
    public bool IsSeventeen => Puzzle.Given.FilledCount == SeventeenClues.ClueCount;

    /// <summary>练习目标文案（非练习局为空）。</summary>
    public string PracticeGoalText => Practice is null
        ? string.Empty
        : $"专项练习 · {Practice.Name}：{Practice.Goal}";

    /// <summary>新建一局。</summary>
    public static GameSession New(Puzzle puzzle, AppSettings settings)
    {
        var session = new GameSession(puzzle) { Settings = settings };
        if (settings.AutoCandidatesOnNewGame)
        {
            session.FillAllCandidates(recordUndo: false);
            session._autoMark = true;
        }

        return session;
    }

    /// <summary>
    /// 新建一局技巧专项练习：盘面直接停在「该技巧可用」的那一步，玩家自己把它找出来。
    /// 练习局默认标好候选数，方便对照结构。
    /// </summary>
    public static GameSession NewPractice(PracticeSetup setup, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(setup);

        var session = new GameSession(setup.Puzzle) { Settings = settings, Practice = setup };
        session.FillAllCandidates(recordUndo: false);
        session._autoMark = true;
        return session;
    }

    /// <summary>从存档恢复。</summary>
    public static GameSession Restore(GameSnapshot snapshot, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Board given = Board.Parse(snapshot.PuzzleSdk);
        Board solution = Board.Parse(snapshot.SolutionSdk);
        var puzzle = new Puzzle(given, solution, (DifficultyLevel)snapshot.LevelValue, snapshot.TechniqueLevel, given.FilledCount)
        {
            Score = snapshot.DifficultyScore,
        };

        var session = new GameSession(puzzle) { Settings = settings };

        if (snapshot.Values.Length == SudokuGrid.CellCount)
        {
            Array.Copy(snapshot.Values, session.Values, SudokuGrid.CellCount);
        }

        if (snapshot.Notes.Length == SudokuGrid.CellCount)
        {
            Array.Copy(snapshot.Notes, session.Notes, SudokuGrid.CellCount);
        }

        session._mistakeCount = snapshot.MistakeCount;
        session._elapsedSeconds = snapshot.ElapsedSeconds;
        session._hintsUsed = snapshot.HintsUsed;
        session._hintCell = snapshot.HintCell;
        session._isCompleted = snapshot.IsCompleted;
        session._isFailed = snapshot.IsFailed;
        session._isPaused = snapshot.IsPaused;
        session.Drawing = LinkDrawing.Parse(snapshot.Links);
        session._coloring = CellColoring.Parse(snapshot.CellColors);
        session._autoMark = snapshot.AutoMark;
        session._lockedDigit = snapshot.LockedDigit;
        session._lockMode = snapshot.LockMode;
        session._drawColorIndex = Math.Clamp(snapshot.DrawColorIndex, 1, CellColoring.MaxColor);

        return session;
    }

    /// <summary>应用（或更新）设置引用。</summary>
    public void ApplySettings(AppSettings settings)
    {
        Settings = settings;
        OnPropertyChanged(nameof(MistakeText));
        RaiseBoardChanged();
    }

    /// <summary>选中一格。</summary>
    public void Select(int cell)
    {
        if (cell is < 0 or >= SudokuGrid.CellCount)
        {
            return;
        }

        SelectedCell = cell;
        ClearHint();
        RaiseBoardChanged();
    }

    /// <summary>清除选中格（取消棋盘上的光标高亮）。</summary>
    public void ClearSelection()
    {
        if (SelectedCell < 0)
        {
            return;
        }

        SelectedCell = -1;
        RaiseBoardChanged();
    }

    /// <summary>按行列偏移移动选中格（键盘方向键）。</summary>
    public void MoveSelection(int rowDelta, int colDelta)
    {
        int row = SelectedCell < 0 ? 0 : SudokuGrid.Row(SelectedCell);
        int col = SelectedCell < 0 ? 0 : SudokuGrid.Col(SelectedCell);
        row = Math.Clamp(row + rowDelta, 0, SudokuGrid.Size - 1);
        col = Math.Clamp(col + colDelta, 0, SudokuGrid.Size - 1);
        Select(SudokuGrid.Index(row, col));
    }

    /// <summary>切换笔记模式。</summary>
    public void ToggleNoteMode() => NoteMode = !NoteMode;

    /// <summary>输入一个数字（按当前模式落子或切换笔记）。</summary>
    public InputOutcome Input(int digit)
    {
        if (digit is < 1 or > 9 || !CanPlay || SelectedCell < 0)
        {
            return InputOutcome.Ignored;
        }

        int cell = SelectedCell;
        if (IsGiven[cell])
        {
            return InputOutcome.Ignored;
        }

        ClearHint();

        if (NoteMode)
        {
            int bit = SudokuGrid.DigitBit(digit);
            RunBatch(new[] { cell }, () => Notes[cell] ^= bit);
            return InputOutcome.NoteChanged;
        }

        bool clear = Values[cell] == digit;
        bool correct = !clear && digit == Puzzle.Solution[cell];
        bool trimPeers = !clear && correct && Settings.AutoRemoveNotes;

        var touched = new List<int> { cell };
        if (trimPeers)
        {
            touched.AddRange(SudokuGrid.Peers[cell]);
        }

        RunBatch(touched, () =>
        {
            if (clear)
            {
                Values[cell] = 0;
                return;
            }

            Values[cell] = digit;
            Notes[cell] = 0;
            Drawing.RemoveForCell(cell);

            if (trimPeers)
            {
                int bit = SudokuGrid.DigitBit(digit);
                foreach (int peer in SudokuGrid.Peers[cell])
                {
                    Notes[peer] &= ~bit;
                }
            }
        });

        if (clear)
        {
            return InputOutcome.Erased;
        }

        // 数字锁定模式下：刚填的这个数字如果已经填满 9 个，就自动锁到下一个还没填完的数字。
        // 同时清掉选中格——否则光标会停在刚填完的那一格上继续高亮，看着像还锁着旧数字。
        if (LockMode && LockedDigit == digit && IsDigitComplete(digit))
        {
            LockedDigit = NextIncompleteDigit(digit);
            ClearSelection();
        }

        if (!correct)
        {
            RegisterMistake();
            return InputOutcome.Placed;
        }

        CheckCompletion();
        return InputOutcome.Placed;
    }

    /// <summary>清除选中格（先清数字，无数字则清笔记）。</summary>
    public InputOutcome Erase()
    {
        if (!CanPlay || SelectedCell < 0)
        {
            return InputOutcome.Ignored;
        }

        int cell = SelectedCell;
        if (IsGiven[cell] || (Values[cell] == 0 && Notes[cell] == 0))
        {
            return InputOutcome.Ignored;
        }

        ClearHint();
        RunBatch(new[] { cell }, () =>
        {
            Values[cell] = 0;
            Notes[cell] = 0;
        });

        return InputOutcome.Erased;
    }

    /// <summary>撤销。</summary>
    public void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        Batch batch = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        Apply(batch, forward: false);
        _redoStack.Add(batch);
        // 撤销/重做会改变 CanUndo / CanRedo，必须走 NotifyStateChanged 才会通知界面刷新按钮状态
        NotifyStateChanged();
    }

    /// <summary>重做。</summary>
    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        Batch batch = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        Apply(batch, forward: true);
        _undoStack.Add(batch);
        NotifyStateChanged();
    }

    /// <summary>一键标记全部候选数。</summary>
    public void FillAllCandidates(bool recordUndo = true)
    {
        int[] candidates = BuildBoard().ComputeCandidates();

        void ApplyAll()
        {
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                Notes[i] = Values[i] == 0 ? candidates[i] : 0;
            }
        }

        if (recordUndo)
        {
            RunBatch(Enumerable.Range(0, SudokuGrid.CellCount), ApplyAll);
        }
        else
        {
            ApplyAll();
            _undoStack.Clear();
            _redoStack.Clear();
            RaiseBoardChanged();
        }
    }

    /// <summary>一键清除全部候选数。</summary>
    public void ClearAllCandidates()
    {
        RunBatch(Enumerable.Range(0, SudokuGrid.CellCount), () => Array.Clear(Notes));
    }

    /// <summary>最简提示：高亮一格并给出正确答案。</summary>
    public HintInfo? RevealHint()
    {
        if (!CanPlay)
        {
            return null;
        }

        int cell = -1;

        if (SelectedCell >= 0 && Values[SelectedCell] == 0 && !IsGiven[SelectedCell])
        {
            cell = SelectedCell;
        }
        else
        {
            int bestCount = 10;
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                if (Values[i] != 0 || IsGiven[i])
                {
                    continue;
                }

                int count = SudokuGrid.CountDigits(BuildBoard().CandidateMask(i));
                if (count < bestCount)
                {
                    bestCount = count;
                    cell = i;
                }
            }
        }

        if (cell < 0)
        {
            return null;
        }

        _hintCell = cell;
        _hintsUsed++;
        OnPropertyChanged(nameof(HintCell));
        OnPropertyChanged(nameof(HintDigit));
        OnPropertyChanged(nameof(HintsUsed));
        RaiseBoardChanged();

        return new HintInfo(cell, Puzzle.Solution[cell], $"{CellName(cell)} 应填 {Puzzle.Solution[cell]}");
    }

    /// <summary>
    /// 当前生效的候选数：以规则推导为基础，再扣掉玩家自己删减过的候选数。
    /// 某格完全没有候选数标记时按规则推导结果处理（否则关掉自动标记后提示会失效）。
    /// </summary>
    public int[] EffectiveCandidates()
    {
        int[] effective = BuildBoard().ComputeCandidates();

        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            if (Values[cell] != 0)
            {
                effective[cell] = 0;
                continue;
            }

            int notes = Notes[cell];
            if (notes != 0)
            {
                effective[cell] &= notes;
            }
        }

        return effective;
    }

    /// <summary>整理当前盘面可用的技巧，供分段式提示逐级选择（跟随玩家删减过的候选数）。</summary>
    public HintPlan BuildHintPlan(int maxLevel = int.MaxValue) =>
        HintPlanner.Plan(BuildBoard(), maxLevel, HintPlanner.DefaultMaxOptionsPerTechnique, EffectiveCandidates());

    /// <summary>
    /// 应用分段式提示里选中的步骤：落子，或按推导删除候选数。
    /// 两种动作都计入提示次数、可撤销，并保留棋盘上的提示高亮。
    /// </summary>
    public HintInfo? ApplyHintStep(TechniqueStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        if (!CanPlay)
        {
            return null;
        }

        HintInfo info;

        if (step.IsPlacement)
        {
            int cell = step.PlaceIndex;
            Select(cell);
            Input(step.PlaceDigit);
            _hintCell = cell;
            info = new HintInfo(cell, step.PlaceDigit, HintPlanner.Effect(step));
        }
        else
        {
            int[] targets = step.Eliminations.Select(e => e.Cell).Distinct().ToArray();
            int[] candidates = EffectiveCandidates();

            RunBatch(targets, () =>
            {
                foreach (int cell in targets)
                {
                    // 该格还没标记候选数时，先按当前盘面标好，让「删除」看得见
                    if (Values[cell] == 0 && Notes[cell] == 0)
                    {
                        Notes[cell] = candidates[cell];
                    }
                }

                foreach (CandidateRef elimination in step.Eliminations)
                {
                    Notes[elimination.Cell] &= ~SudokuGrid.DigitBit(elimination.Digit);
                }
            });

            info = new HintInfo(-1, 0, HintPlanner.Effect(step));
        }

        _hintsUsed++;
        OnPropertyChanged(nameof(HintsUsed));
        OnPropertyChanged(nameof(HintCell));
        OnPropertyChanged(nameof(HintDigit));
        RaiseBoardChanged();

        return info;
    }

    /// <summary>清除提示高亮与棋盘上的提示绘制。</summary>
    public void ClearHint()
    {
        ActiveHintStep = null;

        if (_hintCell < 0)
        {
            return;
        }

        _hintCell = -1;
        OnPropertyChanged(nameof(HintCell));
        OnPropertyChanged(nameof(HintDigit));
        RaiseBoardChanged();
    }

    /// <summary>计时推进（每秒一次）。</summary>
    public void Tick()
    {
        if (!CanPlay)
        {
            return;
        }

        _elapsedSeconds++;
        OnPropertyChanged(nameof(Elapsed));
        OnPropertyChanged(nameof(ElapsedText));
    }

    /// <summary>切换暂停。</summary>
    public void TogglePause() => IsPaused = !IsPaused;

    /// <summary>重开本局（保留题目，清空填入与笔记）。</summary>
    public void Restart()
    {
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            Values[i] = IsGiven[i] ? Puzzle.Given[i] : 0;
            Notes[i] = 0;
        }

        _undoStack.Clear();
        _redoStack.Clear();
        Drawing.Clear();
        _coloring.ClearAll();
        PendingLink = null;
        LockedDigit = 0;
        _mistakeCount = 0;
        _elapsedSeconds = 0;
        _hintsUsed = 0;
        _hintCell = -1;
        _isPaused = false;
        _isCompleted = false;
        _isFailed = false;

        if (Settings.AutoCandidatesOnNewGame)
        {
            FillAllCandidates(recordUndo: false);
            _autoMark = true;
        }
        else
        {
            _autoMark = false;
        }

        NotifyStateChanged();
    }

    /// <summary>该格是否为填错的数字（与答案不符）。</summary>
    public bool IsWrongEntry(int cell) => Values[cell] != 0 && !IsGiven[cell] && Values[cell] != Puzzle.Solution[cell];

    /// <summary>该格是否与同行/列/宫已有数字冲突。</summary>
    public bool HasConflict(int cell)
    {
        int value = Values[cell];
        if (value == 0)
        {
            return false;
        }

        int bit = SudokuGrid.DigitBit(value);
        foreach (int peer in SudokuGrid.Peers[cell])
        {
            if (Values[peer] == value)
            {
                return true;
            }
        }

        _ = bit;
        return false;
    }

    /// <summary>该格是否应显示为用户填入的数字（非题面）。</summary>
    public bool IsUserEntry(int cell) => Values[cell] != 0 && !IsGiven[cell];

    /// <summary>某个数字在盘面上还差几个（9 减去已填数量），最小为 0。</summary>
    public int RemainingOf(int digit)
    {
        if (digit < 1 || digit > SudokuGrid.Size)
        {
            return 0;
        }

        int count = 0;
        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            if (Values[cell] == digit)
            {
                count++;
            }
        }

        return Math.Max(0, SudokuGrid.Size - count);
    }

    /// <summary>某个数字是否已经全部填完（盘面上 9 个都在）。</summary>
    public bool IsDigitComplete(int digit) => RemainingOf(digit) == 0;

    /// <summary>
    /// 沿着数字键顺序找下一个「还没填完」的数字（从 after 之后开始绕一圈）。
    /// 用于数字锁定模式：当前数字填满后自动跳到下一个。全部填完时返回 0（取消锁定）。
    /// </summary>
    public int NextIncompleteDigit(int after)
    {
        for (int step = 1; step <= SudokuGrid.Size; step++)
        {
            int digit = (((after - 1) + step) % SudokuGrid.Size) + 1;
            if (!IsDigitComplete(digit))
            {
                return digit;
            }
        }

        return 0;
    }

    /// <summary>生成存档。</summary>
    public GameSnapshot ToSnapshot() => new()
    {
        PuzzleSdk = Puzzle.Given.ToSdkString(),
        SolutionSdk = Puzzle.Solution.ToSdkString(),
        LevelValue = (int)Puzzle.Level,
        TechniqueLevel = Puzzle.TechniqueLevel,
        DifficultyScore = Puzzle.Score,
        Values = (int[])Values.Clone(),
        Notes = (int[])Notes.Clone(),
        MistakeCount = MistakeCount,
        ElapsedSeconds = _elapsedSeconds,
        HintsUsed = _hintsUsed,
        HintCell = _hintCell,
        Links = Drawing.Serialize(),
        CellColors = _coloring.Serialize(),
        AutoMark = AutoMark,
        LockedDigit = LockedDigit,
        LockMode = LockMode,
        DrawColorIndex = DrawColorIndex,
        IsCompleted = IsCompleted,
        IsFailed = IsFailed,
        IsPaused = IsPaused,
        SavedAtUtc = DateTime.UtcNow.ToString("O"),
    };

    /// <summary>当前局面（含用户已填入的数字）对应的盘面，用于导出。</summary>
    public Board CurrentBoard() => BuildBoard();

    private Board BuildBoard()
    {
        var board = Board.Empty();
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            board[i] = Values[i];
        }

        return board;
    }

    private void RegisterMistake()
    {
        MistakeCount++;

        if (Settings.MistakeLimit > 0 && MistakeCount >= Settings.MistakeLimit)
        {
            IsFailed = true;
        }
    }

    private void CheckCompletion()
    {
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            if (Values[i] != Puzzle.Solution[i])
            {
                return;
            }
        }

        IsCompleted = true;
    }

    private void RunBatch(IEnumerable<int> touchedCells, Action apply)
    {
        List<int> touched = touchedCells.Distinct().ToList();
        var before = new Dictionary<int, (int Value, int Notes)>(touched.Count);
        foreach (int cell in touched)
        {
            before[cell] = (Values[cell], Notes[cell]);
        }

        UserLink[] linksBefore = Drawing.ToArray();
        int[] colorsBefore = _coloring.ToArray();

        apply();

        var changes = new List<CellChange>(touched.Count);
        foreach (int cell in touched)
        {
            (int beforeValue, int beforeNotes) = before[cell];
            if (beforeValue == Values[cell] && beforeNotes == Notes[cell] && colorsBefore[cell] == _coloring[cell])
            {
                continue;
            }

            changes.Add(new CellChange(
                cell, beforeValue, beforeNotes, Values[cell], Notes[cell], colorsBefore[cell], _coloring[cell]));
        }

        // 只改动了涂色的格子（通常不在 touchedCells 里）也要进撤销栈
        for (int cell = 0; cell < SudokuGrid.CellCount; cell++)
        {
            if (colorsBefore[cell] != _coloring[cell] && !before.ContainsKey(cell))
            {
                changes.Add(new CellChange(
                    cell, Values[cell], Notes[cell], Values[cell], Notes[cell], colorsBefore[cell], _coloring[cell]));
            }
        }

        UserLink[] linksAfter = Drawing.ToArray();
        var addedLinks = linksAfter.Where(l => Array.IndexOf(linksBefore, l) < 0).ToList();
        var removedLinks = linksBefore.Where(l => Array.IndexOf(linksAfter, l) < 0).ToList();

        if (changes.Count > 0 || addedLinks.Count > 0 || removedLinks.Count > 0)
        {
            _undoStack.Add(new Batch(changes, addedLinks, removedLinks));
            _redoStack.Clear();
        }

        NotifyStateChanged();
    }

    private void Apply(Batch batch, bool forward)
    {
        foreach (CellChange change in batch.Changes)
        {
            Values[change.Cell] = forward ? change.AfterValue : change.BeforeValue;
            Notes[change.Cell] = forward ? change.AfterNotes : change.BeforeNotes;
            _coloring.Set(change.Cell, forward ? change.AfterColor : change.BeforeColor);
        }

        foreach (UserLink link in batch.AddedLinks)
        {
            if (forward)
            {
                Drawing.Add(link);
            }
            else
            {
                Drawing.Remove(link);
            }
        }

        foreach (UserLink link in batch.RemovedLinks)
        {
            if (forward)
            {
                Drawing.Remove(link);
            }
            else
            {
                Drawing.Add(link);
            }
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(LinkCount));
        OnPropertyChanged(nameof(ColorCount));
        OnPropertyChanged(nameof(AutoMark));
        OnPropertyChanged(nameof(AutoMarkText));
        RaiseBoardChanged();
    }

    private void RaiseBoardChanged()
    {
        // 「两个候选数」高亮要跟着盘面走：填错、撤销、改候选数之后数量都要立刻变
        if (_bivalueMode)
        {
            RefreshBivalueMasks();
            OnPropertyChanged(nameof(BivalueText));
        }

        BoardChanged?.Invoke(this, EventArgs.Empty);
    }


    private static string CellName(int index) => $"R{SudokuGrid.Row(index) + 1}C{SudokuGrid.Col(index) + 1}";

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
