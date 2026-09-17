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

/// <summary>最简提示信息。</summary>
public sealed record HintInfo(int Cell, int Digit, string Text);

/// <summary>
/// 一局游戏的完整状态：盘面、笔记、选中格、计时、错误次数、撤销重做与存档。
/// UI 只通过本类读写状态，便于后续接入链画线与分段式提示。
/// </summary>
public sealed class GameSession : INotifyPropertyChanged
{
    private readonly record struct CellChange(int Cell, int BeforeValue, int BeforeNotes, int AfterValue, int AfterNotes);

    private sealed record Batch(List<CellChange> Changes);

    private readonly List<Batch> _undoStack = new();
    private readonly List<Batch> _redoStack = new();

    private int _selectedCell = -1;
    private bool _noteMode;
    private int _mistakeCount;
    private int _elapsedSeconds;
    private int _hintsUsed;
    private int _hintCell = -1;
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

    /// <summary>笔记模式显示文本。</summary>
    public string NoteModeText => NoteMode ? "笔记 ✓" : "笔记";

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
    public string LevelName => Difficulty.Name(Puzzle.Level);

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>新建一局。</summary>
    public static GameSession New(Puzzle puzzle, AppSettings settings)
    {
        var session = new GameSession(puzzle) { Settings = settings };
        if (settings.AutoCandidatesOnNewGame)
        {
            session.FillAllCandidates(recordUndo: false);
        }

        return session;
    }

    /// <summary>从存档恢复。</summary>
    public static GameSession Restore(GameSnapshot snapshot, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Board given = Board.Parse(snapshot.PuzzleSdk);
        Board solution = Board.Parse(snapshot.SolutionSdk);
        var puzzle = new Puzzle(given, solution, (DifficultyLevel)snapshot.LevelValue, snapshot.TechniqueLevel, given.FilledCount);

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
        RaiseBoardChanged();
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
        RaiseBoardChanged();
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

    /// <summary>清除提示高亮。</summary>
    public void ClearHint()
    {
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

    /// <summary>生成存档。</summary>
    public GameSnapshot ToSnapshot() => new()
    {
        PuzzleSdk = Puzzle.Given.ToSdkString(),
        SolutionSdk = Puzzle.Solution.ToSdkString(),
        LevelValue = (int)Puzzle.Level,
        TechniqueLevel = Puzzle.TechniqueLevel,
        Values = (int[])Values.Clone(),
        Notes = (int[])Notes.Clone(),
        MistakeCount = MistakeCount,
        ElapsedSeconds = _elapsedSeconds,
        HintsUsed = _hintsUsed,
        HintCell = _hintCell,
        IsCompleted = IsCompleted,
        IsFailed = IsFailed,
        IsPaused = IsPaused,
        SavedAtUtc = DateTime.UtcNow.ToString("O"),
    };

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

        apply();

        var changes = new List<CellChange>(touched.Count);
        foreach (int cell in touched)
        {
            (int beforeValue, int beforeNotes) = before[cell];
            if (beforeValue == Values[cell] && beforeNotes == Notes[cell])
            {
                continue;
            }

            changes.Add(new CellChange(cell, beforeValue, beforeNotes, Values[cell], Notes[cell]));
        }

        if (changes.Count > 0)
        {
            _undoStack.Add(new Batch(changes));
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
        }
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(ProgressText));
        RaiseBoardChanged();
    }

    private void RaiseBoardChanged() => BoardChanged?.Invoke(this, EventArgs.Empty);

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
