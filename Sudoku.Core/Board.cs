using System.Text;

namespace Sudoku.Core;

/// <summary>
/// 数独盘面：81 格数值模型，0 表示空格。
/// 盘面只关心"已填入的数字"，玩家笔记（候选数）由上层游戏状态维护。
/// </summary>
public sealed class Board : IEquatable<Board>
{
    private readonly int[] _cells;

    public Board() => _cells = new int[SudokuGrid.CellCount];

    private Board(int[] cells) => _cells = cells;

    /// <summary>空盘面。</summary>
    public static Board Empty() => new();

    /// <summary>按格索引读写（0..9）。</summary>
    public int this[int index]
    {
        get => _cells[index];
        set
        {
            if ((uint)value > 9u)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "格值必须在 0..9 之间（0 表示空格）。");
            }

            _cells[index] = value;
        }
    }

    /// <summary>按行列读写（0..8，0 表示空格）。</summary>
    public int this[int row, int col]
    {
        get => _cells[SudokuGrid.Index(row, col)];
        set => this[SudokuGrid.Index(row, col)] = value;
    }

    /// <summary>已填格数。</summary>
    public int FilledCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                if (_cells[i] != 0)
                {
                    n++;
                }
            }

            return n;
        }
    }

    /// <summary>是否已填满。</summary>
    public bool IsComplete
    {
        get
        {
            for (int i = 0; i < SudokuGrid.CellCount; i++)
            {
                if (_cells[i] == 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>输出格值副本。</summary>
    public int[] ToArray() => (int[])_cells.Clone();

    /// <summary>深拷贝。</summary>
    public Board Clone() => new((int[])_cells.Clone());

    /// <summary>由内部数组包装（不复制）。</summary>
    internal static Board Wrap(int[] cells) => new(cells);

    /// <summary>单格候选数掩码（该格已填则返回 0）。</summary>
    public int CandidateMask(int index)
    {
        if (_cells[index] != 0)
        {
            return 0;
        }

        int used = 0;
        foreach (int p in SudokuGrid.Peers[index])
        {
            int v = _cells[p];
            if (v != 0)
            {
                used |= SudokuGrid.DigitBit(v);
            }
        }

        return SudokuGrid.AllDigitsMask & ~used;
    }

    /// <summary>全盘候选数掩码数组。</summary>
    public int[] ComputeCandidates()
    {
        var result = new int[SudokuGrid.CellCount];
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            result[i] = CandidateMask(i);
        }

        return result;
    }

    /// <summary>是否无冲突（任意行/列/宫内无重复数字）。</summary>
    public bool IsValid()
    {
        foreach (int[] unit in SudokuGrid.AllUnits)
        {
            int seen = 0;
            foreach (int idx in unit)
            {
                int v = _cells[idx];
                if (v == 0)
                {
                    continue;
                }

                int bit = SudokuGrid.DigitBit(v);
                if ((seen & bit) != 0)
                {
                    return false;
                }

                seen |= bit;
            }
        }

        return true;
    }

    /// <summary>是否已完成且合法。</summary>
    public bool IsSolved() => IsComplete && IsValid();

    /// <summary>
    /// 解析 81 个格值：数字为已知数，'.' / '0' / '_' / '*' 为空格；
    /// 空白字符（空格、换行、制表符）视为分隔符直接忽略，便于粘贴带格式的盘面。
    /// </summary>
    public static Board Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var values = new List<int>(SudokuGrid.CellCount);

        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (ch is >= '1' and <= '9')
            {
                values.Add(ch - '0');
            }
            else if (ch is '0' or '.' or '_' or '*')
            {
                values.Add(0);
            }
        }

        if (values.Count != SudokuGrid.CellCount)
        {
            throw new FormatException($"需要 81 个格值，实际解析到 {values.Count} 个。");
        }

        var board = new Board();
        for (int i = 0; i < SudokuGrid.CellCount; i++)
        {
            board._cells[i] = values[i];
        }

        return board;
    }

    /// <summary>尝试解析，失败返回 false。</summary>
    public static bool TryParse(string? text, out Board board)
    {
        try
        {
            board = Parse(text ?? string.Empty);
            return true;
        }
        catch (Exception e) when (e is FormatException or ArgumentNullException)
        {
            board = Empty();
            return false;
        }
    }

    /// <summary>导出为 SDK 文本格式（空位为 '.'）。</summary>
    public string ToSdkString()
    {
        var sb = new StringBuilder(SudokuGrid.CellCount);
        foreach (int v in _cells)
        {
            sb.Append(SudokuGrid.ToChar(v));
        }

        return sb.ToString();
    }

    /// <summary>多行可读输出（带宫分隔线），用于调试与测试输出。</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        for (int r = 0; r < SudokuGrid.Size; r++)
        {
            if (r > 0 && r % SudokuGrid.BoxSize == 0)
            {
                sb.AppendLine("------+-------+------");
            }

            for (int c = 0; c < SudokuGrid.Size; c++)
            {
                if (c > 0 && c % SudokuGrid.BoxSize == 0)
                {
                    sb.Append(" | ");
                }

                sb.Append(SudokuGrid.ToChar(this[r, c]));
                sb.Append(' ');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public bool Equals(Board? other)
    {
        if (other is null || ReferenceEquals(this, other))
        {
            return ReferenceEquals(this, other);
        }

        return _cells.AsSpan().SequenceEqual(other._cells);
    }

    public override bool Equals(object? obj) => obj is Board other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int v in _cells)
        {
            hash.Add(v);
        }

        return hash.ToHashCode();
    }
}
